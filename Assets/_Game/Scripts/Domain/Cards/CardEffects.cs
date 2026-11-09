using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Cards
{
    /// <summary>A validated request handed to an effect: owner, optional target, the card and the match state.</summary>
    public sealed class CardPlayContext
    {
        public MatchState State { get; }
        public PlayerState Player { get; }
        /// <summary>Resolved target, or null for cards without target.</summary>
        public PlayerState Target { get; }
        public CardInstance Card { get; }

        internal CardPlayContext(MatchState state, PlayerState player, PlayerState target, CardInstance card)
        {
            State = state;
            Player = player;
            Target = target;
            Card = card;
        }
    }

    /// <summary>
    /// Rule implementation of one <see cref="CardEffectType"/>. Effects are pure domain code: no Unity types.
    /// Adding a card = definition + effect (or reuse of an effect with new parameters) + registration + tests.
    /// </summary>
    public interface ICardEffect
    {
        CardEffectType Type { get; }

        /// <summary>Effect-specific checks (common checks are done by the resolver). Must not mutate state.</summary>
        CommandError Validate(CardPlayContext context);

        /// <summary>Applies the effect and appends its events. Only called after every validation passed.</summary>
        void Apply(CardPlayContext context, List<MatchEvent> events);
    }

    /// <summary>Lucky Roll: replaces both pending dice using the match dice service (same RNG as normal rolls).</summary>
    internal sealed class RerollEffect : ICardEffect
    {
        private readonly Func<DiceRoll> _roll;

        public RerollEffect(Func<DiceRoll> roll) => _roll = roll ?? throw new ArgumentNullException(nameof(roll));

        public CardEffectType Type => CardEffectType.Reroll;

        public CommandError Validate(CardPlayContext context) =>
            context.State.CurrentRoll == null ? CommandError.CardWrongTiming : CommandError.None;

        public void Apply(CardPlayContext context, List<MatchEvent> events)
        {
            var previous = context.State.CurrentRoll;
            var roll = _roll();
            context.State.CurrentRoll = roll; // phase stays AwaitingDiceChoice
            events.Add(new DiceRerolledEvent(context.Player.Id, previous, roll));
        }
    }

    /// <summary>Swap: exchanges tiles with the target. Tiles are not resolved (no win/elimination).</summary>
    internal sealed class SwapPositionsEffect : ICardEffect
    {
        public CardEffectType Type => CardEffectType.SwapPositions;

        public CommandError Validate(CardPlayContext context) => CommandError.None; // target checked by the resolver

        public void Apply(CardPlayContext context, List<MatchEvent> events)
        {
            var a = context.Player;
            var b = context.Target;
            int aPos = a.Position;
            a.Position = b.Position;
            b.Position = aPos;
            events.Add(new PlayerPositionsSwappedEvent(a.Id, b.Id, a.Position, b.Position));
        }
    }

    /// <summary>Retreat: newPosition = max(0, position - Amount). Valid (and consumed) even at tile 0 (user decision).</summary>
    internal sealed class MoveBackEffect : ICardEffect
    {
        public CardEffectType Type => CardEffectType.MoveBack;

        public CommandError Validate(CardPlayContext context) => CommandError.None;

        public void Apply(CardPlayContext context, List<MatchEvent> events)
        {
            var p = context.Player;
            int from = p.Position;
            p.Position = Math.Max(0, from - context.Card.Definition.Amount);
            events.Add(new PlayerRetreatedEvent(p.Id, from, p.Position));
        }
    }

    /// <summary>Shield: protects the owner from one unlucky elimination until the end of the next move.</summary>
    internal sealed class ShieldEffect : ICardEffect
    {
        public CardEffectType Type => CardEffectType.Shield;

        public CommandError Validate(CardPlayContext context) =>
            context.Player.HasShield ? CommandError.InvalidTarget : CommandError.None;

        public void Apply(CardPlayContext context, List<MatchEvent> events)
        {
            context.Player.HasShield = true;
            events.Add(new ShieldActivatedEvent(context.Player.Id));
        }
    }
}
