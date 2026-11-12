using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Audio;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Feedback;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DontTrustYourLuck.Tests.PlayMode
{
    /// <summary>
    /// Sprint 4 integration on the real GameBoard scene: characters, markers, audio cues, audio settings,
    /// banner and restart. Presentation only: the rules are covered by the EditMode/Sprint 2-3 tests.
    /// </summary>
    public class Sprint4FeedbackPlayModeTests
    {
        private const float TimeoutSeconds = 120f;
        private MatchDirector _director;
        private AudioManager _audio;
        private VisualFeedbackController _feedback;
        private float _master, _music, _sfx;
        private bool _muted;

        private static ParticipantConfig[] Setup(params bool[] bots) =>
            bots.Select((bot, i) => new ParticipantConfig($"P{i + 1}", Color.HSVToRGB(i / 4f, 0.7f, 0.9f), bot)).ToArray();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("GameBoard", LoadSceneMode.Single);
            _director = UnityEngine.Object.FindAnyObjectByType<MatchDirector>();
            _director?.ConfigureSeries(false); // pre-R1 tests cover the single-round match; series: SeriesPlayModeTests
            _audio = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
            _feedback = UnityEngine.Object.FindAnyObjectByType<VisualFeedbackController>();
            Assert.IsNotNull(_director, "MatchDirector");
            Assert.IsNotNull(_audio, "AudioManager (Sprint 4 scene pass)");
            Assert.IsNotNull(_feedback, "VisualFeedbackController (Sprint 4 scene pass)");
            var s = _audio.Settings; // restored in TearDown: the scene persists settings to PlayerPrefs
            _master = s.Master; _music = s.Music; _sfx = s.Sfx; _muted = s.Muted;
            yield return null;
            if (_director.Hud.MainMenu != null) _director.Hud.MainMenu.SkipToGame(); // title screen: start the default match
            _director.SetTimings(0.01f, 0.01f, 0.01f, 0f);
        }

        [TearDown]
        public void RestoreSettings()
        {
            if (_audio == null) return;
            var s = _audio.Settings;
            s.SetMaster(_master); s.SetMusic(_music); s.SetSfx(_sfx); s.SetMuted(_muted);
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

        [UnityTest]
        public IEnumerator DefaultMatch_PiecesShowTheFourAnimals_AndMarkersSitOnLuckyAndUnluckyTiles()
        {
            yield return WaitIdle();
            var ids = _director.ActivePieces.Select(p => p.Character != null ? p.Character.Id : null).ToArray();
            CollectionAssert.AreEqual(new[] { "onca", "capivara", "macaco", "tucano" }, ids);

            var board = _director.Board;
            var state = _director.Session.State;
            Assert.AreEqual(state.LuckyNumber, board.LuckyIndex);
            Assert.AreEqual(state.UnluckyNumber, board.UnluckyIndex);
            Assert.IsNotNull(board.LuckyMarker);
            Assert.IsTrue(board.LuckyMarker.gameObject.activeInHierarchy);
            var lucky = board.Tiles[state.LuckyNumber].transform.position;
            // Markers sit on a tile corner, not on its center.
            Assert.AreEqual(lucky.x + BoardView.MarkerOffsetXZ.x, board.LuckyMarker.position.x, 1e-3f);
            Assert.AreEqual(lucky.z + BoardView.MarkerOffsetXZ.y, board.LuckyMarker.position.z, 1e-3f);
            Assert.AreEqual(31, board.TileCount, "the 31 logical tiles are kept");
            for (int i = 0; i < board.TileCount; i++) Assert.AreEqual(i, board.Tiles[i].Index);
        }

        [UnityTest]
        public IEnumerator BotMatch_PlaysCuesForTheMainEvents_AndActiveMarkerFollowsTheTurn()
        {
            // The default match started in SetUp just played its cues; let their cooldowns (<= 0.3 s) expire.
            yield return new WaitForSecondsRealtime(0.35f);
            var played = new List<SfxId>();
            void OnPlayed(SfxId id) => played.Add(id);
            _audio.Played += OnPlayed;
            try
            {
                Assert.IsTrue(_director.BeginMatch(Setup(true, true), seed: 5));
                int checkedTurns = 0;
                yield return WaitUntil(() =>
                {
                    if (_feedback.MarkedPlayerId > 0 && _director.TryGetPiece(_feedback.MarkedPlayerId, out var piece))
                    {
                        Assert.AreSame(piece.transform, _feedback.ActiveMarker.parent, "marker is parented to the marked piece");
                        checkedTurns++;
                    }
                    return _director.Session.State.IsFinished && !_director.IsBusy;
                }, "bot match to finish");

                CollectionAssert.Contains(played, SfxId.LuckyChime);
                CollectionAssert.Contains(played, SfxId.DangerReveal);
                CollectionAssert.Contains(played, SfxId.TurnChange);
                CollectionAssert.Contains(played, SfxId.DiceRoll);
                CollectionAssert.Contains(played, SfxId.DieChoose);
                Assert.IsTrue(played.Contains(SfxId.PieceStep) || played.Contains(SfxId.PieceLand), "movement cues");
                Assert.IsTrue(played.Contains(SfxId.Victory) || played.Contains(SfxId.Elimination), "end of match cue");
                Assert.Greater(checkedTurns, 0, "active marker was observed on a piece");
                Assert.AreEqual(-1, _feedback.MarkedPlayerId, "marker hidden once the match finished");
            }
            finally
            {
                _audio.Played -= OnPlayed;
            }
        }

        [UnityTest]
        public IEnumerator AudioSettingsView_SlidersAndMute_DriveTheModel()
        {
            yield return WaitIdle();
            var view = _director.Hud.AudioSettings;
            Assert.IsNotNull(view, "HUD has the audio settings view");
            view.MasterSlider.value = 0.3f;
            view.MusicSlider.value = 0.2f;
            view.SfxSlider.value = 0.6f;
            Assert.AreEqual(0.3f, _audio.Settings.Master, 1e-4f);
            Assert.AreEqual(0.2f, _audio.Settings.Music, 1e-4f);
            Assert.AreEqual(0.6f, _audio.Settings.Sfx, 1e-4f);
            view.MuteToggle.isOn = true;
            Assert.IsTrue(_audio.Settings.Muted);
            Assert.AreEqual(0f, _audio.Settings.EffectiveSfx);

            // Model -> view (e.g. restored settings) without feedback loops.
            _audio.Settings.SetMuted(false);
            _audio.Settings.SetMaster(0.9f);
            Assert.IsFalse(view.MuteToggle.isOn);
            Assert.AreEqual(0.9f, view.MasterSlider.value, 1e-4f);
        }

        [UnityTest]
        public IEnumerator HumanTurn_ShowsTurnBanner()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(false, true), seed: 3));
            yield return WaitUntil(() => _director.Hud.Banner != null && _director.Hud.Banner.IsVisible, "turn banner on the human turn");
        }

        [UnityTest]
        public IEnumerator Restart_ResetsPieceVisuals()
        {
            Assert.IsTrue(_director.BeginMatch(Setup(true, true), seed: 9));
            yield return WaitUntil(() => _director.Session.State.IsFinished && !_director.IsBusy, "match end");
            Assert.IsTrue(_director.ActivePieces.Any(p => p.VisualState != PieceVisualState.Normal), "end state is shown on a piece");

            _director.Restart();
            yield return WaitIdle();
            foreach (var piece in _director.ActivePieces)
            {
                Assert.AreEqual(PieceVisualState.Normal, piece.VisualState, piece.name);
                Assert.IsFalse(piece.IsShielded, piece.name);
                Assert.AreEqual(0, piece.LogicalPosition, piece.name);
            }
        }
    }
}
