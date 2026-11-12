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
    ///
    /// Series (R1): one engine is one round of a series. An optional <see cref="RoundSetup"/> rotates the turn
    /// order and gives carried-over hands instead of random starting hands (no card randomness consumed then).
    /// "Round" in this class (RoundNumber, MaxRounds) is the turn cycle; the name is kept for compatibility.
    /// </summary>
    public sealed class MatchEngine
    {
        private readonly MatchRulesConfig _rules;
        private readonly LuckNumberService _luck;
        private readonly DiceService _dice;
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
            _movement = new MovementService(rules);
            _cards = cards;

            // Explicit registration per effect type (TDD §10.1). New effects are added here.
            _cardResolver = new CardEffectResolver();
            _cardResolver.Register(new RerollEffect(_dice.Roll));
            _cardResolver.Register(new SwapPositionsEffect());
            _cardResolver.Register(new MoveBackEffect());
            _cardResolver.Register(new ShieldEffect());
        }

        /// <summary>
        /// Starts the match. Turn order is ascending player id (decision D13), optionally rotated to start at
        /// <see cref="RoundSetup.FirstPlayerId"/>. <paramref name="setup"/> = null keeps the original behavior.
        /// </summary>
        public CommandResult StartMatch(IReadOnlyList<int> playerIds, RoundSetup setup = null)
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

            var setupError = ValidateSetup(setup, ids);
            if (setupError != CommandError.None) return CommandResult.Fail(setupError);

            var events = new List<MatchEvent>();
            int handCapacity = CardsEnabled ? _rules.MaxHandSize : 0;
            foreach (int id in ids) _state.PlayerList.Add(new PlayerState(id, handCapacity));

            int firstIndex = setup != null && setup.FirstPlayerId != 0 ? ids.IndexOf(setup.FirstPlayerId) : 0;
            for (int i = 0; i < ids.Count; i++) _state.TurnOrderList.Add(ids[(firstIndex + i) % ids.Count]);

            _state.LuckyNumber = _luck.DrawLuckyNumber();
            events.Add(new MatchStartedEvent(_state.LuckyNumber, ids.AsReadOnly()));

            if (CardsEnabled)
            {
                if (setup?.StartingHands != null) GiveCarriedHands(setup.StartingHands, events);
                else DealStartingHands(events);
            }

            BeginRound(1, events);
            return CommandResult.Ok(events);
        }

        /// <summary>Validates a <see cref="RoundSetup"/> before any state change (rejections never mutate).</summary>
        private CommandError ValidateSetup(RoundSetup setup, List<int> sortedIds)
        {
            if (setup == null) return CommandError.None;
            if (setup.FirstPlayerId != 0 && !sortedIds.Contains(setup.FirstPlayerId)) return CommandError.InvalidPlayerId;
            if (setup.StartingHands == null) return CommandError.None;
            if (!CardsEnabled) return CommandError.CardsDisabled;
            foreach (var pair in setup.StartingHands)
            {
                if (!sortedIds.Contains(pair.Key)) return CommandError.InvalidPlayerId;
                if (pair.Value == null) continue;
                if (pair.Value.Count > _rules.MaxHandSize) return CommandError.HandFull;
                foreach (string cardId in pair.Value)
                {
                    if (_cards.Catalog.Find(cardId) == null) return CommandError.UnknownCard;
                }
            }
            return CommandError.None;
        }

        /// <summary>Gives each player the cards carried over from the previous round (no randomness consumed).</summary>
        private void GiveCarriedHands(IReadOnlyDictionary<int, IReadOnlyList<string>> hands, List<MatchEvent> events)
        {
            foreach (var player in _state.PlayerList) // ascending id order: deterministic instance ids
            {
                if (!hands.TryGetValue(player.Id, out var cardIds) || cardIds == null) continue;
                foreach (string cardId in cardIds)
                {
                    AddCard(player, _cards.Catalog.Find(cardId), CardDrawSource.CarriedOver, events);
                }
            }
        }

        public CommandResult RollDice(int playerId)
        {
            var error = ValidateTurn(playerId, MatchPhase.AwaitingRoll);
            if (error != CommandError.None) return CommandResult.Fail(error);

            var roll = _dice.Roll();
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
            player.HasShield = false;

            // 2. Lucky number: immediate win.
            if (player.Position == _state.LuckyNumber)
            {
                if (shielded) events.Add(new ShieldConsumedEvent(playerId, false, player.Position));
                Finish(MatchEndReason.ExactLucky, new List<int> { playerId }, events);
                return CommandResult.Ok(events);
            }

            bool onUnlucky = player.Position == _state.UnluckyNumber;
            if (shielded)
            {
                // 4. Shield resolves before an elimination is confirmed; the player stays on the tile.
                events.Add(new ShieldConsumedEvent(playerId, onUnlucky, player.Position));
            }
            else if (onUnlucky)
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
            foreach (int id in _state.TurnOrderList)
            {
                if (_state.GetPlayer(id).IsActive) return id;
            }
            return -1;
        }

        /// <summary>Next active player after <paramref name="playerId"/> in this cycle's turn order; -1 at the end of the cycle.</summary>
        private int NextActivePlayerIdAfter(int playerId)
        {
            var order = _state.TurnOrderList;
            for (int i = order.IndexOf(playerId) + 1; i < order.Count; i++)
            {
                if (_state.GetPlayer(order[i]).IsActive) return order[i];
            }
            return -1;
        }

        private List<int> HighestActivePlayers()
        {
            int best = -1;
            foreach (var p in _state.PlayerList)
            {
                if (p.IsActive && p.Position > best) best = p.Position;
            }

            var winners = new List<int>();
            foreach (var p in _state.PlayerList)
            {
                if (p.IsActive && p.Position == best) winners.Add(p.Id);
            }
            return winners;
        }
    }
}
