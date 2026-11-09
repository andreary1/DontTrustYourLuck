using System;
using System.Collections;
using System.Linq;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DontTrustYourLuck.Tests.PlayMode
{
    /// <summary>
    /// Integration tests on the real GameBoard scene (must be in Build Settings). Timings are shortened and
    /// seeds fixed so runs are reproducible.
    /// </summary>
    public class GameBoardMatchPlayModeTests
    {
        private const float TimeoutSeconds = 120f;
        private MatchDirector _director;

        private static ParticipantConfig[] Setup(params bool[] bots) =>
            bots.Select((bot, i) => new ParticipantConfig($"P{i + 1}", Color.HSVToRGB(i / 4f, 0.7f, 0.9f), bot)).ToArray();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("GameBoard", LoadSceneMode.Single);
            _director = UnityEngine.Object.FindAnyObjectByType<MatchDirector>();
            Assert.IsNotNull(_director, "GameBoard scene has a MatchDirector");
            yield return null; // Start() runs the default match
            if (_director.Hud.MainMenu != null) _director.Hud.MainMenu.SkipToGame(); // title screen: start the default match
            _director.SetTimings(0.01f, 0.01f, 0.01f, 0f);
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

        [UnityTest]
        public IEnumerator HumanTurn_ButtonsFollowPhases_AndPieceMatchesLogicalState()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed: 11));
            yield return WaitIdle();

            var hud = _director.Hud;
            var dice = hud.DicePanel;
            var state = _director.Session.State;
            Assert.AreEqual(MatchPhase.AwaitingRoll, state.Phase);
            Assert.IsTrue(dice.RollButton.interactable, "roll enabled before rolling");
            Assert.IsFalse(dice.ChooseButtons[0].interactable || dice.ChooseButtons[1].interactable, "choose disabled before rolling");

            // Info panel and active highlight.
            Assert.AreEqual(state.LuckyNumber.ToString(), hud.MatchInfo.LuckyText);
            Assert.AreEqual(state.UnluckyNumber.ToString(), hud.MatchInfo.UnluckyText);
            Assert.AreEqual("1 / 15", hud.MatchInfo.RoundText);
            Assert.AreEqual("P1", hud.MatchInfo.TurnText);
            Assert.IsTrue(hud.PlayerStatus.Cards[0].IsHighlighted);
            Assert.IsFalse(hud.PlayerStatus.Cards[1].IsHighlighted);
            Assert.IsFalse(hud.PlayerStatus.Cards[2].gameObject.activeSelf, "unused cards hidden");

            dice.RollButton.onClick.Invoke();
            yield return WaitIdle();
            var roll = _director.Session.State.CurrentRoll;
            Assert.IsNotNull(roll);
            Assert.AreEqual(roll[0].ToString(), dice.DieText(0));
            Assert.AreEqual(roll[1].ToString(), dice.DieText(1));
            Assert.IsFalse(dice.RollButton.interactable, "roll disabled after rolling");
            Assert.IsTrue(dice.ChooseButtons[0].interactable && dice.ChooseButtons[1].interactable);
            StringAssert.Contains("P1 rolled", hud.Messages.LastMessage);

            // Extra clicks are ignored: no second roll.
            dice.RollButton.onClick.Invoke();
            Assert.AreSame(roll, _director.Session.State.CurrentRoll);

            int expected = Math.Min(roll[1], 99);
            dice.ChooseButtons[1].onClick.Invoke();
            Assert.IsTrue(_director.IsBusy, "resolution is playing back");
            Assert.IsFalse(dice.RollButton.interactable || dice.ChooseButtons[0].interactable || dice.ChooseButtons[1].interactable,
                "all gameplay buttons disabled during resolution");
            yield return WaitIdle();

            state = _director.Session.State;
            AssertPiecesMatchState();
            if (!state.IsFinished)
            {
                Assert.AreEqual(expected, state.GetPlayer(1).Position);
                Assert.AreEqual(2, state.CurrentPlayerId, "turn passed to P2");
                Assert.AreEqual("P2", hud.MatchInfo.TurnText);
                Assert.IsTrue(hud.PlayerStatus.Cards[1].IsHighlighted);
                Assert.IsFalse(hud.PlayerStatus.Cards[0].IsHighlighted);
                Assert.AreEqual($"Tile {expected}", hud.PlayerStatus.Cards[0].PositionText);
            }
        }

        [UnityTest]
        public IEnumerator InputDuringBotTurnOrPlayback_IsIgnored_AndRestartMidAnimationIsClean()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(true, false, false), seed: 21)); // P1 bot, P2/P3 human (P2 still plays if P1 is eliminated)
            _director.SetTimings(0.2f, 0.3f, 0.3f, 0f); // slow enough to act mid-playback
            var dice = _director.Hud.DicePanel;

            // Bot turn: human clicks are ignored and commands are not issued.
            yield return WaitUntil(() => !_director.IsBusy, "initial playback");
            Assert.IsTrue(_director.Session.IsBotTurn);
            Assert.IsFalse(dice.RollButton.interactable);
            dice.RollButton.onClick.Invoke();
            Assert.AreEqual(MatchPhase.AwaitingRoll, _director.Session.State.Phase, "click on bot turn ignored");

            // Wait until the bot's move is animating, then click and restart mid-animation.
            yield return WaitUntil(() => _director.IsBusy && _director.Session.State.CurrentPlayerId == 2, "bot move playback");
            dice.RollButton.onClick.Invoke();
            dice.ChooseButtons[0].onClick.Invoke();
            Assert.AreEqual(MatchPhase.AwaitingRoll, _director.Session.State.Phase, "clicks during playback ignored");

            int pieces = UnityEngine.Object.FindObjectsByType<PlayerPieceView>(FindObjectsInactive.Include).Length;
            _director.Restart();
            yield return WaitUntil(() => !_director.IsBusy, "restart playback");
            yield return null;

            var state = _director.Session.State;
            Assert.AreEqual(1, state.RoundNumber);
            Assert.IsTrue(state.Players.All(p => p.Position == 0));
            AssertPiecesMatchState();
            Assert.AreEqual(pieces, UnityEngine.Object.FindObjectsByType<PlayerPieceView>(FindObjectsInactive.Include).Length);

            // BeginMatch twice in a row does not duplicate pieces or listeners.
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed: 3));
            Assert.IsTrue(_director.BeginMatch(Setup(false, false), seed: 3));
            yield return WaitIdle();
            Assert.AreEqual(pieces, UnityEngine.Object.FindObjectsByType<PlayerPieceView>(FindObjectsInactive.Include).Length);
            _director.Hud.DicePanel.RollButton.onClick.Invoke();
            yield return WaitIdle();
            Assert.AreEqual(MatchPhase.AwaitingDiceChoice, _director.Session.State.Phase, "one roll per click");
        }

        [UnityTest]
        public IEnumerator BotsOnly_MatchFinishes_ResultScreenShows_AndPlayAgainResetsCleanly()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(true, true, true, true), seed: 5));
            int startedRound = 0;
            yield return WaitUntil(() =>
            {
                var s = _director.Session.State;
                if (!_director.IsBusy && s.RoundNumber > startedRound)
                {
                    // Round/unlucky display follows the state between commands.
                    Assert.AreEqual($"{s.RoundNumber} / 15", _director.Hud.MatchInfo.RoundText);
                    Assert.AreEqual(s.UnluckyNumber.ToString(), _director.Hud.MatchInfo.UnluckyText);
                    startedRound = s.RoundNumber;
                }
                return s.IsFinished && !_director.IsBusy;
            }, "bots to finish the match");

            var hud = _director.Hud;
            var state = _director.Session.State;
            Assert.IsTrue(hud.ResultScreen.IsVisible);
            StringAssert.Contains(hud.Text.statusWinner, hud.PlayerStatus.Cards[state.WinnerIds[0] - 1].StatusText);
            StringAssert.Contains($"P{state.WinnerIds[0]}", hud.ResultScreen.Title);
            Assert.IsFalse(hud.DicePanel.RollButton.interactable, "dice disabled after the match");
            AssertPiecesMatchState();

            int piecesBefore = UnityEngine.Object.FindObjectsByType<PlayerPieceView>(FindObjectsInactive.Include).Length;
            int matchNumber = _director.Session.MatchNumber;

            hud.ResultScreen.PlayAgainButton.onClick.Invoke();
            yield return WaitUntil(() => _director.Session.MatchNumber == matchNumber + 1, "restart");
            yield return null;

            state = _director.Session.State;
            Assert.IsFalse(hud.ResultScreen.IsVisible, "result screen hidden after Play Again");
            Assert.AreEqual(1, state.RoundNumber);
            Assert.IsTrue(state.Players.All(p => p.Position == 0 && p.Status == PlayerStatus.Active));
            Assert.IsTrue(_director.ActivePieces.All(p => p.LogicalPosition == 0 && p.VisualState == PieceVisualState.Normal));
            Assert.AreEqual(piecesBefore, UnityEngine.Object.FindObjectsByType<PlayerPieceView>(FindObjectsInactive.Include).Length,
                "no duplicated pieces");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<GameHUD>(FindObjectsInactive.Include).Length);
            Assert.LessOrEqual(hud.Messages.LineCount, 4, "log cleared on restart");

            // Bots keep playing the new match automatically.
            yield return WaitUntil(() => _director.Session.State.RoundNumber >= 2 || _director.Session.State.IsFinished,
                "bots to play after restart");
        }
    }
}
