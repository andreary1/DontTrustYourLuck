using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Presentation.Cards;
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
        [SerializeField] private bool circuit;
        [SerializeField] private bool persistentShield;
        [Min(0)] [SerializeField] private int cardDrawInterval;
        [Tooltip("Tiles on each side of the unlucky number that are also dangerous (0 = single tile, 1 = 3-tile zone).")]
        [Min(0)] [SerializeField] private int unluckyZoneRadius;
        [Tooltip("Rotate the first player every round to remove the first-seat advantage.")]
        [SerializeField] private bool rotateFirstPlayer;
        [Tooltip("Ending a die move on an occupied tile pushes the occupants back this many tiles (0 = off).")]
        [Min(0)] [SerializeField] private int bumpDistance;
        public bool Circuit => circuit;

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
        [Tooltip("Cards available in the match. Empty: the default four MVP cards (rules only, no art).")]
        [SerializeField] private List<CardDefinitionAsset> cards = new List<CardDefinitionAsset>();

        [Header("Rounds")]
        [Min(1)] [SerializeField] private int maxRounds = MatchRulesConfig.DefaultMaxRounds;

        public IReadOnlyList<CardDefinitionAsset> Cards => cards;

        /// <summary>Converts the card assets to the pure-domain catalog (default catalog when none are set).</summary>
        public CardCatalog ToCardCatalog()
        {
            var definitions = new List<CardDefinition>();
            foreach (var card in cards)
            {
                if (card != null) definitions.Add(card.ToDomain());
            }
            return definitions.Count > 0 ? new CardCatalog(definitions) : CardCatalog.CreateDefault();
        }

        /// <summary>Editor setup (Sprint 3 builder).</summary>
        public void SetCards(IEnumerable<CardDefinitionAsset> assets) => cards = new List<CardDefinitionAsset>(assets);

        /// <summary>Creates a validated, immutable rules object. Throws ArgumentException if invalid.</summary>
        public MatchRulesConfig ToRulesConfig() => new MatchRulesConfig(
            boardLength, minLuckyNumber, maxLuckyNumber, diceCount, diceSides,
            minPlayers, maxPlayers, startingHandSize, maxHandSize, maxRounds, circuit, persistentShield, cardDrawInterval,
            unluckyZoneRadius, rotateFirstPlayer, bumpDistance);

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
