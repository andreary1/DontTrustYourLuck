using System;
using DontTrustYourLuck.Domain;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain
{
    public class MatchRulesConfigTests
    {
        [Test]
        public void Default_HasSprint1Values()
        {
            var c = MatchRulesConfig.Default;
            Assert.AreEqual(30, c.BoardLength);
            Assert.AreEqual(31, c.TileCount);
            Assert.AreEqual(15, c.MinLuckyNumber);
            Assert.AreEqual(30, c.MaxLuckyNumber);
            Assert.AreEqual(2, c.DiceCount);
            Assert.AreEqual(6, c.DiceSides);
            Assert.AreEqual(2, c.MinPlayers);
            Assert.AreEqual(4, c.MaxPlayers);
            Assert.AreEqual(2, c.StartingHandSize);
            Assert.AreEqual(3, c.MaxHandSize);
            Assert.AreEqual(15, c.MaxRounds);
        }

        [Test]
        public void CustomValidValues_AreKept()
        {
            var c = new MatchRulesConfig(boardLength: 40, minLuckyNumber: 20, maxLuckyNumber: 40,
                diceCount: 3, diceSides: 8, minPlayers: 2, maxPlayers: 6, startingHandSize: 0,
                maxHandSize: 0, maxRounds: 1);
            Assert.AreEqual(40, c.BoardLength);
            Assert.AreEqual(20, c.MinLuckyNumber);
            Assert.AreEqual(3, c.DiceCount);
            Assert.AreEqual(8, c.DiceSides);
            Assert.AreEqual(6, c.MaxPlayers);
        }

        [Test]
        public void Validate_DefaultValues_ReturnsNoErrors()
        {
            var errors = MatchRulesConfig.Validate(30, 15, 30, 2, 6, 2, 4, 2, 3, 15);
            CollectionAssert.IsEmpty(errors);
        }

        [TestCase(1, 15, 30, 2, 6, 2, 4, 2, 3, 15, TestName = "BoardLength too small")]
        [TestCase(30, 1, 30, 2, 6, 2, 4, 2, 3, 15, TestName = "MinLucky below 2")]
        [TestCase(30, 20, 15, 2, 6, 2, 4, 2, 3, 15, TestName = "MaxLucky below MinLucky")]
        [TestCase(30, 15, 31, 2, 6, 2, 4, 2, 3, 15, TestName = "MaxLucky above BoardLength")]
        [TestCase(30, 15, 30, 0, 6, 2, 4, 2, 3, 15, TestName = "No dice")]
        [TestCase(30, 15, 30, 2, 1, 2, 4, 2, 3, 15, TestName = "One-sided die")]
        [TestCase(30, 5, 30, 2, 6, 2, 4, 2, 3, 15, TestName = "DiceSides above MinLucky")]
        [TestCase(30, 15, 30, 2, 6, 1, 4, 2, 3, 15, TestName = "MinPlayers below 2")]
        [TestCase(30, 15, 30, 2, 6, 3, 2, 2, 3, 15, TestName = "MaxPlayers below MinPlayers")]
        [TestCase(30, 15, 30, 2, 6, 2, 4, -1, 3, 15, TestName = "Negative starting hand")]
        [TestCase(30, 15, 30, 2, 6, 2, 4, 3, 2, 15, TestName = "MaxHand below StartingHand")]
        [TestCase(30, 15, 30, 2, 6, 2, 4, 2, 3, 0, TestName = "Zero rounds")]
        public void InvalidValues_AreRejected(int board, int minL, int maxL, int dice, int sides,
            int minP, int maxP, int startHand, int maxHand, int rounds)
        {
            CollectionAssert.IsNotEmpty(MatchRulesConfig.Validate(board, minL, maxL, dice, sides,
                minP, maxP, startHand, maxHand, rounds));
            Assert.Throws<ArgumentException>(() => new MatchRulesConfig(board, minL, maxL, dice, sides,
                minP, maxP, startHand, maxHand, rounds));
        }

        [Test]
        public void Validate_ReportsEveryError()
        {
            var errors = MatchRulesConfig.Validate(1, 1, 0, 0, 1, 0, -1, -1, -2, 0);
            Assert.GreaterOrEqual(errors.Count, 6);
        }

        [Test]
        public void IsValidTileIndex_CoversZeroToBoardLength()
        {
            var c = MatchRulesConfig.Default;
            Assert.IsTrue(c.IsValidTileIndex(0));
            Assert.IsTrue(c.IsValidTileIndex(30));
            Assert.IsFalse(c.IsValidTileIndex(-1));
            Assert.IsFalse(c.IsValidTileIndex(31));
        }
    }
}
