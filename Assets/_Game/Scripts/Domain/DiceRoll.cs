using System;
using System.Collections.Generic;

namespace DontTrustYourLuck.Domain
{
    /// <summary>Immutable result of one dice roll. Values are ordered by die index (0, 1, ...).</summary>
    public sealed class DiceRoll
    {
        private readonly int[] _values;

        public IReadOnlyList<int> Values => _values;
        public int Count => _values.Length;

        public DiceRoll(IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count == 0) throw new ArgumentException("A dice roll needs at least one value.", nameof(values));

            _values = new int[values.Count];
            for (int i = 0; i < values.Count; i++) _values[i] = values[i];
        }

        public int this[int dieIndex] => _values[dieIndex];

        public override string ToString() => "[" + string.Join(", ", _values) + "]";
    }
}
