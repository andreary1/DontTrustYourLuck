using System;
using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Domain.Series;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Series
{
    /// <summary>
    /// R1: series of rounds until PointsToWin. Rounds use a tiny board whose lucky number is always 6 and one
    /// six-sided die, so every round is scripted with a few values: [lucky=6, unlucky (1..5), die values...].
    /// </summary>
    public class SeriesEngineTests
    {
        private static readonly MatchRulesConfig Tiny = new MatchRulesConfig(
            boardLength: 6, minLuckyNumber: 6, maxLuckyNumber: 6, diceCount: 1, diceSides: 6, maxRounds: 15);

        /// <summary>Hands one scripted random source per round, in order; counts factory calls.</summary>
        private sealed class RoundScripts
        {
            private readonly Queue<int[]> _rounds = new Queue<int[]>();
            public int Calls { get; private set; }
            public void Add(params int[] values) => _rounds.Enqueue(values);
            public IRandomSource Next()
            {
                Calls++;
                return new FakeRandomSource(_rounds.Count > 0 ? _rounds.Dequeue() : Array.Empty<int>());
            }
        }

        private static SeriesEngine Series(RoundScripts scripts, int pointsToWin = 5, int maxCycles = 12,
            CardCatalog cards = null, Func<IRandomSource> cardRandom = null) =>
            new SeriesEngine(new SeriesRules(pointsToWin, maxCycles), Tiny, scripts.Next, cards, cardRandom);

        private static List<T> Of<T>(CommandResult result) => result.Events.OfType<T>().ToList();

        /// <summary>Current player rolls <paramref name="value"/> (already scripted) and takes it.</summary>
        private static CommandResult Play(SeriesEngine series)
        {
            int id = series.CurrentRound.State.CurrentPlayerId;
            Assert.IsTrue(series.RollDice(id).Success);
            return series.ChooseDie(id, 0);
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void Rules_DefaultsAndValidation()
        {
            Assert.AreEqual(5, SeriesRules.Default.PointsToWin);
            Assert.AreEqual(12, SeriesRules.Default.MaxCyclesPerRound);
            Assert.Throws<ArgumentException>(() => new SeriesRules(0, 12));
            Assert.Throws<ArgumentException>(() => new SeriesRules(5, 0));
            Assert.AreEqual(12, SeriesRules.Default.CreateRoundRules(MatchRulesConfig.Default).MaxRounds);
        }

        // ------------------------------------------------------------------ start

        [Test]
        public void StartSeries_EmitsSeriesThenRoundEvents_ScoresZero()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5);
            var series = Series(scripts);

            var result = series.StartSeries(new[] { 2, 1 });

            Assert.IsTrue(result.Success);
            Assert.IsInstanceOf<SeriesStartedEvent>(result.Events[0]);
            Assert.IsInstanceOf<SeriesRoundStartedEvent>(result.Events[1]);
            Assert.IsInstanceOf<MatchStartedEvent>(result.Events[2]);
            Assert.AreEqual(SeriesPhase.RoundInProgress, series.State.Phase);
            Assert.AreEqual(1, series.State.RoundNumber);
            CollectionAssert.AreEqual(new[] { 1, 2 }, series.State.PlayerIds);
            Assert.AreEqual(0, series.State.GetScore(1));
            Assert.AreEqual(0, series.State.GetScore(2));
            Assert.AreEqual(1, series.CurrentRound.State.CurrentPlayerId);
            Assert.AreEqual(12, series.CurrentRound.Rules.MaxRounds, "series cycle cap replaces MaxRounds");
        }

        [Test]
        public void StartSeries_InvalidPlayers_RejectedWithoutCallingFactory()
        {
            var scripts = new RoundScripts();
            var series = Series(scripts);

            Assert.AreEqual(CommandError.InvalidPlayerCount, series.StartSeries(new[] { 1 }).Error);
            Assert.AreEqual(CommandError.DuplicatePlayerId, series.StartSeries(new[] { 1, 1 }).Error);
            Assert.AreEqual(CommandError.InvalidPlayerId, series.StartSeries(new[] { 0, 1 }).Error);
            Assert.AreEqual(0, scripts.Calls);
            Assert.AreEqual(SeriesPhase.NotStarted, series.State.Phase);
        }

        [Test]
        public void CommandsBeforeStart_AreRejected()
        {
            var series = Series(new RoundScripts());
            Assert.AreEqual(CommandError.WrongPhase, series.RollDice(1).Error);
            Assert.AreEqual(CommandError.WrongPhase, series.StartNextRound().Error);
        }

        // ------------------------------------------------------------------ scoring

        [Test]
        public void ExactLucky_GivesOnePoint_AndEntersIntermission()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 6); // P1 rolls 6 -> lucky
            var series = Series(scripts);
            series.StartSeries(new[] { 1, 2 });

            var result = Play(series);

            Assert.IsTrue(result.Success);
            var award = Of<SeriesPointsAwardedEvent>(result).Single();
            Assert.AreEqual(MatchEndReason.ExactLucky, award.Reason);
            CollectionAssert.AreEqual(new[] { 1 }, award.ScorerIds);
            Assert.AreEqual(1, award.Scores[1]);
            Assert.AreEqual(0, award.Scores[2]);
            // Order: the round's MatchFinishedEvent comes before the series award.
            int finishedAt = result.Events.ToList().FindIndex(e => e is MatchFinishedEvent);
            int awardAt = result.Events.ToList().IndexOf(award);
            Assert.Less(finishedAt, awardAt);

            Assert.AreEqual(SeriesPhase.Intermission, series.State.Phase);
            Assert.AreEqual(MatchEndReason.ExactLucky, series.State.LastRoundReason);
            CollectionAssert.AreEqual(new[] { 1 }, series.State.LastRoundScorers);
            Assert.IsEmpty(Of<SeriesWonEvent>(result));
        }

        [Test]
        public void Intermission_RejectsGameplayCommands()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 6);
            var series = Series(scripts);
            series.StartSeries(new[] { 1, 2 });
            Play(series);

            Assert.AreEqual(CommandError.WrongPhase, series.RollDice(2).Error);
            Assert.AreEqual(CommandError.WrongPhase, series.ChooseDie(2, 0).Error);
            Assert.AreEqual(CommandError.WrongPhase, series.PlayCard(2, "card-1").Error);
            Assert.AreEqual(1, scripts.Calls, "no round was created by rejected commands");
        }

        [Test]
        public void LastSurvivor_GivesPointToSurvivor()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 3, 3); // unlucky 3; P1 rolls 3 -> eliminated
            var series = Series(scripts);
            series.StartSeries(new[] { 1, 2 });

            var result = Play(series);

            var award = Of<SeriesPointsAwardedEvent>(result).Single();
            Assert.AreEqual(MatchEndReason.LastSurvivor, award.Reason);
            CollectionAssert.AreEqual(new[] { 2 }, award.ScorerIds);
            Assert.AreEqual(1, series.State.GetScore(2));
        }

        [Test]
        public void CycleCap_TiedPlayers_EachScoreOnePoint()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 2, 2, 1); // cap 1 cycle; P1 -> 2, P2 -> 2, P3 -> 1
            var series = Series(scripts, maxCycles: 1);
            series.StartSeries(new[] { 1, 2, 3 });

            Play(series);
            Play(series);
            var result = Play(series);

            var award = Of<SeriesPointsAwardedEvent>(result).Single();
            Assert.AreEqual(MatchEndReason.RoundLimit, award.Reason);
            CollectionAssert.AreEqual(new[] { 1, 2 }, award.ScorerIds);
            Assert.AreEqual(1, series.State.GetScore(1));
            Assert.AreEqual(1, series.State.GetScore(2));
            Assert.AreEqual(0, series.State.GetScore(3));
        }

        [Test]
        public void ReachingPointsToWin_Alone_IsChampion_AndSeriesFinishes()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 6);    // round 1: P1 first, wins
            scripts.Add(6, 5, 1, 6); // round 2: P2 first -> 1, P1 -> 6 wins
            var series = Series(scripts, pointsToWin: 2);
            series.StartSeries(new[] { 1, 2 });
            Play(series);
            Assert.IsTrue(series.StartNextRound().Success);
            Play(series);
            var result = Play(series);

            var won = Of<SeriesWonEvent>(result).Single();
            Assert.AreEqual(1, won.ChampionId);
            Assert.AreEqual(2, won.RoundsPlayed);
            Assert.AreEqual(2, won.Scores[1]);
            Assert.IsTrue(series.State.IsFinished);
            Assert.AreEqual(1, series.State.ChampionId);
            Assert.AreEqual(CommandError.MatchFinished, series.StartNextRound().Error);
            Assert.AreEqual(CommandError.MatchFinished, series.RollDice(1).Error);
            Assert.AreEqual(CommandError.MatchFinished, series.StartSeries(new[] { 1, 2 }).Error);
        }

        [Test]
        public void SeveralReachPointsToWin_Tied_TiebreakerAmongThemOnly()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 2, 2, 1); // round 1 (cap 1): P1 and P2 tie on 2 -> both reach 1 point
            scripts.Add(6, 5, 6);       // round 2 (tiebreaker P1 vs P2): P2 starts (rotation) and wins
            var series = Series(scripts, pointsToWin: 1, maxCycles: 1);
            series.StartSeries(new[] { 1, 2, 3 });
            Play(series);
            Play(series);
            var result = Play(series);

            CollectionAssert.AreEqual(new[] { 1, 2 }, Of<SeriesTiebreakerEvent>(result).Single().PlayerIds);
            Assert.IsEmpty(Of<SeriesWonEvent>(result));
            Assert.AreEqual(SeriesPhase.Intermission, series.State.Phase);
            Assert.IsTrue(series.State.IsTiebreaker);

            var next = series.StartNextRound();
            var roundStart = Of<SeriesRoundStartedEvent>(next).Single();
            Assert.IsTrue(roundStart.IsTiebreaker);
            CollectionAssert.AreEqual(new[] { 1, 2 }, roundStart.ParticipantIds);
            Assert.AreEqual(2, roundStart.FirstPlayerId);
            CollectionAssert.AreEqual(new[] { 1, 2 }, series.CurrentRound.State.Players.Select(p => p.Id));
            Assert.IsNull(series.CurrentRound.State.GetPlayer(3), "P3 sits out the tiebreaker");

            var final = Play(series);
            Assert.AreEqual(2, Of<SeriesWonEvent>(final).Single().ChampionId);
            Assert.AreEqual(2, series.State.GetScore(2));
            Assert.AreEqual(1, series.State.GetScore(1));
            Assert.AreEqual(0, series.State.GetScore(3));
        }

        [Test]
        public void TiebreakerTiedAgain_PlaysAnotherTiebreaker()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 2, 2); // round 1 (cap 1): tie -> both 1
            scripts.Add(6, 5, 3, 3); // round 2 tiebreaker: tie again -> both 2
            var series = Series(scripts, pointsToWin: 1, maxCycles: 1);
            series.StartSeries(new[] { 1, 2 });
            Play(series);
            Play(series);
            series.StartNextRound();
            Play(series);
            var result = Play(series);

            Assert.AreEqual(1, Of<SeriesTiebreakerEvent>(result).Count);
            Assert.AreEqual(SeriesPhase.Intermission, series.State.Phase);
            Assert.IsTrue(series.State.IsTiebreaker);
        }

        // ------------------------------------------------------------------ between rounds

        [Test]
        public void NextRound_ResetsPositions_RevivesEliminated_RotatesFirstPlayer_DrawsNewLucky()
        {
            var scripts = new RoundScripts();
            scripts.Add(6, 3, 2, 3, 6); // round 1: P1 -> 2, P2 -> 3 (eliminated), P3 -> 6 wins
            scripts.Add(6, 5);          // round 2
            var series = Series(scripts);
            series.StartSeries(new[] { 1, 2, 3 });
            Play(series);
            Play(series);
            Play(series);
            var round1 = series.CurrentRound;
            Assert.AreEqual(PlayerStatus.Eliminated, round1.State.GetPlayer(2).Status);

            var result = series.StartNextRound();

            Assert.IsTrue(result.Success);
            Assert.AreNotSame(round1, series.CurrentRound, "one fresh engine per round");
            Assert.AreEqual(2, series.State.RoundNumber);
            Assert.AreEqual(2, scripts.Calls, "one random source per round");
            Assert.IsNotNull(Of<MatchStartedEvent>(result).SingleOrDefault(), "new lucky number drawn");
            foreach (var p in series.CurrentRound.State.Players)
            {
                Assert.AreEqual(0, p.Position);
                Assert.AreEqual(PlayerStatus.Active, p.Status);
            }
            Assert.AreEqual(2, series.CurrentRound.State.CurrentPlayerId, "round 2 starts with P2");
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, series.CurrentRound.State.TurnOrder);
            Assert.AreEqual(1, series.State.GetScore(3), "scores survive the round change");
        }

        [Test]
        public void Hands_AreKeptBetweenRounds_WithoutNewStartingHand()
        {
            var catalog = CardCatalog.CreateDefault();
            var cardScripts = new Queue<IRandomSource>();
            cardScripts.Enqueue(new FakeRandomSource(3, 0, 1, 2)); // round 1: P1 shield+lucky_roll, P2 swap+retreat
            cardScripts.Enqueue(new FakeRandomSource());           // round 2: must not draw
            var scripts = new RoundScripts();
            scripts.Add(6, 5, 6); // P1 plays Shield, then wins
            scripts.Add(6, 5);
            var series = Series(scripts, cards: catalog, cardRandom: () => cardScripts.Dequeue());
            series.StartSeries(new[] { 1, 2 });

            var shield = series.CurrentRound.State.GetPlayer(1).Hand.Cards.First(c => c.CardId == CardIds.Shield);
            Assert.IsTrue(series.PlayCard(1, shield.InstanceId).Success);
            Play(series);

            var result = series.StartNextRound();

            var drawn = Of<CardDrawnEvent>(result);
            Assert.IsTrue(drawn.All(e => e.Source == CardDrawSource.CarriedOver));
            CollectionAssert.AreEqual(new[] { CardIds.LuckyRoll },
                series.CurrentRound.State.GetPlayer(1).Hand.Cards.Select(c => c.CardId), "played card stays consumed");
            CollectionAssert.AreEqual(new[] { CardIds.Swap, CardIds.Retreat },
                series.CurrentRound.State.GetPlayer(2).Hand.Cards.Select(c => c.CardId));
        }

        // ------------------------------------------------------------------ determinism and full runs

        private static List<string> RunBotSeries(int seed, int players, out SeriesState state)
        {
            int counter = 0;
            var catalog = CardCatalog.CreateDefault();
            var series = new SeriesEngine(SeriesRules.Default, MatchRulesConfig.Default,
                () => new SeededRandomSource(seed + counter++), catalog, () => new SeededRandomSource(seed * 31 + counter));
            var bot = new SimpleBotDecisionProvider();
            var log = new List<string>();
            var ids = Enumerable.Range(1, players).ToArray();
            Assert.IsTrue(series.StartSeries(ids).Success);

            int safety = 0;
            while (!series.State.IsFinished)
            {
                Assert.Less(++safety, 20000, "series must finish");
                if (series.State.Phase == SeriesPhase.Intermission)
                {
                    Assert.IsTrue(series.StartNextRound().Success);
                    continue;
                }
                var round = series.CurrentRound.State;
                int id = round.CurrentPlayerId;
                Assert.IsTrue(series.RollDice(id).Success);
                var result = series.ChooseDie(id, bot.ChooseDie(series.CurrentRound, id, round.CurrentRoll));
                Assert.IsTrue(result.Success);
                foreach (var award in result.Events.OfType<SeriesPointsAwardedEvent>())
                    log.Add($"{award.RoundNumber}:{award.Reason}:{string.Join(",", award.ScorerIds)}");
            }
            state = series.State;
            return log;
        }

        [Test]
        public void BotSeries_SameSeeds_SameHistory()
        {
            var a = RunBotSeries(2026, 4, out var stateA);
            var b = RunBotSeries(2026, 4, out var stateB);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(stateA.ChampionId, stateB.ChampionId);
        }

        [TestCase(2, 1)]
        [TestCase(2, 77)]
        [TestCase(4, 5)]
        [TestCase(4, 123)]
        public void BotSeries_EndsWithSingleChampionAtPointsToWin(int players, int seed)
        {
            RunBotSeries(seed, players, out var state);

            Assert.IsTrue(state.IsFinished);
            int champion = state.ChampionId;
            Assert.GreaterOrEqual(state.GetScore(champion), SeriesRules.DefaultPointsToWin);
            foreach (int id in state.PlayerIds)
            {
                if (id != champion) Assert.Less(state.GetScore(id), state.GetScore(champion));
            }
            Assert.GreaterOrEqual(state.RoundNumber, SeriesRules.DefaultPointsToWin);
        }

        // ------------------------------------------------------------------ session (application layer)

        [Test]
        public void Session_SeriesMode_PublishesSeriesEvents_AndRestartStartsFreshSeries()
        {
            int seed = 40;
            var session = new MatchSession(MatchRulesConfig.Default,
                new[] { new PlayerSlot(1, true), new PlayerSlot(2, true) },
                () => new SeededRandomSource(seed++), null, null, null, SeriesRules.Default);
            var published = new List<MatchEvent>();
            session.EventPublished += published.Add;

            Assert.IsTrue(session.IsSeriesMode);
            Assert.IsTrue(session.StartMatch().Success);
            Assert.IsInstanceOf<SeriesStartedEvent>(published[0]);

            int safety = 0;
            while (session.SeriesState.Phase != SeriesPhase.Intermission && !session.IsMatchOver)
            {
                Assert.Less(++safety, 1000);
                session.StepBot();
            }
            Assert.IsTrue(published.OfType<SeriesPointsAwardedEvent>().Any());
            Assert.IsFalse(session.IsBotTurn, "no bot step during the intermission");
            Assert.IsNull(session.StepBot());

            Assert.IsTrue(session.StartNextRound().Success);
            Assert.AreEqual(2, session.SeriesState.RoundNumber);

            Assert.IsTrue(session.RestartMatch().Success);
            Assert.AreEqual(1, session.SeriesState.RoundNumber);
            Assert.AreEqual(0, session.SeriesState.TopScore);
        }

        [Test]
        public void Session_SingleRoundMode_Unchanged()
        {
            var session = new MatchSession(MatchRulesConfig.Default,
                new[] { new PlayerSlot(1, true), new PlayerSlot(2, true) }, () => new SeededRandomSource(3));
            Assert.IsFalse(session.IsSeriesMode);
            Assert.IsTrue(session.StartMatch().Success);
            Assert.IsNull(session.SeriesState);
            Assert.AreEqual(CommandError.WrongPhase, session.StartNextRound().Error);
            int safety = 0;
            while (!session.State.IsFinished)
            {
                Assert.Less(++safety, 1000);
                session.StepBot();
            }
            Assert.IsTrue(session.IsMatchOver);
        }
    }
}
