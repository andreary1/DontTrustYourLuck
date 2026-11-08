using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain;

namespace DontTrustYourLuck.Tests.Domain.Match
{
    /// <summary>
    /// Test RNG for match flows: values can be enqueued while the test runs. Unlucky draws (range starting
    /// at 1 and ending at the lucky number) use <see cref="Unlucky"/> when the queue is empty, so tests only
    /// script the values they care about. Any other draw with an empty queue fails loudly.
    /// </summary>
    public sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values = new Queue<int>();

        public int Unlucky { get; set; } = 1;
        public int DrawCount { get; private set; }
        public List<(int min, int maxExclusive)> Requests { get; } = new List<(int, int)>();

        public ScriptedRandomSource(params int[] values) => Enqueue(values);

        public void Enqueue(params int[] values)
        {
            foreach (int v in values) _values.Enqueue(v);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            DrawCount++;
            Requests.Add((minInclusive, maxExclusive));
            int value;
            if (_values.Count > 0) value = _values.Dequeue();
            else if (minInclusive == 1 && maxExclusive > 6 + 1) value = Unlucky; // unlucky range is 1..Lucky-1 (Lucky >= 15)
            else throw new InvalidOperationException($"No scripted value for [{minInclusive}, {maxExclusive}).");

            if (value < minInclusive || value >= maxExclusive)
                throw new InvalidOperationException($"Scripted value {value} outside [{minInclusive}, {maxExclusive}).");
            return value;
        }
    }
}
