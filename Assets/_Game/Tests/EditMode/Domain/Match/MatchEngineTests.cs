using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Match;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Domain.Match
{
    public class MatchEngineTests
    {
        private ScriptedRandomSource _rng;
        private MatchEngine _engine;

        private MatchState State => _engine.State;

        /// <summary>Starts a match with the given lucky number and first-round unlucky number.</summary>
        private CommandResult Start(int lucky, int unlucky, params int[] ids)
        {
            _rng = new ScriptedRandomSource(lucky) { Unlucky = unlucky };
            _engine = new MatchEngine(MatchRulesConfig.Default, _rng);
            return _engine.StartMatch(ids.Length > 0 ? ids : new[] { 1, 2 });
        }

        /// <summary>Plays the current player's turn: rolls (a, b) and chooses <paramref name="index"/>.</summary>
        private CommandResult Turn(int a, int b = 1, int index = 0)
        {
            int id = State.CurrentPlayerId;
            _rng.Enqueue(a, b);
            Assert.IsTrue(_engine.RollDice(id).Success, "roll");
            var result = _engine.ChooseDie(id, index);
            Assert.IsTrue(result.Success, "choose");
            return result;
        }

        private static List<T> Of<T>(CommandResult r) where T : MatchEvent => r.Events.OfType<T>().ToList();

        // 1. Valid initialization
        [Test]
        public void StartMatch_InitializesPlayersRoundAndTurn()
        {
            var result = Start(20, 5, 3, 1, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(MatchPhase.AwaitingRoll, State.Phase);
            Assert.AreEqual(1, State.RoundNumber);
            Assert.AreEqual(new[] { 1, 2, 3 }, State.Players.Select(p => p.Id).ToArray(), "order by id");
            Assert.IsTrue(State.Players.All(p => p.Position == 0 && p.Status == PlayerStatus.Active));
            Assert.AreEqual(1, State.CurrentPlayerId);
            Assert.IsNull(State.CurrentRoll);
            CollectionAssert.AreEqual(
                new[] { typeof(MatchStartedEvent), typeof(RoundStartedEvent), typeof(UnluckyNumberDrawnEvent), typeof(TurnChangedEvent) },
                result.Events.Select(e => e.GetType()).ToArray());
        }

        [TestCase(new[] { 1 })]
        [TestCase(new[] { 1, 2, 3, 4, 5 })]
        public void StartMatch_InvalidPlayerCount_Rejected(int[] ids)
        {
            _rng = new ScriptedRandomSource(20);
            _engine = new MatchEngine(MatchRulesConfig.Default, _rng);
            Assert.AreEqual(CommandError.InvalidPlayerCount, _engine.StartMatch(ids).Error);
            Assert.AreEqual(MatchPhase.NotStarted, State.Phase);
            Assert.AreEqual(0, _rng.DrawCount);
        }

        [Test]
        public void StartMatch_DuplicateIds_Rejected()
        {
            _rng = new ScriptedRandomSource(20);
            _engine = new MatchEngine(MatchRulesConfig.Default, _rng);
            Assert.AreEqual(CommandError.DuplicatePlayerId, _engine.StartMatch(new[] { 1, 1 }).Error);
            Assert.AreEqual(0, State.Players.Count);
        }

        [Test]
        public void StartMatch_Twice_Rejected()
        {
            Start(20, 5);
            Assert.AreEqual(CommandError.WrongPhase, _engine.StartMatch(new[] { 1, 2 }).Error);
        }

        // 2. Lucky number drawn once at start
        [Test]
        public void LuckyNumber_DrawnOnceInConfiguredRange()
        {
            var result = Start(23, 5);
            Assert.AreEqual(23, State.LuckyNumber);
            Assert.AreEqual((15, 31), _rng.Requests[0]);
            Assert.AreEqual(23, Of<MatchStartedEvent>(result)[0].LuckyNumber);

            Turn(1); Turn(1); // completes round 1 -> round 2
            Assert.AreEqual(23, State.LuckyNumber);
            Assert.AreEqual(1, _rng.Requests.Count(r => r == (15, 31)));
        }

        // 3. Unlucky number drawn each round
        [Test]
        public void UnluckyNumber_DrawnAtEveryRoundStart()
        {
            Start(20, 7);
            Assert.AreEqual(7, State.UnluckyNumber);
            Assert.AreEqual((1, 20), _rng.Requests[1]);

            Turn(1); // P1 -> 1
            _rng.Unlucky = 11;
            var endOfRound = Turn(2); // P2 -> 2, round ends
            Assert.AreEqual(2, State.RoundNumber);
            Assert.AreEqual(11, State.UnluckyNumber);
            var drawn = Of<UnluckyNumberDrawnEvent>(endOfRound);
            Assert.AreEqual(1, drawn.Count);
            Assert.AreEqual(2, drawn[0].RoundNumber);
            Assert.AreEqual(11, drawn[0].UnluckyNumber);
        }

        // 4. Turn alternation
        [Test]
        public void Turns_AlternateInIdOrder_AndWrapToNewRound()
        {
            Start(20, 19, 3, 1, 2);
            var order = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                order.Add(State.CurrentPlayerId);
                Turn(1);
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 1, 2, 3 }, order);
            Assert.AreEqual(3, State.RoundNumber);
        }

        // 5. Commands blocked in invalid phases / wrong player
        [Test]
        public void Commands_InWrongPhaseOrByWrongPlayer_AreRejectedWithoutSideEffects()
        {
            Start(20, 5);
            int draws = _rng.DrawCount;

            Assert.AreEqual(CommandError.WrongPhase, _engine.ChooseDie(1, 0).Error, "choose before roll");
            Assert.AreEqual(CommandError.NotYourTurn, _engine.RollDice(2).Error, "not P2's turn");
            Assert.AreEqual(CommandError.UnknownPlayer, _engine.RollDice(99).Error);
            Assert.AreEqual(draws, _rng.DrawCount, "rejections consume no randomness");
            Assert.AreEqual(MatchPhase.AwaitingRoll, State.Phase);
            Assert.AreEqual(1, State.CurrentPlayerId);

            _rng.Enqueue(3, 4);
            _engine.RollDice(1);
            Assert.AreEqual(CommandError.NotYourTurn, _engine.ChooseDie(2, 0).Error);
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, State.Phase);
        }

        [Test]
        public void Commands_BeforeStart_AreRejected()
        {
            _engine = new MatchEngine(MatchRulesConfig.Default, new ScriptedRandomSource());
            Assert.AreEqual(CommandError.WrongPhase, _engine.RollDice(1).Error);
            Assert.AreEqual(CommandError.WrongPhase, _engine.ChooseDie(1, 0).Error);
        }

        // 6. Two dice rolled and stored
        [Test]
        public void RollDice_ProducesTwoStoredValues()
        {
            Start(20, 5);
            _rng.Enqueue(2, 5);
            var result = _engine.RollDice(1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, State.Phase);
            CollectionAssert.AreEqual(new[] { 2, 5 }, State.CurrentRoll.Values);
            var rolled = Of<DiceRolledEvent>(result).Single();
            Assert.AreEqual(1, rolled.PlayerId);
            CollectionAssert.AreEqual(new[] { 2, 5 }, rolled.Roll.Values);
            Assert.AreEqual(new[] { (1, 7), (1, 7) }, _rng.Requests.Skip(2).ToArray());
        }

        // 7. Valid choice moves by the chosen die only (no sum)
        [Test]
        public void ChooseDie_MovesByChosenValue_NotSum()
        {
            Start(20, 19);
            var result = Turn(2, 5, index: 1);

            Assert.AreEqual(5, State.GetPlayer(1).Position);
            var selected = Of<DieSelectedEvent>(result).Single();
            Assert.AreEqual(1, selected.DieIndex);
            Assert.AreEqual(5, selected.Value);
            var moved = Of<PlayerMovedEvent>(result).Single();
            Assert.AreEqual(0, moved.Movement.StartPosition);
            Assert.AreEqual(5, moved.Movement.FinalPosition);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, moved.Movement.Path);
            Assert.AreEqual(2, State.CurrentPlayerId, "turn advanced after full resolution");
        }

        // 8. Invalid die index
        [TestCase(-1)]
        [TestCase(2)]
        public void ChooseDie_InvalidIndex_Rejected(int index)
        {
            Start(20, 5);
            _rng.Enqueue(2, 5);
            _engine.RollDice(1);

            Assert.AreEqual(CommandError.InvalidDieIndex, _engine.ChooseDie(1, index).Error);
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, State.Phase);
            Assert.AreEqual(0, State.GetPlayer(1).Position);
            CollectionAssert.AreEqual(new[] { 2, 5 }, State.CurrentRoll.Values, "roll kept until a valid choice");
        }

        // 9. Multiple rolls / choices blocked
        [Test]
        public void RollTwice_AndChooseTwice_AreRejected()
        {
            Start(20, 19, 1, 2, 3);
            _rng.Enqueue(2, 5);
            _engine.RollDice(1);
            int draws = _rng.DrawCount;
            Assert.AreEqual(CommandError.WrongPhase, _engine.RollDice(1).Error);
            Assert.AreEqual(draws, _rng.DrawCount);

            Assert.IsTrue(_engine.ChooseDie(1, 0).Success);
            Assert.AreEqual(CommandError.WrongPhase, _engine.ChooseDie(1, 1).Error, "roll already consumed");
            Assert.AreEqual(2, State.GetPlayer(1).Position);
        }

        // 10. Movement with rebound
        [Test]
        public void Movement_ReboundsAtLuckyNumber()
        {
            Start(20, 10);
            Turn(6); Turn(1); Turn(6); Turn(1); Turn(6); Turn(1); // P1: 18, P2: 3
            Assert.AreEqual(18, State.GetPlayer(1).Position);

            var result = Turn(4); // 19, 20, 19, 18
            var moved = Of<PlayerMovedEvent>(result).Single();
            CollectionAssert.AreEqual(new[] { 19, 20, 19, 18 }, moved.Movement.Path);
            Assert.AreEqual(18, State.GetPlayer(1).Position);
        }

        // 11. Win by exact lucky landing
        [Test]
        public void LandingExactlyOnLucky_Wins_AndEndsImmediately()
        {
            Start(20, 10, 1, 2, 3);
            Turn(6); Turn(1); Turn(1);
            Turn(6); Turn(1); Turn(1);
            Turn(4); Turn(1); Turn(1); // P1 at 16
            var result = Turn(4); // 16 + 4 = 20

            Assert.AreEqual(MatchPhase.Finished, State.Phase);
            Assert.AreEqual(MatchEndReason.ExactLucky, State.EndReason);
            CollectionAssert.AreEqual(new[] { 1 }, State.WinnerIds);
            Assert.AreEqual(PlayerStatus.Winner, State.GetPlayer(1).Status);
            Assert.AreEqual(1, Of<PlayerWonEvent>(result).Single().PlayerId);
            Assert.AreEqual(MatchEndReason.ExactLucky, Of<MatchFinishedEvent>(result).Single().Reason);
            Assert.IsEmpty(Of<TurnChangedEvent>(result));
            Assert.IsEmpty(Of<RoundStartedEvent>(result), "no new unlucky draw after the match ended");
        }

        // 12. Passing through lucky does not win
        [Test]
        public void PassingThroughLucky_DoesNotWin()
        {
            Start(20, 10);
            Turn(6); Turn(1); Turn(6); Turn(1); Turn(6); Turn(1); // P1 at 18
            Turn(5); // 19, 20, 19, 18, 17
            Assert.AreEqual(17, State.GetPlayer(1).Position);
            Assert.AreEqual(PlayerStatus.Active, State.GetPlayer(1).Status);
            Assert.IsFalse(State.IsFinished);
        }

        // 13. Elimination by landing on unlucky
        [Test]
        public void LandingOnUnlucky_Eliminates()
        {
            Start(25, 13, 1, 2, 3);
            Turn(5); Turn(1); Turn(1);
            Turn(4); Turn(1); Turn(1); // P1 at 9 (unlucky stays 13)
            var result = Turn(4); // 13

            var p1 = State.GetPlayer(1);
            Assert.AreEqual(13, p1.Position, "eliminated player stays registered with its position");
            Assert.AreEqual(PlayerStatus.Eliminated, p1.Status);
            var eliminated = Of<PlayerEliminatedEvent>(result).Single();
            Assert.AreEqual(1, eliminated.PlayerId);
            Assert.AreEqual(13, eliminated.Tile);
            Assert.IsFalse(State.IsFinished);
            Assert.AreEqual(2, State.CurrentPlayerId);
        }

        // 14. Passing through unlucky does not eliminate; no retroactive elimination
        [Test]
        public void PassingThroughUnlucky_DoesNotEliminate()
        {
            Start(25, 4);
            Turn(6); // 1..6 passes 4
            Assert.AreEqual(PlayerStatus.Active, State.GetPlayer(1).Status);
        }

        [Test]
        public void NewUnluckyOnOccupiedTile_IsNotRetroactive()
        {
            Start(25, 20);
            Turn(3); // P1 -> 3
            _rng.Unlucky = 3; // next round's unlucky is P1's tile
            Turn(1); // P2 -> 1, round 2 starts
            Assert.AreEqual(3, State.UnluckyNumber);
            Assert.AreEqual(PlayerStatus.Active, State.GetPlayer(1).Status);
            Assert.AreEqual(1, State.CurrentPlayerId);
        }

        // 15. Eliminated players skip turns
        [Test]
        public void EliminatedPlayers_ReceiveNoTurns()
        {
            Start(25, 2, 1, 2, 3);
            Turn(2); // P1 eliminated on 2
            _rng.Unlucky = 20; // keep later rounds safe
            var order = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                order.Add(State.CurrentPlayerId);
                Turn(1);
            }
            CollectionAssert.AreEqual(new[] { 2, 3, 2, 3 }, order);
        }

        [Test]
        public void EliminationOfLastPlayerInRound_StartsNextRound()
        {
            Start(25, 2, 1, 2, 3);
            Turn(1); Turn(1); // P1 -> 1, P2 -> 1
            var result = Turn(2); // P3 -> 2 eliminated, last in round
            Assert.AreEqual(2, State.RoundNumber);
            Assert.AreEqual(1, State.CurrentPlayerId);
            Assert.AreEqual(1, Of<RoundStartedEvent>(result).Count);
        }

        // 16. Last survivor wins
        [Test]
        public void LastSurvivor_WinsAutomatically()
        {
            Start(25, 3);
            var result = Turn(3); // P1 eliminated, only P2 remains

            Assert.AreEqual(MatchPhase.Finished, State.Phase);
            Assert.AreEqual(MatchEndReason.LastSurvivor, State.EndReason);
            CollectionAssert.AreEqual(new[] { 2 }, State.WinnerIds);
            Assert.AreEqual(PlayerStatus.Eliminated, State.GetPlayer(1).Status);
            Assert.AreEqual(PlayerStatus.Winner, State.GetPlayer(2).Status);
            var types = result.Events.Select(e => e.GetType()).ToList();
            Assert.Less(types.IndexOf(typeof(PlayerEliminatedEvent)), types.IndexOf(typeof(PlayerWonEvent)));
        }

        // 17. Commands blocked after the match ends
        [Test]
        public void Commands_AfterFinish_AreRejected()
        {
            Start(25, 3);
            Turn(3); // finished
            int draws = _rng.DrawCount;

            Assert.AreEqual(CommandError.MatchFinished, _engine.RollDice(2).Error);
            Assert.AreEqual(CommandError.MatchFinished, _engine.ChooseDie(2, 0).Error);
            Assert.AreEqual(CommandError.MatchFinished, _engine.StartMatch(new[] { 1, 2 }).Error);
            Assert.AreEqual(draws, _rng.DrawCount);
            Assert.AreEqual(-1, State.CurrentPlayerId);
        }

        // Round limit (D12)
        [Test]
        public void RoundLimit_HighestActivePlayersShareVictory()
        {
            var rules = new MatchRulesConfig(maxRounds: 2);
            _rng = new ScriptedRandomSource(25) { Unlucky = 24 };
            _engine = new MatchEngine(rules, _rng);
            _engine.StartMatch(new[] { 1, 2, 3 });

            Turn(3); Turn(3); Turn(1); // round 1: 3, 3, 1
            Turn(2); Turn(2); var last = Turn(5); // round 2: 5, 5, 6

            Assert.AreEqual(MatchEndReason.RoundLimit, State.EndReason);
            CollectionAssert.AreEqual(new[] { 3 }, State.WinnerIds);
            Assert.AreEqual(2, Of<MatchFinishedEvent>(last).Single().RoundNumber);

            _rng = new ScriptedRandomSource(25) { Unlucky = 24 };
            _engine = new MatchEngine(rules, _rng);
            _engine.StartMatch(new[] { 1, 2, 3 });
            Turn(3); Turn(3); Turn(1);
            Turn(2); Turn(2); Turn(3); // 5, 5, 4
            CollectionAssert.AreEqual(new[] { 1, 2 }, State.WinnerIds, "tie on the same tile shares the win");
            Assert.AreEqual(PlayerStatus.Winner, State.GetPlayer(1).Status);
            Assert.AreEqual(PlayerStatus.Winner, State.GetPlayer(2).Status);
            Assert.AreEqual(PlayerStatus.Active, State.GetPlayer(3).Status);
        }

        [Test]
        public void RoundLimit_IgnoresEliminatedPlayersEvenIfFurtherAhead()
        {
            var rules = new MatchRulesConfig(maxRounds: 2);
            _rng = new ScriptedRandomSource(25) { Unlucky = 6 };
            _engine = new MatchEngine(rules, _rng);
            _engine.StartMatch(new[] { 1, 2, 3 });

            Turn(6); // P1 eliminated on 6 (furthest ahead)
            _rng.Unlucky = 24;
            Turn(2); Turn(1); // round 1: P2 2, P3 1
            Turn(2); Turn(3); // round 2: P2 4, P3 4 -> limit

            Assert.AreEqual(MatchEndReason.RoundLimit, State.EndReason);
            CollectionAssert.AreEqual(new[] { 2, 3 }, State.WinnerIds);
            Assert.AreEqual(PlayerStatus.Eliminated, State.GetPlayer(1).Status);
        }

        [Test]
        public void LastSurvivor_TakesPrecedenceOverRoundLimit_OnFinalRound()
        {
            var rules = new MatchRulesConfig(maxRounds: 1);
            _rng = new ScriptedRandomSource(25) { Unlucky = 3 };
            _engine = new MatchEngine(rules, _rng);
            _engine.StartMatch(new[] { 1, 2 });

            Turn(1);            // P1 -> 1
            var last = Turn(3); // P2 eliminated on the last turn of the last round

            Assert.AreEqual(MatchEndReason.LastSurvivor, State.EndReason);
            CollectionAssert.AreEqual(new[] { 1 }, State.WinnerIds);
            Assert.AreEqual(1, last.Events.OfType<MatchFinishedEvent>().Count());
        }

        [Test]
        public void Turns_SkipCorrectly_WithNonContiguousIds()
        {
            Start(25, 24, 10, 3, 7);
            var order = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                order.Add(State.CurrentPlayerId);
                Turn(1);
            }
            CollectionAssert.AreEqual(new[] { 3, 7, 10, 3 }, order);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void StartMatch_NonPositiveId_Rejected(int badId)
        {
            _rng = new ScriptedRandomSource(20);
            _engine = new MatchEngine(MatchRulesConfig.Default, _rng);
            Assert.AreEqual(CommandError.InvalidPlayerId, _engine.StartMatch(new[] { badId, 2 }).Error);
            Assert.AreEqual(MatchPhase.NotStarted, State.Phase);
            Assert.AreEqual(0, State.Players.Count);
            Assert.AreEqual(0, _rng.DrawCount);
        }

        [Test]
        public void PreviewMove_DoesNotChangeStateOrConsumeRandomness()
        {
            Start(20, 5);
            int draws = _rng.DrawCount;
            var preview = _engine.PreviewMove(1, 4);
            Assert.AreEqual(4, preview.FinalPosition);
            Assert.AreEqual(0, State.GetPlayer(1).Position);
            Assert.AreEqual(draws, _rng.DrawCount);
        }
    }
}
