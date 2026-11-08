using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain;

namespace DontTrustYourLuck.Tests.Domain
{
    /// <summary>
    /// Test double returning a scripted sequence. Records every requested range and fails loudly
    /// if a scripted value is outside the requested range or the script runs out.
    /// </summary>
    public sealed class FakeRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public List<(int min, int maxExclusive)> Requests { get; } = new List<(int, int)>();

        public FakeRandomSource(params int[] values)
        {
            _values = new Queue<int>(values);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            Requests.Add((minInclusive, maxExclusive));
            if (_values.Count == 0)
                throw new InvalidOperationException("FakeRandomSource ran out of scripted values.");

            int value = _values.Dequeue();
            if (value < minInclusive || value >= maxExclusive)
                throw new InvalidOperationException(
                    $"Scripted value {value} is outside requested range [{minInclusive}, {maxExclusive}).");
            return value;
        }
    }
}
