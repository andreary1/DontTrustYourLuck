namespace DontTrustYourLuck.Domain
{
    /// <summary>
    /// Single entry point for randomness in the rules layer. Inject a seeded implementation for
    /// reproducible matches or a fake one for tests. Never call UnityEngine.Random from rules.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive). Requires minInclusive &lt; maxExclusive.</summary>
        int NextInt(int minInclusive, int maxExclusive);
    }
}
