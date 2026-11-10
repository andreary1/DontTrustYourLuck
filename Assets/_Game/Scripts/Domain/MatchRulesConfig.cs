using System;
using System.Collections.Generic;

namespace DontTrustYourLuck.Domain
{
    /// <summary>
    /// Immutable rule parameters for a match. Pure C# (no UnityEngine).
    /// Values here are prototype defaults; see docs/TECHNICAL_DESIGN.md §3 for decision status.
    /// Mutable match state must never live here.
    /// </summary>
    public sealed class MatchRulesConfig
    {
        public const int DefaultBoardLength = 30;
        public const int DefaultMinLuckyNumber = 15;
        public const int DefaultMaxLuckyNumber = 30;
        public const int DefaultDiceCount = 2;
        public const int DefaultDiceSides = 6;
        public const int DefaultMinPlayers = 2;
        public const int DefaultMaxPlayers = 4;
        public const int DefaultStartingHandSize = 2;
        public const int DefaultMaxHandSize = 3;
        public const int DefaultMaxRounds = 15;

        /// <summary>Highest logical tile index. Tiles are 0..BoardLength (0 = start).</summary>
        public int BoardLength { get; }
        public int MinLuckyNumber { get; }
        public int MaxLuckyNumber { get; }
        public int DiceCount { get; }
        public int DiceSides { get; }
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public int StartingHandSize { get; }
        public int MaxHandSize { get; }
        /// <summary>Round limit (Decided for M1: 15). Not used by Sprint 1 services yet.</summary>
        public int MaxRounds { get; }
        public bool Circuit { get; }
        public bool PersistentShield { get; }
        public int CardDrawInterval { get; }
        /// <summary>
        /// Tiles on each side of the unlucky number that are also dangerous (0 = single tile, 1 = 3-tile zone).
        /// Proposed 10/11/2026 after simulation; see GDD §3. Tile 0 and the lucky tile are never dangerous.
        /// </summary>
        public int UnluckyZoneRadius { get; }
        /// <summary>When true the first player of each round rotates (round 1: lowest id, round 2: next id, ...).</summary>
        public bool RotateFirstPlayer { get; }
        /// <summary>
        /// Same-tile bump (approved 10/11/2026 for playtest): ending a die move on an occupied tile pushes every
        /// occupant back this many tiles (clamped at 0). 0 disables it. Tile 0 and the lucky tile never bump.
        /// </summary>
        public int BumpDistance { get; }

        /// <summary>Number of logical tiles including the start tile (BoardLength + 1).</summary>
        public int TileCount => BoardLength + 1;

        public static MatchRulesConfig Default { get; } = new MatchRulesConfig();

        /// <exception cref="ArgumentException">Thrown when the combination of values is invalid.</exception>
        public MatchRulesConfig(
            int boardLength = DefaultBoardLength,
            int minLuckyNumber = DefaultMinLuckyNumber,
            int maxLuckyNumber = DefaultMaxLuckyNumber,
            int diceCount = DefaultDiceCount,
            int diceSides = DefaultDiceSides,
            int minPlayers = DefaultMinPlayers,
            int maxPlayers = DefaultMaxPlayers,
            int startingHandSize = DefaultStartingHandSize,
            int maxHandSize = DefaultMaxHandSize,
            int maxRounds = DefaultMaxRounds, bool circuit = false, bool persistentShield = false,
            int cardDrawInterval = 0, int unluckyZoneRadius = 0, bool rotateFirstPlayer = false, int bumpDistance = 0)
        {
            var errors = Validate(boardLength, minLuckyNumber, maxLuckyNumber, diceCount, diceSides,
                minPlayers, maxPlayers, startingHandSize, maxHandSize, maxRounds);
            if (errors.Count > 0)
            {
                throw new ArgumentException("Invalid MatchRulesConfig: " + string.Join(" ", errors));
            }

            BoardLength = boardLength;
            MinLuckyNumber = minLuckyNumber;
            MaxLuckyNumber = maxLuckyNumber;
            DiceCount = diceCount;
            DiceSides = diceSides;
            MinPlayers = minPlayers;
            MaxPlayers = maxPlayers;
            StartingHandSize = startingHandSize;
            MaxHandSize = maxHandSize;
            MaxRounds = maxRounds;
            Circuit = circuit;
            PersistentShield = persistentShield;
            if (cardDrawInterval < 0) throw new ArgumentOutOfRangeException(nameof(cardDrawInterval));
            CardDrawInterval = cardDrawInterval;
            if (unluckyZoneRadius < 0 || 2 * unluckyZoneRadius + 1 >= minLuckyNumber)
                throw new ArgumentOutOfRangeException(nameof(unluckyZoneRadius), unluckyZoneRadius,
                    "Unlucky zone radius must be >= 0 and leave safe tiles before the lucky number.");
            UnluckyZoneRadius = unluckyZoneRadius;
            RotateFirstPlayer = rotateFirstPlayer;
            if (bumpDistance < 0) throw new ArgumentOutOfRangeException(nameof(bumpDistance), bumpDistance, "Must be >= 0.");
            BumpDistance = bumpDistance;
        }

        /// <summary>
        /// Returns every validation error for the given values (empty list when valid).
        /// Useful for editor validation without throwing.
        /// </summary>
        public static IReadOnlyList<string> Validate(
            int boardLength,
            int minLuckyNumber,
            int maxLuckyNumber,
            int diceCount,
            int diceSides,
            int minPlayers,
            int maxPlayers,
            int startingHandSize,
            int maxHandSize,
            int maxRounds)
        {
            var errors = new List<string>();

            if (boardLength < 2)
                errors.Add($"BoardLength must be >= 2 (was {boardLength}).");

            // Lucky >= 2 so the unlucky range 1..Lucky-1 is never empty.
            if (minLuckyNumber < 2)
                errors.Add($"MinLuckyNumber must be >= 2 (was {minLuckyNumber}).");
            if (maxLuckyNumber < minLuckyNumber)
                errors.Add($"MaxLuckyNumber ({maxLuckyNumber}) must be >= MinLuckyNumber ({minLuckyNumber}).");
            if (maxLuckyNumber > boardLength)
                errors.Add($"MaxLuckyNumber ({maxLuckyNumber}) must be <= BoardLength ({boardLength}).");

            if (diceCount < 1)
                errors.Add($"DiceCount must be >= 1 (was {diceCount}).");
            if (diceSides < 2)
                errors.Add($"DiceSides must be >= 2 (was {diceSides}).");
            // Guarantees a bounce at the lucky number never goes below tile 0 (TDD §7.2).
            if (diceSides > minLuckyNumber)
                errors.Add($"DiceSides ({diceSides}) must be <= MinLuckyNumber ({minLuckyNumber}) so a rebound never passes the start tile.");

            if (minPlayers < 2)
                errors.Add($"MinPlayers must be >= 2 (was {minPlayers}).");
            if (maxPlayers < minPlayers)
                errors.Add($"MaxPlayers ({maxPlayers}) must be >= MinPlayers ({minPlayers}).");

            if (startingHandSize < 0)
                errors.Add($"StartingHandSize must be >= 0 (was {startingHandSize}).");
            if (maxHandSize < startingHandSize)
                errors.Add($"MaxHandSize ({maxHandSize}) must be >= StartingHandSize ({startingHandSize}).");

            if (maxRounds < 1)
                errors.Add($"MaxRounds must be >= 1 (was {maxRounds}).");

            return errors;
        }

        public bool IsValidTileIndex(int index) => index >= 0 && index <= BoardLength;

        /// <summary>
        /// True when <paramref name="tile"/> is within <see cref="UnluckyZoneRadius"/> of <paramref name="center"/>.
        /// Circuit: distance wraps around 1..BoardLength. Linear: only tiles 1..lucky-1 count.
        /// Tile 0, the lucky tile and a non-positive center are never dangerous.
        /// </summary>
        public bool IsInUnluckyZone(int tile, int center, int luckyNumber)
        {
            if (center <= 0 || tile <= 0 || tile > BoardLength || tile == luckyNumber) return false;
            if (!Circuit && tile > luckyNumber) return false;
            int diff = Math.Abs(tile - center);
            if (Circuit) diff = Math.Min(diff, BoardLength - diff);
            return diff <= UnluckyZoneRadius;
        }

        /// <summary>Fills <paramref name="into"/> with the zone tiles in path order (center - radius .. center + radius).</summary>
        public void GetUnluckyZone(int center, int luckyNumber, List<int> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int d = -UnluckyZoneRadius; d <= UnluckyZoneRadius; d++)
            {
                int tile = center + d;
                if (Circuit) tile = ((tile - 1) % BoardLength + BoardLength) % BoardLength + 1;
                if (IsInUnluckyZone(tile, center, luckyNumber)) into.Add(tile);
            }
        }

        public bool IsInLuckyRange(int luckyNumber) =>
            luckyNumber >= MinLuckyNumber && luckyNumber <= MaxLuckyNumber;
    }
}
