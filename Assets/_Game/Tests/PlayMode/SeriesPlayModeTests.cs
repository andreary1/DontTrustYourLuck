using System;
using System.Collections;
using System.Linq;
using DontTrustYourLuck.Domain.Series;
using DontTrustYourLuck.Presentation.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DontTrustYourLuck.Tests.PlayMode
{
    /// <summary>R1 integration: series of rounds to PointsToWin on the real GameBoard scene, with bots.</summary>
    public class SeriesPlayModeTests
    {
        private const float TimeoutSeconds = 240f;
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
            if (_director.Hud.MainMenu != null) _director.Hud.MainMenu.SkipToGame();
            _director.SetTimings(0.01f, 0.01f, 0.01f, 0f);
            _director.SetIntermissionTimings(0.05f, 0f);
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

        [UnityTest]
        public IEnumerator BotsOnly_SeriesToFivePoints_ScoreboardIntermissionChampion_AndPlayAgainResets()
        {
            _director.ConfigureSeries(true, 5);
            Assert.IsTrue(_director.BeginMatch(Setup(true, true), seed: 7));
            var hud = _director.Hud;
            bool sawIntermission = false;

            yield return WaitUntil(() =>
            {
                sawIntermission |= _director.IsInIntermission && hud.Intermission.IsVisible;
                return _director.Session.IsMatchOver && !_director.IsBusy;
            }, "series champion");

            var series = _director.Session.SeriesState;
            Assert.IsTrue(sawIntermission, "an intermission was shown between rounds");
            Assert.GreaterOrEqual(series.RoundNumber, 5);
            int champion = series.ChampionId;
            Assert.AreEqual(5, series.GetScore(champion));
            Assert.AreEqual(5, _director.DisplayedScore(champion), "HUD mirrors the domain score");
            Assert.IsTrue(hud.Scoreboard.IsVisible);
            Assert.IsTrue(hud.ResultScreen.IsVisible, "champion screen");
            StringAssert.Contains($"P{champion}", hud.ResultScreen.Title);
            Assert.IsFalse(hud.Intermission.IsVisible);

            hud.ResultScreen.PlayAgainButton.onClick.Invoke();
            yield return null;
            yield return WaitUntil(() => _director.DisplayedScore(champion) == 0, "new series presented");
            Assert.IsFalse(hud.ResultScreen.IsVisible);
            Assert.AreEqual(1, _director.Session.SeriesState.RoundNumber);
            Assert.AreEqual(0, _director.Session.SeriesState.TopScore);
            Assert.AreEqual(0, _director.DisplayedScore(champion));
        }

        [UnityTest]
        public IEnumerator Intermission_NextRoundButton_StartsNextRound_AfterMinimumTime()
        {
            _director.ConfigureSeries(true, 5);
            _director.SetIntermissionTimings(60f, 0.2f); // only the button can end it within the test
            Assert.IsTrue(_director.BeginMatch(Setup(true, true), seed: 11));
            yield return WaitUntil(() => _director.IsInIntermission, "first intermission");

            var button = _director.Hud.Intermission.ContinueButton;
            Assert.IsFalse(button.interactable, "locked during the minimum time");
            yield return WaitUntil(() => button.interactable, "button unlocked");
            button.onClick.Invoke();
            yield return WaitUntil(() => _director.Session.SeriesState.RoundNumber == 2, "round 2");
            Assert.IsFalse(_director.Hud.Intermission.IsVisible);
            Assert.AreEqual(SeriesPhase.RoundInProgress, _director.Session.SeriesState.Phase);
            foreach (var piece in _director.ActivePieces) Assert.AreEqual(0, piece.LogicalPosition, "everyone back on tile 0");
        }

        [UnityTest]
        public IEnumerator RestartDuringIntermission_LeavesNoStaleRoundStart()
        {
            _director.ConfigureSeries(true, 5);
            _director.SetIntermissionTimings(0.5f, 0f);
            Assert.IsTrue(_director.BeginMatch(Setup(true, true, true), seed: 21));
            yield return WaitUntil(() => _director.IsInIntermission, "first intermission");

            _director.Restart();
            Assert.IsFalse(_director.IsInIntermission);
            Assert.IsFalse(_director.Hud.Intermission.IsVisible);
            int matchNumber = _director.Session.MatchNumber;

            // Past the old intermission's end: a leaked coroutine would have started round 2 of the new series.
            _director.SetTimings(0.5f, 5f, 5f, 0f); // keep the new series' bots from finishing a round meanwhile
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(matchNumber, _director.Session.MatchNumber);
            Assert.AreEqual(1, _director.Session.SeriesState.RoundNumber);
            Assert.AreEqual(0, _director.Session.SeriesState.TopScore);
        }

        [UnityTest]
        public IEnumerator SingleRoundMode_StillAvailable()
        {
            _director.ConfigureSeries(false);
            Assert.IsTrue(_director.BeginMatch(Setup(true, true), seed: 5));
            yield return WaitUntil(() => _director.Session.State.IsFinished && !_director.IsBusy, "match end");
            Assert.IsNull(_director.Session.SeriesState);
            Assert.IsTrue(_director.Hud.ResultScreen.IsVisible);
            Assert.IsFalse(_director.Hud.Scoreboard.IsVisible);
        }
    }
}
