using System.Collections;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.Narration
{
    /// <summary>
    /// Glue between the match playback and the Cantador (docs/NARRATIVE.md). Listens to
    /// <see cref="MatchDirector.EventPresenting"/> like the audio router, so lines stay in sync with animations.
    /// Presentation only: it never touches the session. Also opens the cordel intro over the main menu the first
    /// time in a session and adds a "Story" button to the menu to replay it.
    /// </summary>
    public sealed class NarrationController : MonoBehaviour
    {
        [SerializeField] private CantadorLines lines = new CantadorLines();
        [SerializeField] private bool narrateMatch = true;
        [SerializeField] private bool introOnFirstMenu = true;

        private static bool s_introSeen;

        private MatchDirector _director;
        private GameHUD _hud;
        private CantadorNarrator _narrator;
        private CantadorBoxView _box;
        private StoryIntroView _intro;
        private Button _storyButton;
        private bool _subscribed;

        public CantadorNarrator Narrator => _narrator;
        public CantadorBoxView Box => _box;
        public StoryIntroView Intro => _intro;
        public Button StoryButton => _storyButton;
        public CantadorLines Lines => lines;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => s_introSeen = false;

        public void Configure(MatchDirector director, GameHUD hud)
        {
            _director = director;
            _hud = hud;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            Subscribe();
            if (_hud == null) return;
            EnsureViews();
            AddStoryButton();
            if (introOnFirstMenu && !s_introSeen) StartCoroutine(OpenIntroWhenMenuShows());
        }

        private void OnDestroy()
        {
            if (_storyButton != null) _storyButton.onClick.RemoveListener(OpenIntro);
            if (_intro != null) _intro.Closed -= OnIntroClosed;
        }

        private void EnsureViews()
        {
            if (_narrator != null) return;
            _narrator = new CantadorNarrator(lines, id => _director != null ? _director.PlayerName(id) : "Player " + id,
                System.Environment.TickCount);
            var root = (RectTransform)_hud.transform;
            var font = RuntimeUi.FontFrom(_hud);
            _box = CantadorBoxView.Create(root, font, lines.speaker);
            _intro = StoryIntroView.Create(root, font, lines);
            _intro.Closed += OnIntroClosed;
        }

        private void Subscribe()
        {
            if (_subscribed || _director == null) return;
            _subscribed = true;
            _director.EventPresenting += OnEventPresenting;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_director != null) _director.EventPresenting -= OnEventPresenting;
        }

        private void OnEventPresenting(MatchEvent e)
        {
            if (!narrateMatch || _hud == null) return;
            EnsureViews();
            if (e is MatchStartedEvent) _box.Clear(); // new round: drop lines left from the previous one
            _narrator.SeriesMode = _director.Session != null && _director.Session.IsSeriesMode;
            _narrator.MaxCycles = _director.MaxCyclesThisRound;
            if (_narrator.TryNarrate(e, out var line)) _box.Say(line);
        }

        // ------------------------------------------------------------ intro

        private IEnumerator OpenIntroWhenMenuShows()
        {
            yield return null; // the menu decides in its own Start whether it shows
            var menu = _hud.MainMenu;
            if (menu != null && menu.IsVisible && !s_introSeen) OpenIntro();
        }

        public void OpenIntro()
        {
            EnsureViews();
            _box.Clear();
            _intro.Open();
        }

        private void OnIntroClosed() => s_introSeen = true;

        /// <summary>Clones the How to Play button so "Story" matches the menu look; skipped when there is no menu.</summary>
        private void AddStoryButton()
        {
            var menu = _hud.MainMenu;
            if (menu == null || menu.HowToButton == null || _storyButton != null) return;
            var source = menu.HowToButton;
            var clone = Instantiate(source.gameObject, source.transform.parent, false);
            clone.name = "StoryButton";
            // The menu's entrance animation drives its own buttons only; the clone may be copied mid-animation.
            clone.transform.localScale = Vector3.one;
            var group = clone.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            _storyButton = clone.GetComponent<Button>();
            _storyButton.onClick = new Button.ButtonClickedEvent(); // drop listeners copied from the source
            _storyButton.onClick.AddListener(OpenIntro);
            foreach (var label in clone.GetComponentsInChildren<Text>(true)) label.text = lines.storyButton;
        }
    }
}
