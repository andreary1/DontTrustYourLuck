using System;
using System.Collections;
using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using DontTrustYourLuck.Presentation.Art;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Cards;
using DontTrustYourLuck.Presentation.Config;
using DontTrustYourLuck.Presentation.UI;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Match
{
    /// <summary>
    /// Scene-level controller (presentation). Forwards HUD input and bot steps to <see cref="MatchSession"/>,
    /// queues the resulting domain events and plays them back in order (piece hops, highlights, HUD, log).
    /// Flow: Input -> Command -> Rules -> State -> Events -> Presentation. It never edits logical state; the
    /// pieces' Transforms only mirror logical positions after each event.
    /// Bot delays and animation timing live here only; the domain never waits.
    /// </summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchConfigAsset config;
        [SerializeField] private BoardView board;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private GameHUD hud;
        [SerializeField] private PlayerPieceView piecePrefab;
        [SerializeField] private Transform piecesRoot;
        [Tooltip("Pieces already in the scene, reused before instantiating the prefab.")]
        [SerializeField] private List<PlayerPieceView> scenePieces = new List<PlayerPieceView>();

        [Header("Participants (count must respect MatchRulesConfig)")]
        [SerializeField] private List<ParticipantConfig> participants = new List<ParticipantConfig>
        {
            new ParticipantConfig("Jaguar", GamePalette.SolarYellow, false),
            new ParticipantConfig("Capybara", GamePalette.ColonialBlue, true),
            new ParticipantConfig("Monkey", GamePalette.LeafGreen, true),
            new ParticipantConfig("Toucan", GamePalette.HibiscusPink, true),
        };

        [Header("Randomness")]
        [Tooltip("Off: a new seed per match (logged to the console). On: fixedSeed, +1 per restart.")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 2026;

        [Header("Timing (presentation only)")]
        [Min(0.01f)] [SerializeField] private float stepDuration = 0.16f;
        [Min(0f)] [SerializeField] private float hopHeight = 0.35f;
        [Min(0f)] [SerializeField] private float botRollDelay = 0.7f;
        [Min(0f)] [SerializeField] private float botChooseDelay = 0.9f;
        [Min(0f)] [SerializeField] private float eventPause = 0.12f;
        [SerializeField] private bool autoStart = true;
        [Tooltip("Multiplies every playback duration (hops, pauses, bot delays, dice flicker). 1 = original pace, 1.5 = 1.5x slower.")]
        [Min(0.1f)] [SerializeField] private float playbackSlowdown = 1.5f;

        [Header("Feedback timing (presentation only, scaled down by SetTimings in tests)")]
        [Tooltip("Dice faces flicker before showing the rolled values (halved for bots).")]
        [Min(0f)] [SerializeField] private float rollAnimation = 0.4f;
        [Tooltip("Pause after a card is played so its banner reads before the effect.")]
        [Min(0f)] [SerializeField] private float cardPlayPause = 0.45f;
        [Tooltip("Show where each die would land (pure rules query, no state change).")]
        [SerializeField] private bool previewDestinations = true;

        private readonly Queue<MatchEvent> _queue = new Queue<MatchEvent>();
        private readonly Dictionary<int, PlayerPieceView> _pieceById = new Dictionary<int, PlayerPieceView>();
        private readonly List<PlayerPieceView> _activePieces = new List<PlayerPieceView>();
        private readonly List<PlayerDisplay> _players = new List<PlayerDisplay>();
        private readonly List<int> _lastRoll = new List<int>(2);
        // Card presentation state, mirrored from domain events (never the source of truth).
        private readonly Dictionary<int, List<CardInstance>> _hands = new Dictionary<int, List<CardInstance>>();
        private readonly Dictionary<string, CardDefinitionAsset> _cardAssets = new Dictionary<string, CardDefinitionAsset>();
        private readonly List<CardSlotDisplay> _slotBuffer = new List<CardSlotDisplay>(3);
        private readonly List<CardTargetDisplay> _targetBuffer = new List<CardTargetDisplay>(4);
        private CardCatalog _cardCatalog;
        private string _selectedCard;
        private int _selectedTarget = -1;

        private MatchRulesConfig _rules;
        private MatchSession _session;
        private bool _processing;
        private Coroutine _processRoutine;
        private Coroutine _botRoutine;
        private int _seedCounter;
        private ParticipantConfig[] _seats = Array.Empty<ParticipantConfig>();
        private int _lucky;
        private int _unlucky;
        private readonly List<int> _zoneBuffer = new List<int>();
        private readonly List<int> _unluckyZone = new List<int>();
        private int _round;
        private int _currentId = -1;
        private int _selectedDie = -1;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _viewportPending;
        private float _feedbackScale = 1f;
        private MovementService _movementPreview;
        private readonly List<int> _destinations = new List<int>(2);

        /// <summary>Raised when an event starts playing back (in order, after the previous one finished).
        /// Audio and visual feedback listen to this, never to the domain directly.</summary>
        public event Action<MatchEvent> EventPresenting;
        /// <summary>(playerId, tile): a piece finished one hop of a move/retreat animation.</summary>
        public event Action<int, int> PieceHopped;
        /// <summary>A command was rejected by the rules layer.</summary>
        public event Action<CommandError> CommandRejected;

        public MatchSession Session => _session;
        public GameHUD Hud => hud;
        public BoardView Board => board;
        /// <summary>True while events are being played back; gameplay buttons stay disabled meanwhile.</summary>
        public bool IsBusy => _processing || _queue.Count > 0;
        public int LastSeed { get; private set; }
        public int LuckyNumber => _lucky;
        public int UnluckyNumber => _unlucky;
        /// <summary>True when the rules use an unlucky zone wider than one tile.</summary>
        public bool HasUnluckyZone => _rules != null && _rules.UnluckyZoneRadius > 0;

        /// <summary>Unlucky number or zone as shown to players ("13", "12-14", "30, 1, 2"); empty when none.</summary>
        public string UnluckyDisplayFor(int center)
        {
            if (center <= 0) return string.Empty;
            if (_rules == null || _lucky <= 0 || _rules.UnluckyZoneRadius == 0) return center.ToString();
            _rules.GetUnluckyZone(center, _lucky, _zoneBuffer);
            if (_zoneBuffer.Count <= 1) return center.ToString();
            bool contiguous = true;
            for (int i = 1; i < _zoneBuffer.Count; i++) contiguous &= _zoneBuffer[i] == _zoneBuffer[i - 1] + 1;
            return contiguous ? _zoneBuffer[0] + "-" + _zoneBuffer[_zoneBuffer.Count - 1] : string.Join(", ", _zoneBuffer);
        }
        public IReadOnlyList<PlayerPieceView> ActivePieces => _activePieces;
        public IReadOnlyList<PlayerDisplay> Players => _players;
        /// <summary>Instance id of the card selected in the hand panel, or null.</summary>
        public string SelectedCardId => _selectedCard;
        /// <summary>Target picked for the selected card, or -1.</summary>
        public int SelectedTargetId => _selectedTarget;

        public void Configure(MatchConfigAsset configAsset, BoardView boardView, BoardCameraRig rig, GameHUD gameHud,
            PlayerPieceView prefab, Transform root, List<PlayerPieceView> existingPieces)
        {
            config = configAsset; board = boardView; cameraRig = rig; hud = gameHud;
            piecePrefab = prefab; piecesRoot = root; scenePieces = existingPieces ?? new List<PlayerPieceView>();
        }

        /// <summary>Editor/builder setup of the seats (names, colors, bot flags, animals). Takes effect on the next match.</summary>
        public void SetParticipants(IEnumerable<ParticipantConfig> seats)
        {
            participants = seats != null ? new List<ParticipantConfig>(seats) : new List<ParticipantConfig>();
        }

        public IReadOnlyList<ParticipantConfig> Participants => participants;

        /// <summary>Speeds up or slows down playback (tests use small values).</summary>
        public void SetTimings(float step, float botRoll, float botChoose, float pause)
        {
            stepDuration = Mathf.Max(0.01f, step);
            botRollDelay = Mathf.Max(0f, botRoll);
            botChooseDelay = Mathf.Max(0f, botChoose);
            eventPause = Mathf.Max(0f, pause);
            // Feedback (dice flicker, card banner pause) keeps its proportion to the step speed.
            _feedbackScale = Mathf.Clamp01(stepDuration / 0.16f);
        }

        public bool TryGetPiece(int playerId, out PlayerPieceView piece) => _pieceById.TryGetValue(playerId, out piece);

        public bool TryGetPlayer(int playerId, out PlayerDisplay player)
        {
            foreach (var p in _players)
            {
                if (p.Id != playerId) continue;
                player = p;
                return true;
            }
            player = default;
            return false;
        }

        public bool IsBotPlayer(int playerId) => _session != null && _session.IsBot(playerId);

        public CardDefinitionAsset GetCardAsset(string cardId) =>
            cardId != null && _cardAssets.TryGetValue(cardId, out var asset) ? asset : null;

        public string PlayerName(int playerId) => NameOf(playerId);

        private void Awake()
        {
            if (board != null && config != null) board.SetCircuit(config.Circuit);
            if (cameraRig != null) cameraRig.Configure(board, 38f);
            if (hud != null && hud.MainMenu != null)
            {
                hud.MainMenu.ConfigureSetup(participants, seats => SetParticipants(seats));
                if (config != null && config.Circuit) hud.MainMenu.UseCircuitInstructions();
            }
            if (hud != null)
            {
                hud.RollRequested += OnRollRequested;
                hud.DieChosen += OnDieChosen;
                hud.PlayAgainRequested += OnPlayAgainRequested;
                hud.StartGameRequested += OnStartGameRequested;
                hud.CardSlotClicked += OnCardSlotClicked;
                hud.CardTargetClicked += OnCardTargetClicked;
                hud.CardPlayRequested += OnCardPlayRequested;
                hud.CardCancelRequested += OnCardCancelRequested;
            }
        }

        private void Start()
        {
            if (autoStart) BeginMatch();
        }

        private void OnDestroy()
        {
            if (hud != null)
            {
                hud.RollRequested -= OnRollRequested;
                hud.DieChosen -= OnDieChosen;
                hud.PlayAgainRequested -= OnPlayAgainRequested;
                hud.StartGameRequested -= OnStartGameRequested;
                hud.CardSlotClicked -= OnCardSlotClicked;
                hud.CardTargetClicked -= OnCardTargetClicked;
                hud.CardPlayRequested -= OnCardPlayRequested;
                hud.CardCancelRequested -= OnCardCancelRequested;
            }
            DisposeSession();
        }

        private void LateUpdate()
        {
            bool resized = Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight;
            if (!resized && !_viewportPending) return;
            // Layout groups settle one frame after a resize, so the viewport is applied again next frame.
            _viewportPending = resized;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            if (hud == null || cameraRig == null) return;
            Canvas.ForceUpdateCanvases();
            cameraRig.SetViewport(hud.GetBoardViewport());
        }

        /// <summary>
        /// Creates a new session for the given participants (or the Inspector list) and starts the match.
        /// Returns false when the setup is invalid (logged).
        /// </summary>
        public bool BeginMatch(IReadOnlyList<ParticipantConfig> setup = null, int? seed = null)
        {
            if (hud == null || board == null)
            {
                Debug.LogError("[MatchDirector] HUD and BoardView references are required.", this);
                return false;
            }
            if (setup != null) participants = new List<ParticipantConfig>(setup);
            if (seed.HasValue)
            {
                useFixedSeed = true;
                fixedSeed = seed.Value;
                _seedCounter = 0; // first match of this setup uses exactly `seed`
            }

            try
            {
                _rules = config != null ? config.ToRulesConfig() : MatchRulesConfig.Default;
                _cardCatalog = config != null ? config.ToCardCatalog() : CardCatalog.CreateDefault();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"[MatchDirector] Invalid match config: {e.Message}", this);
                return false;
            }

            if (participants.Count < _rules.MinPlayers || participants.Count > _rules.MaxPlayers)
            {
                Debug.LogError($"[MatchDirector] {participants.Count} participants; the rules allow " +
                               $"{_rules.MinPlayers}-{_rules.MaxPlayers}.", this);
                return false;
            }

            DisposeSession();
            // Snapshot per session: Inspector edits during a match cannot desync names/colors from player ids.
            _seats = participants.ToArray();
            board.EnsureBuilt(_rules.TileCount);
            PreparePieces();

            var slots = new PlayerSlot[_seats.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = new PlayerSlot(i + 1, _seats[i].isBot);
            _cardAssets.Clear();
            if (config != null)
            {
                foreach (var card in config.Cards)
                {
                    if (card != null) _cardAssets[card.Id] = card;
                }
            }
            _movementPreview = new MovementService(_rules);
            _session = new MatchSession(_rules, slots, CreateRandomSource, null, _cardCatalog, CreateCardRandomSource);
            _session.EventPublished += OnEvent;
            _session.CommandRejected += OnCommandRejected;
            _session.StartMatch();
            Pump();
            return true;
        }

        /// <summary>Discards the current match (any phase) and starts a new one, without reloading the scene.</summary>
        public void Restart()
        {
            if (_session == null)
            {
                BeginMatch();
                return;
            }
            StopPlayback();
            ClearCardSelection();
            _session.RestartMatch();
            Pump();
        }

        /// <summary>Card draw stream, derived from the match seed (called right after <see cref="CreateRandomSource"/>).</summary>
        private IRandomSource CreateCardRandomSource() => new SeededRandomSource(CardSeedFor(LastSeed));

        /// <summary>Seed of the card draw stream for a match seed (logged seeds reproduce hands too).</summary>
        public static int CardSeedFor(int matchSeed) => unchecked(matchSeed * 486187739 + 0x5EED);

        private IRandomSource CreateRandomSource()
        {
            LastSeed = useFixedSeed
                ? unchecked(fixedSeed + _seedCounter)
                : unchecked(Environment.TickCount * 397 ^ (_seedCounter + 1) * 7919);
            _seedCounter++;
            Debug.Log($"[MatchDirector] Match seed: {LastSeed}", this);
            return new SeededRandomSource(LastSeed);
        }

        private void DisposeSession()
        {
            StopPlayback();
            if (_session == null) return;
            _session.EventPublished -= OnEvent;
            _session.CommandRejected -= OnCommandRejected;
            _session = null;
        }

        private void StopPlayback()
        {
            if (_processRoutine != null) StopCoroutine(_processRoutine);
            if (_botRoutine != null) StopCoroutine(_botRoutine);
            _processRoutine = null;
            _botRoutine = null;
            _processing = false;
            _queue.Clear();
        }

        private void PreparePieces()
        {
            _pieceById.Clear();
            _activePieces.Clear();

            var pool = new List<PlayerPieceView>();
            foreach (var p in scenePieces)
            {
                if (p != null) pool.Add(p);
            }
            while (pool.Count < _seats.Length)
            {
                if (piecePrefab == null)
                {
                    pool.Add(PlayerPieceView.CreateDefault(null, piecesRoot));
                }
                else
                {
                    pool.Add(Instantiate(piecePrefab, piecesRoot));
                }
                scenePieces.Add(pool[pool.Count - 1]); // remembered so restarts reuse instead of duplicating
            }

            for (int i = 0; i < pool.Count; i++)
            {
                var piece = pool[i];
                bool used = i < _seats.Length;
                piece.gameObject.SetActive(used);
                if (!used) continue;
                int id = i + 1;
                piece.Configure(id, _seats[i].color);
                piece.SetCharacter(_seats[i].character);
                piece.SetShielded(false);
                piece.SetVisualState(PieceVisualState.Normal);
                piece.SetLogicalPosition(0);
                _pieceById[id] = piece;
                _activePieces.Add(piece);
            }
            board.PlacePieces(_activePieces);
        }

        // ---------------- Input (human) ----------------

        private void OnRollRequested()
        {
            if (!CanHumanAct(MatchPhase.AwaitingRoll)) return;
            _session.RollDice(_session.State.CurrentPlayerId);
            Pump();
        }

        private void OnDieChosen(int dieIndex)
        {
            if (!CanHumanAct(MatchPhase.AwaitingDiceChoice)) return;
            _session.ChooseDie(_session.State.CurrentPlayerId, dieIndex);
            Pump();
        }

        private void OnPlayAgainRequested() => Restart();

        /// <summary>Main menu "Play": first match of the session, or a fresh one after returning to the menu.</summary>
        private void OnStartGameRequested() => BeginMatch();

        // ---------------- Input (cards) ----------------
        // Selection is presentation state only; legality always comes from the rules layer (ValidateCardPlay),
        // and the final PlayCard is validated again by the domain.

        private bool CanHumanUseCards =>
            _session != null && !IsBusy && !_session.IsBotTurn && !_session.State.IsFinished &&
            (_session.State.Phase == MatchPhase.AwaitingRoll || _session.State.Phase == MatchPhase.AwaitingDiceChoice);

        private void OnCardSlotClicked(int slot)
        {
            if (!CanHumanUseCards) return;
            var hand = HandOf(_session.State.CurrentPlayerId);
            if (hand == null || slot < 0 || slot >= hand.Count) return;
            var card = hand[slot];
            if (_selectedCard == card.InstanceId)
            {
                ClearCardSelection(); // clicking the selected card again deselects it
            }
            else if (IsCardPlayable(card))
            {
                _selectedCard = card.InstanceId;
                _selectedTarget = -1;
            }
            RefreshHud();
        }

        private void OnCardTargetClicked(int playerId)
        {
            if (!CanHumanUseCards || _selectedCard == null) return;
            if (_session.Engine.ValidateCardPlay(_session.State.CurrentPlayerId, _selectedCard, playerId) != CommandError.None) return;
            _selectedTarget = playerId;
            RefreshHud();
        }

        private void OnCardPlayRequested()
        {
            if (!CanHumanUseCards || _selectedCard == null) return;
            string card = _selectedCard;
            int? target = _selectedTarget >= 0 ? _selectedTarget : (int?)null;
            ClearCardSelection();
            _session.PlayCard(_session.State.CurrentPlayerId, card, target); // a rejection is logged, card kept
            Pump();
            RefreshHud();
        }

        private void OnCardCancelRequested()
        {
            ClearCardSelection(); // nothing was sent to the rules layer: the card is not consumed
            RefreshHud();
        }

        private void ClearCardSelection()
        {
            _selectedCard = null;
            _selectedTarget = -1;
        }

        private List<CardInstance> HandOf(int playerId) =>
            playerId >= 0 && _hands.TryGetValue(playerId, out var hand) ? hand : null;

        private bool IsCardPlayable(CardInstance card)
        {
            int current = _session.State.CurrentPlayerId;
            var error = _session.Engine.ValidateCardPlay(current, card.InstanceId);
            if (error == CommandError.None) return true;
            if (error != CommandError.TargetRequired) return false;
            foreach (var p in _session.State.Players)
            {
                if (_session.Engine.ValidateCardPlay(current, card.InstanceId, p.Id) == CommandError.None) return true;
            }
            return false;
        }

        private string CardName(string cardId) =>
            _cardAssets.TryGetValue(cardId, out var asset) && !string.IsNullOrEmpty(asset.DisplayName) ? asset.DisplayName : cardId;

        private bool CanHumanAct(MatchPhase phase) =>
            _session != null && !IsBusy && !_session.IsBotTurn && _session.State.Phase == phase;

        private void OnCommandRejected(CommandError error)
        {
            Debug.LogWarning($"[MatchDirector] Command rejected: {error}", this);
            CommandRejected?.Invoke(error);
        }

        // ---------------- Event playback ----------------

        // Events of a command are only queued here; Pump() starts playback once the command returned,
        // so the whole batch is presented in order.
        private void OnEvent(MatchEvent e) => _queue.Enqueue(e);

        private void Pump()
        {
            if (_processing || _queue.Count == 0 || !isActiveAndEnabled) return;
            _processing = true;
            _processRoutine = StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _processing = true;
            RefreshHud();
            while (_queue.Count > 0)
            {
                var e = _queue.Dequeue();
                EventPresenting?.Invoke(e);
                yield return Present(e);
                RefreshHud();
            }
            _processing = false;
            _processRoutine = null;
            RefreshHud();
            ScheduleBot();
        }

        private IEnumerator Present(MatchEvent e)
        {
            var text = hud.Text;
            switch (e)
            {
                case MatchStartedEvent started:
                    ResetPresentation(started);
                    Log(string.Format(text.matchStarted, started.LuckyNumber));
                    break;

                case RoundStartedEvent round:
                    _round = round.RoundNumber;
                    Log(string.Format(text.roundStarted, round.RoundNumber));
                    break;

                case UnluckyNumberDrawnEvent unlucky:
                    _unlucky = unlucky.UnluckyNumber;
                    if (_rules != null) _rules.GetUnluckyZone(_unlucky, _lucky, _unluckyZone); else _unluckyZone.Clear();
                    board.SetHighlights(_lucky, _unlucky, _unluckyZone);
                    Log(HasUnluckyZone ? string.Format(text.unluckyZoneDrawn, UnluckyDisplayFor(_unlucky))
                        : string.Format(text.unluckyDrawn, unlucky.UnluckyNumber));
                    RefreshHud(); // show the new number during the pause
                    yield return Pause(eventPause * 2f);
                    break;

                case TurnChangedEvent turn:
                    _currentId = turn.PlayerId;
                    if (cameraRig != null && _pieceById.TryGetValue(turn.PlayerId, out var focusPiece)) cameraRig.FocusOn(focusPiece.transform);
                    _lastRoll.Clear();
                    _selectedDie = -1;
                    ClearCardSelection(); // pending card/target selection never crosses turns
                    Log(string.Format(text.turnChanged, NameOf(turn.PlayerId)));
                    break;

                case DebuffChangedEvent debuff:
                    for (int i = 0; i < _players.Count; i++)
                    {
                        if (_players[i].Id != debuff.PlayerId) continue;
                        var p = _players[i];
                        p.Debuff = debuff.Effect;
                        p.DebuffRound = debuff.ActiveRound;
                        p.StickyNumber = debuff.StickyNumber;
                        _players[i] = p;
                    }
                    Log(debuff.Effect.HasValue
                        ? $"{NameOf(debuff.PlayerId)}: {DebuffLabel(debuff.Effect.Value)} in round {debuff.ActiveRound}."
                        : $"The effect on {NameOf(debuff.PlayerId)} wore off.");
                    break;

                case FrozenTurnEvent frozen:
                    Log($"{NameOf(frozen.PlayerId)} is frozen. Danger is resolved at the end of the round.");
                    yield return Pause(eventPause * 2f);
                    break;

                case DiceRolledEvent rolled:
                    _lastRoll.Clear();
                    for (int i = 0; i < rolled.Roll.Count; i++) _lastRoll.Add(rolled.Roll[i]);
                    _selectedDie = -1;
                    yield return PlayRollAnimation(rolled.PlayerId);
                    Log(string.Format(text.diceRolled, NameOf(rolled.PlayerId), rolled.Roll[0],
                        rolled.Roll.Count > 1 ? rolled.Roll[1] : rolled.Roll[0]));
                    break;

                case DieSelectedEvent selected:
                    _selectedDie = selected.DieIndex;
                    Log(string.Format(text.dieSelected, NameOf(selected.PlayerId), selected.Value));
                    yield return Pause(eventPause);
                    break;

                case PlayerMovedEvent moved:
                    yield return AnimateMove(moved);
                    SetDisplayPosition(moved.PlayerId, moved.Movement.FinalPosition);
                    Log(string.Format(moved.Movement.Bounced ? text.playerMovedRebound : text.playerMoved,
                        NameOf(moved.PlayerId), moved.Movement.FinalPosition));
                    break;

                case PlayerEliminatedEvent eliminated:
                    SetDisplayStatus(eliminated.PlayerId, PlayerDisplayStatus.Eliminated);
                    if (_pieceById.TryGetValue(eliminated.PlayerId, out var lost)) lost.SetVisualState(PieceVisualState.Eliminated);
                    Log(string.Format(text.playerEliminated, NameOf(eliminated.PlayerId), eliminated.Tile));
                    yield return Pause(eventPause * 2f);
                    break;

                case PlayerWonEvent won:
                    SetDisplayStatus(won.PlayerId, PlayerDisplayStatus.Winner);
                    if (_pieceById.TryGetValue(won.PlayerId, out var winner)) winner.SetVisualState(PieceVisualState.Winner);
                    Log(string.Format(text.playerWon, NameOf(won.PlayerId)));
                    break;

                // ---- Cards (messages and visuals only from resolved domain events) ----
                case CardDrawnEvent drawn:
                    HandOrNew(drawn.PlayerId).Add(drawn.Card);
                    if (drawn.Source == CardDrawSource.Granted)
                        Log(string.Format(text.cardGranted, NameOf(drawn.PlayerId), CardName(drawn.Card.CardId)));
                    break;

                case CardPlayedEvent played:
                    if (played.Card.Definition.Effect == CardEffectType.Reroll)
                        Log(string.Format(text.luckyRollActivated, NameOf(played.PlayerId)));
                    yield return Pause(cardPlayPause * _feedbackScale); // let the card banner read first
                    break;

                case CardConsumedEvent consumed:
                    HandOf(consumed.PlayerId)?.Remove(consumed.Card);
                    break;

                case DiceRerolledEvent rerolled:
                    _lastRoll.Clear();
                    for (int i = 0; i < rerolled.Roll.Count; i++) _lastRoll.Add(rerolled.Roll[i]);
                    _selectedDie = -1;
                    yield return PlayRollAnimation(rerolled.PlayerId);
                    Log(string.Format(text.diceRerolled, rerolled.Roll[0], rerolled.Roll.Count > 1 ? rerolled.Roll[1] : rerolled.Roll[0]));
                    yield return Pause(eventPause);
                    break;

                case PlayerPositionsSwappedEvent swapped:
                    yield return AnimateSwap(swapped);
                    SetDisplayPosition(swapped.PlayerId, swapped.PlayerPosition);
                    SetDisplayPosition(swapped.TargetPlayerId, swapped.TargetPosition);
                    Log(string.Format(text.positionsSwapped, NameOf(swapped.PlayerId), NameOf(swapped.TargetPlayerId)));
                    break;

                case PlayerRetreatedEvent retreated:
                    if (retreated.Steps > 0)
                    {
                        var path = new List<int>(retreated.Steps);
                        for (int tile = retreated.From - 1; tile >= retreated.To; tile--) path.Add(tile);
                        yield return AnimatePath(retreated.PlayerId, path, retreated.To);
                        Log(string.Format(text.playerRetreated, NameOf(retreated.PlayerId), retreated.Steps));
                    }
                    else
                    {
                        Log(string.Format(text.playerRetreatedNone, NameOf(retreated.PlayerId)));
                    }
                    SetDisplayPosition(retreated.PlayerId, retreated.To);
                    break;

                case PlayerBumpedEvent bumped:
                    if (bumped.Steps > 0)
                    {
                        var bumpPath = new List<int>(bumped.Steps);
                        for (int tile = bumped.From - 1; tile >= bumped.To; tile--) bumpPath.Add(tile);
                        yield return AnimatePath(bumped.PlayerId, bumpPath, bumped.To);
                    }
                    SetDisplayPosition(bumped.PlayerId, bumped.To);
                    Log(string.Format(text.playerBumped, NameOf(bumped.BumperId), NameOf(bumped.PlayerId), bumped.To));
                    break;

                case ShieldActivatedEvent shield:
                    SetDisplayShield(shield.PlayerId, true);
                    if (_pieceById.TryGetValue(shield.PlayerId, out var shieldedPiece)) shieldedPiece.SetShielded(true);
                    Log(string.Format(text.shieldActivated, NameOf(shield.PlayerId)));
                    break;

                case ShieldConsumedEvent shieldEnd:
                    SetDisplayShield(shieldEnd.PlayerId, false);
                    if (_pieceById.TryGetValue(shieldEnd.PlayerId, out var unshielded)) unshielded.SetShielded(false);
                    Log(shieldEnd.BlockedElimination
                        ? string.Format(text.shieldBlocked, NameOf(shieldEnd.PlayerId), shieldEnd.Tile)
                        : string.Format(text.shieldExpired, NameOf(shieldEnd.PlayerId)));
                    if (shieldEnd.BlockedElimination) yield return Pause(eventPause * 2f);
                    break;

                case MatchFinishedEvent finished:
                    if (cameraRig != null) cameraRig.FocusOn(null);
                    ClearCardSelection();
                    _currentId = -1;
                    _lastRoll.Clear();
                    board.PlacePieces(_activePieces);
                    ShowResult(finished);
                    break;
            }
        }

        private void ResetPresentation(MatchStartedEvent started)
        {
            _lucky = started.LuckyNumber;
            _unlucky = 0;
            _round = 0;
            _currentId = -1;
            _selectedDie = -1;
            _lastRoll.Clear();
            _hands.Clear();
            if (cameraRig != null) { cameraRig.FocusOn(null); cameraRig.Frame(); }
            ClearCardSelection();
            hud.ResultScreen.Hide();
            hud.Messages.Clear();

            _players.Clear();
            foreach (int id in started.PlayerIds)
            {
                var setup = _seats[id - 1];
                _players.Add(new PlayerDisplay
                {
                    Id = id, Name = setup.displayName, Color = setup.color, IsBot = setup.isBot,
                    Position = 0, Status = PlayerDisplayStatus.Active,
                    Portrait = setup.character != null ? setup.character.Portrait : null
                });
                if (_pieceById.TryGetValue(id, out var piece))
                {
                    piece.SetShielded(false);
                    piece.SetVisualState(PieceVisualState.Normal);
                    piece.SetLogicalPosition(0);
                }
            }
            board.PlacePieces(_activePieces);
            board.SetHighlights(_lucky, -1);
        }

        private IEnumerator AnimateMove(PlayerMovedEvent moved) =>
            AnimatePath(moved.PlayerId, moved.Movement.Path, moved.Movement.FinalPosition);

        /// <summary>Swap: both pieces hop to each other's tile at the same time, then snap to logical slots.</summary>
        private IEnumerator AnimateSwap(PlayerPositionsSwappedEvent e)
        {
            if (!_pieceById.TryGetValue(e.PlayerId, out var a) || !_pieceById.TryGetValue(e.TargetPlayerId, out var b)) yield break;
            Vector3 fromA = a.transform.position, fromB = b.transform.position;
            Vector3 toA = board.GetTileAnchor(e.PlayerPosition).position;
            Vector3 toB = board.GetTileAnchor(e.TargetPosition).position;
            float duration = stepDuration * 3f * playbackSlowdown;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                Vector3 hop = Vector3.up * (Mathf.Sin(k * Mathf.PI) * hopHeight * 2f);
                a.transform.position = Vector3.Lerp(fromA, toA, k) + hop;
                b.transform.position = Vector3.Lerp(fromB, toB, k) + hop;
                yield return null;
            }
            a.SetLogicalPosition(e.PlayerPosition);
            b.SetLogicalPosition(e.TargetPosition);
            board.PlacePieces(_activePieces);
        }

        private IEnumerator AnimatePath(int playerId, IReadOnlyList<int> path, int finalPosition)
        {
            if (!_pieceById.TryGetValue(playerId, out var piece)) yield break;
            var t = piece.transform;
            float step = stepDuration * playbackSlowdown;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 from = t.position;
                Vector3 to = board.GetTileAnchor(path[i]).position;
                float elapsed = 0f;
                while (elapsed < step)
                {
                    elapsed += Time.deltaTime;
                    float k = Mathf.Clamp01(elapsed / step);
                    t.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * hopHeight);
                    yield return null;
                }
                t.position = to;
                PieceHopped?.Invoke(playerId, path[i]);
            }
            // Logical position is the source of truth; snap all pieces to their slots.
            piece.SetLogicalPosition(finalPosition);
            board.PlacePieces(_activePieces);
        }

        /// <summary>Dice faces flicker (cosmetic only; values come from the already-resolved event).</summary>
        private IEnumerator PlayRollAnimation(int playerId)
        {
            float duration = rollAnimation * _feedbackScale * playbackSlowdown * (IsBotPlayer(playerId) ? 0.5f : 1f);
            if (duration <= 0f || hud.DicePanel == null) yield break;
            hud.DicePanel.PlayRollAnimation(duration);
            yield return new WaitForSeconds(duration);
            hud.DicePanel.StopRollAnimation();
        }

        /// <summary>Waits <paramref name="seconds"/> scaled by <see cref="playbackSlowdown"/>.</summary>
        private IEnumerator Pause(float seconds)
        {
            seconds *= playbackSlowdown;
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }

        private void ShowResult(MatchFinishedEvent finished)
        {
            var text = hud.Text;
            var colors = new List<Color>();
            var names = new List<string>();
            foreach (int id in finished.WinnerIds)
            {
                names.Add(NameOf(id));
                colors.Add(ColorOf(id));
            }

            string title = names.Count == 0 ? "Draw - no survivors" : names.Count == 1
                ? string.Format(text.resultSingleWinner, names[0])
                : string.Format(text.resultSharedWinners, string.Join(text.winnerJoin, names));

            string reason;
            switch (finished.Reason)
            {
                case MatchEndReason.ExactLucky: reason = string.Format(text.reasonExactLucky, _lucky); break;
                case MatchEndReason.LastSurvivor: reason = text.reasonLastSurvivor; break;
                case MatchEndReason.RoundLimit: reason = string.Format(text.reasonRoundLimit, _lucky); break;
                case MatchEndReason.NoSurvivors: reason = "Bad luck caught every remaining player."; break;
                default: reason = string.Empty; break;
            }
            hud.ResultScreen.Show(title, reason, colors);
        }

        // ---------------- Bots ----------------

        private void ScheduleBot()
        {
            if (_session == null || _botRoutine != null || IsBusy || !_session.IsBotTurn) return;
            _botRoutine = StartCoroutine(BotStep(_session.MatchNumber));
        }

        private IEnumerator BotStep(int matchNumber)
        {
            bool rolling = _session.State.Phase == MatchPhase.AwaitingRoll;
            yield return Pause(rolling ? botRollDelay : botChooseDelay);
            _botRoutine = null;
            // Stale step (restart, finished match, or someone else acted): do nothing.
            if (_session == null || _session.MatchNumber != matchNumber || IsBusy || !_session.IsBotTurn) yield break;
            _session.StepBot(); // same RollDice/ChooseDie commands as a human
            Pump();
            ScheduleBot(); // only continues if StepBot produced no playback (defensive)
        }

        // ---------------- HUD ----------------

        private void RefreshHud()
        {
            if (hud == null || _session == null) return;
            var text = hud.Text;
            var state = _session.State;
            bool finished = state.IsFinished;
            bool idle = !IsBusy;
            // While events play back, the displayed turn (_currentId) can lag behind the domain's current player.
            bool shownIsBot = _currentId >= 0 && _session.IsBot(_currentId);
            bool humanTurn = idle && !finished && _currentId >= 0 && !shownIsBot && !_session.IsBotTurn;

            hud.MatchInfo.Show(text, _lucky, UnluckyDisplayFor(_unlucky), _round, _rules.MaxRounds,
                _currentId >= 0 ? NameOf(_currentId) : null, _currentId >= 0 ? ColorOf(_currentId) : Color.clear);
            hud.PlayerStatus.Show(text, _players, _currentId);

            bool canRoll = idle && humanTurn && state.Phase == MatchPhase.AwaitingRoll;
            bool canChoose = idle && humanTurn && state.Phase == MatchPhase.AwaitingDiceChoice;
            string status = !finished && shownIsBot
                ? string.Format(text.botThinking, NameOf(_currentId))
                : string.Empty;
            bool frozenTurn = !finished && state.GetPlayer(state.CurrentPlayerId)?.HasDebuff(CardEffectType.Freeze, state.RoundNumber) == true;
            if (frozenTurn) status = "Frozen: play a card or end your turn. Danger at the end of the round.";
            hud.DicePanel.Show(text, _lastRoll.Count > 0 ? _lastRoll : null, canRoll, canChoose, _selectedDie, status);
            hud.DicePanel.SetRollLabel(frozenTurn ? "End turn" : text.rollButton);
            var currentPlayer = state.GetPlayer(state.CurrentPlayerId);
            var engine = _session.Engine;
            int dangerFor = currentPlayer != null ? currentPlayer.Id : -1;
            hud.DicePanel.ShowDestinations(text, ComputeDestinations(canChoose), _lucky,
                tile => engine != null && dangerFor >= 0 && engine.IsDangerous(dangerFor, tile),
                tile => engine != null && dangerFor >= 0 && engine.CountBumpVictims(dangerFor, tile) > 0);

            if (hud.CardHand != null) RefreshCardHand(text, state, humanTurn, shownIsBot, idle, finished);
        }

        private void RefreshCardHand(HudText text, MatchState state, bool humanTurn, bool shownIsBot, bool idle, bool finished)
        {
            bool canUse = humanTurn && CanHumanUseCards;
            var hand = HandOf(_currentId);

            // Drop a selection the rules no longer allow (phase changed, card gone, ...).
            CardInstance selected = null;
            if (_selectedCard != null)
            {
                selected = hand?.Find(c => c.InstanceId == _selectedCard);
                if (!canUse || selected == null || !IsCardPlayable(selected))
                {
                    ClearCardSelection();
                    selected = null;
                }
            }

            _slotBuffer.Clear();
            bool anyAvailable = false;
            if (hand != null)
            {
                foreach (var card in hand)
                {
                    bool available = canUse && IsCardPlayable(card);
                    anyAvailable |= available;
                    _cardAssets.TryGetValue(card.CardId, out var asset);
                    _slotBuffer.Add(new CardSlotDisplay
                    {
                        InstanceId = card.InstanceId,
                        Name = CardName(card.CardId),
                        Description = asset != null ? asset.ShortDescription : string.Empty,
                        Art = asset != null ? asset.Art : null,
                        Available = available,
                        Selected = selected != null && card.InstanceId == selected.InstanceId
                    });
                }
            }

            List<CardTargetDisplay> targets = null;
            string hint;
            if (finished || _currentId < 0) hint = string.Empty;
            else if (shownIsBot) hint = string.Format(text.hintBotHand, NameOf(_currentId));
            else if (!idle) hint = text.hintWaiting;
            else if (state.CardPlayedThisTurn) hint = text.hintCardUsed;
            else if (selected != null && selected.Definition.RequiresTarget)
            {
                hint = string.Format(text.hintPickTarget, CardName(selected.CardId));
                _targetBuffer.Clear();
                foreach (var p in state.Players)
                {
                    if (_session.Engine.ValidateCardPlay(state.CurrentPlayerId, selected.InstanceId, p.Id) != CommandError.None) continue;
                    _targetBuffer.Add(new CardTargetDisplay { PlayerId = p.Id, Name = NameOf(p.Id), Color = ColorOf(p.Id) });
                }
                targets = _targetBuffer;
            }
            else if (selected != null) hint = string.Format(text.hintConfirm, CardName(selected.CardId));
            else if (anyAvailable) hint = text.hintPickCard;
            else hint = hand != null && hand.Count > 0 ? text.hintNoPlayable : string.Empty;

            string title = _currentId >= 0 ? string.Format(text.cardsTitleOwner, NameOf(_currentId)) : text.cardsTitle;
            bool canPlay = selected != null && (!selected.Definition.RequiresTarget || _selectedTarget >= 0);
            hud.CardHand.Show(title, _slotBuffer, hint, targets, _selectedTarget, canPlay, selected != null);
        }

        /// <summary>
        /// Where each die would end (rebound included), via the domain MovementService. Read-only query used
        /// only for display; the move itself is resolved by the rules layer when a die is chosen.
        /// </summary>
        private IReadOnlyList<int> ComputeDestinations(bool canChoose)
        {
            _destinations.Clear();
            if (!previewDestinations || !canChoose || _movementPreview == null || _lastRoll.Count == 0) return null;
            var player = _session.State.GetPlayer(_session.State.CurrentPlayerId);
            if (player == null || _lucky <= 0) return null;
            foreach (int value in _lastRoll)
            {
                try { _destinations.Add(_movementPreview.Move(player.Position, value, _lucky).FinalPosition); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
            return _destinations;
        }

        private List<CardInstance> HandOrNew(int playerId)
        {
            if (!_hands.TryGetValue(playerId, out var hand))
            {
                hand = new List<CardInstance>(3);
                _hands[playerId] = hand;
            }
            return hand;
        }

        private void SetDisplayShield(int id, bool shielded)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id != id) continue;
                var p = _players[i];
                p.Shielded = shielded;
                _players[i] = p;
            }
        }

        private void Log(string message) => hud.Messages.Add(message);

        public static string DebuffLabel(CardEffectType type) => type == CardEffectType.Freeze ? "Freeze" :
            type == CardEffectType.HeavyFeet ? "Heavy Feet" : type == CardEffectType.CrackedDie ? "Cracked Die" : "Sticky Bad Luck";

        private string NameOf(int id) =>
            id >= 1 && id <= _seats.Length ? _seats[id - 1].displayName : $"P{id}";

        private Color ColorOf(int id) =>
            id >= 1 && id <= _seats.Length ? _seats[id - 1].color : Color.white;

        private void SetDisplayPosition(int id, int position)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id != id) continue;
                var p = _players[i];
                p.Position = position;
                _players[i] = p;
            }
        }

        private void SetDisplayStatus(int id, PlayerDisplayStatus status)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id != id) continue;
                var p = _players[i];
                p.Status = status;
                _players[i] = p;
            }
        }
    }
}
