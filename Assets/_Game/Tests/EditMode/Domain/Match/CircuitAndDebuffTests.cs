using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Match
{
    public class CircuitAndDebuffTests
    {
        private ScriptedRandomSource _random;
        private MatchEngine _engine;
        private PlayerState P(int id) => _engine.State.GetPlayer(id);
        private void Start(int players = 2, int interval = 0, int rounds = 15)
        {
            _random = new ScriptedRandomSource(25) { Unlucky = 7 };
            _engine = new MatchEngine(new MatchRulesConfig(startingHandSize: 0, circuit: true,
                persistentShield: true, cardDrawInterval: interval, maxRounds: rounds), _random,
                new CardSetup(CardCatalog.CreateExpanded(), new SeededRandomSource(42)));
            Assert.IsTrue(_engine.StartMatch(Enumerable.Range(1, players).ToArray()).Success);
        }
        private string Grant(int id, string card)
        {
            var result = _engine.GrantCard(id, card);
            Assert.IsTrue(result.Success);
            return ((CardDrawnEvent)result.Events[0]).Card.InstanceId;
        }
        private void Turn(int a = 1, int b = 1)
        {
            int id = _engine.State.CurrentPlayerId;
            bool frozen = P(id).HasDebuff(CardEffectType.Freeze, _engine.State.RoundNumber);
            if (!frozen) _random.Enqueue(a, b);
            Assert.IsTrue(_engine.RollDice(id).Success);
            if (!frozen) Assert.IsTrue(_engine.ChooseDie(id, 0).Success);
        }

        [TestCase(29, 4, 3)] [TestCase(24, 3, 27)] [TestCase(0, 1, 1)] [TestCase(30, 60, 30)]
        public void Circuit_WrapsWithoutBouncing(int start, int steps, int expected)
        {
            var result = new MovementService(new MatchRulesConfig(circuit: true)).Move(start, steps, 25);
            Assert.AreEqual(expected, result.FinalPosition);
            Assert.IsFalse(result.Bounced);
            Assert.IsTrue(result.Path.All(t => t >= 1 && t <= 30));
        }

        [Test]
        public void PassingLuckyDoesNotWin_ExactLandingDoes()
        {
            Start(); P(1).Position = 24; Turn(3);
            Assert.IsFalse(_engine.State.IsFinished); Assert.AreEqual(27, P(1).Position);
            P(2).Position = 24; Turn();
            Assert.AreEqual(MatchEndReason.ExactLucky, _engine.State.EndReason);
        }

        [Test]
        public void UnluckyCanBeBeyondLucky_AndNeverEqualsIt()
        {
            var rng = new ScriptedRandomSource(29, 24, 25);
            var luck = new LuckNumberService(rng, new MatchRulesConfig(circuit: true));
            Assert.AreEqual(30, luck.DrawUnluckyNumber(25));
            Assert.AreEqual(24, luck.DrawUnluckyNumber(25));
            Assert.AreEqual(26, luck.DrawUnluckyNumber(25));
        }

        [Test]
        public void Freeze_AnnouncedBeforeActivation_EliminatesOnlyAtRoundEnd()
        {
            Start();
            Assert.IsTrue(_engine.PlayCard(1, Grant(1, CardIds.Freeze), 2).Success);
            Assert.AreEqual(2, P(2).DebuffRound);
            Turn(); Turn(2);
            Assert.AreEqual(2, P(2).Position, "not frozen in the application round");
            P(2).Position = 7; Turn();
            Assert.IsTrue(P(2).IsActive);
            Turn();
            Assert.AreEqual(PlayerStatus.Eliminated, P(2).Status);
            Assert.AreEqual(MatchEndReason.LastSurvivor, _engine.State.EndReason);
        }

        [TestCase(CardIds.Shield)] [TestCase(CardIds.Retreat)] [TestCase(CardIds.Swap)]
        public void FrozenPlayerCanDefendUsingCards(string defense)
        {
            Start(); P(1).Position = 7; P(1).Debuff = CardEffectType.Freeze; P(1).DebuffRound = 1;
            var card = Grant(1, defense);
            Assert.IsTrue(_engine.PlayCard(1, card, defense == CardIds.Swap ? 2 : (int?)null).Success);
            Turn(); Turn();
            Assert.IsTrue(P(1).IsActive);
            Assert.IsNull(P(1).Debuff);
        }

        [Test]
        public void Freeze_OnSafeTileExpires_NoRandomDiceConsumed()
        {
            Start(); P(1).Debuff = CardEffectType.Freeze; P(1).DebuffRound = 1;
            int draws = _random.DrawCount;
            Turn(); Assert.AreEqual(draws, _random.DrawCount);
            Assert.AreEqual(0, P(1).Position);
            Turn(); Assert.IsNull(P(1).Debuff); Assert.IsTrue(P(1).IsActive);
        }

        [Test]
        public void DebuffsCannotStackOrRefresh_RejectWithoutConsuming()
        {
            Start(); P(2).Debuff = CardEffectType.Freeze; P(2).DebuffRound = 2;
            string card = Grant(1, CardIds.HeavyFeet);
            Assert.AreEqual(CommandError.InvalidTarget, _engine.PlayCard(1, card, 2).Error);
            Assert.IsNotNull(P(1).Hand.Find(card)); Assert.IsFalse(_engine.State.CardPlayedThisTurn);
        }

        [Test]
        public void HeavyFeet_ClampsAndAlsoAffectsReroll()
        {
            Start(); P(1).Debuff = CardEffectType.HeavyFeet; P(1).DebuffRound = 1;
            _random.Enqueue(1, 6); _engine.RollDice(1);
            CollectionAssert.AreEqual(new[] { 1, 4 }, _engine.State.CurrentRoll.Values);
            _random.Enqueue(2, 5);
            Assert.IsTrue(_engine.PlayCard(1, Grant(1, CardIds.LuckyRoll)).Success);
            CollectionAssert.AreEqual(new[] { 1, 3 }, _engine.State.CurrentRoll.Values);
        }

        [Test]
        public void CrackedDie_OneDrawAndOneChoiceIncludingReroll()
        {
            Start(); P(1).Debuff = CardEffectType.CrackedDie; P(1).DebuffRound = 1;
            int before = _random.DrawCount;
            _random.Enqueue(6); _engine.RollDice(1);
            Assert.AreEqual(1, _engine.State.CurrentRoll.Count); Assert.AreEqual(before + 1, _random.DrawCount);
            Assert.AreEqual(CommandError.InvalidDieIndex, _engine.ChooseDie(1, 1).Error);
            _random.Enqueue(3); _engine.PlayCard(1, Grant(1, CardIds.LuckyRoll));
            Assert.AreEqual(1, _engine.State.CurrentRoll.Count); Assert.AreEqual(3, _engine.State.CurrentRoll[0]);
        }

        [Test]
        public void StickyLuck_RemembersOriginalDangerAndExpires()
        {
            Start(); _engine.PlayCard(1, Grant(1, CardIds.StickyLuck), 2);
            Assert.AreEqual(7, P(2).StickyUnluckyNumber);
            Turn(); _random.Unlucky = 9; Turn();
            Assert.IsTrue(_engine.IsDangerous(2, 7)); Assert.IsTrue(_engine.IsDangerous(2, 9));
            Assert.IsFalse(_engine.IsDangerous(1, 7));
            Turn(); Turn(); Assert.IsFalse(_engine.IsDangerous(2, 7));
        }

        [Test]
        public void StickyTileEliminatesOnLanding()
        {
            Start(); P(1).Debuff = CardEffectType.StickyLuck; P(1).DebuffRound = 1; P(1).StickyUnluckyNumber = 3;
            Turn(3); Assert.AreEqual(PlayerStatus.Eliminated, P(1).Status);
        }

        [Test]
        public void PersistentShield_SurvivesSafeMoveThenBlocksOnce()
        {
            Start(); _engine.PlayCard(1, Grant(1, CardIds.Shield)); Turn();
            Assert.IsTrue(P(1).HasShield); Turn(); P(1).Position = 6; Turn();
            Assert.IsTrue(P(1).IsActive); Assert.IsFalse(P(1).HasShield);
        }

        [Test]
        public void RefillEveryTwoRounds_RespectsCapacity()
        {
            Start(interval: 2); Grant(1, CardIds.Shield); Grant(1, CardIds.Shield); Grant(1, CardIds.Shield);
            for (int i = 0; i < 4; i++) Turn();
            Assert.AreEqual(3, _engine.State.RoundNumber);
            Assert.AreEqual(3, P(1).Hand.Count); Assert.AreEqual(1, P(2).Hand.Count);
        }

        [Test]
        public void SimultaneousFrozenEliminationsProduceDraw()
        {
            Start();
            foreach (var p in _engine.State.Players) { p.Position = 7; p.Debuff = CardEffectType.Freeze; p.DebuffRound = 1; }
            Turn(); Turn();
            Assert.AreEqual(MatchEndReason.NoSurvivors, _engine.State.EndReason);
            Assert.IsEmpty(_engine.State.WinnerIds);
        }

        [Test]
        public void RoundLimitUsesForwardCircuitDistance_NotLargestTile()
        {
            Start(rounds: 1); P(1).Position = 22; P(2).Position = 28;
            Turn(); Turn();
            CollectionAssert.AreEqual(new[] { 1 }, _engine.State.WinnerIds);
        }

        [Test]
        public void BotsUseCards_CompleteMatchesAndRestartWithFreshState()
        {
            int used = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                int fixedSeed = seed;
                var session = new MatchSession(new MatchRulesConfig(circuit: true, persistentShield: true, cardDrawInterval: 2),
                    Enumerable.Range(1, 4).Select(i => new PlayerSlot(i, true)).ToArray(), () => new SeededRandomSource(fixedSeed),
                    cardCatalog: CardCatalog.CreateExpanded(), cardRandomFactory: () => new SeededRandomSource(fixedSeed + 100));
                session.EventPublished += e => { if (e is CardPlayedEvent) used++; };
                session.StartMatch();
                for (int step = 0; step < 300 && !session.State.IsFinished; step++) Assert.IsTrue(session.StepBot().Success);
                Assert.IsTrue(session.State.IsFinished, $"seed {seed}");
                session.RestartMatch();
                Assert.IsTrue(session.State.Players.All(p => p.Position == 0 && p.Debuff == null && !p.HasShield && p.Hand.Count == 2));
            }
            Assert.Greater(used, 0);
        }
    }
}
