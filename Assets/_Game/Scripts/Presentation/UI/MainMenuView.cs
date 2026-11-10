using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Every player-facing main-menu string (kept apart from the match HUD table).</summary>
    [Serializable]
    public sealed class MenuText
    {
        public string menuTitle = "MAIN MENU";
        public string play = "Play";
        public string howToPlay = "How to Play";
        public string options = "Options";
        public string credits = "Credits";
        public string back = "Back";
        public string footerHint = "Click to select  -  Esc to go back";
        public string footerEvent = "Unity Game Jam 2026";
        public string howToTitle = "HOW TO PLAY";
        [TextArea(2, 4)]
        public string[] howToSteps =
        {
            "<b>Lucky number</b>: drawn once per match. Land on it <b>exactly</b> to win.",
            "<b>Unlucky number</b>: changes every round. End a move on it and you are <b>out</b>.",
            "<b>Your turn</b>: roll two dice and choose <b>one</b> of them to move.",
            "<b>Too far?</b> Extra steps bounce back from the lucky number.",
            "<b>Cards</b>: one per turn. Lucky Roll, Swap, Retreat or Shield.",
            "<b>Round 15</b>: the closest to the lucky number wins. The last animal standing wins too.",
        };
        public string optionsTitle = "OPTIONS";
        public string creditsTitle = "CREDITS";
        [TextArea(6, 16)]
        public string creditsBody =
            "<b>Don't Trust Your Luck!</b>\nA party board game made for Unity Game Jam 2026.\n\n" +
            "<b>Design & development</b>\nandreary1\n\n" +
            "<b>Built with</b>\nUnity 6 - Universal Render Pipeline\n\n" +
            "<b>Art & audio</b>\nOriginal art and music made for this project.\nVisual inspiration: Fortaleza, Ceará (Brazil).\n\n" +
            "<b>Thanks for playing!</b>";
    }

    /// <summary>
    /// Title screen drawn over the diorama: logo entrance animation, Play / How to Play / Options / Credits.
    /// Pure presentation: it only raises <see cref="StartGameRequested"/>; the director starts the match.
    /// Animations use unscaled time and never touch match state.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        private enum Phase { Hidden, Intro, Idle, Outro }

        [SerializeField] private MenuText text = new MenuText();
        [Tooltip("Show the menu when the scene starts. Off: the match starts at once (menu skipped).")]
        [SerializeField] private bool showOnStart = true;

        [Header("Layout")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private RectTransform logo;
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private CanvasGroup[] buttonGroups = new CanvasGroup[0];
        [SerializeField] private CanvasGroup footer;
        [Tooltip("HUD regions hidden while the menu is up and faded back in when the match starts.")]
        [SerializeField] private CanvasGroup[] hudGroups = new CanvasGroup[0];

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button howToButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Text playLabel;
        [SerializeField] private Text howToLabel;
        [SerializeField] private Text optionsLabel;
        [SerializeField] private Text creditsLabel;
        [SerializeField] private Text menuTitleText;
        [SerializeField] private Text footerHintText;
        [SerializeField] private Text footerEventText;

        [Header("Panels")]
        [SerializeField] private GameObject howToPanel;
        [SerializeField] private Text howToTitleText;
        [SerializeField] private Text[] howToStepTexts = new Text[0];
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Text optionsTitleText;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Text creditsTitleText;
        [SerializeField] private Text creditsBodyText;
        [SerializeField] private Button[] backButtons = new Button[0];
        [SerializeField] private Text[] backLabels = new Text[0];

        [Header("Timing (seconds, unscaled)")]
        [Min(0.1f)] [SerializeField] private float introDuration = 1.35f;
        [Min(0.1f)] [SerializeField] private float outroDuration = 0.6f;
        [Min(0.05f)] [SerializeField] private float panelPopDuration = 0.18f;

        private const float MaxFrameStep = 0.1f; // >= 10 fps keeps real-time pacing; slower frames slow the animation

        private Phase _phase = Phase.Hidden;
        private float _t;
        private float _idleT;
        private bool _entered;
        private Vector2 _logoHome, _cardHome;
        private GameObject _openPanel;
        private float _panelT;
        private MatchSetupView _setup;
        public MatchSetupView Setup => _setup;
        public void UseCircuitInstructions()
        {
            text.howToSteps = new[]
            {
                "<b>Lucky number:</b> stop <b>exactly</b> on the golden number to win. Passing over it does not count.",
                "<b>Circuit:</b> after tile 30, keep going from tile 1. The starting tile 0 is not part of the laps.",
                "<b>Unlucky number:</b> changes every round. End a move on it and you are <b>out</b>, unless a Shield saves you.",
                "<b>Cards:</b> one per turn. Draw one every two rounds, up to three in hand. A Shield lasts until it blocks.",
                "<b>Debuffs:</b> start next round and last one round. Frozen players can still use cards; danger strikes at round end.",
                "<b>Final:</b> the last animal standing wins or, at round 15, whoever is fewest steps from the target along the circuit."
            };
            ApplyLabels();
        }

        /// <summary>Reuses the menu's own sprites and font so runtime-built screens match the printed-paper look.</summary>
        public MenuSkin BuildSkin()
        {
            Transform step = howToPanel != null ? howToPanel.transform.Find("Step1") : null;
            Transform back = backButtons.Length > 0 && backButtons[0] != null ? backButtons[0].transform : null;
            Transform band = backdrop != null ? backdrop.transform.Find("TopBand") : null;
            return new MenuSkin
            {
                Window = SpriteOf(howToPanel != null ? howToPanel.transform : null),
                Plaque = SpriteOf(howToTitleText != null ? howToTitleText.transform.parent : null),
                Button = SpriteOf(back),
                Primary = SpriteOf(playButton != null ? playButton.transform : null),
                Badge = SpriteOf(step != null ? step.Find("Badge") : null),
                Tile = SpriteOf(band),
                IconBack = SpriteOf(back != null ? back.Find("Icon") : null),
                IconPlay = SpriteOf(playButton != null ? playButton.transform.Find("Icon") : null),
                IconStar = SpriteOf(creditsButton != null ? creditsButton.transform.Find("Icon") : null),
                IconSun = SpriteOf(step != null ? step.Find("Badge/Icon") : null),
                IconDie = SpriteOf(howToPanel != null ? howToPanel.transform.Find("Step3/Badge/Icon") : null),
                IconClock = SpriteOf(howToPanel != null ? howToPanel.transform.Find("Step6/Badge/Icon") : null),
                Font = howToStepTexts.Length > 0 && howToStepTexts[0] != null ? howToStepTexts[0].font : null,
                FontBold = howToTitleText != null ? howToTitleText.font : null,
            };
        }

        private static Sprite SpriteOf(Transform t)
        {
            var image = t != null ? t.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }
        public void ConfigureSetup(System.Collections.Generic.IEnumerable<DontTrustYourLuck.Presentation.Match.ParticipantConfig> seats,
            Action<System.Collections.Generic.IReadOnlyList<DontTrustYourLuck.Presentation.Match.ParticipantConfig>> selected)
        {
            if (_setup != null) return;
            var go = new GameObject("MatchSetup", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(rootGroup != null ? rootGroup.transform : transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _setup = go.AddComponent<MatchSetupView>();
            _setup.Configure(seats, chosen => { selected(chosen); BeginOutro(); }, ClosePanel, BuildSkin());
        }

        /// <summary>Raised once the Play outro finished (or immediately when the menu is skipped).</summary>
        public event Action StartGameRequested;

        public bool IsVisible => _phase != Phase.Hidden;
        public bool IsAnimating => _phase == Phase.Intro || _phase == Phase.Outro;
        public bool IsPanelOpen => _openPanel != null;
        public GameObject OpenPanel => _openPanel;
        public GameObject HowToPanel => howToPanel;
        public GameObject OptionsPanel => optionsPanel;
        public GameObject CreditsPanel => creditsPanel;
        public Button PlayButton => playButton;
        public Button HowToButton => howToButton;
        public Button OptionsButton => optionsButton;
        public Button CreditsButton => creditsButton;
        public Button[] BackButtons => backButtons;
        public RectTransform Logo => logo;
        public MenuText Text => text;

        public void Bind(CanvasGroup root, CanvasGroup backdropGroup, RectTransform logoRect, RectTransform cardRect,
            CanvasGroup cardCanvasGroup, CanvasGroup[] buttons, CanvasGroup footerGroup, CanvasGroup[] hud,
            Button play, Button howTo, Button options, Button credits,
            Text playText, Text howToText, Text optionsText, Text creditsText,
            Text menuTitle, Text footerHint, Text footerEvent)
        {
            rootGroup = root; backdrop = backdropGroup; logo = logoRect; card = cardRect; cardGroup = cardCanvasGroup;
            buttonGroups = buttons; footer = footerGroup; hudGroups = hud;
            playButton = play; howToButton = howTo; optionsButton = options; creditsButton = credits;
            playLabel = playText; howToLabel = howToText; optionsLabel = optionsText; creditsLabel = creditsText;
            menuTitleText = menuTitle; footerHintText = footerHint; footerEventText = footerEvent;
        }

        public void BindPanels(GameObject howTo, Text howToTitle, Text[] steps, GameObject options, Text optionsTitle,
            GameObject credits, Text creditsTitle, Text creditsBody, Button[] backs, Text[] backTexts)
        {
            howToPanel = howTo; howToTitleText = howToTitle; howToStepTexts = steps;
            optionsPanel = options; optionsTitleText = optionsTitle;
            creditsPanel = credits; creditsTitleText = creditsTitle; creditsBodyText = creditsBody;
            backButtons = backs; backLabels = backTexts;
        }

        private void Awake()
        {
            if (logo != null) _logoHome = logo.anchoredPosition;
            if (card != null) _cardHome = card.anchoredPosition;
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (howToButton != null) howToButton.onClick.AddListener(OpenHowTo);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
            foreach (var b in backButtons)
            {
                if (b != null) b.onClick.AddListener(ClosePanel);
            }
            ApplyLabels();
            ClosePanelsImmediate();
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (howToButton != null) howToButton.onClick.RemoveListener(OpenHowTo);
            if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
            if (creditsButton != null) creditsButton.onClick.RemoveListener(OpenCredits);
            foreach (var b in backButtons)
            {
                if (b != null) b.onClick.RemoveListener(ClosePanel);
            }
        }

        private void Start()
        {
            if (_entered) return; // SkipToGame() already ran (tests, deep links)
            if (showOnStart) Show();
            else SkipToGame();
        }

        public void ApplyLabels()
        {
            SetText(playLabel, text.play);
            SetText(howToLabel, text.howToPlay);
            SetText(optionsLabel, text.options);
            SetText(creditsLabel, text.credits);
            SetText(menuTitleText, text.menuTitle);
            SetText(footerHintText, text.footerHint);
            SetText(footerEventText, text.footerEvent);
            SetText(howToTitleText, text.howToTitle);
            for (int i = 0; i < howToStepTexts.Length; i++)
            {
                SetText(howToStepTexts[i], text.howToSteps != null && i < text.howToSteps.Length ? text.howToSteps[i] : string.Empty);
            }
            SetText(optionsTitleText, text.optionsTitle);
            SetText(creditsTitleText, text.creditsTitle);
            SetText(creditsBodyText, text.creditsBody);
            foreach (var l in backLabels) SetText(l, text.back);
        }

        private static void SetText(Text t, string value)
        {
            if (t != null) t.text = value;
        }

        // ------------------------------------------------------------------ flow

        /// <summary>Shows the menu with the entrance animation (scene start or "Main Menu" from the result screen).</summary>
        public void Show()
        {
            _entered = true;
            gameObject.SetActive(true);
            enabled = true;
            ClosePanelsImmediate();
            _phase = Phase.Intro;
            _t = 0f;
            _idleT = 0f;
            SetInteractable(false);
            ApplyIntro(0f);
        }

        /// <summary>Plays the exit animation, then asks for the match to start.</summary>
        public void Play()
        {
            if (_phase == Phase.Outro || _phase == Phase.Hidden) return;
            if (_setup != null)
            {
                if (_phase == Phase.Intro) { ApplyIntro(1f); _phase = Phase.Idle; SetInteractable(true); }
                OpenPanelObject(_setup.gameObject);
                _setup.Show();
                return;
            }
            BeginOutro();
        }

        private void BeginOutro()
        {
            ClosePanelsImmediate();
            if (_phase == Phase.Intro) ApplyIntro(1f); // clicked while still animating: settle first
            _phase = Phase.Outro;
            _t = 0f;
            SetInteractable(false);
        }

        /// <summary>Hides the menu at once and starts the match (no animation).</summary>
        public void SkipToGame()
        {
            _entered = true;
            ClosePanelsImmediate();
            FinishOutro();
        }

        public void OpenHowTo() => OpenPanelObject(howToPanel);
        public void OpenOptions() => OpenPanelObject(optionsPanel);
        public void OpenCredits() => OpenPanelObject(creditsPanel);

        public void ClosePanel()
        {
            if (_openPanel == null) return;
            if (_setup != null && _openPanel == _setup.gameObject && _setup.IsBoardSelection)
            { _setup.Back(); return; }
            _openPanel.SetActive(false);
            _openPanel = null;
            if (cardGroup != null) cardGroup.interactable = _phase == Phase.Idle;
        }

        private void OpenPanelObject(GameObject panel)
        {
            if (panel == null || _phase != Phase.Idle) return;
            ClosePanelsImmediate();
            _openPanel = panel;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            _panelT = 0f;
            ApplyPanelPop(0f);
            if (cardGroup != null) cardGroup.interactable = false; // modal: one panel at a time
        }

        private void ClosePanelsImmediate()
        {
            if (_setup != null) _setup.gameObject.SetActive(false);
            if (howToPanel != null) howToPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            _openPanel = null;
        }

        private void SetInteractable(bool value)
        {
            if (rootGroup != null)
            {
                rootGroup.interactable = value;
                rootGroup.blocksRaycasts = _phase != Phase.Hidden;
            }
            if (cardGroup != null) cardGroup.interactable = value && _openPanel == null;
        }

        // ------------------------------------------------------------------ animation

        private void Update()
        {
            // Clamped: the first frames after a scene load (or a WebGL hitch) must not skip the animation.
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
            switch (_phase)
            {
                case Phase.Intro:
                    if (SkipPressed()) _t = introDuration;
                    _t += dt;
                    ApplyIntro(Mathf.Clamp01(_t / introDuration));
                    if (_t >= introDuration)
                    {
                        _phase = Phase.Idle;
                        SetInteractable(true);
                    }
                    break;

                case Phase.Idle:
                    _idleT += dt;
                    if (logo != null)
                    {
                        // Gentle "printed card on a breeze" idle: bob and a tiny sway.
                        logo.anchoredPosition = _logoHome + new Vector2(0f, Mathf.Sin(_idleT * 2f * Mathf.PI / 3.2f) * 5f);
                        logo.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(_idleT * 2f * Mathf.PI / 5.5f) * 0.8f);
                    }
                    if (_openPanel != null && _panelT < panelPopDuration)
                    {
                        _panelT += dt;
                        ApplyPanelPop(Mathf.Clamp01(_panelT / panelPopDuration));
                    }
                    if (_openPanel != null && BackPressed()) ClosePanel();
                    break;

                case Phase.Outro:
                    _t += dt;
                    ApplyOutro(Mathf.Clamp01(_t / outroDuration));
                    if (_t >= outroDuration) FinishOutro();
                    break;

                default:
                    enabled = false;
                    break;
            }
        }

        private void FinishOutro()
        {
            _phase = Phase.Hidden;
            SetInteractable(false);
            SetHud(1f, true);
            if (rootGroup != null) rootGroup.alpha = 0f;
            gameObject.SetActive(false);
            StartGameRequested?.Invoke();
        }

        private void ApplyIntro(float p)
        {
            float t = p * introDuration;
            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.blocksRaycasts = true;
            }
            SetHud(0f, false);
            if (backdrop != null) backdrop.alpha = Ease01(t, 0f, 0.35f);

            if (logo != null)
            {
                float lp = Ease01(t, 0.1f, 0.85f);
                float back = EaseOutBack(lp);
                logo.anchoredPosition = _logoHome + new Vector2(0f, Mathf.LerpUnclamped(420f, 0f, back));
                float s = Mathf.LerpUnclamped(0.55f, 1f, back);
                logo.localScale = new Vector3(s, s, 1f);
                // Lands with a damped wobble, like a card dropped on the table.
                float wobble = lp >= 1f ? 0f : -9f * Mathf.Exp(-4.5f * lp) * Mathf.Cos(13f * lp);
                logo.localEulerAngles = new Vector3(0f, 0f, wobble);
                SetAlpha(logo, Ease01(t, 0.1f, 0.3f));
            }
            if (card != null)
            {
                float cp = EaseOutCubic(Ease01(t, 0.45f, 0.5f));
                card.anchoredPosition = _cardHome + new Vector2(Mathf.Lerp(420f, 0f, cp), 0f);
                if (cardGroup != null) cardGroup.alpha = cp;
            }
            for (int i = 0; i < buttonGroups.Length; i++)
            {
                var g = buttonGroups[i];
                if (g == null) continue;
                float bp = Ease01(t, 0.7f + i * 0.08f, 0.3f);
                g.alpha = bp;
                float s = Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(bp));
                g.transform.localScale = new Vector3(s, s, 1f);
            }
            if (footer != null) footer.alpha = Ease01(t, 1.05f, 0.3f);
        }

        private void ApplyOutro(float p)
        {
            float e = EaseInCubic(p);
            if (logo != null)
            {
                logo.anchoredPosition = _logoHome + new Vector2(0f, 70f * e);
                float s = 1f + 0.15f * e;
                logo.localScale = new Vector3(s, s, 1f);
                SetAlpha(logo, 1f - e);
            }
            if (card != null)
            {
                card.anchoredPosition = _cardHome + new Vector2(420f * e, 0f);
                if (cardGroup != null) cardGroup.alpha = 1f - e;
            }
            if (footer != null) footer.alpha = 1f - e;
            if (backdrop != null) backdrop.alpha = 1f - Mathf.Clamp01(p * 1.4f);
            SetHud(Mathf.Clamp01((p - 0.35f) / 0.65f), false); // the HUD unfolds around the board
        }

        private void ApplyPanelPop(float p)
        {
            if (_openPanel == null) return;
            float s = Mathf.LerpUnclamped(0.9f, 1f, EaseOutBack(p));
            _openPanel.transform.localScale = new Vector3(s, s, 1f);
            var group = _openPanel.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = Mathf.Clamp01(p * 1.5f);
        }

        private void SetHud(float alpha, bool interactive)
        {
            foreach (var g in hudGroups)
            {
                if (g == null) continue;
                g.alpha = alpha;
                g.interactable = interactive;
                g.blocksRaycasts = interactive;
            }
        }

        private static void SetAlpha(RectTransform rt, float alpha)
        {
            var g = rt.GetComponent<CanvasGroup>();
            if (g != null) g.alpha = alpha;
        }

        private static bool SkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            var keyboard = Keyboard.current;
            return (pointer != null && pointer.press.wasPressedThisFrame) ||
                   (keyboard != null && keyboard.anyKey.wasPressedThisFrame);
#else
            return false;
#endif
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        // Normalized progress of [start, start + duration] at time t.
        private static float Ease01(float t, float start, float duration) => Mathf.Clamp01((t - start) / duration);
        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        private static float EaseInCubic(float x) => x * x * x;

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
