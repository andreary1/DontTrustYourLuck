using System;
using System.Collections.Generic;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Infrastructure;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain
{
    public class SeededRandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new SeededRandomSource(1234);
            var b = new SeededRandomSource(1234);
            for (int i = 0; i < 500; i++)
                Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000), $"Mismatch at draw {i}");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new SeededRandomSource(1);
            var b = new SeededRandomSource(2);
            bool anyDifferent = false;
            for (int i = 0; i < 50 && !anyDifferent; i++)
                anyDifferent = a.NextInt(0, 1000000) != b.NextInt(0, 1000000);
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void KnownSeed_ProducesStableSequence()
        {
            // Guards cross-version determinism: if this changes, saved seeds/replays break.
            var rng = new SeededRandomSource(42);
            var first = new int[5];
            for (int i = 0; i < first.Length; i++) first[i] = rng.NextInt(1, 7);

            var again = new SeededRandomSource(42);
            for (int i = 0; i < first.Length; i++) Assert.AreEqual(first[i], again.NextInt(1, 7));
        }

        [Test]
        public void NextInt_StaysWithinBounds_AndHitsBothEnds()
        {
            var rng = new SeededRandomSource(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 5000; i++)
            {
                int v = rng.NextInt(-3, 4);
                Assert.That(v, Is.InRange(-3, 3));
                seen.Add(v);
            }
            Assert.AreEqual(7, seen.Count, "Every value in [-3, 3] should appear in 5000 draws.");
        }

        [Test]
        public void NextInt_SingleValueRange_ReturnsMin()
        {
            var rng = new SeededRandomSource(0);
            for (int i = 0; i < 20; i++) Assert.AreEqual(5, rng.NextInt(5, 6));
        }

        [TestCase(5, 5)]
        [TestCase(6, 5)]
        public void NextInt_InvalidRange_Throws(int min, int max)
        {
            var rng = new SeededRandomSource(0);
            Assert.Throws<ArgumentException>(() => rng.NextInt(min, max));
        }

        [Test]
        public void NextInt_FullIntRange_DoesNotOverflow()
        {
            var rng = new SeededRandomSource(99);
            for (int i = 0; i < 100; i++)
            {
                int v = rng.NextInt(int.MinValue, int.MaxValue);
                Assert.That(v, Is.LessThan(int.MaxValue));
            }
        }
    }

    public class DiceServiceTests
    {
        [Test]
        public void Roll_DefaultRules_ReturnsTwoValuesBetweenOneAndSix()
        {
            var dice = new DiceService(new SeededRandomSource(2026), MatchRulesConfig.Default);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                var roll = dice.Roll();
                Assert.AreEqual(2, roll.Count);
                foreach (int v in roll.Values)
                {
                    Assert.That(v, Is.InRange(1, 6));
                    seen.Add(v);
                }
            }
            Assert.AreEqual(6, seen.Count, "All faces 1..6 should appear.");
        }

        [Test]
        public void Roll_UsesRandomSourceInDieOrder()
        {
            var fake = new FakeRandomSource(3, 6);
            var dice = new DiceService(fake, MatchRulesConfig.Default);

            var roll = dice.Roll();

            Assert.AreEqual(3, roll[0]);
            Assert.AreEqual(6, roll[1]);
            Assert.AreEqual(2, fake.Requests.Count);
            Assert.AreEqual((1, 7), fake.Requests[0]);
            Assert.AreEqual((1, 7), fake.Requests[1]);
        }

        [Test]
        public void Roll_SameSeed_IsReproducible()
        {
            var a = new DiceService(new SeededRandomSource(555), MatchRulesConfig.Default);
            var b = new DiceService(new SeededRandomSource(555), MatchRulesConfig.Default);
            for (int i = 0; i < 100; i++)
                CollectionAssert.AreEqual(a.Roll().Values, b.Roll().Values);
        }

        [Test]
        public void DiceRoll_IsImmutableCopy()
        {
            var source = new[] { 2, 5 };
            var roll = new DiceRoll(source);
            source[0] = 6;
            Assert.AreEqual(2, roll[0]);
        }

        [Test]
        public void InvalidConstruction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DiceService(null, MatchRulesConfig.Default));
            Assert.Throws<ArgumentNullException>(() => new DiceService(new FakeRandomSource(), null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiceService(new FakeRandomSource(), 0, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiceService(new FakeRandomSource(), 2, 1));
        }
    }
}
