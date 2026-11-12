namespace DontTrustYourLuck.Domain.Match
{
    /// <summary>Read-only outcome of a same-tile bump (see <see cref="MatchEngine.PreviewBumps"/>).</summary>
    public readonly struct BumpPreview
    {
        public int Victims { get; }
        /// <summary>Victims pushed into the unlucky zone without a Shield.</summary>
        public int Eliminated { get; }
        /// <summary>The push lands the victims on the lucky tile (the first of them would win).</summary>
        public bool GivesWin { get; }

        public BumpPreview(int victims, int eliminated, bool givesWin)
        {
            Victims = victims;
            Eliminated = eliminated;
            GivesWin = givesWin;
        }
    }
}
