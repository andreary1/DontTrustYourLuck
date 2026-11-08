using System;
using System.Collections;
using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using DontTrustYourLuck.Presentation.Board;
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
            new ParticipantConfig("Jaguar", HudPalette.Hex(0x287FA3), false),
            new ParticipantConfig("Macaw", HudPalette.Hex(0xED638B), true),
            new ParticipantConfig("Monkey", HudPalette.Hex(0x438B52), true),
            new ParticipantConfig("Capybara", HudPalette.Hex(0x713E91), true),
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

        private readonly Queue<MatchEvent> _queue = new Queue<MatchEvent>();
        private readonly Dictionary<int, PlayerPieceView> _pieceById = new Dictionary<int, PlayerPieceView>();
        private readonly List<PlayerPieceView> _activePieces = new List<PlayerPieceView>();
        private readonly List<PlayerDisplay> _players = new List<PlayerDisplay>();
        private readonly List<int> _lastRoll = new List<int>(2);

        private MatchRulesConfig _rules;
        private MatchSession _session;
        private bool _processing;
        private Coroutine _processRoutine;
        private Coroutine _botRoutine;
        private int _seedCounter;
        private ParticipantConfig[] _seats = Array.Empty<ParticipantConfig>();
        private int _lucky;
        private int _unlucky;
        private int _round;
        private int _currentId = -1;
        private int _selectedDie = -1;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _viewportPending;

        public MatchSession Session => _session;
        public GameHUD Hud => hud;
        public BoardView Board => board;
        /// <summary>True while events are being played back; gameplay buttons stay disabled meanwhile.</summary>
        public bool IsBusy => _processing || _queue.Count > 0;
        public int LastSeed { get; private set; }
        public IReadOnlyList<PlayerPieceView> ActivePieces => _activePieces;
        public IReadOnlyList<PlayerDisplay> Players => _players;

        public void Configure(MatchConfigAsset configAsset, BoardView boardView, BoardCameraRig rig, GameHUD gameHud,
            PlayerPieceView prefab, Transform root, List<PlayerPieceView> existingPieces)
        {
            config = configAsset; board = boardView; cameraRig = rig; hud = gameHud;
            piecePrefab = prefab; piecesRoot = root; scenePieces = existingPieces ?? new List<PlayerPieceView>();
        }

        /// <summary>Speeds up or slows down playback (tests use small values).</summary>
        public void SetTimings(float step, float botRoll, float botChoose, float pause)
        {
            stepDuration = Mathf.Max(0.01f, step);
            botRollDelay = Mathf.Max(0f, botRoll);
            botChooseDelay = Mathf.Max(0f, botChoose);
            eventPause = Mathf.Max(0f, pause);
        }

        private void Awake()
        {
            if (hud != null)
            {
                hud.RollRequested += OnRollRequested;
                hud.DieChosen += OnDieChosen;
                hud.PlayAgainRequested += OnPlayAgainRequested;
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
            _session = new MatchSession(_rules, slots, CreateRandomSource);
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
            _session.RestartMatch();
            Pump();
        }

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

        private bool CanHumanAct(MatchPhase phase) =>
            _session != null && !IsBusy && !_session.IsBotTurn && _session.State.Phase == phase;

        private void OnCommandRejected(CommandError error)
        {
            Debug.LogWarning($"[MatchDirector] Command rejected: {error}", this);
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
                    board.SetHighlights(_lucky, _unlucky);
                    Log(string.Format(text.unluckyDrawn, unlucky.UnluckyNumber));
                    RefreshHud(); // show the new number during the pause
                    yield return Pause(eventPause * 2f);
                    break;

                case TurnChangedEvent turn:
                    _currentId = turn.PlayerId;
                    _lastRoll.Clear();
                    _selectedDie = -1;
                    Log(string.Format(text.turnChanged, NameOf(turn.PlayerId)));
                    break;

                case DiceRolledEvent rolled:
                    _lastRoll.Clear();
                    for (int i = 0; i < rolled.Roll.Count; i++) _lastRoll.Add(rolled.Roll[i]);
                    _selectedDie = -1;
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

                case MatchFinishedEvent finished:
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
            hud.ResultScreen.Hide();
            hud.Messages.Clear();

            _players.Clear();
            foreach (int id in started.PlayerIds)
            {
                var setup = _seats[id - 1];
                _players.Add(new PlayerDisplay
                {
                    Id = id, Name = setup.displayName, Color = setup.color, IsBot = setup.isBot,
                    Position = 0, Status = PlayerDisplayStatus.Active
                });
                if (_pieceById.TryGetValue(id, out var piece))
                {
                    piece.SetVisualState(PieceVisualState.Normal);
                    piece.SetLogicalPosition(0);
                }
            }
            board.PlacePieces(_activePieces);
            board.SetHighlights(_lucky, -1);
        }

        private IEnumerator AnimateMove(PlayerMovedEvent moved)
        {
            if (!_pieceById.TryGetValue(moved.PlayerId, out var piece)) yield break;
            var path = moved.Movement.Path;
            var t = piece.transform;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 from = t.position;
                Vector3 to = board.GetTileAnchor(path[i]).position;
                float elapsed = 0f;
                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float k = Mathf.Clamp01(elapsed / stepDuration);
                    t.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * hopHeight);
                    yield return null;
                }
                t.position = to;
            }
            // Logical position is the source of truth; snap all pieces to their slots.
            piece.SetLogicalPosition(moved.Movement.FinalPosition);
            board.PlacePieces(_activePieces);
        }

        private static IEnumerator Pause(float seconds)
        {
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

            string title = names.Count == 1
                ? string.Format(text.resultSingleWinner, names[0])
                : string.Format(text.resultSharedWinners, string.Join(text.winnerJoin, names));

            string reason;
            switch (finished.Reason)
            {
                case MatchEndReason.ExactLucky: reason = string.Format(text.reasonExactLucky, _lucky); break;
                case MatchEndReason.LastSurvivor: reason = text.reasonLastSurvivor; break;
                case MatchEndReason.RoundLimit: reason = string.Format(text.reasonRoundLimit, _lucky); break;
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

            hud.MatchInfo.Show(text, _lucky, _unlucky, _round, _rules.MaxRounds,
                _currentId >= 0 ? NameOf(_currentId) : null, _currentId >= 0 ? ColorOf(_currentId) : Color.clear);
            hud.PlayerStatus.Show(text, _players, _currentId);

            bool canRoll = idle && humanTurn && state.Phase == MatchPhase.AwaitingRoll;
            bool canChoose = idle && humanTurn && state.Phase == MatchPhase.AwaitingDiceChoice;
            string status = !finished && shownIsBot
                ? string.Format(text.botThinking, NameOf(_currentId))
                : string.Empty;
            hud.DicePanel.Show(text, _lastRoll.Count > 0 ? _lastRoll : null, canRoll, canChoose, _selectedDie, status);
        }

        private void Log(string message) => hud.Messages.Add(message);

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
