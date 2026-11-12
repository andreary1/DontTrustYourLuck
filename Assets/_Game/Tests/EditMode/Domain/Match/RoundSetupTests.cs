using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Match
{
    /// <summary>R1: optional RoundSetup on MatchEngine.StartMatch (first player and carried-over hands).</summary>
    public class RoundSetupTests
    {
        private static Dictionary<int, IReadOnlyList<string>> Hands(params (int id, string[] cards)[] hands) =>
            hands.ToDictionary(h => h.id, h => (IReadOnlyList<string>)h.cards);

        [Test]
        public void NullSetup_KeepsAscendingTurnOrder()
        {
            var engine = new MatchEngine(MatchRulesConfig.Default, new ScriptedRandomSource(20));
            Assert.IsTrue(engine.StartMatch(new[] { 3, 1, 2 }, null).Success);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, engine.State.TurnOrder);
            Assert.AreEqual(1, engine.State.CurrentPlayerId);
        }

        [Test]
        public void FirstPlayer_RotatesTurnOrder_AndCycleEndsAfterLastInOrder()
        {
            var rng = new ScriptedRandomSource(20) { Unlucky = 19 };
            var engine = new MatchEngine(MatchRulesConfig.Default, rng);
            Assert.IsTrue(engine.StartMatch(new[] { 1, 2, 3 }, new RoundSetup(firstPlayerId: 2)).Success);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, engine.State.TurnOrder);

            var turns = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int id = engine.State.CurrentPlayerId;
                turns.Add(id);
                rng.Enqueue(1, 1);
                engine.RollDice(id);
                engine.ChooseDie(id, 0);
            }
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 2 }, turns);
            Assert.AreEqual(2, engine.State.RoundNumber, "the cycle ended after P1 and P2 opened cycle 2");
        }

        [Test]
        public void FirstPlayer_SkipsEliminatedPlayersInRotatedOrder()
        {
            var rng = new ScriptedRandomSource(20) { Unlucky = 3 };
            var engine = new MatchEngine(MatchRulesConfig.Default, rng);
            engine.StartMatch(new[] { 1, 2, 3 }, new RoundSetup(firstPlayerId: 3));
            rng.Enqueue(3, 3); // P3 -> 3: eliminated
            engine.RollDice(3); engine.ChooseDie(3, 0);
            Assert.AreEqual(1, engine.State.CurrentPlayerId);
            rng.Enqueue(1, 1);
            engine.RollDice(1); engine.ChooseDie(1, 0);
            Assert.AreEqual(2, engine.State.CurrentPlayerId);
            rng.Enqueue(1, 1);
            engine.RollDice(2); engine.ChooseDie(2, 0);
            Assert.AreEqual(2, engine.State.RoundNumber);
            Assert.AreEqual(1, engine.State.CurrentPlayerId, "next cycle starts at the first active player in order");
        }

        [Test]
        public void UnknownFirstPlayer_RejectedWithoutChanges()
        {
            var rng = new ScriptedRandomSource();
            var engine = new MatchEngine(MatchRulesConfig.Default, rng);
            Assert.AreEqual(CommandError.InvalidPlayerId, engine.StartMatch(new[] { 1, 2 }, new RoundSetup(firstPlayerId: 9)).Error);
            Assert.AreEqual(MatchPhase.NotStarted, engine.State.Phase);
            Assert.AreEqual(0, rng.DrawCount);
        }

        [Test]
        public void CarriedHands_GivenWithoutRandomDraws()
        {
            var cardRng = new ScriptedRandomSource(); // any draw would throw
            var engine = new MatchEngine(MatchRulesConfig.Default, new ScriptedRandomSource(20),
                new CardSetup(CardCatalog.CreateDefault(), cardRng));

            var result = engine.StartMatch(new[] { 1, 2 },
                new RoundSetup(0, Hands((1, new[] { CardIds.Shield, CardIds.Shield }), (2, new string[0]))));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, cardRng.DrawCount);
            CollectionAssert.AreEqual(new[] { CardIds.Shield, CardIds.Shield },
                engine.State.GetPlayer(1).Hand.Cards.Select(c => c.CardId));
            Assert.AreEqual(0, engine.State.GetPlayer(2).Hand.Count);
            var drawn = result.Events.OfType<CardDrawnEvent>().ToList();
            Assert.AreEqual(2, drawn.Count);
            Assert.IsTrue(drawn.All(e => e.Source == CardDrawSource.CarriedOver));
            Assert.AreNotEqual(drawn[0].Card.InstanceId, drawn[1].Card.InstanceId, "copies keep unique instance ids");
        }

        [Test]
        public void CarriedHands_InvalidData_Rejected()
        {
            RoundSetup Setup(params (int, string[])[] h) => new RoundSetup(0, Hands(h));
            MatchEngine WithCards() => new MatchEngine(MatchRulesConfig.Default, new ScriptedRandomSource(20),
                new CardSetup(CardCatalog.CreateDefault(), new ScriptedRandomSource()));

            Assert.AreEqual(CommandError.UnknownCard,
                WithCards().StartMatch(new[] { 1, 2 }, Setup((1, new[] { "nope" }))).Error);
            Assert.AreEqual(CommandError.HandFull,
                WithCards().StartMatch(new[] { 1, 2 }, Setup((1, new[] { "shield", "shield", "shield", "shield" }))).Error);
            Assert.AreEqual(CommandError.InvalidPlayerId,
                WithCards().StartMatch(new[] { 1, 2 }, Setup((7, new[] { "shield" }))).Error);
            Assert.AreEqual(CommandError.CardsDisabled,
                new MatchEngine(MatchRulesConfig.Default, new ScriptedRandomSource(20))
                    .StartMatch(new[] { 1, 2 }, Setup((1, new[] { "shield" }))).Error);
        }
    }
}
