using System.Collections.Generic;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Domain.Series;
using DontTrustYourLuck.Presentation.Narration;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Presentation
{
    public sealed class CantadorNarratorTests
    {
        private static CantadorNarrator Create(CantadorLines lines = null, int seed = 7) =>
            new CantadorNarrator(lines ?? new CantadorLines(), id => "P" + id, seed);

        private static CantadorLines Single(string template)
        {
            var lines = new CantadorLines();
            foreach (CantadorCue cue in System.Enum.GetValues(typeof(CantadorCue)))
            {
                var pool = lines.For(cue);
                for (int i = 0; i < pool.Length; i++) pool[i] = cue + ":" + template;
            }
            return lines;
        }

        [Test]
        public void MatchStarted_FillsLuckyAndSplitsCouplet()
        {
            var lines = new CantadorLines { luckyDrawn = new[] { "first {lucky} / second" } };
            var n = Create(lines);

            Assert.IsTrue(n.TryNarrate(new MatchStartedEvent(23, new[] { 1, 2 }), out var line));
            Assert.AreEqual(CantadorCue.LuckyDrawn, line.Cue);
            Assert.AreEqual("first 23\nsecond", line.Text);
        }

        [Test]
        public void UnluckyDrawn_UsesAzarUntilTheLastCycle()
        {
            var n = Create(Single("{unlucky}"));
            n.MaxCycles = 12;

            Assert.IsTrue(n.TryNarrate(new UnluckyNumberDrawnEvent(11, 9), out var azar));
            Assert.AreEqual(CantadorCue.AzarLands, azar.Cue);
            StringAssert.EndsWith(":9", azar.Text);

            Assert.IsTrue(n.TryNarrate(new UnluckyNumberDrawnEvent(12, 4), out var last));
            Assert.AreEqual(CantadorCue.LastCycle, last.Cue);
        }

        [Test]
        public void Elimination_FillsPlayerName()
        {
            var n = Create(Single("{player}"));
            Assert.IsTrue(n.TryNarrate(new PlayerEliminatedEvent(3, 12, default), out var line));
            Assert.AreEqual(CantadorCue.Elimination, line.Cue);
            Assert.AreEqual("Elimination:P3", line.Text);
        }

        [Test]
        public void Shield_OnlyWhenItBlockedAnElimination()
        {
            var n = Create();
            Assert.IsFalse(n.TryNarrate(new ShieldConsumedEvent(1, false, 5), out _));
            Assert.IsTrue(n.TryNarrate(new ShieldConsumedEvent(1, true, 5), out var line));
            Assert.AreEqual(CantadorCue.ShieldSaved, line.Cue);
        }

        [Test]
        public void Bump_NarratesTheBumperOncePerTurn()
        {
            var n = Create();
            Assert.IsTrue(n.TryNarrate(new PlayerBumpedEvent(1, 2, 10, 7), out _));
            Assert.IsFalse(n.TryNarrate(new PlayerBumpedEvent(1, 3, 10, 7), out _), "second victim of the same bump");
            n.TryNarrate(new TurnChangedEvent(2, 1), out _);
            Assert.IsTrue(n.TryNarrate(new PlayerBumpedEvent(1, 2, 14, 11), out _));
        }

        [Test]
        public void QuickMatch_FinishIsTheChampionLineWithTheMoral()
        {
            var lines = Single("{player}");
            var n = Create(lines);
            n.SeriesMode = false;

            Assert.IsTrue(n.TryNarrate(new MatchFinishedEvent(MatchEndReason.ExactLucky, new[] { 2 }, 4), out var line));
            Assert.AreEqual(CantadorCue.Champion, line.Cue);
            StringAssert.StartsWith("Champion:P2", line.Text);
            StringAssert.Contains(lines.moral, line.Text);
            Assert.IsTrue(line.IsFinale);
        }

        [Test]
        public void QuickMatch_TieUsesSharedLineWithAllNames()
        {
            var n = Create(Single("{players}"));
            Assert.IsTrue(n.TryNarrate(new MatchFinishedEvent(MatchEndReason.RoundLimit, new[] { 1, 3 }, 15), out var line));
            Assert.AreEqual(CantadorCue.ChampionShared, line.Cue);
            StringAssert.StartsWith("ChampionShared:P1 & P3", line.Text);
        }

        [TestCase(MatchEndReason.ExactLucky, CantadorCue.PointExactLucky)]
        [TestCase(MatchEndReason.LastSurvivor, CantadorCue.PointLastSurvivor)]
        [TestCase(MatchEndReason.RoundLimit, CantadorCue.PointClosest)]
        public void Series_RoundEndAwardsPointLineByReason(MatchEndReason reason, CantadorCue expected)
        {
            var n = Create();
            n.SeriesMode = true;
            Assert.IsTrue(n.TryNarrate(new MatchFinishedEvent(reason, new[] { 1 }, 3), out var line));
            Assert.AreEqual(expected, line.Cue);
            Assert.IsFalse(line.IsFinale);
        }

        [Test]
        public void Series_TieAtCapAndChampion()
        {
            var n = Create();
            n.SeriesMode = true;
            Assert.IsTrue(n.TryNarrate(new MatchFinishedEvent(MatchEndReason.RoundLimit, new[] { 1, 2 }, 12), out var tie));
            Assert.AreEqual(CantadorCue.PointClosestTie, tie.Cue);

            Assert.IsTrue(n.TryNarrate(new SeriesWonEvent(2, 6, new Dictionary<int, int> { { 1, 3 }, { 2, 5 } }), out var won));
            Assert.AreEqual(CantadorCue.Champion, won.Cue);
            StringAssert.Contains("P2", won.Text);
        }

        [Test]
        public void NoSurvivors_HasItsOwnLine()
        {
            var n = Create();
            Assert.IsTrue(n.TryNarrate(new MatchFinishedEvent(MatchEndReason.NoSurvivors, new int[0], 5), out var line));
            Assert.AreEqual(CantadorCue.NoSurvivors, line.Cue);
        }

        [Test]
        public void SameCue_NeverRepeatsThePreviousLine()
        {
            var n = Create(seed: 123);
            string previous = null;
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(n.TryNarrate(new UnluckyNumberDrawnEvent(1, 5), out var line));
                Assert.AreNotEqual(previous, line.Text, $"repeat at call {i}");
                previous = line.Text;
            }
        }

        [Test]
        public void EveryDefaultLine_HasNoUnfilledPlaceholder()
        {
            var lines = new CantadorLines();
            var n = Create(lines);
            n.TryNarrate(new MatchStartedEvent(20, new[] { 1, 2 }), out _);
            foreach (CantadorCue cue in System.Enum.GetValues(typeof(CantadorCue)))
            {
                Assert.IsNotEmpty(lines.For(cue), cue.ToString());
            }
            var events = new MatchEvent[]
            {
                new MatchStartedEvent(20, new[] { 1, 2 }),
                new UnluckyNumberDrawnEvent(1, 6),
                new PlayerEliminatedEvent(1, 6, default),
                new ShieldConsumedEvent(2, true, 6),
                new PlayerBumpedEvent(2, 1, 9, 6),
                new MatchFinishedEvent(MatchEndReason.RoundLimit, new[] { 1, 2 }, 15),
                new MatchFinishedEvent(MatchEndReason.ExactLucky, new[] { 2 }, 4)
            };
            for (int round = 0; round < 10; round++)
            {
                foreach (var e in events)
                {
                    n.TryNarrate(new TurnChangedEvent(1, 1), out _);
                    Assert.IsTrue(n.TryNarrate(e, out var line), e.GetType().Name);
                    StringAssert.DoesNotContain("{", line.Text, line.Text);
                }
            }
        }
    }
}
