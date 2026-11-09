using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;

namespace DontTrustYourLuck.Domain.Cards
{
    /// <summary>
    /// Validates and resolves card plays (TDD §10). Every check runs before any mutation; a rejected play changes
    /// nothing and consumes no randomness. Resolution order on success:
    /// CardPlayed -> effect events -> card removed from hand (CardConsumed); the per-turn card limit is spent.
    /// No dependency on Unity types: effects are registered explicitly by <see cref="CardEffectType"/>.
    /// </summary>
    public sealed class CardEffectResolver
    {
        private readonly Dictionary<CardEffectType, ICardEffect> _effects = new Dictionary<CardEffectType, ICardEffect>();

        public void Register(ICardEffect effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (_effects.ContainsKey(effect.Type)) throw new ArgumentException($"Effect {effect.Type} already registered.", nameof(effect));
            _effects.Add(effect.Type, effect);
        }

        public bool IsRegistered(CardEffectType type) => _effects.ContainsKey(type);

        /// <summary>Runs every validation without changing state. Returns <see cref="CommandError.None"/> if the play is legal.</summary>
        public CommandError Validate(MatchState state, int playerId, string cardInstanceId, int? targetPlayerId) =>
            Prepare(state, playerId, cardInstanceId, targetPlayerId, out _, out _);

        public CommandResult Resolve(MatchState state, int playerId, string cardInstanceId, int? targetPlayerId)
        {
            var error = Prepare(state, playerId, cardInstanceId, targetPlayerId, out var context, out var effect);
            if (error != CommandError.None) return CommandResult.Fail(error);

            var events = new List<MatchEvent>
            {
                new CardPlayedEvent(playerId, context.Card, context.Target?.Id ?? -1)
            };
            effect.Apply(context, events);

            // Consumed only after the effect was applied.
            context.Player.Hand.Remove(context.Card);
            state.CardPlayedThisTurn = true;
            events.Add(new CardConsumedEvent(playerId, context.Card));
            return CommandResult.Ok(events);
        }

        private CommandError Prepare(MatchState state, int playerId, string cardInstanceId, int? targetPlayerId,
            out CardPlayContext context, out ICardEffect effect)
        {
            context = null;
            effect = null;
            if (state == null) throw new ArgumentNullException(nameof(state));

            // 1. Phase.
            if (state.Phase == MatchPhase.Finished) return CommandError.MatchFinished;
            if (state.Phase != MatchPhase.AwaitingRoll && state.Phase != MatchPhase.AwaitingDiceChoice)
                return CommandError.WrongPhase;

            // 2. Player (eliminated players never hold the turn).
            var player = state.GetPlayer(playerId);
            if (player == null) return CommandError.UnknownPlayer;
            if (playerId != state.CurrentPlayerId || !player.IsActive) return CommandError.NotYourTurn;

            // 3. Card in the player's own hand (another player's instance id is "not in hand").
            var card = player.Hand?.Find(cardInstanceId);
            if (card == null) return CommandError.CardNotInHand;

            // 4. One card per turn.
            if (state.CardPlayedThisTurn) return CommandError.CardAlreadyPlayedThisTurn;

            // 5. Timing.
            var definition = card.Definition;
            if (state.Phase != definition.AllowedPhase) return CommandError.CardWrongTiming;

            if (!_effects.TryGetValue(definition.Effect, out effect)) return CommandError.CardEffectUnavailable;

            // 6. Target.
            PlayerState target = null;
            switch (definition.Targeting)
            {
                case CardTargeting.None:
                    if (targetPlayerId.HasValue) return CommandError.InvalidTarget;
                    break;
                case CardTargeting.OtherActivePlayer:
                    if (!targetPlayerId.HasValue) return CommandError.TargetRequired;
                    target = state.GetPlayer(targetPlayerId.Value);
                    // Unknown, self, eliminated or winner targets are all invalid.
                    if (target == null || target.Id == playerId || !target.IsActive) return CommandError.InvalidTarget;
                    break;
            }

            // 7. Effect-specific checks.
            context = new CardPlayContext(state, player, target, card);
            var effectError = effect.Validate(context);
            if (effectError != CommandError.None)
            {
                context = null;
                return effectError;
            }
            return CommandError.None;
        }
    }
}
