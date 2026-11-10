using System;

namespace DontTrustYourLuck.Domain
{
    /// <summary>
    /// Computes forward movement with rebound. Isolated on purpose: the overshoot rule is a prototype
    /// decision (TDD §7.2, Decided for M1) and may change after playtests.
    ///
    /// Rule: move one tile per step; on reaching the bounce point (the lucky number) with steps left,
    /// the remaining steps go backwards. Only the final tile matters for luck/unluck; passing through
    /// the bounce point does not count as landing on it.
    /// Example (bounceAt = 30): 28 + 5 -> 29, 30, 29, 28, 27.
    ///
    /// No win/elimination handling here, and no Unity types.
    /// </summary>
    public sealed class MovementService
    {
        private readonly MatchRulesConfig _rules;

        public MovementService(MatchRulesConfig rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <param name="startPosition">Current tile, in [0, bounceAt].</param>
        /// <param name="steps">Chosen die value, >= 1.</param>
        /// <param name="bounceAt">Rebound tile (the match's lucky number), in [1, BoardLength].</param>
        /// <exception cref="ArgumentOutOfRangeException">For invalid positions, steps or bounce point.</exception>
        public MovementResult Move(int startPosition, int steps, int bounceAt)
        {
            if (_rules.Circuit)
            {
                if (startPosition < 0 || startPosition > _rules.BoardLength || steps < 1 ||
                    bounceAt < 1 || bounceAt > _rules.BoardLength)
                    throw new ArgumentOutOfRangeException(nameof(startPosition));
                var circuitPath = new int[steps];
                int tile = startPosition;
                for (int i = 0; i < steps; i++)
                {
                    tile = tile % _rules.BoardLength + 1;
                    circuitPath[i] = tile;
                }
                return new MovementResult(startPosition, steps, bounceAt, circuitPath, false);
            }
            if (bounceAt < 1 || bounceAt > _rules.BoardLength)
                throw new ArgumentOutOfRangeException(nameof(bounceAt), bounceAt,
                    $"Bounce point must be in [1, {_rules.BoardLength}].");
            if (startPosition < 0 || startPosition > bounceAt)
                throw new ArgumentOutOfRangeException(nameof(startPosition), startPosition,
                    $"Start position must be in [0, {bounceAt}].");
            if (steps < 1)
                throw new ArgumentOutOfRangeException(nameof(steps), steps, "Steps must be >= 1.");
            // Beyond this the rebound would pass the start tile; behavior is undefined by design (TDD §7.2).
            int maxSteps = 2 * bounceAt - startPosition;
            if (steps > maxSteps)
                throw new ArgumentOutOfRangeException(nameof(steps), steps,
                    $"Steps must be <= {maxSteps} from position {startPosition} (rebound would pass tile 0).");

            var path = new int[steps];
            int position = startPosition;
            int direction = position == bounceAt ? -1 : 1;
            bool bounced = position == bounceAt;

            for (int i = 0; i < steps; i++)
            {
                position += direction;
                path[i] = position;
                if (position == bounceAt && direction > 0)
                {
                    direction = -1;
                    if (i < steps - 1) bounced = true;
                }
            }

            return new MovementResult(startPosition, steps, bounceAt, path, bounced);
        }
    }
}
