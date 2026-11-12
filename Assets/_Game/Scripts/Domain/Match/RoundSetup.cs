using System.Collections.Generic;

namespace DontTrustYourLuck.Domain.Match
{
    /// <summary>
    /// Optional start-of-match options used by the series layer to chain rounds (R1). Passing null to
    /// <see cref="MatchEngine.StartMatch(IReadOnlyList{int}, RoundSetup)"/> keeps the original behavior:
    /// turn order by ascending id and random starting hands.
    /// </summary>
    public sealed class RoundSetup
    {
        /// <summary>First player of the turn order; the order then continues by ascending id, wrapping around.
        /// 0 = lowest id (default).</summary>
        public int FirstPlayerId { get; }

        /// <summary>
        /// Card ids each player starts with, in hand order (carried over from the previous round). When set,
        /// no random starting hand is dealt: players missing from the map start with an empty hand.
        /// Null = deal random starting hands as usual.
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<string>> StartingHands { get; }

        public RoundSetup(int firstPlayerId = 0, IReadOnlyDictionary<int, IReadOnlyList<string>> startingHands = null)
        {
            FirstPlayerId = firstPlayerId;
            StartingHands = startingHands;
        }
    }
}
