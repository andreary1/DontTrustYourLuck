using System;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Gameplay
{
    /// <summary>Deterministic, public-information policy. Never samples or inspects future random values.</summary>
    public static class BotCardDecision
    {
        public static CardInstance Choose(MatchEngine engine, out int? targetId)
        {
            targetId = null;
            var state = engine.State;
            if (!engine.CardsEnabled || state.CardPlayedThisTurn) return null;
            var player = state.GetPlayer(state.CurrentPlayerId);
            CardInstance best = null;
            double bestScore = 0;
            foreach (var card in player.Hand.Cards)
            {
                foreach (var candidate in state.Players)
                {
                    int? target = card.Definition.RequiresTarget ? candidate.Id : (int?)null;
                    if (engine.ValidateCardPlay(player.Id, card.InstanceId, target) != CommandError.None) continue;
                    double score = Evaluate(engine, player, card, candidate);
                    if (score > bestScore) { best = card; bestScore = score; targetId = target; }
                    if (!card.Definition.RequiresTarget) break;
                }
            }
            return best;
        }

        private static double Evaluate(MatchEngine engine, PlayerState player, CardInstance card, PlayerState target)
        {
            var state = engine.State;
            bool trapped = player.HasDebuff(CardEffectType.Freeze, state.RoundNumber) && engine.IsDangerous(player.Id, player.Position);
            switch (card.Definition.Effect)
            {
                case CardEffectType.Shield: return trapped ? 100 : engine.Rules.PersistentShield ? 8 : 0;
                case CardEffectType.SwapPositions:
                    if (trapped && !engine.IsDangerous(player.Id, target.Position)) return 110;
                    return Expected(engine, player, target.Position) - Expected(engine, player, player.Position);
                case CardEffectType.MoveBack:
                    int retreat = Math.Max(0, player.Position - card.Definition.Amount);
                    if (trapped && !engine.IsDangerous(player.Id, retreat)) return 110;
                    return Expected(engine, player, retreat) - Expected(engine, player, player.Position);
                case CardEffectType.Reroll:
                    double current = double.MinValue;
                    foreach (int value in state.CurrentRoll.Values)
                        current = Math.Max(current, Score(engine, player, engine.PreviewMove(player.Id, value).FinalPosition));
                    return Expected(engine, player, player.Position) - current - 2;
                default:
                    if (state.RoundNumber >= engine.Rules.MaxRounds) return 0;
                    return 4 + (engine.Rules.BoardLength - engine.DistanceToLucky(target.Position)) * 0.15;
            }
        }

        private static double Expected(MatchEngine engine, PlayerState player, int position)
        {
            if (player.HasDebuff(CardEffectType.Freeze, engine.State.RoundNumber))
                return Score(engine, player, position);
            var movement = new MovementService(engine.Rules);
            int sides = engine.Rules.DiceSides;
            double total = 0;
            for (int a = 1; a <= sides; a++)
                for (int b = 1; b <= sides; b++)
                {
                    int va = player.HasDebuff(CardEffectType.HeavyFeet, engine.State.RoundNumber) ? Math.Max(1, a - 2) : a;
                    int vb = player.HasDebuff(CardEffectType.HeavyFeet, engine.State.RoundNumber) ? Math.Max(1, b - 2) : b;
                    double first = Score(engine, player, movement.Move(position, va, engine.State.LuckyNumber).FinalPosition);
                    double second = Score(engine, player, movement.Move(position, vb, engine.State.LuckyNumber).FinalPosition);
                    total += player.HasDebuff(CardEffectType.CrackedDie, engine.State.RoundNumber) ? first : Math.Max(first, second);
                }
            return total / (sides * sides);
        }

        private static double Score(MatchEngine engine, PlayerState player, int tile) =>
            tile == engine.State.LuckyNumber ? 150 :
            engine.IsDangerous(player.Id, tile) && !player.HasShield ? -200 : -engine.DistanceToLucky(tile);
    }
}
