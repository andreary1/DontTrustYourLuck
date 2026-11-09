using DontTrustYourLuck.Domain.Cards;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Cards
{
    /// <summary>
    /// Designer-facing card data: rule parameters (converted to the pure <see cref="CardDefinition"/>) plus display
    /// name, short description and placeholder art. Static data only: hands, shields and per-turn state live in
    /// the runtime MatchState, never in this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "Card", menuName = "Don't Trust Your Luck/Card Definition")]
    public sealed class CardDefinitionAsset : ScriptableObject
    {
        [Header("Rules")]
        [Tooltip("Stable id used by the rules and saved data (e.g. lucky_roll).")]
        [SerializeField] private string id = "card";
        [SerializeField] private CardEffectType effect;
        [SerializeField] private CardTiming timing;
        [SerializeField] private CardTargeting targeting;
        [Min(0)] [SerializeField] private int amount;

        [Header("Display")]
        [SerializeField] private string displayName = "Card";
        [TextArea(2, 3)] [SerializeField] private string shortDescription = "";
        [SerializeField] private Sprite art;

        public string Id => id;
        public string DisplayName => displayName;
        public string ShortDescription => shortDescription;
        public Sprite Art => art;

        public CardDefinition ToDomain() => new CardDefinition(id, effect, timing, targeting, amount);

        /// <summary>Editor setup (used by the Sprint 3 builder).</summary>
        public void Configure(string cardId, CardEffectType cardEffect, CardTiming cardTiming, CardTargeting cardTargeting,
            int cardAmount, string name, string description, Sprite sprite)
        {
            id = cardId; effect = cardEffect; timing = cardTiming; targeting = cardTargeting; amount = cardAmount;
            displayName = name; shortDescription = description; art = sprite;
        }
    }
}
