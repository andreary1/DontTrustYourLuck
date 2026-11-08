using System;

namespace DontTrustYourLuck.Domain
{
    /// <summary>
    /// Draws the lucky and unlucky numbers. Pure rules; tile highlighting lives in Presentation.
    ///
    /// Rules (GDD/TDD, Decided for M1 on 07/11/2026):
    /// - Lucky: drawn once at match start in [MinLuckyNumber, MaxLuckyNumber]. Calling it once is the
    ///   caller's (future MatchState) responsibility.
    /// - Unlucky: drawn every round in [1, Lucky - 1], via a single NextInt(1, lucky) call.
    ///   Tile 0 is always safe and the unlucky number can never equal the lucky number by construction,
    ///   so no retry loop is needed (no risk of infinite loops). Consecutive rounds may repeat a value.
    /// </summary>
    public sealed class LuckNumberService
    {
        private readonly IRandomSource _random;
        private readonly MatchRulesConfig _rules;

        public LuckNumberService(IRandomSource random, MatchRulesConfig rules)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public int DrawLuckyNumber()
        {
            return _random.NextInt(_rules.MinLuckyNumber, _rules.MaxLuckyNumber + 1);
        }

        /// <exception cref="ArgumentOutOfRangeException">If luckyNumber is outside the configured lucky range.</exception>
        public int DrawUnluckyNumber(int luckyNumber)
        {
            if (!_rules.IsInLuckyRange(luckyNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(luckyNumber), luckyNumber,
                    $"Lucky number must be in [{_rules.MinLuckyNumber}, {_rules.MaxLuckyNumber}].");
            }

            return _random.NextInt(1, luckyNumber);
        }
    }
}
