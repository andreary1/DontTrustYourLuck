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
    /// <summary>Same-tile bump (approved 10/11/2026): landing on occupants pushes them back BumpDistance tiles.</summary>
    public class BumpTests
    {
        private ScriptedRandomSource _random;
        private MatchEngine _engine;
        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        private PlayerState P(int id) => _engine.State.GetPlayer(id);

        private void Start(int players = 3, int bump = 3, int radius = 0, bool cards = false, bool persistentShield = false,
            bool circuit = false)
        {
            _random = new ScriptedRandomSource(25) { Unlucky = 20 };
            var rules = new MatchRulesConfig(startingHandSize: 0, unluckyZoneRadius: radius, bumpDistance: bump,
                persistentShield: persistentShield, circuit: circuit);
            _engine = new MatchEngine(rules, _random,
                cards ? new CardSetup(CardCatalog.CreateDefault(), new SeededRandomSource(1)) : null);
            Assert.IsTrue(_engine.StartMatch(Enumerable.Range(1, players).ToArray()).Success);
        }

        /// <summary>Current player rolls (a, b) and picks die 0.</summary>
        private void Move(int a, int b = 1)
        {
            int id = _engine.State.CurrentPlayerId;
            _random.Enqueue(a, b);
            Assert.IsTrue(_engine.RollDice(id).Success);
            var result = _engine.ChooseDie(id, 0);
            Assert.IsTrue(result.Success);
            _events.Clear();
            _events.AddRange(result.Events);
        }

        private List<PlayerBumpedEvent> Bumps => _events.OfType<PlayerBumpedEvent>().ToList();

        [Test]
        public void LandingOnAnOpponent_PushesItBack()
        {
            Start();
            P(2).Position = 10;
            P(1).Position = 6;
            Move(4);
            Assert.AreEqual(10, P(1).Position, "the mover stays");
            Assert.AreEqual(7, P(2).Position);
            var bump = Bumps.Single();
            Assert.AreEqual((1, 2, 10, 7), (bump.BumperId, bump.PlayerId, bump.From, bump.To));
        }

        [Test]
        public void EveryOccupantIsBumped_InIdOrder()
        {
            Start(players: 4);
            P(2).Position = 10; P(3).Position = 10;
            P(1).Position = 8;
            Move(2);
            CollectionAssert.AreEqual(new[] { 2, 3 }, Bumps.Select(b => b.PlayerId));
            Assert.AreEqual(7, P(2).Position);
            Assert.AreEqual(7, P(3).Position);
            Assert.AreEqual(10, P(1).Position);
        }

        [Test]
        public void BumpIsClampedAtStart()
        {
            Start();
            P(2).Position = 2;
            Move(2);
            Assert.AreEqual(0, P(2).Position);
            Assert.AreEqual(2, Bumps.Single().Steps);
        }

        [Test]
        public void PassingThroughAnOpponent_DoesNotBump()
        {
            Start();
            P(2).Position = 3;
            Move(5);
            Assert.IsEmpty(Bumps);
            Assert.AreEqual(3, P(2).Position);
        }

        [Test]
        public void LuckyTile_WinsWithoutBumping()
        {
            Start();
            P(2).Position = 25; // standing on lucky (e.g. after a Swap)
            P(1).Position = 22;
            Move(3);
            Assert.AreEqual(MatchEndReason.ExactLucky, _engine.State.EndReason);
            Assert.IsEmpty(Bumps);
            Assert.AreEqual(25, P(2).Position);
        }

        [Test]
        public void EliminatedMover_DoesNotBump()
        {
            Start();
            P(2).Position = 20; // the unlucky tile can already hold a player (no retroactive elimination)
            P(1).Position = 17;
            Move(3);
            Assert.AreEqual(PlayerStatus.Eliminated, P(1).Status);
            Assert.IsEmpty(Bumps);
            Assert.AreEqual(20, P(2).Position);
        }

        [Test]
        public void ShieldedMoverOnUnluckyTile_StillBumps()
        {
            Start(cards: true);
            var grant = _engine.GrantCard(1, CardIds.Shield);
            Assert.IsTrue(_engine.PlayCard(1, ((CardDrawnEvent)grant.Events[0]).Card.InstanceId).Success);
            P(2).Position = 20;
            P(1).Position = 17;
            Move(3);
            Assert.IsTrue(P(1).IsActive);
            Assert.AreEqual(17, P(2).Position);
        }

        [Test]
        public void BumpedIntoTheUnluckyZone_IsEliminated()
        {
            Start(radius: 1);
            P(2).Position = 24; // 24 - 3 = 21: inside the zone 19-21
            P(1).Position = 18;
            Move(6);
            Assert.AreEqual(21, P(2).Position);
            Assert.AreEqual(PlayerStatus.Eliminated, P(2).Status);
            var eliminated = _events.OfType<PlayerEliminatedEvent>().Single();
            Assert.AreEqual((2, 21), (eliminated.PlayerId, eliminated.Tile));
            Assert.Less(_events.FindIndex(e => e is PlayerBumpedEvent), _events.IndexOf(eliminated), "push first, then the tile");
            Assert.IsFalse(_engine.State.IsFinished, "two players are still in");
        }

        [Test]
        public void BumpedOntoTheLuckyTile_Wins()
        {
            Start(circuit: true); // only on the circuit can a rival stand past the lucky tile
            P(2).Position = 28; // 28 - 3 = 25: the lucky number
            P(1).Position = 24;
            Move(4);
            Assert.AreEqual(25, P(2).Position);
            Assert.IsTrue(_engine.State.IsFinished);
            CollectionAssert.AreEqual(new[] { 2 }, _engine.State.WinnerIds, "the bumped player wins, not the mover");
            Assert.AreEqual(MatchEndReason.ExactLucky, _events.OfType<MatchFinishedEvent>().Single().Reason);
        }

        [Test]
        public void BumpedPlayerWithShield_IsSaved_AndLosesTheShield()
        {
            Start(radius: 1, cards: true, persistentShield: true);
            var grant = _engine.GrantCard(1, CardIds.Shield);
            Assert.IsTrue(_engine.PlayCard(1, ((CardDrawnEvent)grant.Events[0]).Card.InstanceId).Success);
            Move(1); // P1 keeps its persistent shield into P2's turn
            Assert.IsTrue(P(1).HasShield);
            P(1).Position = 24;
            P(2).Position = 18;
            Move(6); // P2 lands on 24 and pushes P1 to 21 (unlucky zone)
            Assert.AreEqual(21, P(1).Position);
            Assert.IsTrue(P(1).IsActive);
            Assert.IsFalse(P(1).HasShield);
            var shield = _events.OfType<ShieldConsumedEvent>().Single();
            Assert.AreEqual(1, shield.PlayerId);
        }

        [Test]
        public void BumpLeavingOneSurvivor_EndsTheMatch()
        {
            Start(players: 2, radius: 1);
            P(2).Position = 24;
            P(1).Position = 18;
            Move(6);
            Assert.IsTrue(_engine.State.IsFinished);
            CollectionAssert.AreEqual(new[] { 1 }, _engine.State.WinnerIds);
            Assert.AreEqual(MatchEndReason.LastSurvivor, _events.OfType<MatchFinishedEvent>().Single().Reason);
        }

        [Test]
        public void PreviewBumps_MatchesTheResolution_WithoutChangingState()
        {
            Start(radius: 1, circuit: true);
            P(2).Position = 24;
            P(3).Position = 28;
            var intoZone = _engine.PreviewBumps(1, 24);
            Assert.AreEqual((1, 1, false), (intoZone.Victims, intoZone.Eliminated, intoZone.GivesWin));
            var gift = _engine.PreviewBumps(1, 28);
            Assert.IsTrue(gift.GivesWin);
            Assert.AreEqual(0, _engine.PreviewBumps(1, 10).Victims);
            Assert.AreEqual(24, P(2).Position);
            Assert.IsTrue(P(2).IsActive);
        }

        [Test]
        public void Bot_NeverBumpsARivalOntoTheLuckyTile()
        {
            Start(circuit: true);
            P(2).Position = 28;
            P(1).Position = 24;
            _random.Enqueue(4, 3); // 28 would push P2 onto 25 (a gift); 27 is safe
            Assert.IsTrue(_engine.RollDice(1).Success);
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(_engine, 1, _engine.State.CurrentRoll));
        }

        [Test]
        public void Bot_PrefersABumpThatKnocksARivalOut()
        {
            Start(radius: 1);
            P(2).Position = 24; // pushed to 21: out
            P(1).Position = 18;
            _random.Enqueue(5, 6); // 23 is closer to 25 by one tile, 24 eliminates P2
            Assert.IsTrue(_engine.RollDice(1).Success);
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(_engine, 1, _engine.State.CurrentRoll));
        }

        [Test]
        public void BumpOff_NothingHappens()
        {
            Start(bump: 0);
            P(2).Position = 10;
            P(1).Position = 6;
            Move(4);
            Assert.IsEmpty(Bumps);
            Assert.AreEqual(10, P(2).Position);
            Assert.AreEqual(0, _engine.CountBumpVictims(1, 10));
        }

        [Test]
        public void CardMoves_NeverBump()
        {
            Start(cards: true);
            P(1).Position = 13; P(2).Position = 10;
            var grant = _engine.GrantCard(1, CardIds.Retreat);
            var result = _engine.PlayCard(1, ((CardDrawnEvent)grant.Events[0]).Card.InstanceId);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, P(1).Position);
            Assert.AreEqual(10, P(2).Position);
            Assert.IsFalse(result.Events.OfType<PlayerBumpedEvent>().Any());
        }

        [Test]
        public void Bot_PrefersABumpWhenDistancesAreClose()
        {
            Start();
            P(2).Position = 10;
            P(1).Position = 6;
            _random.Enqueue(4, 5); // 10 bumps P2; 11 is one tile closer but bumps nobody
            Assert.IsTrue(_engine.RollDice(1).Success);
            Assert.AreEqual(0, new SimpleBotDecisionProvider().ChooseDie(_engine, 1, _engine.State.CurrentRoll));
        }

        [Test]
        public void Bot_StillPrefersAMuchBetterMove()
        {
            Start();
            P(2).Position = 7;
            P(1).Position = 6;
            _random.Enqueue(1, 6); // bump at 7 vs 12 (five tiles closer)
            Assert.IsTrue(_engine.RollDice(1).Success);
            Assert.AreEqual(1, new SimpleBotDecisionProvider().ChooseDie(_engine, 1, _engine.State.CurrentRoll));
        }
    }
}
