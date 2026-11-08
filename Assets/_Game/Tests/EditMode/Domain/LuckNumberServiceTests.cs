using System;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Infrastructure;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain
{
    public class LuckNumberServiceTests
    {
        [Test]
        public void DrawLuckyNumber_RequestsConfiguredInclusiveRange()
        {
            var fake = new FakeRandomSource(15);
            var service = new LuckNumberService(fake, MatchRulesConfig.Default);

            Assert.AreEqual(15, service.DrawLuckyNumber());
            Assert.AreEqual((15, 31), fake.Requests[0]);
        }

        [TestCase(15)]
        [TestCase(30)]
        public void DrawLuckyNumber_AcceptsRangeEdges(int scripted)
        {
            var service = new LuckNumberService(new FakeRandomSource(scripted), MatchRulesConfig.Default);
            Assert.AreEqual(scripted, service.DrawLuckyNumber());
        }

        [Test]
        public void DrawLuckyNumber_ManySeeds_StaysInRange()
        {
            for (int seed = 0; seed < 2000; seed++)
            {
                var service = new LuckNumberService(new SeededRandomSource(seed), MatchRulesConfig.Default);
                Assert.That(service.DrawLuckyNumber(), Is.InRange(15, 30));
            }
        }

        [Test]
        public void DrawUnluckyNumber_RequestsOneToLuckyExclusive()
        {
            var fake = new FakeRandomSource(1);
            var service = new LuckNumberService(fake, MatchRulesConfig.Default);

            Assert.AreEqual(1, service.DrawUnluckyNumber(20));
            Assert.AreEqual((1, 20), fake.Requests[0]);
        }

        [Test]
        public void DrawUnluckyNumber_ForEveryLucky_StaysInRangeAndNeverEqualsLucky()
        {
            var rng = new SeededRandomSource(31337);
            var service = new LuckNumberService(rng, MatchRulesConfig.Default);
            for (int lucky = 15; lucky <= 30; lucky++)
            {
                for (int round = 0; round < 500; round++)
                {
                    int unlucky = service.DrawUnluckyNumber(lucky);
                    Assert.That(unlucky, Is.InRange(1, lucky - 1));
                    Assert.AreNotEqual(lucky, unlucky);
                    Assert.AreNotEqual(0, unlucky, "Start tile must stay safe.");
                }
            }
        }

        [Test]
        public void DrawUnluckyNumber_MaySeeAllValuesBelowLucky()
        {
            var service = new LuckNumberService(new SeededRandomSource(8), MatchRulesConfig.Default);
            var seen = new bool[30];
            for (int i = 0; i < 5000; i++) seen[service.DrawUnluckyNumber(30)] = true;
            for (int v = 1; v < 30; v++) Assert.IsTrue(seen[v], $"Unlucky value {v} never drawn.");
        }

        [Test]
        public void DrawUnluckyNumber_MinimalConfig_DoesNotLoop()
        {
            // Lucky = 2 leaves a single valid unlucky value; must return immediately.
            var rules = new MatchRulesConfig(boardLength: 2, minLuckyNumber: 2, maxLuckyNumber: 2, diceSides: 2);
            var service = new LuckNumberService(new SeededRandomSource(1), rules);
            Assert.AreEqual(2, service.DrawLuckyNumber());
            Assert.AreEqual(1, service.DrawUnluckyNumber(2));
        }

        [TestCase(14)]
        [TestCase(31)]
        [TestCase(0)]
        public void DrawUnluckyNumber_LuckyOutsideRange_Throws(int lucky)
        {
            var service = new LuckNumberService(new FakeRandomSource(), MatchRulesConfig.Default);
            Assert.Throws<ArgumentOutOfRangeException>(() => service.DrawUnluckyNumber(lucky));
        }

        [Test]
        public void SameSeed_ReproducesLuckyAndUnluckySequence()
        {
            var a = new LuckNumberService(new SeededRandomSource(77), MatchRulesConfig.Default);
            var b = new LuckNumberService(new SeededRandomSource(77), MatchRulesConfig.Default);
            int luckyA = a.DrawLuckyNumber();
            Assert.AreEqual(luckyA, b.DrawLuckyNumber());
            for (int round = 0; round < 15; round++)
                Assert.AreEqual(a.DrawUnluckyNumber(luckyA), b.DrawUnluckyNumber(luckyA));
        }
    }
}
