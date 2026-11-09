using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using DontTrustYourLuck.Tests.Domain.Match;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Cards
{
    /// <summary>
    /// Sprint 3 card rules (TDD §10.2). Most tests start with an empty hand (StartingHandSize 0) and grant the
    /// exact cards they need through the test-only <c>MatchEngine.GrantCard</c>. Positions are set directly via
    /// internal setters to keep scenarios short.
    /// </summary>
    public class CardRulesTests
    {
        private ScriptedRandomSource _rng;
        private ScriptedRandomSource _cardRng;
        private MatchEngine _engine;

        private MatchState State => _engine.State;
        private PlayerState P(int id) => State.GetPlayer(id);

        private void Start(int lucky = 25, int unlucky = 7, int players = 2, int handSize = 0, int[] cardDraws = null)
        {
            _rng = new ScriptedRandomSource(lucky) { Unlucky = unlucky };
            _cardRng = new ScriptedRandomSource(cardDraws ?? new int[0]);
            var rules = new MatchRulesConfig(startingHandSize: handSize);
            _engine = new MatchEngine(rules, _rng, new CardSetup(CardCatalog.CreateDefault(), _cardRng));
            var ids = Enumerable.Range(1, players).ToArray();
            Assert.IsTrue(_engine.StartMatch(ids).Success, "start");
        }

        private string Grant(int playerId, string cardId)
        {
            var result = _engine.GrantCard(playerId, cardId);
            Assert.IsTrue(result.Success, $"grant {cardId}: {result}");
            return ((CardDrawnEvent)result.Events.Single()).Card.InstanceId;
        }

        private void Roll(int a, int b)
        {
            _rng.Enqueue(a, b);
            Assert.IsTrue(_engine.RollDice(State.CurrentPlayerId).Success, "roll");
        }

        private CommandResult Choose(int index) => _engine.ChooseDie(State.CurrentPlayerId, index);

        /// <summary>Plays a whole dice-only turn for the current player (choose die A).</summary>
        private CommandResult Turn(int a, int b = 1)
        {
            Roll(a, b);
            var r = Choose(0);
            Assert.IsTrue(r.Success, "choose");
            return r;
        }

        private static List<T> Of<T>(CommandResult r) where T : MatchEvent => r.Events.OfType<T>().ToList();

        // ======================================================== Lucky Roll

        [Test]
        public void LuckyRoll_AfterRoll_ReplacesBothDice_AndStaysInDiceChoice()
        {
            Start();
            string card = Grant(1, CardIds.LuckyRoll);
            Roll(2, 3);
            _rng.Enqueue(4, 6);

            var result = _engine.PlayCard(1, card);

            Assert.IsTrue(result.Success, result.ToString());
            CollectionAssert.AreEqual(new[] { 4, 6 }, State.CurrentRoll.Values.ToArray());
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, State.Phase);
            Assert.AreEqual(1, State.CurrentPlayerId);
            CollectionAssert.AreEqual(
                new[] { typeof(CardPlayedEvent), typeof(DiceRerolledEvent), typeof(CardConsumedEvent) },
                result.Events.Select(e => e.GetType()).ToArray());
            var rerolled = Of<DiceRerolledEvent>(result).Single();
            CollectionAssert.AreEqual(new[] { 2, 3 }, rerolled.PreviousRoll.Values.ToArray());
            CollectionAssert.AreEqual(new[] { 4, 6 }, rerolled.Roll.Values.ToArray());
        }

        [Test]
        public void LuckyRoll_BeforeRoll_Rejected_NotConsumed_NoRandomness()
        {
            Start();
            string card = Grant(1, CardIds.LuckyRoll);
            int draws = _rng.DrawCount;

            Assert.AreEqual(CommandError.CardWrongTiming, _engine.PlayCard(1, card).Error);
            Assert.IsNotNull(P(1).Hand.Find(card), "card kept");
            Assert.IsFalse(State.CardPlayedThisTurn);
            Assert.AreEqual(draws, _rng.DrawCount);
            Assert.AreEqual(MatchPhase.AwaitingRoll, State.Phase);
        }

        [Test]
        public void LuckyRoll_IsConsumed_AndBlocksASecondCard()
        {
            Start();
            string first = Grant(1, CardIds.LuckyRoll);
            string second = Grant(1, CardIds.LuckyRoll);
            Roll(1, 1);
            _rng.Enqueue(5, 5);
            Assert.IsTrue(_engine.PlayCard(1, first).Success);

            Assert.IsNull(P(1).Hand.Find(first), "first copy consumed");
            Assert.IsNotNull(P(1).Hand.Find(second), "the other copy stays");
            Assert.AreEqual(1, P(1).Hand.Count);
            int draws = _rng.DrawCount;
            Assert.AreEqual(CommandError.CardAlreadyPlayedThisTurn, _engine.PlayCard(1, second).Error);
            Assert.AreEqual(draws, _rng.DrawCount, "rejected reroll draws nothing");
        }

        [Test]
        public void LuckyRoll_ChoiceUsesTheNewDice()
        {
            Start(lucky: 25, unlucky: 20);
            string card = Grant(1, CardIds.LuckyRoll);
            Roll(1, 2);
            _rng.Enqueue(5, 6);
            _engine.PlayCard(1, card);

            var result = Choose(1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(6, Of<DieSelectedEvent>(result).Single().Value);
            Assert.AreEqual(6, P(1).Position);
            Assert.IsNull(State.CurrentRoll, "roll consumed; no second movement");
            Assert.AreEqual(2, State.CurrentPlayerId);
        }

        // ======================================================== Swap

        [Test]
        public void Swap_ExchangesPositions_AndTurnContinues()
        {
            Start();
            P(1).Position = 4;
            P(2).Position = 10;
            string card = Grant(1, CardIds.Swap);

            var result = _engine.PlayCard(1, card, 2);

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(10, P(1).Position);
            Assert.AreEqual(4, P(2).Position);
            Assert.AreEqual(MatchPhase.AwaitingRoll, State.Phase);
            Assert.AreEqual(1, State.CurrentPlayerId);
            var swapped = Of<PlayerPositionsSwappedEvent>(result).Single();
            Assert.AreEqual(2, swapped.TargetPlayerId);
            Assert.AreEqual(10, swapped.PlayerPosition);
            Assert.AreEqual(4, swapped.TargetPosition);
            Assert.AreEqual(2, Of<CardPlayedEvent>(result).Single().TargetPlayerId);

            // The following move starts from the new tile.
            Turn(3);
            Assert.AreEqual(13, P(1).Position);
        }

        [Test]
        public void Swap_RejectsSelfEliminatedUnknownAndMissingTargets_WithoutConsuming()
        {
            Start(players: 3);
            P(3).Status = PlayerStatus.Eliminated;
            string card = Grant(1, CardIds.Swap);

            Assert.AreEqual(CommandError.InvalidTarget, _engine.PlayCard(1, card, 1).Error, "self");
            Assert.AreEqual(CommandError.InvalidTarget, _engine.PlayCard(1, card, 3).Error, "eliminated");
            Assert.AreEqual(CommandError.InvalidTarget, _engine.PlayCard(1, card, 99).Error, "unknown");
            Assert.AreEqual(CommandError.TargetRequired, _engine.PlayCard(1, card).Error, "missing");
            Assert.IsNotNull(P(1).Hand.Find(card));
            Assert.IsFalse(State.CardPlayedThisTurn);
            Assert.IsTrue(_engine.PlayCard(1, card, 2).Success, "a valid target still works");
        }

        [Test]
        public void Swap_OntoUnluckyTile_DoesNotEliminateOrWin()
        {
            Start(lucky: 25, unlucky: 7);
            P(1).Position = 3;
            P(2).Position = 7; // standing on the unlucky tile (no retroactive elimination)
            string card = Grant(1, CardIds.Swap);

            var result = _engine.PlayCard(1, card, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(7, P(1).Position);
            Assert.IsTrue(P(1).IsActive && P(2).IsActive);
            Assert.IsEmpty(Of<PlayerEliminatedEvent>(result));
            Assert.IsEmpty(Of<PlayerWonEvent>(result));
            Assert.IsFalse(State.IsFinished);
        }

        [Test]
        public void Swap_WithTargetOnSameTile_IsValidAndConsumed()
        {
            Start();
            P(1).Position = 5;
            P(2).Position = 5;
            string card = Grant(1, CardIds.Swap);

            Assert.IsTrue(_engine.PlayCard(1, card, 2).Success);
            Assert.AreEqual(0, P(1).Hand.Count);
            Assert.IsTrue(State.CardPlayedThisTurn);
        }

        [Test]
        public void Swap_AfterRoll_Rejected()
        {
            Start();
            string card = Grant(1, CardIds.Swap);
            Roll(1, 2);
            Assert.AreEqual(CommandError.CardWrongTiming, _engine.PlayCard(1, card, 2).Error);
        }

        // ======================================================== Retreat

        [TestCase(10, 7)]
        [TestCase(3, 0)]
        [TestCase(2, 0)]
        [TestCase(0, 0)] // no effect but valid and consumed (decision D10)
        public void Retreat_MovesBackThree_ClampedAtZero(int from, int expected)
        {
            Start(lucky: 25, unlucky: 20);
            P(1).Position = from;
            string card = Grant(1, CardIds.Retreat);

            var result = _engine.PlayCard(1, card);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(expected, P(1).Position);
            var retreated = Of<PlayerRetreatedEvent>(result).Single();
            Assert.AreEqual(from, retreated.From);
            Assert.AreEqual(expected, retreated.To);
            Assert.AreEqual(0, P(1).Hand.Count, "consumed");
            Assert.AreEqual(MatchPhase.AwaitingRoll, State.Phase);
            Assert.AreEqual(1, State.CurrentPlayerId);
        }

        [Test]
        public void Retreat_OntoUnluckyTile_DoesNotEliminate_AndNextMoveStartsFromNewTile()
        {
            Start(lucky: 25, unlucky: 7);
            P(1).Position = 10;
            string card = Grant(1, CardIds.Retreat);

            var result = _engine.PlayCard(1, card);

            Assert.AreEqual(7, P(1).Position);
            Assert.IsTrue(P(1).IsActive);
            Assert.IsEmpty(Of<PlayerEliminatedEvent>(result));

            Turn(2);
            Assert.AreEqual(9, P(1).Position, "move starts from the retreated tile");
            Assert.IsTrue(P(1).IsActive);
        }

        [Test]
        public void Retreat_WithTarget_Rejected()
        {
            Start();
            string card = Grant(1, CardIds.Retreat);
            Assert.AreEqual(CommandError.InvalidTarget, _engine.PlayCard(1, card, 2).Error);
            Assert.IsNotNull(P(1).Hand.Find(card));
        }

        // ======================================================== Shield

        [Test]
        public void Shield_BeforeRoll_Activates()
        {
            Start();
            string card = Grant(1, CardIds.Shield);

            var result = _engine.PlayCard(1, card);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(P(1).HasShield);
            Assert.AreEqual(1, Of<ShieldActivatedEvent>(result).Single().PlayerId);
            Assert.AreEqual(MatchPhase.AwaitingRoll, State.Phase);
        }

        [Test]
        public void Shield_BlocksUnluckyElimination_AndIsConsumed()
        {
            Start(lucky: 25, unlucky: 7);
            P(1).Position = 5;
            _engine.PlayCard(1, Grant(1, CardIds.Shield));

            var result = Turn(2);

            Assert.AreEqual(7, P(1).Position, "stays on the unlucky tile");
            Assert.IsTrue(P(1).IsActive);
            Assert.IsEmpty(Of<PlayerEliminatedEvent>(result));
            var consumed = Of<ShieldConsumedEvent>(result).Single();
            Assert.IsTrue(consumed.BlockedElimination);
            Assert.AreEqual(7, consumed.Tile);
            Assert.IsFalse(P(1).HasShield);
            Assert.AreEqual(2, State.CurrentPlayerId, "turn ends normally");

            // Resolution order: move -> shield -> turn end.
            var types = result.Events.Select(e => e.GetType()).ToList();
            Assert.Less(types.IndexOf(typeof(PlayerMovedEvent)), types.IndexOf(typeof(ShieldConsumedEvent)));
            Assert.Less(types.IndexOf(typeof(ShieldConsumedEvent)), types.IndexOf(typeof(TurnChangedEvent)));
        }

        [Test]
        public void Shield_ExpiresAfterSafeMove()
        {
            Start(lucky: 25, unlucky: 7);
            _engine.PlayCard(1, Grant(1, CardIds.Shield));

            var result = Turn(3);

            Assert.IsFalse(Of<ShieldConsumedEvent>(result).Single().BlockedElimination);
            Assert.IsFalse(P(1).HasShield);
        }

        [Test]
        public void Shield_AfterRoll_Rejected()
        {
            Start();
            string card = Grant(1, CardIds.Shield);
            Roll(1, 2);
            Assert.AreEqual(CommandError.CardWrongTiming, _engine.PlayCard(1, card).Error);
            Assert.IsFalse(P(1).HasShield);
            Assert.IsNotNull(P(1).Hand.Find(card));
        }

        [Test]
        public void Shield_DoesNotPersistBetweenTurns()
        {
            Start(lucky: 25, unlucky: 7);
            _engine.PlayCard(1, Grant(1, CardIds.Shield));
            Turn(2);   // P1 -> 2 (safe), shield expires
            Turn(1);   // P2 -> 1, round 2 starts (unlucky stays 7)
            Assert.AreEqual(1, State.CurrentPlayerId);

            var result = Turn(5); // P1 -> 7 without shield

            Assert.AreEqual(PlayerStatus.Eliminated, P(1).Status);
            Assert.AreEqual(1, Of<PlayerEliminatedEvent>(result).Count);
        }

        [Test]
        public void Shield_DoesNotProtectOtherPlayers()
        {
            Start(lucky: 25, unlucky: 7, players: 3);
            _engine.PlayCard(1, Grant(1, CardIds.Shield));
            Turn(2);              // P1 safe
            P(2).Position = 5;
            var result = Turn(2); // P2 -> 7
            Assert.AreEqual(PlayerStatus.Eliminated, P(2).Status);
            Assert.IsEmpty(Of<ShieldConsumedEvent>(result));
        }

        [Test]
        public void Shield_LandingOnLucky_StillWins()
        {
            Start(lucky: 25, unlucky: 7);
            P(1).Position = 22;
            _engine.PlayCard(1, Grant(1, CardIds.Shield));

            var result = Turn(3);

            Assert.IsTrue(State.IsFinished);
            Assert.AreEqual(MatchEndReason.ExactLucky, State.EndReason);
            Assert.IsFalse(Of<ShieldConsumedEvent>(result).Single().BlockedElimination);
        }

        [Test]
        public void Shield_BlockedElimination_KeepsLastSurvivorRuleIntact()
        {
            // P2 eliminated by a later normal move still ends the match by survival.
            Start(lucky: 25, unlucky: 7);
            P(1).Position = 5;
            _engine.PlayCard(1, Grant(1, CardIds.Shield));
            Turn(2);              // P1 blocked on 7
            P(2).Position = 4;
            var result = Turn(3); // P2 -> 7, eliminated

            Assert.IsTrue(State.IsFinished);
            Assert.AreEqual(MatchEndReason.LastSurvivor, State.EndReason);
            CollectionAssert.AreEqual(new[] { 1 }, State.WinnerIds.ToArray());
            Assert.AreEqual(1, Of<PlayerWonEvent>(result).Single().PlayerId);
        }

        // ======================================================== General rules

        [Test]
        public void StartingHand_TwoCardsEach_FromCardStream_WithUniqueInstanceIds()
        {
            _rng = new ScriptedRandomSource(20) { Unlucky = 5 };
            _cardRng = new ScriptedRandomSource(0, 1, 2, 3, 3, 3);
            _engine = new MatchEngine(MatchRulesConfig.Default, _rng, new CardSetup(CardCatalog.CreateDefault(), _cardRng));

            var result = _engine.StartMatch(new[] { 1, 2, 3 });

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { CardIds.LuckyRoll, CardIds.Swap }, P(1).Hand.Cards.Select(c => c.CardId).ToArray());
            CollectionAssert.AreEqual(new[] { CardIds.Retreat, CardIds.Shield }, P(2).Hand.Cards.Select(c => c.CardId).ToArray());
            CollectionAssert.AreEqual(new[] { CardIds.Shield, CardIds.Shield }, P(3).Hand.Cards.Select(c => c.CardId).ToArray(), "duplicates allowed");
            var all = State.Players.SelectMany(p => p.Hand.Cards).Select(c => c.InstanceId).ToList();
            Assert.AreEqual(6, all.Distinct().Count());
            Assert.AreEqual(6, _cardRng.DrawCount);
            Assert.AreEqual(2, _rng.DrawCount, "match stream only drew lucky + round-1 unlucky");

            // Draws happen after MatchStarted and before the first round.
            var types = result.Events.Select(e => e.GetType()).ToList();
            Assert.AreEqual(typeof(MatchStartedEvent), types[0]);
            Assert.AreEqual(6, types.Skip(1).Take(6).Count(t => t == typeof(CardDrawnEvent)));
            Assert.AreEqual(typeof(RoundStartedEvent), types[7]);
            Assert.IsTrue(Of<CardDrawnEvent>(result).All(e => e.Source == CardDrawSource.StartingHand));
        }

        [Test]
        public void HandLimit_IsThree()
        {
            Start(handSize: 2, cardDraws: new[] { 0, 1, 2, 3 });
            Assert.AreEqual(2, P(1).Hand.Count);
            Grant(1, CardIds.Shield);
            Assert.AreEqual(3, P(1).Hand.Count);
            Assert.IsTrue(P(1).Hand.IsFull);

            Assert.AreEqual(CommandError.HandFull, _engine.GrantCard(1, CardIds.Swap).Error);
            Assert.AreEqual(3, P(1).Hand.Count);
        }

        [Test]
        public void Grant_RejectsUnknownCardAndEliminatedPlayer()
        {
            Start(players: 3);
            P(3).Status = PlayerStatus.Eliminated;
            Assert.AreEqual(CommandError.UnknownCard, _engine.GrantCard(1, "nope").Error);
            Assert.AreEqual(CommandError.UnknownPlayer, _engine.GrantCard(42, CardIds.Shield).Error);
            Assert.AreEqual(CommandError.NotYourTurn, _engine.GrantCard(3, CardIds.Shield).Error);
        }

        [Test]
        public void SeededMatches_DealSameHands_AndCardsDoNotChangeLuckOrDice()
        {
            MatchEngine Make(bool cards) => new MatchEngine(MatchRulesConfig.Default, new SeededRandomSource(99),
                cards ? new CardSetup(CardCatalog.CreateDefault(), new SeededRandomSource(1234)) : null);

            var a = Make(true);
            var b = Make(true);
            var plain = Make(false);
            foreach (var e in new[] { a, b, plain }) e.StartMatch(new[] { 1, 2, 3, 4 });

            for (int id = 1; id <= 4; id++)
            {
                CollectionAssert.AreEqual(
                    a.State.GetPlayer(id).Hand.Cards.Select(c => c.CardId).ToArray(),
                    b.State.GetPlayer(id).Hand.Cards.Select(c => c.CardId).ToArray());
            }
            Assert.AreEqual(plain.State.LuckyNumber, a.State.LuckyNumber);
            Assert.AreEqual(plain.State.UnluckyNumber, a.State.UnluckyNumber);
            a.RollDice(1);
            plain.RollDice(1);
            CollectionAssert.AreEqual(plain.State.CurrentRoll.Values.ToArray(), a.State.CurrentRoll.Values.ToArray());
        }

        [Test]
        public void InvalidPlays_NeverConsumeCards()
        {
            Start();
            string swap = Grant(1, CardIds.Swap);
            string retreat = Grant(1, CardIds.Retreat);

            Assert.IsFalse(_engine.PlayCard(1, swap).Success);          // no target
            Assert.IsFalse(_engine.PlayCard(1, retreat, 2).Success);    // target not allowed
            Assert.IsFalse(_engine.PlayCard(1, "card-999").Success);    // not in hand
            Assert.AreEqual(2, P(1).Hand.Count);
            Assert.IsFalse(State.CardPlayedThisTurn);
            Assert.AreEqual(0, P(1).Position);
        }

        [Test]
        public void OneCardPerTurn_LimitResetsNextTurn()
        {
            Start(lucky: 25, unlucky: 20);
            string shield = Grant(1, CardIds.Shield);
            string retreat = Grant(1, CardIds.Retreat);
            P(1).Position = 6;

            Assert.IsTrue(_engine.PlayCard(1, shield).Success);
            Assert.AreEqual(CommandError.CardAlreadyPlayedThisTurn, _engine.PlayCard(1, retreat).Error);
            Assert.AreEqual(6, P(1).Position);

            Turn(1);  // P1
            Assert.IsFalse(State.CardPlayedThisTurn, "reset for P2");
            Turn(1);  // P2
            Assert.AreEqual(1, State.CurrentPlayerId);
            Assert.IsTrue(_engine.PlayCard(1, retreat).Success, "limit restored on the next own turn");
        }

        [Test]
        public void CardsOfOtherPlayers_AreRejected()
        {
            Start();
            string p2Card = Grant(2, CardIds.Shield);

            Assert.AreEqual(CommandError.CardNotInHand, _engine.PlayCard(1, p2Card).Error, "P1 using P2's card");
            Assert.AreEqual(CommandError.NotYourTurn, _engine.PlayCard(2, p2Card).Error, "P2 off-turn");
            Assert.IsNotNull(P(2).Hand.Find(p2Card));
        }

        [Test]
        public void PlayCard_RejectedForEliminatedFinishedAndDisabled()
        {
            Start(lucky: 25, unlucky: 7, players: 3);
            string p2Card = Grant(2, CardIds.Shield);
            P(2).Status = PlayerStatus.Eliminated;
            Turn(1); // P1 -> next is P3 (P2 skipped)
            Assert.AreEqual(3, State.CurrentPlayerId);
            Assert.AreEqual(CommandError.NotYourTurn, _engine.PlayCard(2, p2Card).Error, "eliminated");

            // Finish the match: P3 wins exactly.
            P(3).Position = 24;
            string p3Card = Grant(3, CardIds.Shield);
            Turn(1);
            Assert.IsTrue(State.IsFinished);
            Assert.AreEqual(CommandError.MatchFinished, _engine.PlayCard(3, p3Card).Error);
            Assert.AreEqual(CommandError.MatchFinished, _engine.GrantCard(3, CardIds.Shield).Error);

            var noCards = new MatchEngine(MatchRulesConfig.Default, new ScriptedRandomSource(20));
            noCards.StartMatch(new[] { 1, 2 });
            Assert.AreEqual(CommandError.CardsDisabled, noCards.PlayCard(1, "card-1").Error);
            Assert.AreEqual(0, noCards.State.GetPlayer(1).Hand.Count);
        }

        [Test]
        public void ValidateCardPlay_MatchesPlayCard_WithoutChangingState()
        {
            Start();
            string swap = Grant(1, CardIds.Swap);
            Assert.AreEqual(CommandError.TargetRequired, _engine.ValidateCardPlay(1, swap));
            Assert.AreEqual(CommandError.None, _engine.ValidateCardPlay(1, swap, 2));
            Assert.AreEqual(0, P(1).Position);
            Assert.IsNotNull(P(1).Hand.Find(swap));
            Assert.IsFalse(State.CardPlayedThisTurn);
        }

        [Test]
        public void Restart_DiscardsHandsShieldsAndLimit()
        {
            int seed = 0;
            var session = new MatchSession(MatchRulesConfig.Default,
                new[] { new PlayerSlot(1, false), new PlayerSlot(2, false) },
                () => new SeededRandomSource(++seed), null,
                CardCatalog.CreateDefault(), () => new SeededRandomSource(seed * 7));
            session.StartMatch();
            var oldEngine = session.Engine;
            Assert.AreEqual(2, session.State.GetPlayer(1).Hand.Count);

            var shield = oldEngine.GrantCard(1, CardIds.Shield); // hand now 3
            Assert.IsTrue(shield.Success);
            string shieldId = ((CardDrawnEvent)shield.Events[0]).Card.InstanceId;
            Assert.IsTrue(session.PlayCard(1, shieldId).Success);
            Assert.IsTrue(session.State.GetPlayer(1).HasShield);

            session.RestartMatch();

            Assert.AreNotSame(oldEngine, session.Engine);
            Assert.IsFalse(session.State.CardPlayedThisTurn);
            foreach (var p in session.State.Players)
            {
                Assert.AreEqual(2, p.Hand.Count, "fresh starting hand");
                Assert.IsFalse(p.HasShield);
            }
            Assert.AreEqual("card-1", session.State.GetPlayer(1).Hand.Cards[0].InstanceId, "instance ids restart");
        }

        [Test]
        public void Session_PlayCard_PublishesEvents_AndRejectionsAreReported()
        {
            var session = new MatchSession(MatchRulesConfig.Default,
                new[] { new PlayerSlot(1, false), new PlayerSlot(2, true) },
                () => new SeededRandomSource(5), null,
                CardCatalog.CreateDefault(), () => new SeededRandomSource(6));
            var published = new List<MatchEvent>();
            var rejected = new List<CommandError>();
            session.EventPublished += published.Add;
            session.CommandRejected += rejected.Add;
            session.StartMatch();
            Assert.AreEqual(4, published.OfType<CardDrawnEvent>().Count());

            session.PlayCard(1, "card-999");
            CollectionAssert.AreEqual(new[] { CommandError.CardNotInHand }, rejected);
        }

        [Test]
        public void BotOnlyMatches_WithCards_AlwaysFinish()
        {
            for (int s = 1; s <= 25; s++)
            {
                int seed = s;
                var session = new MatchSession(MatchRulesConfig.Default,
                    new[] { new PlayerSlot(1, true), new PlayerSlot(2, true), new PlayerSlot(3, true), new PlayerSlot(4, true) },
                    () => new SeededRandomSource(seed), null,
                    CardCatalog.CreateDefault(), () => new SeededRandomSource(seed + 1000));
                session.StartMatch();
                int guard = 0;
                while (!session.State.IsFinished)
                {
                    Assert.IsNotNull(session.StepBot(), $"seed {seed}: bot got stuck holding cards");
                    Assert.Less(++guard, 1000, $"seed {seed}: match did not finish");
                }
                Assert.IsTrue(session.State.Players.Any(p => p.Hand.Count > 0), "bots keep their unused cards");
            }
        }

        [Test]
        public void DiceOnlyFlow_UnchangedWhenCardsEnabled()
        {
            Start(lucky: 20, unlucky: 13, handSize: 0);
            P(1).Position = 12;
            var result = Turn(1); // -> 13, eliminated (no shield)
            Assert.AreEqual(PlayerStatus.Eliminated, P(1).Status);
            Assert.IsTrue(State.IsFinished, "last survivor");
            Assert.AreEqual(MatchEndReason.LastSurvivor, State.EndReason);
            Assert.IsEmpty(Of<ShieldConsumedEvent>(result));
        }

        [Test]
        public void Catalog_RejectsDuplicateIds_AndDefaultHasFourCards()
        {
            var def = CardCatalog.CreateDefault();
            Assert.AreEqual(4, def.Count);
            Assert.AreEqual(3, def.Find(CardIds.Retreat).Amount);
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, def.Find(CardIds.LuckyRoll).AllowedPhase);
            Assert.AreEqual(MatchPhase.AwaitingRoll, def.Find(CardIds.Shield).AllowedPhase);
            Assert.Throws<System.ArgumentException>(() => new CardCatalog(new[]
            {
                new CardDefinition("x", CardEffectType.Shield, CardTiming.BeforeRoll, CardTargeting.None),
                new CardDefinition("x", CardEffectType.MoveBack, CardTiming.BeforeRoll, CardTargeting.None, 3),
            }));
        }
    }
}
