using System;
using System.Collections.Generic;

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
    /// Rejected commands consume no random values.
    ///
    /// Resolution order after a move (TDD §9.3, Sprint 2 spec §4.4):
    ///   position -> lucky (win, ends immediately) -> unlucky (elimination) -> last survivor -> next turn/round
    ///   -> round limit (MaxRounds; highest-position active players win, ties share).
    /// </summary>
    public sealed class MatchEngine
    {
        private readonly MatchRulesConfig _rules;
        private readonly LuckNumberService _luck;
        private readonly DiceService _dice;
        private readonly MovementService _movement;
        private readonly MatchState _state = new MatchState();

        public MatchState State => _state;
        public MatchRulesConfig Rules => _rules;

        public MatchEngine(MatchRulesConfig rules, IRandomSource random)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (random == null) throw new ArgumentNullException(nameof(random));
            _luck = new LuckNumberService(random, rules);
            _dice = new DiceService(random, rules);
            _movement = new MovementService(rules);
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
            foreach (int id in ids) _state.PlayerList.Add(new PlayerState(id));

            _state.LuckyNumber = _luck.DrawLuckyNumber();
            events.Add(new MatchStartedEvent(_state.LuckyNumber, ids.AsReadOnly()));

            BeginRound(1, events);
            return CommandResult.Ok(events);
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

            // 2. Lucky number: immediate win.
            if (player.Position == _state.LuckyNumber)
            {
                Finish(MatchEndReason.ExactLucky, new List<int> { playerId }, events);
                return CommandResult.Ok(events);
            }

            // 3-4. Unlucky number: elimination (only on the final tile; passing through is safe).
            if (player.Position == _state.UnluckyNumber)
            {
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
