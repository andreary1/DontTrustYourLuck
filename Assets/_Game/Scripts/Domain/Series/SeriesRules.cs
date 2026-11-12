using System;
using System.Collections.Generic;

namespace DontTrustYourLuck.Domain.Series
{
    /// <summary>
    /// Rules of a series ("partida" in the UI): rounds are played until someone reaches
    /// <see cref="PointsToWin"/> (R1, decisions D1/D6). Immutable and validated on construction.
    /// </summary>
    public sealed class SeriesRules
    {
        public const int DefaultPointsToWin = 5;
        public const int DefaultMaxCyclesPerRound = 12;

        /// <summary>Points needed to become champion (3 quick, 5 normal, 7 epic).</summary>
        public int PointsToWin { get; }

        /// <summary>
        /// Cycle cap of one round (overrides <see cref="MatchRulesConfig.MaxRounds"/> for every round engine).
        /// When it is reached, the active players closest to the lucky number score (every tied player scores).
        /// </summary>
        public int MaxCyclesPerRound { get; }

        public static SeriesRules Default { get; } = new SeriesRules();

        /// <exception cref="ArgumentException">Thrown when a value is invalid.</exception>
        public SeriesRules(int pointsToWin = DefaultPointsToWin, int maxCyclesPerRound = DefaultMaxCyclesPerRound)
        {
            var errors = Validate(pointsToWin, maxCyclesPerRound);
            if (errors.Count > 0) throw new ArgumentException("Invalid SeriesRules: " + string.Join(" ", errors));
            PointsToWin = pointsToWin;
            MaxCyclesPerRound = maxCyclesPerRound;
        }

        public static IReadOnlyList<string> Validate(int pointsToWin, int maxCyclesPerRound)
        {
            var errors = new List<string>();
            if (pointsToWin < 1) errors.Add($"PointsToWin must be >= 1 (was {pointsToWin}).");
            if (maxCyclesPerRound < 1) errors.Add($"MaxCyclesPerRound must be >= 1 (was {maxCyclesPerRound}).");
            return errors;
        }

        /// <summary>Copy of <paramref name="baseRules"/> whose cycle cap is <see cref="MaxCyclesPerRound"/>.</summary>
        public MatchRulesConfig CreateRoundRules(MatchRulesConfig baseRules)
        {
            if (baseRules == null) throw new ArgumentNullException(nameof(baseRules));
            return new MatchRulesConfig(baseRules.BoardLength, baseRules.MinLuckyNumber, baseRules.MaxLuckyNumber,
                baseRules.DiceCount, baseRules.DiceSides, baseRules.MinPlayers, baseRules.MaxPlayers,
                baseRules.StartingHandSize, baseRules.MaxHandSize, MaxCyclesPerRound);
        }
    }
}
