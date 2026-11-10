using System;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Gameplay
{
    /// <summary>
    /// Decides which die a computer-controlled player picks. Humans have no provider: their choice comes
    /// from input. Providers only return a decision; the session sends it through the normal commands.
    /// </summary>
    public interface IPlayerDecisionProvider
    {
        /// <summary>Returns the die index to choose for <paramref name="playerId"/> given the pending roll.</summary>
        int ChooseDie(MatchEngine engine, int playerId, DiceRoll roll);
    }

    /// <summary>
    /// Simple bot (D11). Priorities: 1) immediate win; 2) avoid elimination; 3) final tile closest to the
    /// lucky number; 4) tie -> lowest die index. Deterministic and never consumes randomness.
    /// </summary>
    public sealed class SimpleBotDecisionProvider : IPlayerDecisionProvider
    {
        public int ChooseDie(MatchEngine engine, int playerId, DiceRoll roll)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (roll == null) throw new ArgumentNullException(nameof(roll));

            var state = engine.State;
            int bestIndex = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < roll.Count; i++)
            {
                int final = engine.PreviewMove(playerId, roll[i]).FinalPosition;
                int score = final == state.LuckyNumber ? 1_000_000 :
                    engine.IsDangerous(playerId, final) && !state.GetPlayer(playerId).HasShield ? -1_000_000 :
                    -engine.DistanceToLucky(final);
                if (score > bestScore) // strict: ties keep the lower index
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        // Positions are always <= lucky (rebound), so "closest to lucky" is "smallest lucky - final".
        private static int Score(int final, int lucky, int unlucky)
        {
            if (final == lucky) return 1_000_000;
            if (final == unlucky) return -1_000_000;
            return -(lucky - final);
        }
    }
}
