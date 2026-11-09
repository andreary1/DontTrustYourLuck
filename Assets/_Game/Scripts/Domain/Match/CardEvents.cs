using DontTrustYourLuck.Domain.Cards;

namespace DontTrustYourLuck.Domain.Match
{
    public enum CardDrawSource
    {
        /// <summary>Dealt at match start.</summary>
        StartingHand,
        /// <summary>Granted during the match (tests only for now; special draw tiles are not designed yet).</summary>
        Granted
    }

    /// <summary>A card entered a player's hand.</summary>
    public sealed class CardDrawnEvent : MatchEvent
    {
        public int PlayerId { get; }
        public CardInstance Card { get; }
        public CardDrawSource Source { get; }
        public CardDrawnEvent(int playerId, CardInstance card, CardDrawSource source)
        {
            PlayerId = playerId;
            Card = card;
            Source = source;
        }
    }

    /// <summary>A card play was validated; effect events follow, then <see cref="CardConsumedEvent"/>.</summary>
    public sealed class CardPlayedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public CardInstance Card { get; }
        /// <summary>Target player id, or -1 when the card has no target.</summary>
        public int TargetPlayerId { get; }
        public CardPlayedEvent(int playerId, CardInstance card, int targetPlayerId)
        {
            PlayerId = playerId;
            Card = card;
            TargetPlayerId = targetPlayerId;
        }
    }

    /// <summary>The played card left the hand (only after its effect was applied).</summary>
    public sealed class CardConsumedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public CardInstance Card { get; }
        public CardConsumedEvent(int playerId, CardInstance card)
        {
            PlayerId = playerId;
            Card = card;
        }
    }

    /// <summary>Lucky Roll: the pending roll was replaced. The previous values can no longer be chosen.</summary>
    public sealed class DiceRerolledEvent : MatchEvent
    {
        public int PlayerId { get; }
        public DiceRoll PreviousRoll { get; }
        public DiceRoll Roll { get; }
        public DiceRerolledEvent(int playerId, DiceRoll previousRoll, DiceRoll roll)
        {
            PlayerId = playerId;
            PreviousRoll = previousRoll;
            Roll = roll;
        }
    }

    /// <summary>Swap: both players exchanged tiles. Never triggers lucky/unlucky resolution.</summary>
    public sealed class PlayerPositionsSwappedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public int TargetPlayerId { get; }
        /// <summary>New tile of <see cref="PlayerId"/> (the target's old tile).</summary>
        public int PlayerPosition { get; }
        /// <summary>New tile of <see cref="TargetPlayerId"/> (the player's old tile).</summary>
        public int TargetPosition { get; }
        public PlayerPositionsSwappedEvent(int playerId, int targetPlayerId, int playerPosition, int targetPosition)
        {
            PlayerId = playerId;
            TargetPlayerId = targetPlayerId;
            PlayerPosition = playerPosition;
            TargetPosition = targetPosition;
        }
    }

    /// <summary>Retreat: the player moved back (clamped at tile 0). Never triggers lucky/unlucky resolution.</summary>
    public sealed class PlayerRetreatedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public int From { get; }
        public int To { get; }
        public int Steps => From - To;
        public PlayerRetreatedEvent(int playerId, int from, int to)
        {
            PlayerId = playerId;
            From = from;
            To = to;
        }
    }

    public sealed class ShieldActivatedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public ShieldActivatedEvent(int playerId) { PlayerId = playerId; }
    }

    /// <summary>The shield ended after the move: it either blocked an elimination or simply expired.</summary>
    public sealed class ShieldConsumedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public bool BlockedElimination { get; }
        /// <summary>Tile where the move ended.</summary>
        public int Tile { get; }
        public ShieldConsumedEvent(int playerId, bool blockedElimination, int tile)
        {
            PlayerId = playerId;
            BlockedElimination = blockedElimination;
            Tile = tile;
        }
    }
}
