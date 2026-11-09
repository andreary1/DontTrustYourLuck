using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Cards
{
    /// <summary>What a card does. Each value is resolved by exactly one registered <see cref="ICardEffect"/>.</summary>
    public enum CardEffectType
    {
        /// <summary>Discard the pending roll and roll every die again (Lucky Roll).</summary>
        Reroll,
        /// <summary>Swap positions with another active player (Swap).</summary>
        SwapPositions,
        /// <summary>Move back <see cref="CardDefinition.Amount"/> tiles, never below tile 0 (Retreat).</summary>
        MoveBack,
        /// <summary>Block one unlucky-tile elimination during the next move (Shield).</summary>
        Shield
    }

    /// <summary>When a card may be played inside the player's own turn (TDD §10.2).</summary>
    public enum CardTiming
    {
        /// <summary>Phase <see cref="MatchPhase.AwaitingRoll"/>.</summary>
        BeforeRoll,
        /// <summary>Phase <see cref="MatchPhase.AwaitingDiceChoice"/>.</summary>
        AfterRoll
    }

    public enum CardTargeting
    {
        /// <summary>No target; passing one is rejected.</summary>
        None,
        /// <summary>Another active player (not the card owner).</summary>
        OtherActivePlayer
    }

    /// <summary>Stable ids of the four MVP cards (decision D10).</summary>
    public static class CardIds
    {
        public const string LuckyRoll = "lucky_roll";
        public const string Swap = "swap";
        public const string Retreat = "retreat_3";
        public const string Shield = "shield";
    }

    /// <summary>
    /// Static rule data of one card type. Pure C#: display name, description and art live in the Unity
    /// <c>CardDefinitionAsset</c>, which converts itself to this model.
    /// </summary>
    public sealed class CardDefinition
    {
        public string Id { get; }
        public CardEffectType Effect { get; }
        public CardTiming Timing { get; }
        public CardTargeting Targeting { get; }
        /// <summary>Effect parameter (e.g. tiles for <see cref="CardEffectType.MoveBack"/>); 0 when unused.</summary>
        public int Amount { get; }

        public CardDefinition(string id, CardEffectType effect, CardTiming timing, CardTargeting targeting, int amount = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A card needs an id.", nameof(id));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must be >= 0.");
            Id = id;
            Effect = effect;
            Timing = timing;
            Targeting = targeting;
            Amount = amount;
        }

        /// <summary>The only phase in which this card can be played.</summary>
        public MatchPhase AllowedPhase => Timing == CardTiming.BeforeRoll ? MatchPhase.AwaitingRoll : MatchPhase.AwaitingDiceChoice;

        public bool RequiresTarget => Targeting != CardTargeting.None;

        public override string ToString() => Id;
    }

    /// <summary>Immutable set of card definitions available in a match. Ids are unique.</summary>
    public sealed class CardCatalog
    {
        private readonly List<CardDefinition> _definitions = new List<CardDefinition>();

        public IReadOnlyList<CardDefinition> Definitions => _definitions;
        public int Count => _definitions.Count;

        public CardCatalog(IEnumerable<CardDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (var d in definitions)
            {
                if (d == null) throw new ArgumentException("Null card definition.", nameof(definitions));
                if (Find(d.Id) != null) throw new ArgumentException($"Duplicate card id '{d.Id}'.", nameof(definitions));
                _definitions.Add(d);
            }
            if (_definitions.Count == 0) throw new ArgumentException("A card catalog needs at least one card.", nameof(definitions));
        }

        /// <summary>Returns the definition with this id, or null.</summary>
        public CardDefinition Find(string id)
        {
            foreach (var d in _definitions)
            {
                if (d.Id == id) return d;
            }
            return null;
        }

        /// <summary>The four MVP cards with the approved rules (TDD §10.2).</summary>
        public static CardCatalog CreateDefault() => new CardCatalog(new[]
        {
            new CardDefinition(CardIds.LuckyRoll, CardEffectType.Reroll, CardTiming.AfterRoll, CardTargeting.None),
            new CardDefinition(CardIds.Swap, CardEffectType.SwapPositions, CardTiming.BeforeRoll, CardTargeting.OtherActivePlayer),
            new CardDefinition(CardIds.Retreat, CardEffectType.MoveBack, CardTiming.BeforeRoll, CardTargeting.None, 3),
            new CardDefinition(CardIds.Shield, CardEffectType.Shield, CardTiming.BeforeRoll, CardTargeting.None),
        });
    }

    /// <summary>
    /// Enables cards in a <see cref="MatchEngine"/>: which cards exist and the random stream used to draw them.
    /// Card draws use their own stream so the lucky/unlucky/dice sequence of a seed is unchanged by cards.
    /// </summary>
    public sealed class CardSetup
    {
        public CardCatalog Catalog { get; }
        public IRandomSource Random { get; }

        public CardSetup(CardCatalog catalog, IRandomSource random)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Random = random ?? throw new ArgumentNullException(nameof(random));
        }
    }
}
