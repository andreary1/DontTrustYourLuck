using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Cards
{
    // Debuffs are announced now, active throughout the next round, and cannot stack or refresh.
    internal sealed class DebuffEffect : ICardEffect
    {
        public CardEffectType Type { get; }
        public DebuffEffect(CardEffectType type) => Type = type;
        public CommandError Validate(CardPlayContext context) => context.Target.Debuff.HasValue
            ? CommandError.InvalidTarget : CommandError.None;
        public void Apply(CardPlayContext context, List<MatchEvent> events)
        {
            var target = context.Target;
            target.Debuff = Type;
            target.DebuffRound = context.State.RoundNumber + 1;
            target.StickyUnluckyNumber = Type == CardEffectType.StickyLuck ? context.State.UnluckyNumber : 0;
            events.Add(new DebuffChangedEvent(target.Id, target.Debuff, target.DebuffRound, target.StickyUnluckyNumber));
        }
    }
}
