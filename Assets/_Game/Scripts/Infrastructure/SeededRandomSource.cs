using System;
using DontTrustYourLuck.Domain;

namespace DontTrustYourLuck.Infrastructure
{
    /// <summary>
    /// Deterministic seeded PRNG (SplitMix64). Implemented by hand instead of System.Random so the
    /// sequence for a given seed is identical across runtimes (Mono Editor, IL2CPP/WebGL, future server).
    /// Single-threaded by design; not cryptographically secure.
    /// </summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        private ulong _state;

        public int Seed { get; }

        public SeededRandomSource(int seed)
        {
            Seed = seed;
            _state = unchecked((ulong)(long)seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                throw new ArgumentException($"minInclusive ({minInclusive}) must be < maxExclusive ({maxExclusive}).");

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            // Rejection sampling removes modulo bias. Rejection probability is < range / 2^64,
            // so the loop effectively always finishes on the first iteration.
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
            ulong value;
            do
            {
                value = NextUInt64();
            } while (value >= limit);

            return (int)((long)minInclusive + (long)(value % range));
        }

        private ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
