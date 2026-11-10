using DontTrustYourLuck.Domain.Cards;

namespace DontTrustYourLuck.Domain.Match
{
    public sealed class DebuffChangedEvent : MatchEvent
    {
        public int PlayerId { get; }
        public CardEffectType? Effect { get; }
        public int ActiveRound { get; }
        public int StickyNumber { get; }
        public DebuffChangedEvent(int playerId, CardEffectType? effect, int round, int stickyNumber)
        { PlayerId = playerId; Effect = effect; ActiveRound = round; StickyNumber = stickyNumber; }
    }
    public sealed class FrozenTurnEvent : MatchEvent
    {
        public int PlayerId { get; }
        public FrozenTurnEvent(int playerId) => PlayerId = playerId;
    }
}
