using System;
using System.Collections;
using System.Linq;
using DontTrustYourLuck.Presentation.Audio;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DontTrustYourLuck.Tests.PlayMode
{
    /// <summary>Title screen on the real GameBoard scene: entrance, panels, options sync, Play and return to menu.</summary>
    public class MainMenuPlayModeTests
    {
        private const float TimeoutSeconds = 60f;
        private MatchDirector _director;
        private MainMenuView _menu;
        private AudioManager _audio;
        private float _master, _music, _sfx;
        private bool _muted;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("GameBoard", LoadSceneMode.Single);
            _director = UnityEngine.Object.FindAnyObjectByType<MatchDirector>();
            _director?.ConfigureSeries(false); // pre-R1 tests cover the single-round match; series: SeriesPlayModeTests
            Assert.IsNotNull(_director, "MatchDirector");
            _menu = _director.Hud.MainMenu;
            Assert.IsNotNull(_menu, "HUD has the main menu (run the main menu builder)");
            _audio = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
            if (_audio != null)
            {
                var s = _audio.Settings; // restored in TearDown: the scene persists settings to PlayerPrefs
                _master = s.Master; _music = s.Music; _sfx = s.Sfx; _muted = s.Muted;
            }
            yield return null;
            _director.SetTimings(0.01f, 0.01f, 0.01f, 0f);
        }

        [TearDown]
        public void RestoreSettings()
        {
            if (_audio == null) return;
            var s = _audio.Settings;
            s.SetMaster(_master); s.SetMusic(_music); s.SetSfx(_sfx); s.SetMuted(_muted);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > TimeoutSeconds) Assert.Fail($"Timed out waiting for {what}.");
                yield return null;
            }
        }

        private IEnumerator WaitIdleMenu() => WaitUntil(() => _menu.IsVisible && !_menu.IsAnimating, "menu entrance");

        [UnityTest]
        public IEnumerator SceneStart_ShowsMenu_AndWaitsForPlay()
        {
            Assert.IsTrue(_menu.IsVisible, "menu is up on load");
            Assert.IsNull(_director.Session, "no match before Play");
            yield return WaitIdleMenu();
            yield return null;
            Assert.AreEqual(Vector3.one, _menu.Logo.localScale, "logo settled after the entrance");
            Assert.IsTrue(_menu.PlayButton.IsInteractable());
            var topBar = _director.Hud.transform.Find("TopBar").GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, topBar.alpha, 1e-3f, "HUD hidden behind the menu");
            Assert.IsFalse(topBar.blocksRaycasts);
            Assert.IsNull(_director.Session, "still no match while idling on the menu");
        }

        [UnityTest]
        public IEnumerator Play_HidesMenu_StartsMatch_AndShowsHud()
        {
            yield return WaitIdleMenu();
            _menu.PlayButton.onClick.Invoke();
            Assert.IsNotNull(_menu.Setup);
            Assert.IsTrue(_menu.Setup.gameObject.activeSelf);
            _menu.Setup.SelectCharacter(2);
            _menu.Setup.ShowBoards();
            Assert.IsTrue(_menu.Setup.IsBoardSelection);
            _menu.Setup.ConfirmBoard();
            Assert.IsNull(_director.Session, "match starts after the exit animation");
            yield return WaitUntil(() => !_menu.IsVisible, "menu exit");
            Assert.IsNotNull(_director.Session, "Play started a match");
            Assert.IsFalse(_menu.gameObject.activeSelf);
            var topBar = _director.Hud.transform.Find("TopBar").GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, topBar.alpha, 1e-3f);
            Assert.IsTrue(topBar.blocksRaycasts, "HUD is clickable again");
            Assert.AreEqual(31, _director.Board.TileCount);
        }

        [UnityTest]
        public IEnumerator BoardChoice_Fortaleza_SwapsDiorama_Circuit_AndSubtitle()
        {
            var themes = _director.BoardThemes;
            Assert.IsNotNull(themes, "GameBoard has the city board switcher (run Rebuild City Boards)");
            var oaxaca = themes.Find("oaxaca");
            var fortaleza = themes.Find("fortaleza");
            Assert.IsNotNull(oaxaca); Assert.IsNotNull(fortaleza);
            Assert.AreEqual("oaxaca", themes.CurrentId, "Oaxaca is the default board");
            Assert.IsTrue(oaxaca.environment.activeSelf);
            Assert.IsFalse(fortaleza.environment.activeSelf, "only one diorama renders at a time");

            yield return WaitIdleMenu();
            _menu.PlayButton.onClick.Invoke();
            var setup = _menu.Setup;
            setup.ShowBoards();
            Assert.AreEqual("oaxaca", setup.SelectedBoard);
            setup.SelectBoard("fortaleza");
            Assert.AreEqual("fortaleza", themes.CurrentId, "selection previews the board behind the modal");
            Assert.IsTrue(fortaleza.environment.activeSelf);
            Assert.IsFalse(oaxaca.environment.activeSelf);
            Assert.AreEqual(DontTrustYourLuck.Presentation.Board.CircuitShape.Bay, _director.Board.Shape);
            setup.SelectBoard("salvador"); // locked board: ignored
            Assert.AreEqual("fortaleza", setup.SelectedBoard);

            setup.ConfirmBoard();
            yield return WaitUntil(() => !_menu.IsVisible, "menu exit");
            Assert.IsNotNull(_director.Session, "match started on Fortaleza");
            yield return null; // deferred Destroy of the previous outline's connectors
            int connectors = 0;
            foreach (Transform child in _director.Board.transform)
                if (child.name.StartsWith("Connector_")) connectors++;
            Assert.AreEqual(31, connectors, "one connector per circuit edge, none left from the plaza outline");
            var tile12 = _director.Board.GetTile(12).transform.localPosition;
            Assert.AreEqual(DontTrustYourLuck.Presentation.Board.BoardView.CircuitPose(12, 30, DontTrustYourLuck.Presentation.Board.CircuitShape.Bay).position, tile12);
            var subtitle = _director.Hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text == "Fortaleza");
            Assert.IsNotNull(subtitle, "HUD subtitle names the chosen city");
            foreach (var piece in _director.ActivePieces)
                Assert.Less(Vector3.Distance(piece.transform.position, _director.Board.GetTileAnchor(piece.LogicalPosition).position), 1f,
                    $"{piece.name} stands on its tile of the new outline");
        }

        [UnityTest]
        public IEnumerator Setup_UsesMenuSkin_SeatsFollowSelection_AndBackSteps()
        {
            yield return WaitIdleMenu();
            _menu.PlayButton.onClick.Invoke();
            var setup = _menu.Setup;
            var window = setup.transform.Find("Characters").GetComponent<Image>();
            Assert.IsNotNull(window.sprite, "setup window reuses the menu's paper frame");
            Assert.AreEqual(_menu.HowToPanel.GetComponent<Image>().sprite, window.sprite);

            setup.SelectCharacter(1);
            Assert.AreEqual(0, setup.SeatOf(1), "selected animal is the player");
            Assert.AreEqual(1, setup.SeatOf(2));
            while (setup.PlayerCount != 2) setup.CyclePlayerCount();
            Assert.AreEqual(-1, setup.SeatOf(0), "with 2 players the card before the pick sits out");

            setup.ShowBoards();
            Assert.IsTrue(setup.IsBoardSelection);
            _menu.ClosePanel(); // Esc / Back on the board page returns to the animals
            Assert.IsFalse(setup.IsBoardSelection);
            Assert.IsTrue(setup.gameObject.activeSelf);
            _menu.ClosePanel();
            Assert.IsFalse(setup.gameObject.activeSelf);
            Assert.IsFalse(_menu.IsPanelOpen);
        }

        [UnityTest]
        public IEnumerator Panels_OpenOneAtATime_AndBackCloses()
        {
            yield return WaitIdleMenu();
            _menu.HowToButton.onClick.Invoke();
            Assert.AreSame(_menu.HowToPanel, _menu.OpenPanel);
            Assert.IsTrue(_menu.HowToPanel.activeSelf);
            yield return null; // selectables pick up CanvasGroup changes on the next update
            Assert.IsFalse(_menu.PlayButton.IsInteractable(), "menu buttons are blocked while a panel is open");

            _menu.OpenCredits(); // switching panels closes the previous one
            Assert.IsFalse(_menu.HowToPanel.activeSelf);
            Assert.IsTrue(_menu.CreditsPanel.activeSelf);

            var back = _menu.BackButtons.First(b => b.transform.IsChildOf(_menu.CreditsPanel.transform));
            back.onClick.Invoke();
            Assert.IsFalse(_menu.IsPanelOpen);
            Assert.IsFalse(_menu.CreditsPanel.activeSelf);
            yield return null;
            Assert.IsTrue(_menu.PlayButton.IsInteractable());
            Assert.IsNull(_director.Session, "panels never start a match");
        }

        [UnityTest]
        public IEnumerator OptionsPanel_DrivesAudioSettings_AndStaysInSyncWithHudPopover()
        {
            if (_audio == null) Assert.Ignore("No AudioManager in the scene.");
            yield return WaitIdleMenu();
            _menu.OptionsButton.onClick.Invoke();
            Assert.IsTrue(_menu.OptionsPanel.activeSelf);
            var views = _director.Hud.AudioSettingsViews;
            Assert.AreEqual(2, views.Count, "HUD popover + menu options");
            var menuView = views[1];
            menuView.MusicSlider.value = 0.25f;
            Assert.AreEqual(0.25f, _audio.Settings.Music, 1e-4f);
            Assert.AreEqual(0.25f, views[0].MusicSlider.value, 1e-4f, "HUD popover shows the same value");
            menuView.MuteToggle.isOn = true;
            Assert.IsTrue(_audio.Settings.Muted);
            Assert.IsTrue(views[0].MuteToggle.isOn);
        }

        [UnityTest]
        public IEnumerator ResultScreen_MainMenu_ReturnsToMenu_ThenPlayStartsANewMatch()
        {
            _menu.SkipToGame();
            Assert.IsTrue(_director.BeginMatch(Enumerable.Range(0, 2)
                .Select(i => new ParticipantConfig($"B{i + 1}", Color.white, true)).ToArray(), seed: 4));
            yield return WaitUntil(() => _director.Session.State.IsFinished && !_director.IsBusy, "bot match end");
            var result = _director.Hud.ResultScreen;
            Assert.IsTrue(result.IsVisible);
            Assert.IsNotNull(result.MenuButton, "result screen has a Main Menu button");

            result.MenuButton.onClick.Invoke();
            Assert.IsTrue(_menu.IsVisible);
            yield return WaitIdleMenu();
            var finished = _director.Session;

            _menu.PlayButton.onClick.Invoke();
            _menu.Setup.ShowBoards();
            _menu.Setup.ConfirmBoard();
            yield return WaitUntil(() => !_menu.IsVisible, "menu exit");
            Assert.IsFalse(_director.Session.State.IsFinished, "a fresh match started");
            Assert.IsFalse(result.IsVisible, "result screen closed by the new match");
            Assert.AreNotSame(finished, _director.Session, "New setup creates a fresh session with the selected seats");
        }
    }
}
