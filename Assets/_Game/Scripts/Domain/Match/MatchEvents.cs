using System.Collections.Generic;

namespace DontTrustYourLuck.Domain.Match
{
    /// <summary>
    /// Domain events (TDD §6.3). They carry data, never UI text; presentation formats messages.
    /// Events of one command are emitted in resolution order.
    /// </summary>
    public abstract class MatchEvent
    {
    }

    public sealed class MatchStartedEvent : MatchEvent
    {
        public int LuckyNumber { get; }
        public IReadOnlyList<int> PlayerIds { get; }
        public MatchStartedEvent(int luckyNumber, IReadOnlyList<int> playerIds)
        {
            LuckyNumber = luckyNumber;
            PlayerIds = playerIds;
        }
    }

    public sealed class RoundStartedEvent : MatchEvent
    {
        public int RoundNumber { get; }
        public RoundStartedEvent(int roundNumber) { RoundNumber = roundNumber; }
    }

    public sealed class UnluckyNumberDrawnEvent : MatchEvent
    {
        public int RoundNumber { get; }
        public int UnluckyNumber { get; }
        public UnluckyNumberDrawnEvent(int roundNumber, int unluckyNumber)
        {
            RoundNumber = roundNumber;
            UnluckyNumber = unluckyNumber;
        }
    }

    public sealed class TurnChangedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public int RoundNumber { get; }
        public TurnChangedEvent(int playerId, int roundNumber)
        {
            PlayerId = playerId;
            RoundNumber = roundNumber;
        }
    }

    public sealed class DiceRolledEvent : MatchEvent
    {
        public int PlayerId { get; }
        public DiceRoll Roll { get; }
        public DiceRolledEvent(int playerId, DiceRoll roll)
        {
            PlayerId = playerId;
            Roll = roll;
        }
    }

    public sealed class DieSelectedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public int DieIndex { get; }
        public int Value { get; }
        public DieSelectedEvent(int playerId, int dieIndex, int value)
        {
            PlayerId = playerId;
            DieIndex = dieIndex;
            Value = value;
        }
    }

    public sealed class PlayerMovedEvent : MatchEvent
    {
        public int PlayerId { get; }
        /// <summary>Start, final position and full path (rebound included).</summary>
        public MovementResult Movement { get; }
        public PlayerMovedEvent(int playerId, MovementResult movement)
        {
            PlayerId = playerId;
            Movement = movement;
        }
    }

    public sealed class PlayerEliminatedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public int Tile { get; }
        public EliminationReason Reason { get; }
        public PlayerEliminatedEvent(int playerId, int tile, EliminationReason reason)
        {
            PlayerId = playerId;
            Tile = tile;
            Reason = reason;
        }
    }

    public sealed class PlayerWonEvent : MatchEvent
    {
        public int PlayerId { get; }
        public MatchEndReason Reason { get; }
        public PlayerWonEvent(int playerId, MatchEndReason reason)
        {
            PlayerId = playerId;
            Reason = reason;
        }
    }

    public sealed class MatchFinishedEvent : MatchEvent
    {
        public MatchEndReason Reason { get; }
        public IReadOnlyList<int> WinnerIds { get; }
        public int RoundNumber { get; }
        public MatchFinishedEvent(MatchEndReason reason, IReadOnlyList<int> winnerIds, int roundNumber)
        {
            Reason = reason;
            WinnerIds = winnerIds;
            RoundNumber = roundNumber;
        }
    }
}
