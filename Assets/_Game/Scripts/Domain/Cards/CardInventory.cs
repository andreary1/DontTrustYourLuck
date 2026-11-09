using System.Collections.Generic;

namespace DontTrustYourLuck.Domain.Cards
{
    /// <summary>
    /// One card owned by a player. <see cref="InstanceId"/> is unique within the match, so two copies of the
    /// same card in one hand are never ambiguous.
    /// </summary>
    public sealed class CardInstance
    {
        public string InstanceId { get; }
        public CardDefinition Definition { get; }
        public string CardId => Definition.Id;

        internal CardInstance(string instanceId, CardDefinition definition)
        {
            InstanceId = instanceId;
            Definition = definition;
        }

        public override string ToString() => $"{InstanceId}:{Definition.Id}";
    }

    /// <summary>A player's hand. Read-only outside the rules layer; capacity comes from MatchRulesConfig.MaxHandSize.</summary>
    public sealed class CardInventory
    {
        private readonly List<CardInstance> _cards = new List<CardInstance>();

        public int Capacity { get; }
        public IReadOnlyList<CardInstance> Cards => _cards;
        public int Count => _cards.Count;
        public bool IsFull => _cards.Count >= Capacity;

        internal CardInventory(int capacity)
        {
            Capacity = capacity;
        }

        /// <summary>Returns the card with this instance id, or null.</summary>
        public CardInstance Find(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            foreach (var c in _cards)
            {
                if (c.InstanceId == instanceId) return c;
            }
            return null;
        }

        internal bool TryAdd(CardInstance card)
        {
            if (card == null || IsFull) return false;
            _cards.Add(card);
            return true;
        }

        internal bool Remove(CardInstance card) => _cards.Remove(card);
    }
}
