using System;

namespace DontTrustYourLuck.Presentation.Audio
{
    /// <summary>
    /// Shuffle-bag order for the soundtrack: every track plays once per round, in random order,
    /// and the first track of a new round never repeats the last one of the previous round.
    /// Plain C# (no Unity API) so it is testable and seedable.
    /// </summary>
    public sealed class MusicPlaylistOrder
    {
        private readonly Random _random;
        private readonly int[] _bag;
        private int _cursor;
        private int _last = -1;

        public MusicPlaylistOrder(int trackCount, int seed)
        {
            if (trackCount < 0) throw new ArgumentOutOfRangeException(nameof(trackCount));
            _random = new Random(seed);
            _bag = new int[trackCount];
            for (int i = 0; i < trackCount; i++) _bag[i] = i;
            _cursor = trackCount; // forces a shuffle on the first Next()
        }

        public int Count => _bag.Length;

        /// <summary>Index of the next track, or -1 when the playlist is empty.</summary>
        public int Next()
        {
            if (_bag.Length == 0) return -1;
            if (_cursor >= _bag.Length) Reshuffle();
            _last = _bag[_cursor++];
            return _last;
        }

        private void Reshuffle()
        {
            for (int i = _bag.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
            if (_bag.Length > 1 && _bag[0] == _last)
            {
                int j = 1 + _random.Next(_bag.Length - 1);
                (_bag[0], _bag[j]) = (_bag[j], _bag[0]);
            }
            _cursor = 0;
        }
    }
}
