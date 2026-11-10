using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain.Cards;

namespace DontTrustYourLuck.Domain.Match
{
    /// <summary>Outcome of a command: success flag, error code and the ordered events it produced.</summary>
    public sealed class CommandResult
    {
        private static readonly MatchEvent[] NoEvents = Array.Empty<MatchEvent>();

        public bool Success => Error == CommandError.None;
        public CommandError Error { get; }
        public IReadOnlyList<MatchEvent> Events { get; }

        private CommandResult(CommandError error, IReadOnlyList<MatchEvent> events)
        {
            Error = error;
            Events = events ?? NoEvents;
        }

        internal static CommandResult Ok(List<MatchEvent> events) => new CommandResult(CommandError.None, events);
        internal static CommandResult Fail(CommandError error) => Rejected(error);

        /// <summary>Creates a rejected result (for orchestration layers that validate before reaching the engine).</summary>
        public static CommandResult Rejected(CommandError error)
        {
            if (error == CommandError.None) throw new ArgumentException("A rejection needs an error.", nameof(error));
            return new CommandResult(error, NoEvents);
        }

        public override string ToString() => Success ? $"OK ({Events.Count} events)" : $"Rejected: {Error}";
    }

    /// <summary>
    /// Rules engine and state machine for one match (TDD §6, §9). Pure C#, deterministic for a given
    /// <see cref="IRandomSource"/>. One engine = one match; restarting means creating a new engine.
    ///
    /// Random draw order (documented for reproducibility):
    ///   1. Lucky number (once, StartMatch)
    ///   2. Unlucky number of round 1
    ///   3. Per turn: one value per die, in die-index order (RollDice)
    ///   4. Unlucky number at the start of every following round
    ///   5. Lucky Roll card: one value per die, from the same stream as normal rolls, when the card resolves
    /// Card draws (starting hands; StartingHandSize per player in id order, NextInt(0, catalog size) each) use the
    /// separate <see cref="CardSetup.Random"/> stream, so cards never shift the sequence above.
    /// Rejected commands consume no random values.
    ///
    /// Turn flow with cards (TDD §9.3, §10.2):
    ///   AwaitingRoll [+ optional pre-roll card: Swap / Retreat / Shield] -> RollDice ->
    ///   AwaitingDiceChoice [+ optional Lucky Roll] -> ChooseDie -> resolution. At most one card per turn.
    /// Resolution order after a move:
    ///   position -> lucky (win, ends immediately) -> unlucky (Shield blocks the elimination, else elimination)
    ///   -> shield expires -> last survivor -> next turn/round
    ///   -> round limit (MaxRounds; highest-position active players win, ties share).
    /// Card moves (Swap, Retreat) never resolve lucky/unlucky tiles.
    /// </summary>
    public sealed class MatchEngine
    {
        private readonly MatchRulesConfig _rules;
        private readonly LuckNumberService _luck;
        private readonly DiceService _dice;
        private readonly DiceService _singleDie;
        private readonly MovementService _movement;
        private readonly MatchState _state = new MatchState();
        private readonly CardSetup _cards;
        private readonly CardEffectResolver _cardResolver;

        public MatchState State => _state;
        public MatchRulesConfig Rules => _rules;
        /// <summary>True when the match was created with a <see cref="CardSetup"/>.</summary>
        public bool CardsEnabled => _cards != null;
        /// <summary>Card catalog of this match, or null when cards are disabled.</summary>
        public CardCatalog CardCatalog => _cards?.Catalog;

        /// <param name="cards">Enables cards (catalog + card random stream). Null keeps the Sprint 2 dice-only match.</param>
        public MatchEngine(MatchRulesConfig rules, IRandomSource random, CardSetup cards = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (random == null) throw new ArgumentNullException(nameof(random));
            _luck = new LuckNumberService(random, rules);
            _dice = new DiceService(random, rules);
            _singleDie = new DiceService(random, 1, rules.DiceSides);
            _movement = new MovementService(rules);
            _cards = cards;

            // Explicit registration per effect type (TDD §10.1). New effects are added here.
            _cardResolver = new CardEffectResolver();
            _cardResolver.Register(new RerollEffect(RollForCurrentPlayer));
            _cardResolver.Register(new SwapPositionsEffect());
            _cardResolver.Register(new MoveBackEffect());
            _cardResolver.Register(new ShieldEffect());
            _cardResolver.Register(new DebuffEffect(CardEffectType.Freeze));
            _cardResolver.Register(new DebuffEffect(CardEffectType.HeavyFeet));
            _cardResolver.Register(new DebuffEffect(CardEffectType.CrackedDie));
            _cardResolver.Register(new DebuffEffect(CardEffectType.StickyLuck));
        }

        /// <summary>Starts the match. Turn order is ascending player id (decision D13).</summary>
        public CommandResult StartMatch(IReadOnlyList<int> playerIds)
        {
            if (_state.Phase == MatchPhase.Finished) return CommandResult.Fail(CommandError.MatchFinished);
            if (_state.Phase != MatchPhase.NotStarted) return CommandResult.Fail(CommandError.WrongPhase);
            if (playerIds == null || playerIds.Count < _rules.MinPlayers || playerIds.Count > _rules.MaxPlayers)
                return CommandResult.Fail(CommandError.InvalidPlayerCount);

            var ids = new List<int>(playerIds);
            ids.Sort();
            if (ids[0] <= 0) return CommandResult.Fail(CommandError.InvalidPlayerId); // -1 is the "no turn" sentinel
            for (int i = 1; i < ids.Count; i++)
            {
                if (ids[i] == ids[i - 1]) return CommandResult.Fail(CommandError.DuplicatePlayerId);
            }

            var events = new List<MatchEvent>();
            int handCapacity = CardsEnabled ? _rules.MaxHandSize : 0;
            foreach (int id in ids) _state.PlayerList.Add(new PlayerState(id, handCapacity));

            _state.LuckyNumber = _luck.DrawLuckyNumber();
            events.Add(new MatchStartedEvent(_state.LuckyNumber, ids.AsReadOnly()));

            if (CardsEnabled) DealStartingHands(events);

            BeginRound(1, events);
            return CommandResult.Ok(events);
        }

        public CommandResult RollDice(int playerId)
        {
            var error = ValidateTurn(playerId, MatchPhase.AwaitingRoll);
            if (error != CommandError.None) return CommandResult.Fail(error);

            if (_state.GetPlayer(playerId).HasDebuff(CardEffectType.Freeze, _state.RoundNumber))
            {
                var frozenEvents = new List<MatchEvent> { new FrozenTurnEvent(playerId) };
                EndTurn(frozenEvents);
                return CommandResult.Ok(frozenEvents);
            }
            var roll = RollForCurrentPlayer();
            _state.CurrentRoll = roll;
            _state.Phase = MatchPhase.AwaitingDiceChoice;
            return CommandResult.Ok(new List<MatchEvent> { new DiceRolledEvent(playerId, roll) });
        }

        public CommandResult ChooseDie(int playerId, int dieIndex)
        {
            var error = ValidateTurn(playerId, MatchPhase.AwaitingDiceChoice);
            if (error != CommandError.None) return CommandResult.Fail(error);
            var roll = _state.CurrentRoll;
            if (roll == null || dieIndex < 0 || dieIndex >= roll.Count) return CommandResult.Fail(CommandError.InvalidDieIndex);

            var events = new List<MatchEvent>();
            var player = _state.GetPlayer(playerId);
            int value = roll[dieIndex];

            _state.Phase = MatchPhase.ResolvingAction;
            _state.CurrentRoll = null; // consumed: the same roll can never be chosen twice
            events.Add(new DieSelectedEvent(playerId, dieIndex, value));

            // 1. Logical position.
            var movement = _movement.Move(player.Position, value, _state.LuckyNumber);
            player.Position = movement.FinalPosition;
            events.Add(new PlayerMovedEvent(playerId, movement));

            // The shield only covers this move: it is cleared whatever the outcome.
            bool shielded = player.HasShield;
            if (!_rules.PersistentShield) player.HasShield = false;

            // 2. Lucky number: immediate win.
            if (player.Position == _state.LuckyNumber)
            {
                if (shielded) events.Add(new ShieldConsumedEvent(playerId, false, player.Position));
                Finish(MatchEndReason.ExactLucky, new List<int> { playerId }, events);
                return CommandResult.Ok(events);
            }

            bool onUnlucky = IsDangerous(playerId, player.Position);
            if (shielded && (onUnlucky || !_rules.PersistentShield))
            {
                player.HasShield = false;
                // 4. Shield resolves before an elimination is confirmed; the player stays on the tile.
                events.Add(new ShieldConsumedEvent(playerId, onUnlucky, player.Position));
            }
            else if (onUnlucky && !shielded)
            {
                // 3. Unlucky number: elimination (only on the final tile; passing through is safe).
                player.Status = PlayerStatus.Eliminated;
                events.Add(new PlayerEliminatedEvent(playerId, player.Position, EliminationReason.UnluckyTile));

                // 5. Last survivor.
                if (_state.ActivePlayerCount == 1)
                {
                    Finish(MatchEndReason.LastSurvivor, new List<int> { FirstActivePlayerId() }, events);
                    return CommandResult.Ok(events);
                }
            }

            // 6. Next turn or round.
            EndTurn(events);
            return CommandResult.Ok(events);
        }

        /// <summary>
        /// Plays a card from the current player's hand (TDD §6.2 <c>PlayCard</c>). Lucky Roll in AwaitingDiceChoice;
        /// Swap, Retreat and Shield in AwaitingRoll. The phase never advances: the player still rolls/chooses.
        /// All validation happens before any change; a rejected play keeps the card and the per-turn limit.
        /// </summary>
        /// <param name="cardInstanceId">Instance id (distinguishes copies of the same card).</param>
        /// <param name="targetPlayerId">Required for Swap; must be null for cards without target.</param>
        public CommandResult PlayCard(int playerId, string cardInstanceId, int? targetPlayerId = null)
        {
            if (!CardsEnabled)
                return CommandResult.Fail(_state.Phase == MatchPhase.Finished ? CommandError.MatchFinished : CommandError.CardsDisabled);
            return _cardResolver.Resolve(_state, playerId, cardInstanceId, targetPlayerId);
        }

        /// <summary>Validates a card play without changing state (UI availability, future bots).</summary>
        public CommandError ValidateCardPlay(int playerId, string cardInstanceId, int? targetPlayerId = null)
        {
            if (!CardsEnabled) return CommandError.CardsDisabled;
            return _cardResolver.Validate(_state, playerId, cardInstanceId, targetPlayerId);
        }

        /// <summary>
        /// Controlled card grant (tests / future draw tiles). Not a player command and not exposed by MatchSession.
        /// Respects MaxHandSize; consumes no randomness.
        /// </summary>
        internal CommandResult GrantCard(int playerId, string cardId)
        {
            if (!CardsEnabled) return CommandResult.Fail(CommandError.CardsDisabled);
            if (_state.Phase == MatchPhase.Finished) return CommandResult.Fail(CommandError.MatchFinished);
            if (_state.Phase == MatchPhase.NotStarted) return CommandResult.Fail(CommandError.WrongPhase);
            var player = _state.GetPlayer(playerId);
            if (player == null) return CommandResult.Fail(CommandError.UnknownPlayer);
            if (!player.IsActive) return CommandResult.Fail(CommandError.NotYourTurn);
            var definition = _cards.Catalog.Find(cardId);
            if (definition == null) return CommandResult.Fail(CommandError.UnknownCard);
            if (player.Hand.IsFull) return CommandResult.Fail(CommandError.HandFull);

            var events = new List<MatchEvent>();
            AddCard(player, definition, CardDrawSource.Granted, events);
            return CommandResult.Ok(events);
        }

        private void DealStartingHands(List<MatchEvent> events)
        {
            var catalog = _cards.Catalog;
            foreach (var player in _state.PlayerList) // ascending id order
            {
                for (int i = 0; i < _rules.StartingHandSize && !player.Hand.IsFull; i++)
                {
                    // Uniform draw with replacement (D10): duplicates are possible.
                    var definition = catalog.Definitions[_cards.Random.NextInt(0, catalog.Count)];
                    AddCard(player, definition, CardDrawSource.StartingHand, events);
                }
            }
        }

        private void AddCard(PlayerState player, CardDefinition definition, CardDrawSource source, List<MatchEvent> events)
        {
            var card = new CardInstance($"card-{_state.NextCardInstanceNumber++}", definition);
            player.Hand.TryAdd(card);
            events.Add(new CardDrawnEvent(player.Id, card, source));
        }

        /// <summary>
        /// Previews where a die would take a player without changing state. Used by bots and UI hints;
        /// it never consumes randomness.
        /// </summary>
        public MovementResult PreviewMove(int playerId, int steps)
        {
            var player = _state.GetPlayer(playerId) ?? throw new ArgumentException($"Unknown player {playerId}.", nameof(playerId));
            return _movement.Move(player.Position, steps, _state.LuckyNumber);
        }

        public bool IsDangerous(int playerId, int tile)
        {
            var player = _state.GetPlayer(playerId);
            return tile == _state.UnluckyNumber ||
                (player.HasDebuff(CardEffectType.StickyLuck, _state.RoundNumber) && tile == player.StickyUnluckyNumber);
        }

        public int DistanceToLucky(int position) => _rules.Circuit
            ? (_state.LuckyNumber - position + _rules.BoardLength) % _rules.BoardLength
            : _state.LuckyNumber - position;

        private DiceRoll RollForCurrentPlayer()
        {
            var player = _state.GetPlayer(_state.CurrentPlayerId);
            var roll = player.HasDebuff(CardEffectType.CrackedDie, _state.RoundNumber) ? _singleDie.Roll() : _dice.Roll();
            if (!player.HasDebuff(CardEffectType.HeavyFeet, _state.RoundNumber)) return roll;
            var values = new int[roll.Count];
            for (int i = 0; i < values.Length; i++) values[i] = Math.Max(1, roll[i] - 2);
            return new DiceRoll(values);
        }

        private CommandError ValidateTurn(int playerId, MatchPhase expected)
        {
            if (_state.Phase == MatchPhase.Finished) return CommandError.MatchFinished;
            if (_state.Phase != expected) return CommandError.WrongPhase;
            var player = _state.GetPlayer(playerId);
            if (player == null) return CommandError.UnknownPlayer;
            if (playerId != _state.CurrentPlayerId || !player.IsActive) return CommandError.NotYourTurn;
            return CommandError.None;
        }

        private void BeginRound(int roundNumber, List<MatchEvent> events)
        {
            _state.Phase = MatchPhase.RoundStart;
            _state.RoundNumber = roundNumber;
            events.Add(new RoundStartedEvent(roundNumber));

            // No retroactive elimination: players already on the new unlucky tile stay active.
            _state.UnluckyNumber = _luck.DrawUnluckyNumber(_state.LuckyNumber);
            events.Add(new UnluckyNumberDrawnEvent(roundNumber, _state.UnluckyNumber));

            if (CardsEnabled && _rules.CardDrawInterval > 0 && roundNumber > 1 &&
                (roundNumber - 1) % _rules.CardDrawInterval == 0)
            {
                foreach (var player in _state.PlayerList)
                    if (player.IsActive && !player.Hand.IsFull)
                        AddCard(player, _cards.Catalog.Definitions[_cards.Random.NextInt(0, _cards.Catalog.Count)], CardDrawSource.Granted, events);
            }

            StartTurn(FirstActivePlayerId(), events);
        }

        private void StartTurn(int playerId, List<MatchEvent> events)
        {
            _state.CurrentPlayerId = playerId;
            _state.CardPlayedThisTurn = false; // per-turn card limit restored
            _state.Phase = MatchPhase.AwaitingRoll;
            events.Add(new TurnChangedEvent(playerId, _state.RoundNumber));
        }

        private void EndTurn(List<MatchEvent> events)
        {
            _state.Phase = MatchPhase.TurnEnd;
            int next = NextActivePlayerIdAfter(_state.CurrentPlayerId);
            if (next >= 0)
            {
                StartTurn(next, events);
                return;
            }

            // Frozen players get the whole round to escape by card before the standing-hazard check.
            foreach (var player in _state.PlayerList)
            {
                if (player.IsActive && player.HasDebuff(CardEffectType.Freeze, _state.RoundNumber) &&
                    player.Position == _state.UnluckyNumber)
                {
                    if (player.HasShield)
                    {
                        player.HasShield = false;
                        events.Add(new ShieldConsumedEvent(player.Id, true, player.Position));
                    }
                    else
                    {
                        player.Status = PlayerStatus.Eliminated;
                        events.Add(new PlayerEliminatedEvent(player.Id, player.Position, EliminationReason.UnluckyTile));
                    }
                }
                if (player.Debuff.HasValue && player.DebuffRound <= _state.RoundNumber)
                {
                    player.Debuff = null;
                    player.StickyUnluckyNumber = 0;
                    events.Add(new DebuffChangedEvent(player.Id, null, 0, 0));
                }
            }
            if (_state.ActivePlayerCount <= 1)
            {
                // Simultaneous frozen eliminations can leave no survivors: this is a draw.
                Finish(_state.ActivePlayerCount == 1 ? MatchEndReason.LastSurvivor : MatchEndReason.NoSurvivors,
                    _state.ActivePlayerCount == 1 ? new List<int> { FirstActivePlayerId() } : new List<int>(), events);
                return;
            }

            // Every eligible player has acted: the round is over.
            if (_state.RoundNumber >= _rules.MaxRounds)
            {
                Finish(MatchEndReason.RoundLimit, HighestActivePlayers(), events);
                return;
            }

            BeginRound(_state.RoundNumber + 1, events);
        }

        private void Finish(MatchEndReason reason, List<int> winners, List<MatchEvent> events)
        {
            _state.WinnerList.Clear();
            foreach (int id in winners)
            {
                _state.GetPlayer(id).Status = PlayerStatus.Winner;
                _state.WinnerList.Add(id);
                events.Add(new PlayerWonEvent(id, reason));
            }

            _state.EndReason = reason;
            _state.CurrentRoll = null;
            _state.CurrentPlayerId = -1;
            _state.Phase = MatchPhase.Finished;
            events.Add(new MatchFinishedEvent(reason, _state.WinnerList.ToArray(), _state.RoundNumber));
        }

        private int FirstActivePlayerId()
        {
            foreach (var p in _state.PlayerList)
            {
                if (p.IsActive) return p.Id;
            }
            return -1;
        }

        private int NextActivePlayerIdAfter(int playerId)
        {
            foreach (var p in _state.PlayerList) // ascending id order
            {
                if (p.Id > playerId && p.IsActive) return p.Id;
            }
            return -1;
        }

        private List<int> HighestActivePlayers()
        {
            int best = int.MaxValue;
            foreach (var p in _state.PlayerList)
            {
                if (p.IsActive) best = Math.Min(best, DistanceToLucky(p.Position));
            }

            var winners = new List<int>();
            foreach (var p in _state.PlayerList)
            {
                if (p.IsActive && DistanceToLucky(p.Position) == best) winners.Add(p.Id);
            }
            return winners;
        }
    }
}
