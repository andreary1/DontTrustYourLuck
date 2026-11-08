using DontTrustYourLuck.Domain;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Config
{
    /// <summary>
    /// Designer-facing serialization of <see cref="MatchRulesConfig"/>. Holds only static rule data;
    /// never store mutable match state here (ScriptableObjects persist changes in the Editor).
    /// </summary>
    [CreateAssetMenu(fileName = "MatchConfig", menuName = "Don't Trust Your Luck/Match Config")]
    public sealed class MatchConfigAsset : ScriptableObject
    {
        [Header("Board")]
        [Min(2)] [SerializeField] private int boardLength = MatchRulesConfig.DefaultBoardLength;

        [Header("Lucky number")]
        [SerializeField] private int minLuckyNumber = MatchRulesConfig.DefaultMinLuckyNumber;
        [SerializeField] private int maxLuckyNumber = MatchRulesConfig.DefaultMaxLuckyNumber;

        [Header("Dice")]
        [Min(1)] [SerializeField] private int diceCount = MatchRulesConfig.DefaultDiceCount;
        [Min(2)] [SerializeField] private int diceSides = MatchRulesConfig.DefaultDiceSides;

        [Header("Players")]
        [SerializeField] private int minPlayers = MatchRulesConfig.DefaultMinPlayers;
        [SerializeField] private int maxPlayers = MatchRulesConfig.DefaultMaxPlayers;

        [Header("Cards")]
        [Min(0)] [SerializeField] private int startingHandSize = MatchRulesConfig.DefaultStartingHandSize;
        [Min(0)] [SerializeField] private int maxHandSize = MatchRulesConfig.DefaultMaxHandSize;

        [Header("Rounds")]
        [Min(1)] [SerializeField] private int maxRounds = MatchRulesConfig.DefaultMaxRounds;

        /// <summary>Creates a validated, immutable rules object. Throws ArgumentException if invalid.</summary>
        public MatchRulesConfig ToRulesConfig() => new MatchRulesConfig(
            boardLength, minLuckyNumber, maxLuckyNumber, diceCount, diceSides,
            minPlayers, maxPlayers, startingHandSize, maxHandSize, maxRounds);

        private void OnValidate()
        {
            var errors = MatchRulesConfig.Validate(boardLength, minLuckyNumber, maxLuckyNumber, diceCount,
                diceSides, minPlayers, maxPlayers, startingHandSize, maxHandSize, maxRounds);
            foreach (var error in errors)
            {
                Debug.LogWarning($"[{name}] {error}", this);
            }
        }
    }
}
