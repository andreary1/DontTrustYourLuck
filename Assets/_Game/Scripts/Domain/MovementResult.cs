using System.Collections.Generic;

namespace DontTrustYourLuck.Domain
{
    /// <summary>Immutable outcome of a movement. Path lists every tile entered, in order (start excluded).</summary>
    public sealed class MovementResult
    {
        private readonly int[] _path;

        public int StartPosition { get; }
        public int Steps { get; }
        public int BounceAt { get; }
        public int FinalPosition { get; }
        public IReadOnlyList<int> Path => _path;
        public bool Bounced { get; }

        internal MovementResult(int startPosition, int steps, int bounceAt, int[] path, bool bounced)
        {
            StartPosition = startPosition;
            Steps = steps;
            BounceAt = bounceAt;
            _path = path;
            FinalPosition = path.Length > 0 ? path[path.Length - 1] : startPosition;
            Bounced = bounced;
        }

        public override string ToString() =>
            $"{StartPosition} +{Steps} -> {FinalPosition} (path: {string.Join(" > ", _path)}{(Bounced ? ", bounced" : "")})";
    }
}
