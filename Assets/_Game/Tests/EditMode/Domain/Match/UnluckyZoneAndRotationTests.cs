using System;
using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Match
{
    /// <summary>Unlucky zone (radius around the unlucky number) and first-player rotation (proposed 10/11/2026).</summary>
    public class UnluckyZoneAndRotationTests
    {
        private ScriptedRandomSource _random;
        private MatchEngine _engine;
        private readonly List<int> _turns = new List<int>();
        private PlayerState P(int id) => _engine.State.GetPlayer(id);

        private void Start(int players = 3, bool rotate = false, int radius = 1, bool circuit = false, bool cards = false)
        {
            _random = new ScriptedRandomSource(25) { Unlucky = 10 };
            var rules = new MatchRulesConfig(startingHandSize: 0, circuit: circuit, persistentShield: circuit,
                unluckyZoneRadius: radius, rotateFirstPlayer: rotate);
            _engine = new MatchEngine(rules, _random,
                cards ? new CardSetup(CardCatalog.CreateExpanded(), new SeededRandomSource(1)) : null);
            _turns.Clear();
            Record(_engine.StartMatch(Enumerable.Range(1, players).ToArray()));
        }

        private void Record(CommandResult result)
        {
            Assert.IsTrue(result.Success, result.ToString());
            foreach (var e in result.Events)
                if (e is TurnChangedEvent t) _turns.Add(t.PlayerId);
        }

        private void Turn(int a = 1, int b = 1)
        {
            int id = _engine.State.CurrentPlayerId;
            bool frozen = P(id).HasDebuff(CardEffectType.Freeze, _engine.State.RoundNumber);
            if (!frozen) _random.Enqueue(a, b);
            Record(_engine.RollDice(id));
            if (!frozen) Record(_engine.ChooseDie(id, 0));
        }

        // ---- Pure zone rules

        [TestCase(9, true)] [TestCase(10, true)] [TestCase(11, true)] [TestCase(8, false)] [TestCase(12, false)]
        public void Linear_ZoneCoversRadiusAroundCenter(int tile, bool expected)
        {
            var rules = new MatchRulesConfig(unluckyZoneRadius: 1);
            Assert.AreEqual(expected, rules.IsInUnluckyZone(tile, 10, 25));
        }

        [Test]
        public void RadiusZero_IsTheOriginalSingleTileRule()
        {
            var rules = new MatchRulesConfig();
            Assert.IsTrue(rules.IsInUnluckyZone(10, 10, 25));
            Assert.IsFalse(rules.IsInUnluckyZone(9, 10, 25));
            Assert.IsFalse(rules.IsInUnluckyZone(11, 10, 25));
        }

        [TestCase(false, 1, new[] { 1, 2 })]          // tile 0 (start) is always safe
        [TestCase(false, 24, new[] { 23, 24 })]       // the lucky tile is never dangerous
        [TestCase(true, 30, new[] { 29, 30, 1 })]     // circuit wraps
        [TestCase(true, 1, new[] { 30, 1, 2 })]
        [TestCase(true, 26, new[] { 26, 27 })]        // circuit: lucky 25 next to the center is excluded
        public void Zone_ExcludesStartAndLucky_AndWrapsOnCircuit(bool circuit, int center, int[] expected)
        {
            var rules = new MatchRulesConfig(circuit: circuit, unluckyZoneRadius: 1);
            var zone = new List<int>();
            rules.GetUnluckyZone(center, 25, zone);
            CollectionAssert.AreEqual(expected, zone);
        }

        [TestCase(-1)] [TestCase(7)]
        public void InvalidRadius_Throws(int radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRulesConfig(unluckyZoneRadius: radius));
        }

        // ---- Engine

        [TestCase(4, PlayerStatus.Eliminated)]  // 5 + 4 = 9 (zone edge)
        [TestCase(6, PlayerStatus.Eliminated)]  // 5 + 6 = 11 (zone edge)
        [TestCase(5, PlayerStatus.Eliminated)]  // 5 + 5 = 10 (center)
        [TestCase(2, PlayerStatus.Active)]      // 5 + 2 = 7
        public void LandingAnywhereInTheZone_Eliminates(int die, PlayerStatus expected)
        {
            Start();
            P(1).Position = 5;
            Turn(die, die);
            Assert.AreEqual(expected, P(1).Status);
        }

        [Test]
        public void Shield_BlocksAZoneEdgeElimination()
        {
            Start(cards: true);
            var grant = _engine.GrantCard(1, CardIds.Shield);
            string card = ((CardDrawnEvent)grant.Events[0]).Card.InstanceId;
            Record(_engine.PlayCard(1, card));
            P(1).Position = 5;
            Turn(6, 6);
            Assert.IsTrue(P(1).IsActive);
            Assert.AreEqual(11, P(1).Position);
        }

        [Test]
        public void Bot_AvoidsTheWholeZone()
        {
            Start();
            P(1).Position = 5;
            _random.Enqueue(4, 3); // 9 is in the zone, 8 is safe
            Record(_engine.RollDice(1));
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(_engine, 1, _engine.State.CurrentRoll));
        }

        [Test]
        public void Frozen_OnZoneEdgeAtRoundEnd_IsEliminated()
        {
            Start(players: 2, circuit: true, cards: true);
            P(2).Position = 11;
            P(2).Debuff = CardEffectType.Freeze;
            P(2).DebuffRound = 1;
            Turn(); Turn();
            Assert.AreEqual(PlayerStatus.Eliminated, P(2).Status);
        }

        // ---- Rotation

        [Test]
        public void Rotation_MovesTheFirstSeatEveryRound()
        {
            Start(rotate: true);
            for (int i = 0; i < 9; i++) Turn();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 2, 3, 1, 3, 1, 2, 1 }, _turns);
        }

        [Test]
        public void WithoutRotation_OrderIsAscendingIdEveryRound()
        {
            Start(rotate: false);
            for (int i = 0; i < 6; i++) Turn();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 1, 2, 3, 1 }, _turns);
        }

        [Test]
        public void Rotation_SkipsAnEliminatedStartingSeat()
        {
            Start(rotate: true);
            Turn();                          // P1 -> 1
            P(2).Position = 8; Turn(2, 2);   // P2 -> 10: eliminated
            Turn();                          // P3 -> 1, round 2 should start with seat 2 -> skipped
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 3 }, _turns);
            Turn();
            Assert.AreEqual(1, _engine.State.CurrentPlayerId);
        }

        [Test]
        public void BotsCompleteMatchesWithZoneAndRotation()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                int fixedSeed = seed;
                var session = new MatchSession(
                    new MatchRulesConfig(circuit: true, persistentShield: true, cardDrawInterval: 2,
                        unluckyZoneRadius: 1, rotateFirstPlayer: true),
                    Enumerable.Range(1, 4).Select(i => new PlayerSlot(i, true)).ToArray(), () => new SeededRandomSource(fixedSeed),
                    cardCatalog: CardCatalog.CreateExpanded(), cardRandomFactory: () => new SeededRandomSource(fixedSeed + 100));
                session.StartMatch();
                for (int step = 0; step < 400 && !session.State.IsFinished; step++) Assert.IsTrue(session.StepBot().Success);
                Assert.IsTrue(session.State.IsFinished, $"seed {seed}");
            }
        }
    }
}
