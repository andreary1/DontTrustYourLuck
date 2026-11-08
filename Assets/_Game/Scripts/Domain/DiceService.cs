using System;

namespace DontTrustYourLuck.Domain
{
    /// <summary>
    /// Rolls the configured dice (default: two D6). Draw order is documented for reproducibility:
    /// one NextInt(1, DiceSides + 1) call per die, in die-index order.
    /// Choosing a die is NOT handled here (later sprint).
    /// </summary>
    public sealed class DiceService
    {
        private readonly IRandomSource _random;

        public int DiceCount { get; }
        public int DiceSides { get; }

        public DiceService(IRandomSource random, MatchRulesConfig rules)
            : this(random, rules?.DiceCount ?? throw new ArgumentNullException(nameof(rules)), rules.DiceSides)
        {
        }

        public DiceService(IRandomSource random, int diceCount, int diceSides)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (diceCount < 1) throw new ArgumentOutOfRangeException(nameof(diceCount), diceCount, "Must be >= 1.");
            if (diceSides < 2) throw new ArgumentOutOfRangeException(nameof(diceSides), diceSides, "Must be >= 2.");
            DiceCount = diceCount;
            DiceSides = diceSides;
        }

        public DiceRoll Roll()
        {
            var values = new int[DiceCount];
            for (int i = 0; i < DiceCount; i++)
            {
                values[i] = _random.NextInt(1, DiceSides + 1);
            }
            return new DiceRoll(values);
        }
    }
}
