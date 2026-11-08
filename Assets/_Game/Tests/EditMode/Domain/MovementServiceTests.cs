using System;
using DontTrustYourLuck.Domain;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain
{
    public class MovementServiceTests
    {
        private MovementService _movement;

        [SetUp]
        public void SetUp() => _movement = new MovementService(MatchRulesConfig.Default);

        [Test]
        public void NormalMove_AdvancesBySteps()
        {
            var r = _movement.Move(4, 5, 30);
            Assert.AreEqual(9, r.FinalPosition);
            CollectionAssert.AreEqual(new[] { 5, 6, 7, 8, 9 }, r.Path);
            Assert.IsFalse(r.Bounced);
        }

        [Test]
        public void Overshoot_BouncesAtThirty()
        {
            var r = _movement.Move(28, 5, 30);
            Assert.AreEqual(27, r.FinalPosition);
            CollectionAssert.AreEqual(new[] { 29, 30, 29, 28, 27 }, r.Path);
            Assert.IsTrue(r.Bounced);
        }

        [Test]
        public void Overshoot_BouncesAtLuckyNumber_TddExample()
        {
            var r = _movement.Move(18, 4, 20);
            CollectionAssert.AreEqual(new[] { 19, 20, 19, 18 }, r.Path);
            Assert.AreEqual(18, r.FinalPosition);
            Assert.IsTrue(r.Bounced);
        }

        [Test]
        public void ExactLanding_OnBouncePoint_DoesNotBounce()
        {
            var r = _movement.Move(18, 2, 20);
            Assert.AreEqual(20, r.FinalPosition);
            Assert.IsFalse(r.Bounced);
        }

        [Test]
        public void MoveFromZero_StartsAtOne()
        {
            var r = _movement.Move(0, 6, 30);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, r.Path);
            Assert.AreEqual(6, r.FinalPosition);
        }

        [Test]
        public void MoveFromZero_SingleStep()
        {
            Assert.AreEqual(1, _movement.Move(0, 1, 30).FinalPosition);
        }

        [Test]
        public void MoveFromThirty_GoesBackwards()
        {
            var r = _movement.Move(30, 6, 30);
            CollectionAssert.AreEqual(new[] { 29, 28, 27, 26, 25, 24 }, r.Path);
            Assert.AreEqual(24, r.FinalPosition);
        }

        [Test]
        public void MoveFromTwentyNine_ByOne_LandsOnThirty()
        {
            Assert.AreEqual(30, _movement.Move(29, 1, 30).FinalPosition);
        }

        [Test]
        public void PathLength_AlwaysEqualsSteps_AndStaysOnBoard()
        {
            for (int lucky = 15; lucky <= 30; lucky++)
            for (int start = 0; start <= lucky; start++)
            for (int steps = 1; steps <= 6; steps++)
            {
                var r = _movement.Move(start, steps, lucky);
                Assert.AreEqual(steps, r.Path.Count);
                Assert.AreEqual(r.Path[r.Path.Count - 1], r.FinalPosition);
                foreach (int tile in r.Path) Assert.That(tile, Is.InRange(0, lucky));
                int expected = start + steps <= lucky ? start + steps : 2 * lucky - start - steps;
                Assert.AreEqual(expected, r.FinalPosition, $"start {start}, steps {steps}, lucky {lucky}");
            }
        }

        [Test]
        public void Result_ExposesInputs()
        {
            var r = _movement.Move(10, 3, 25);
            Assert.AreEqual(10, r.StartPosition);
            Assert.AreEqual(3, r.Steps);
            Assert.AreEqual(25, r.BounceAt);
        }

        [TestCase(-1, 3, 30, TestName = "Negative start")]
        [TestCase(31, 3, 30, TestName = "Start beyond board")]
        [TestCase(25, 3, 20, TestName = "Start beyond bounce point")]
        [TestCase(5, 0, 30, TestName = "Zero steps")]
        [TestCase(5, -2, 30, TestName = "Negative steps")]
        [TestCase(5, 3, 0, TestName = "Bounce point zero")]
        [TestCase(5, 3, 31, TestName = "Bounce point beyond board")]
        [TestCase(0, 61, 30, TestName = "Rebound would pass start tile")]
        public void InvalidInput_Throws(int start, int steps, int bounceAt)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _movement.Move(start, steps, bounceAt));
        }

        [Test]
        public void MaximumAllowedSteps_EndsOnStartTile()
        {
            Assert.AreEqual(0, _movement.Move(0, 60, 30).FinalPosition);
        }

        [Test]
        public void NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MovementService(null));
        }
    }
}
