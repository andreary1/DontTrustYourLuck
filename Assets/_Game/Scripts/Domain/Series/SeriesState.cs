using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Series
{
    /// <summary>Series lifecycle. Between rounds the series waits in <see cref="Intermission"/> for StartNextRound.</summary>
    public enum SeriesPhase
    {
        NotStarted,
        RoundInProgress,
        Intermission,
        Finished
    }

    /// <summary>
    /// Mutable state of one series (scoreboard and round bookkeeping). Read-only outside the domain; mutated
    /// only by <see cref="SeriesEngine"/>. Per-round state (positions, turn, hands) lives in the round's MatchState.
    /// </summary>
    public sealed class SeriesState
    {
        internal readonly List<int> PlayerIdList = new List<int>();
        internal readonly Dictionary<int, int> ScoreMap = new Dictionary<int, int>();
        internal readonly List<int> ParticipantList = new List<int>();
        internal readonly List<int> LastScorerList = new List<int>();

        public SeriesPhase Phase { get; internal set; } = SeriesPhase.NotStarted;
        /// <summary>1-based number of the current (or last finished) round; 0 before the series starts.</summary>
        public int RoundNumber { get; internal set; }
        /// <summary>True when the current round is a sudden-death tiebreaker among the tied leaders (D7).</summary>
        public bool IsTiebreaker { get; internal set; }
        /// <summary>Champion id once <see cref="SeriesPhase.Finished"/>; -1 otherwise.</summary>
        public int ChampionId { get; internal set; } = -1;
        /// <summary>How the last finished round ended.</summary>
        public MatchEndReason LastRoundReason { get; internal set; } = MatchEndReason.None;

        /// <summary>Every player of the series, ascending id.</summary>
        public IReadOnlyList<int> PlayerIds => PlayerIdList;
        /// <summary>Players taking part in the current round (all, or only the tied leaders in a tiebreaker).</summary>
        public IReadOnlyList<int> RoundParticipants => ParticipantList;
        /// <summary>Players who scored in the last finished round.</summary>
        public IReadOnlyList<int> LastRoundScorers => LastScorerList;

        public bool IsFinished => Phase == SeriesPhase.Finished;

        /// <summary>Points of a player (0 for unknown ids).</summary>
        public int GetScore(int playerId) => ScoreMap.TryGetValue(playerId, out int score) ? score : 0;

        /// <summary>Highest score of the series (0 before any point).</summary>
        public int TopScore
        {
            get
            {
                int best = 0;
                foreach (var pair in ScoreMap)
                {
                    if (pair.Value > best) best = pair.Value;
                }
                return best;
            }
        }
    }
}
