using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Match
{
    public class MatchSessionAndBotTests
    {
        private static PlayerSlot[] Slots(params bool[] bots) =>
            bots.Select((isBot, i) => new PlayerSlot(i + 1, isBot)).ToArray();

        // ---------- Bot decisions (19-21) ----------

        /// <summary>
        /// Two-player engine where P1 stands on <paramref name="p1Position"/>, it is P1's turn and the
        /// current round's unlucky number is <paramref name="unlucky"/>.
        /// </summary>
        private static MatchEngine EngineAt(int lucky, int unlucky, int p1Position)
        {
            var rng = new ScriptedRandomSource(lucky) { Unlucky = lucky - 1 };
            var engine = new MatchEngine(MatchRulesConfig.Default, rng);
            engine.StartMatch(new[] { 1, 2 });
            while (engine.State.GetPlayer(1).Position < p1Position)
            {
                int step = System.Math.Min(6, p1Position - engine.State.GetPlayer(1).Position);
                rng.Enqueue(step, 1);
                engine.RollDice(1); engine.ChooseDie(1, 0);
                // P2 closes the round; the next round's unlucky becomes the requested one once P1 arrived.
                if (engine.State.GetPlayer(1).Position == p1Position) rng.Unlucky = unlucky;
                rng.Enqueue(1, 1);
                engine.RollDice(2); engine.ChooseDie(2, 0);
            }
            Assert.AreEqual(p1Position, engine.State.GetPlayer(1).Position);
            Assert.AreEqual(1, engine.State.CurrentPlayerId);
            Assert.AreEqual(unlucky, engine.State.UnluckyNumber);
            return engine;
        }

        [Test]
        public void Bot_PrefersImmediateWin()
        {
            var engine = EngineAt(lucky: 20, unlucky: 19, p1Position: 18);
            var roll = new DiceRoll(new[] { 5, 2 }); // 5 rebounds to 17, 2 lands on 20
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(engine, 1, roll));
        }

        [Test]
        public void Bot_AvoidsElimination()
        {
            var engine = EngineAt(lucky: 25, unlucky: 13, p1Position: 8);
            // 5 -> 13 (eliminated, though closer to lucky); 1 -> 9 (safe).
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(engine, 1, new DiceRoll(new[] { 5, 1 })));
        }

        [Test]
        public void Bot_PicksDestinationClosestToLucky()
        {
            var engine = EngineAt(lucky: 20, unlucky: 1, p1Position: 15);
            // 6 -> 21? rebounds: 16,17,18,19,20,19 = 19 ; 3 -> 18. 19 is closer.
            Assert.AreEqual(0, new SimpleBotDecisionProvider().ChooseDie(engine, 1, new DiceRoll(new[] { 6, 3 })));
            // 2 -> 17 ; 4 -> 19
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(engine, 1, new DiceRoll(new[] { 2, 4 })));
        }

        [Test]
        public void Bot_TieBreaksOnFirstDie()
        {
            var engine = EngineAt(lucky: 20, unlucky: 1, p1Position: 15);
            // 4 -> 19 and 6 -> 19 (rebound): equivalent, prefer die A.
            Assert.AreEqual(0, new SimpleBotDecisionProvider().ChooseDie(engine, 1, new DiceRoll(new[] { 4, 6 })));
            Assert.AreEqual(0, new SimpleBotDecisionProvider().ChooseDie(engine, 1, new DiceRoll(new[] { 3, 3 })));
        }

        // ---------- Session / same command API (22) ----------

        [Test]
        public void Bot_UsesSameCommandApi_AndCannotActOnHumanTurn()
        {
            var session = new MatchSession(MatchRulesConfig.Default, Slots(false, true), () => new SeededRandomSource(7));
            var events = new List<MatchEvent>();
            session.EventPublished += events.Add;
            session.StartMatch();

            Assert.IsFalse(session.IsBotTurn);
            Assert.IsNull(session.StepBot(), "bot never acts on a human turn");
            Assert.AreEqual(-1, session.GetBotChoice());

            Assert.IsTrue(session.RollDice(1).Success);
            Assert.IsTrue(session.ChooseDie(1, 0).Success);
            if (session.State.IsFinished) Assert.Inconclusive("seed ended the match on the first move");

            Assert.IsTrue(session.IsBotTurn);
            var roll = session.StepBot();
            Assert.IsTrue(roll.Success);
            Assert.IsInstanceOf<DiceRolledEvent>(roll.Events.Single());
            var choose = session.StepBot();
            Assert.IsTrue(choose.Success);
            Assert.IsInstanceOf<DieSelectedEvent>(choose.Events[0], "bot choice goes through ChooseDie");
            Assert.AreEqual(2, events.OfType<DiceRolledEvent>().Count());
        }

        [Test]
        public void BotsOnlyMatch_FinishesWithinRoundLimit()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                int s = seed;
                var session = new MatchSession(MatchRulesConfig.Default, Slots(true, true, true, true), () => new SeededRandomSource(s));
                session.StartMatch();
                int guard = 0;
                while (!session.State.IsFinished)
                {
                    var result = session.StepBot();
                    Assert.IsNotNull(result, $"seed {s}: bot had nothing to do");
                    Assert.IsTrue(result.Success, $"seed {s}: {result.Error}");
                    Assert.Less(++guard, 1000, $"seed {s}: no termination");
                }

                var state = session.State;
                Assert.LessOrEqual(state.RoundNumber, MatchRulesConfig.Default.MaxRounds);
                Assert.IsNotEmpty(state.WinnerIds);
                Assert.IsNull(session.StepBot(), "no commands after the match ended");
            }
        }

        [Test]
        public void SameSeed_ProducesSameEventSequence()
        {
            string Run()
            {
                var session = new MatchSession(MatchRulesConfig.Default, Slots(true, true, true), () => new SeededRandomSource(42));
                var log = new List<string>();
                session.EventPublished += e => log.Add(Describe(e));
                session.StartMatch();
                while (!session.State.IsFinished) session.StepBot();
                return string.Join("|", log);
            }

            Assert.AreEqual(Run(), Run());
        }

        // 18. Full restart
        [Test]
        public void Restart_CreatesFreshStateAndDoesNotDuplicateEvents()
        {
            int seed = 0;
            var session = new MatchSession(MatchRulesConfig.Default, Slots(false, true, true), () => new SeededRandomSource(++seed));
            int matchStarted = 0;
            session.EventPublished += e => { if (e is MatchStartedEvent) matchStarted++; };
            session.StartMatch();

            // Play until finished, humans included.
            int guard = 0;
            while (!session.State.IsFinished && guard++ < 1000)
            {
                var st = session.State;
                if (session.IsBotTurn) session.StepBot();
                else if (st.Phase == MatchPhase.AwaitingRoll) session.RollDice(st.CurrentPlayerId);
                else session.ChooseDie(st.CurrentPlayerId, 0);
            }
            Assert.IsTrue(session.State.IsFinished);
            var oldState = session.State;

            var result = session.RestartMatch();
            Assert.IsTrue(result.Success);
            var state = session.State;
            Assert.AreNotSame(oldState, state);
            Assert.AreEqual(MatchPhase.AwaitingRoll, state.Phase);
            Assert.AreEqual(1, state.RoundNumber);
            Assert.AreEqual(1, state.CurrentPlayerId);
            Assert.IsNull(state.CurrentRoll);
            Assert.IsEmpty(state.WinnerIds);
            Assert.AreEqual(MatchEndReason.None, state.EndReason);
            Assert.IsTrue(state.Players.All(p => p.Position == 0 && p.Status == PlayerStatus.Active));
            Assert.AreEqual(3, state.Players.Count);
            Assert.AreEqual(2, matchStarted, "one MatchStarted per start, no duplicated listeners");
            Assert.AreEqual(2, session.MatchNumber);
            Assert.AreEqual(2, seed, "restart draws from a fresh random source");
        }

        [Test]
        public void Restart_MidMatch_DropsPendingRoll()
        {
            var session = new MatchSession(MatchRulesConfig.Default, Slots(false, false), () => new SeededRandomSource(3));
            session.StartMatch();
            session.RollDice(1);
            Assert.IsNotNull(session.State.CurrentRoll);

            session.RestartMatch();
            Assert.IsNull(session.State.CurrentRoll);
            Assert.AreEqual(MatchPhase.AwaitingRoll, session.State.Phase);
        }

        [Test]
        public void RejectedCommands_RaiseCommandRejected()
        {
            var session = new MatchSession(MatchRulesConfig.Default, Slots(false, false), () => new SeededRandomSource(3));
            var errors = new List<CommandError>();
            session.CommandRejected += errors.Add;
            session.RollDice(1); // before start
            session.StartMatch();
            session.ChooseDie(1, 0); // before roll
            CollectionAssert.AreEqual(new[] { CommandError.WrongPhase, CommandError.WrongPhase }, errors);
        }

        private static string Describe(MatchEvent e)
        {
            switch (e)
            {
                case MatchStartedEvent m: return $"start{m.LuckyNumber}";
                case UnluckyNumberDrawnEvent u: return $"unlucky{u.UnluckyNumber}";
                case DiceRolledEvent d: return $"roll{d.PlayerId}:{d.Roll}";
                case DieSelectedEvent s: return $"pick{s.PlayerId}:{s.DieIndex}";
                case PlayerMovedEvent m: return $"move{m.PlayerId}:{m.Movement.FinalPosition}";
                case MatchFinishedEvent f: return $"end{f.Reason}:{string.Join(",", f.WinnerIds)}";
                default: return e.GetType().Name;
            }
        }
    }
}
