using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Series
{
    /// <summary>
    /// Series-level domain events. They derive from <see cref="MatchEvent"/> so presentation keeps a single
    /// ordered event stream (round events and series events interleaved in resolution order).
    /// </summary>
    public abstract class SeriesEvent : MatchEvent
    {
    }

    /// <summary>A new series started: scores are all zero.</summary>
    public sealed class SeriesStartedEvent : SeriesEvent
    {
        public IReadOnlyList<int> PlayerIds { get; }
        public int PointsToWin { get; }
        public SeriesStartedEvent(IReadOnlyList<int> playerIds, int pointsToWin)
        {
            PlayerIds = playerIds;
            PointsToWin = pointsToWin;
        }
    }

    /// <summary>A round begins (emitted right before the round's MatchStartedEvent).</summary>
    public sealed class SeriesRoundStartedEvent : SeriesEvent
    {
        public int RoundNumber { get; }
        /// <summary>Players taking part (only the tied leaders in a tiebreaker).</summary>
        public IReadOnlyList<int> ParticipantIds { get; }
        public int FirstPlayerId { get; }
        public bool IsTiebreaker { get; }
        public SeriesRoundStartedEvent(int roundNumber, IReadOnlyList<int> participantIds, int firstPlayerId, bool isTiebreaker)
        {
            RoundNumber = roundNumber;
            ParticipantIds = participantIds;
            FirstPlayerId = firstPlayerId;
            IsTiebreaker = isTiebreaker;
        }
    }

    /// <summary>The round ended and its winners scored one point each (emitted after MatchFinishedEvent).</summary>
    public sealed class SeriesPointsAwardedEvent : SeriesEvent
    {
        public int RoundNumber { get; }
        public MatchEndReason Reason { get; }
        public IReadOnlyList<int> ScorerIds { get; }
        /// <summary>Scoreboard after the award, keyed by player id.</summary>
        public IReadOnlyDictionary<int, int> Scores { get; }
        public SeriesPointsAwardedEvent(int roundNumber, MatchEndReason reason, IReadOnlyList<int> scorerIds,
            IReadOnlyDictionary<int, int> scores)
        {
            RoundNumber = roundNumber;
            Reason = reason;
            ScorerIds = scorerIds;
            Scores = scores;
        }
    }

    /// <summary>Several players reached PointsToWin with the same score: the next round is a tiebreaker among them (D7).</summary>
    public sealed class SeriesTiebreakerEvent : SeriesEvent
    {
        public IReadOnlyList<int> PlayerIds { get; }
        public SeriesTiebreakerEvent(IReadOnlyList<int> playerIds) { PlayerIds = playerIds; }
    }

    /// <summary>The series is over.</summary>
    public sealed class SeriesWonEvent : SeriesEvent
    {
        public int ChampionId { get; }
        public int RoundsPlayed { get; }
        public IReadOnlyDictionary<int, int> Scores { get; }
        public SeriesWonEvent(int championId, int roundsPlayed, IReadOnlyDictionary<int, int> scores)
        {
            ChampionId = championId;
            RoundsPlayed = roundsPlayed;
            Scores = scores;
        }
    }
}
