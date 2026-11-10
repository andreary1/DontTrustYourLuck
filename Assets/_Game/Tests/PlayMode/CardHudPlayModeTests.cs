using System;
using System.Collections;
using System.Linq;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Gameplay;
using DontTrustYourLuck.Infrastructure;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DontTrustYourLuck.Tests.PlayMode
{
    /// <summary>
    /// Sprint 3: card hand panel on the real GameBoard scene. Hands come from the seeded card stream; each test
    /// searches a seed whose starting hand has the card it needs (same derivation as the director), so no
    /// test-only command is exposed in the game.
    /// </summary>
    public class CardHudPlayModeTests
    {
        private const float TimeoutSeconds = 180f;
        private MatchDirector _director;

        private static ParticipantConfig[] Setup(params bool[] bots) =>
            bots.Select((bot, i) => new ParticipantConfig($"P{i + 1}", Color.HSVToRGB(i / 4f, 0.7f, 0.9f), bot)).ToArray();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("GameBoard", LoadSceneMode.Single);
            _director = UnityEngine.Object.FindAnyObjectByType<MatchDirector>();
            Assert.IsNotNull(_director, "GameBoard scene has a MatchDirector");
            yield return null;
            if (_director.Hud.MainMenu != null) _director.Hud.MainMenu.SkipToGame(); // title screen: start the default match
            _director.SetTimings(0.01f, 0.01f, 0.01f, 0f);
            Assert.IsNotNull(_director.Hud.CardHand, "HUD prefab has the card hand panel (run the Sprint 3 builder)");
        }

        private IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > TimeoutSeconds) Assert.Fail($"Timed out waiting for {what}.");
                yield return null;
            }
        }

        private IEnumerator WaitIdle() => WaitUntil(() => !_director.IsBusy, "playback to finish");

        /// <summary>Finds a seed whose starting state satisfies <paramref name="predicate"/> (pure simulation).</summary>
        private int FindSeed(int players, Func<MatchState, bool> predicate)
        {
            var rules = _director.Session.Rules;
            var catalog = _director.Session.Engine.CardCatalog;
            var ids = Enumerable.Range(1, players).ToArray();
            for (int seed = 1; seed < 5000; seed++)
            {
                var engine = new MatchEngine(rules, new SeededRandomSource(seed),
                    new CardSetup(catalog, new SeededRandomSource(MatchDirector.CardSeedFor(seed))));
                engine.StartMatch(ids);
                if (predicate(engine.State)) return seed;
            }
            Assert.Fail("No seed found for the requested hand.");
            return -1;
        }

        private static bool Has(MatchState s, int player, string cardId) =>
            s.GetPlayer(player).Hand.Cards.Any(c => c.CardId == cardId);

        private int SlotOf(string cardId)
        {
            var hand = _director.Session.State.GetPlayer(_director.Session.State.CurrentPlayerId).Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand.Cards[i].CardId == cardId) return i;
            }
            Assert.Fail($"{cardId} not in hand");
            return -1;
        }

        private CardHandView Hand => _director.Hud.CardHand;

        private void AssertPiecesMatchState()
        {
            var state = _director.Session.State;
            foreach (var piece in _director.ActivePieces)
            {
                var player = state.GetPlayer(piece.PlayerId);
                Assert.AreEqual(player.Position, piece.LogicalPosition, $"piece P{piece.PlayerId} logical position");
                var anchor = _director.Board.GetTileAnchor(player.Position).position;
                float distance = Vector3.Distance(new Vector3(anchor.x, 0f, anchor.z),
                    new Vector3(piece.transform.position.x, 0f, piece.transform.position.z));
                Assert.Less(distance, 0.5f, $"piece P{piece.PlayerId} stands on tile {player.Position}");
            }
        }

        private int VisibleSlots() => Hand.Slots.Count(s => s.gameObject.activeSelf);

        [UnityTest]
        public IEnumerator Hand_ShowsCurrentPlayersCards_WithNameArtAndDescription()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed: 11));
            yield return WaitIdle();

            var state = _director.Session.State;
            var cards = state.GetPlayer(1).Hand.Cards;
            Assert.AreEqual(2, cards.Count, "starting hand");
            Assert.AreEqual(2, VisibleSlots());
            Assert.IsFalse(Hand.Slots[2].gameObject.activeSelf, "third slot hidden");
            StringAssert.Contains("P1", Hand.TitleText);
            var config = _director.Session.Engine.CardCatalog;
            Assert.IsNotNull(config);
            for (int i = 0; i < cards.Count; i++)
            {
                Assert.AreEqual(cards[i].InstanceId, Hand.Slots[i].InstanceId);
                Assert.IsFalse(string.IsNullOrEmpty(Hand.Slots[i].NameText));
                Assert.AreNotEqual(cards[i].CardId, Hand.Slots[i].NameText, "display name comes from the card asset");
                bool shouldBeAvailable = cards[i].Definition.Timing == CardTiming.BeforeRoll;
                Assert.AreEqual(shouldBeAvailable, Hand.Slots[i].IsAvailable, $"{cards[i].CardId} availability before rolling");
            }
            Assert.IsFalse(Hand.PlayButton.interactable, "nothing selected");
            Assert.IsFalse(Hand.TargetsVisible);
        }

        [UnityTest]
        public IEnumerator Hand_HoverShowsRulesTooltip_AndPlayerCardsHideTheTileLine()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed: 11));
            yield return WaitIdle();

            Assert.AreEqual(-1, Hand.TooltipSlot, "no tooltip until a card is hovered or selected");
            Hand.Slots[0].OnPointerEnter(null);
            Assert.AreEqual(0, Hand.TooltipSlot);
            Assert.AreEqual(Hand.Slots[0].Description, Hand.TooltipText);
            Assert.IsFalse(string.IsNullOrEmpty(Hand.TooltipText), "rules text comes from the card asset");
            Hand.Slots[0].OnPointerExit(null);
            Assert.AreEqual(-1, Hand.TooltipSlot);

            var card = _director.Hud.PlayerStatus.Cards[0];
            Assert.AreEqual("Tile 0", card.PositionText, "value kept for tests/accessibility");
            var position = card.transform.Find("Position")?.GetComponent<UnityEngine.UI.Text>();
            if (position != null) Assert.IsFalse(position.enabled, "tile line hidden: the board shows positions");
        }

        [UnityTest]
        public IEnumerator LuckyRoll_OnlyAfterRolling_ReplacesDice_AndBlocksSecondCard()
        {
            int seed = FindSeed(2, s => Has(s, 1, CardIds.LuckyRoll) && s.GetPlayer(1).Hand.Cards.Any(c => c.CardId != CardIds.LuckyRoll));
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed));
            yield return WaitIdle();

            int slot = SlotOf(CardIds.LuckyRoll);
            Assert.IsFalse(Hand.Slots[slot].IsAvailable, "Lucky Roll disabled before rolling");
            Hand.Slots[slot].Button.onClick.Invoke(); // ignored
            Assert.IsNull(_director.SelectedCardId);

            _director.Hud.DicePanel.RollButton.onClick.Invoke();
            yield return WaitIdle();
            Assert.IsTrue(Hand.Slots[slot].IsAvailable, "Lucky Roll enabled after rolling");

            Hand.Slots[slot].Button.onClick.Invoke();
            Assert.IsTrue(Hand.Slots[slot].IsSelected);
            Assert.IsTrue(Hand.PlayButton.interactable);
            Hand.PlayButton.onClick.Invoke();
            yield return WaitIdle();

            var state = _director.Session.State;
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, state.Phase);
            Assert.AreEqual(1, state.GetPlayer(1).Hand.Count, "card consumed");
            Assert.AreEqual(1, VisibleSlots(), "hand panel updated");
            Assert.AreEqual(state.CurrentRoll[0].ToString(), _director.Hud.DicePanel.DieText(0));
            Assert.AreEqual(state.CurrentRoll[1].ToString(), _director.Hud.DicePanel.DieText(1));
            StringAssert.Contains("New dice", _director.Hud.Messages.LastMessage);
            Assert.IsFalse(Hand.Slots[0].IsAvailable, "second card unavailable this turn");
            Assert.AreEqual(_director.Hud.Text.hintCardUsed, Hand.HintText);
            Assert.IsTrue(_director.Hud.DicePanel.ChooseButtons[0].interactable, "player still chooses a die");
        }

        [UnityTest]
        public IEnumerator Swap_TargetSelection_CancelKeepsCard_ThenSwapsPieces()
        {
            int seed = FindSeed(3, s => Has(s, 1, CardIds.Swap));
            Assert.IsTrue(_director.BeginMatch(Setup(false, false, false), seed));
            yield return WaitIdle();
            var state = _director.Session.State;
            state.GetPlayer(2).Position = 9; // test setup through the rules assembly's internals

            int slot = SlotOf(CardIds.Swap);
            Hand.Slots[slot].Button.onClick.Invoke();
            Assert.IsTrue(Hand.TargetsVisible, "targets shown for Swap");
            Assert.IsFalse(Hand.PlayButton.interactable, "needs a target first");
            var shown = Enumerable.Range(0, Hand.TargetButtons.Count).Select(Hand.TargetIdAt).Where(id => id >= 0).ToArray();
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, shown, "only other active players");

            // Cancel: nothing consumed.
            Hand.CancelButton.onClick.Invoke();
            Assert.IsNull(_director.SelectedCardId);
            Assert.IsFalse(Hand.TargetsVisible);
            Assert.AreEqual(2, state.GetPlayer(1).Hand.Count);
            Assert.IsFalse(state.CardPlayedThisTurn);

            // Select again, pick P2, confirm.
            Hand.Slots[slot].Button.onClick.Invoke();
            int p2Button = Enumerable.Range(0, Hand.TargetButtons.Count).First(i => Hand.TargetIdAt(i) == 2);
            Hand.TargetButtons[p2Button].onClick.Invoke();
            Assert.AreEqual(2, _director.SelectedTargetId);
            Assert.IsTrue(Hand.PlayButton.interactable);
            Hand.PlayButton.onClick.Invoke();
            yield return WaitIdle();

            Assert.AreEqual(9, state.GetPlayer(1).Position);
            Assert.AreEqual(0, state.GetPlayer(2).Position);
            AssertPiecesMatchState();
            StringAssert.Contains("swapped positions with P2", _director.Hud.Messages.LastMessage);
            Assert.AreEqual(1, state.GetPlayer(1).Hand.Count);
            Assert.AreEqual(MatchPhase.AwaitingRoll, state.Phase, "turn continues");
            Assert.AreEqual(1, state.CurrentPlayerId);
            Assert.IsTrue(_director.Hud.DicePanel.RollButton.interactable);
            Assert.AreEqual("Tile 9", _director.Hud.PlayerStatus.Cards[0].PositionText);
        }

        [UnityTest]
        public IEnumerator Retreat_AndShield_UpdateBoardAndStatus()
        {
            int seed = FindSeed(2, s => Has(s, 1, CardIds.Retreat) && Has(s, 2, CardIds.Shield));
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed));
            yield return WaitIdle();
            var state = _director.Session.State;
            state.GetPlayer(1).Position = 5;

            Hand.Slots[SlotOf(CardIds.Retreat)].Button.onClick.Invoke();
            Hand.PlayButton.onClick.Invoke();
            yield return WaitIdle();
            Assert.AreEqual(2, state.GetPlayer(1).Position);
            AssertPiecesMatchState();
            StringAssert.Contains("moved back 3 tiles", _director.Hud.Messages.LastMessage);

            // Finish P1's turn with dice, then P2 activates Shield.
            _director.Hud.DicePanel.RollButton.onClick.Invoke();
            yield return WaitIdle();
            int safe = new SimpleBotDecisionProvider().ChooseDie(_director.Session.Engine, 1, state.CurrentRoll);
            _director.Hud.DicePanel.ChooseButtons[safe].onClick.Invoke();
            yield return WaitIdle();
            if (state.IsFinished) Assert.Inconclusive("P1 won with this seed before P2 acted.");
            Assert.AreEqual(2, state.CurrentPlayerId);

            Hand.Slots[SlotOf(CardIds.Shield)].Button.onClick.Invoke();
            Hand.PlayButton.onClick.Invoke();
            yield return WaitIdle();
            Assert.IsTrue(state.GetPlayer(2).HasShield);
            Assert.AreEqual(_director.Hud.Text.statusShielded, _director.Hud.PlayerStatus.Cards[1].StatusText);
            StringAssert.Contains("activated Shield", _director.Hud.Messages.LastMessage);

            _director.Hud.DicePanel.RollButton.onClick.Invoke();
            yield return WaitIdle();
            _director.Hud.DicePanel.ChooseButtons[0].onClick.Invoke();
            yield return WaitIdle();
            bool danger = _director.Session.Engine.IsDangerous(2, state.GetPlayer(2).Position);
            Assert.AreEqual(!danger, state.GetPlayer(2).HasShield, "shield persists until blocking danger");
            Assert.AreEqual(!danger, _director.Hud.PlayerStatus.Cards[1].StatusText == _director.Hud.Text.statusShielded);
            Assert.AreNotEqual(PlayerStatus.Eliminated, state.GetPlayer(2).Status, "a shielded move never eliminates");
        }

        [UnityTest]
        public IEnumerator BotTurn_CardsDisabled_AndRestartClearsHandAndSelection()
        {
            int seed = FindSeed(2, s => s.GetPlayer(1).Hand.Cards.Any(c => c.Definition.Timing == CardTiming.BeforeRoll));
            Assert.IsTrue(_director.BeginMatch(Setup(false, true), seed));
            yield return WaitIdle();

            var card = _director.Session.State.GetPlayer(1).Hand.Cards.First(c => c.Definition.Timing == CardTiming.BeforeRoll);
            Hand.Slots[SlotOf(card.CardId)].Button.onClick.Invoke();
            Assert.IsNotNull(_director.SelectedCardId);

            _director.Restart();
            yield return WaitIdle();
            Assert.IsNull(_director.SelectedCardId, "selection cleared on restart");
            Assert.IsFalse(_director.Session.State.CardPlayedThisTurn);
            Assert.AreEqual(2, VisibleSlots(), "fresh hand, no duplicated slots");
            Assert.IsTrue(_director.Session.State.Players.All(p => p.Hand.Count == 2 && !p.HasShield));

            // Play P1's turn with dice; on the bot's turn every card is disabled.
            _director.SetTimings(0.01f, 0.5f, 0.5f, 0f);
            _director.Hud.DicePanel.RollButton.onClick.Invoke();
            yield return WaitIdle();
            int choice = new SimpleBotDecisionProvider().ChooseDie(_director.Session.Engine, 1, _director.Session.State.CurrentRoll);
            _director.Hud.DicePanel.ChooseButtons[choice].onClick.Invoke();
            yield return WaitIdle();
            if (_director.Session.State.IsFinished) Assert.Inconclusive("Match ended before the bot turn.");
            Assert.IsTrue(_director.Session.IsBotTurn);
            Assert.IsTrue(Hand.Slots.Where(s => s.gameObject.activeSelf).All(s => !s.IsAvailable), "bot hand is read-only");
            StringAssert.Contains("dice only", Hand.HintText);
            yield return WaitUntil(() => !_director.Session.IsBotTurn || _director.Session.State.IsFinished, "bot to act");
        }

        [UnityTest]
        public IEnumerator FullMatch_HumanPlaysCardsEveryTurn_WithBots_FinishesWithoutDeadlock()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(false, true, true, true), seed: 77));
            var bot = new SimpleBotDecisionProvider();
            int cardsPlayed = 0;
            float start = Time.realtimeSinceStartup;

            while (!_director.Session.State.IsFinished)
            {
                if (Time.realtimeSinceStartup - start > TimeoutSeconds) Assert.Fail("Match did not finish (deadlock?).");
                var state = _director.Session.State;
                if (_director.IsBusy || _director.Session.IsBotTurn)
                {
                    yield return null;
                    continue;
                }

                // Human (P1): try every available card once, then dice.
                if (!state.CardPlayedThisTurn)
                {
                    for (int i = 0; i < Hand.Slots.Count; i++)
                    {
                        if (!Hand.Slots[i].gameObject.activeSelf || !Hand.Slots[i].IsAvailable) continue;
                        Hand.Slots[i].Button.onClick.Invoke();
                        if (Hand.TargetsVisible) Hand.TargetButtons[0].onClick.Invoke();
                        Assert.IsTrue(Hand.PlayButton.interactable, "a selectable card can be confirmed");
                        Hand.PlayButton.onClick.Invoke();
                        cardsPlayed++;
                        break;
                    }
                    yield return WaitIdle();
                    if (_director.Session.State.IsFinished) break;
                }

                state = _director.Session.State;
                if (state.Phase == MatchPhase.AwaitingRoll) _director.Hud.DicePanel.RollButton.onClick.Invoke();
                else if (state.Phase == MatchPhase.AwaitingDiceChoice)
                    _director.Hud.DicePanel.ChooseButtons[bot.ChooseDie(_director.Session.Engine, 1, state.CurrentRoll)].onClick.Invoke();
                yield return WaitIdle();
            }

            yield return WaitIdle();
            Assert.IsTrue(_director.Hud.ResultScreen.IsVisible);
            Assert.Greater(cardsPlayed, 0, "the human played at least one card");
            AssertPiecesMatchState();
            Assert.IsNull(_director.SelectedCardId);
        }
    }
}
