using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>
    /// Root of the match HUD prefab. Only displays data and forwards user intents (roll, choose, play again);
    /// it contains no rules. The director decides what is enabled.
    /// </summary>
    public sealed class GameHUD : MonoBehaviour
    {
        [SerializeField] private HudText text = new HudText();
        [SerializeField] private MatchInfoView matchInfo;
        [SerializeField] private PlayerStatusView playerStatus;
        [SerializeField] private DicePanelView dicePanel;
        [SerializeField] private MessagePanelView messages;
        [SerializeField] private ResultScreenView resultScreen;
        [Tooltip("Hand panel (Sprint 3). Optional: without it the match runs dice-only in the HUD.")]
        [SerializeField] private CardHandView cardHand;
        [Header("Sprint 4 (optional)")]
        [SerializeField] private FeedbackBannerView banner;
        [SerializeField] private AudioSettingsView audioSettings;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [Header("Main menu (optional)")]
        [SerializeField] private MainMenuView mainMenu;
        [SerializeField] private AudioSettingsView menuAudioSettings;

        [Header("Board viewport (areas covered by the HUD)")]
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform leftColumn;
        [SerializeField] private RectTransform rightColumn;
        [SerializeField] private RectTransform bottomBar;

        public event Action RollRequested;
        public event Action<int> DieChosen;
        public event Action PlayAgainRequested;
        /// <summary>Hand slot index clicked.</summary>
        public event Action<int> CardSlotClicked;
        /// <summary>Target player id clicked.</summary>
        public event Action<int> CardTargetClicked;
        public event Action CardPlayRequested;
        public event Action CardCancelRequested;
        /// <summary>The main menu asked for a match (Play). The director starts or restarts it.</summary>
        public event Action StartGameRequested;

        public CardHandView CardHand => cardHand;
        public FeedbackBannerView Banner => banner;
        public AudioSettingsView AudioSettings => audioSettings;
        public MainMenuView MainMenu => mainMenu;

        /// <summary>Every audio settings view (HUD popover and menu options panel), so all stay in sync.</summary>
        public IReadOnlyList<AudioSettingsView> AudioSettingsViews
        {
            get
            {
                if (_audioViews == null)
                {
                    _audioViews = new List<AudioSettingsView>(2);
                    if (audioSettings != null) _audioViews.Add(audioSettings);
                    if (menuAudioSettings != null) _audioViews.Add(menuAudioSettings);
                }
                return _audioViews;
            }
        }

        private List<AudioSettingsView> _audioViews;

        public HudText Text => text;
        public MatchInfoView MatchInfo => matchInfo;
        public PlayerStatusView PlayerStatus => playerStatus;
        public DicePanelView DicePanel => dicePanel;
        public MessagePanelView Messages => messages;
        public ResultScreenView ResultScreen => resultScreen;

        public void Bind(MatchInfoView info, PlayerStatusView players, DicePanelView dice, MessagePanelView log,
            ResultScreenView result, RectTransform top, RectTransform left, RectTransform right)
        {
            matchInfo = info; playerStatus = players; dicePanel = dice; messages = log; resultScreen = result;
            topBar = top; leftColumn = left; rightColumn = right;
        }

        public void BindCards(CardHandView hand, RectTransform bottom)
        {
            cardHand = hand;
            bottomBar = bottom;
        }

        public void BindSprint4(FeedbackBannerView bannerView, AudioSettingsView audioView, Text title, Text subtitle)
        {
            banner = bannerView;
            audioSettings = audioView;
            titleText = title;
            subtitleText = subtitle;
        }

        public void BindMainMenu(MainMenuView menu, AudioSettingsView menuAudio)
        {
            mainMenu = menu;
            menuAudioSettings = menuAudio;
            _audioViews = null;
        }

        private void Awake()
        {
            ApplyLabels();
            dicePanel.RollClicked += OnRoll;
            dicePanel.ChooseClicked += OnChoose;
            resultScreen.PlayAgainClicked += OnPlayAgain;
            resultScreen.MenuClicked += OnMainMenu;
            if (mainMenu != null) mainMenu.StartGameRequested += OnStartGame;
            resultScreen.Hide();
            if (cardHand != null)
            {
                cardHand.CardClicked += OnCardClicked;
                cardHand.TargetClicked += OnTargetClicked;
                cardHand.PlayClicked += OnCardPlay;
                cardHand.CancelClicked += OnCardCancel;
            }
        }

        private void OnDestroy()
        {
            if (dicePanel != null)
            {
                dicePanel.RollClicked -= OnRoll;
                dicePanel.ChooseClicked -= OnChoose;
            }
            if (resultScreen != null)
            {
                resultScreen.PlayAgainClicked -= OnPlayAgain;
                resultScreen.MenuClicked -= OnMainMenu;
            }
            if (mainMenu != null) mainMenu.StartGameRequested -= OnStartGame;
            if (cardHand != null)
            {
                cardHand.CardClicked -= OnCardClicked;
                cardHand.TargetClicked -= OnTargetClicked;
                cardHand.PlayClicked -= OnCardPlay;
                cardHand.CancelClicked -= OnCardCancel;
            }
        }

        public void ApplyLabels()
        {
            matchInfo.ApplyLabels(text);
            playerStatus.ApplyLabels(text);
            dicePanel.ApplyLabels(text);
            messages.ApplyLabels(text);
            resultScreen.ApplyLabels(text);
            if (cardHand != null) cardHand.ApplyLabels(text);
            if (audioSettings != null) audioSettings.ApplyLabels(text);
            if (menuAudioSettings != null) menuAudioSettings.ApplyLabels(text);
            if (mainMenu != null) mainMenu.ApplyLabels();
            if (titleText != null)
            {
                titleText.text = text.gameTitle;
                // Shrinks to one line inside the plaque instead of wrapping over the city subtitle.
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.verticalOverflow = VerticalWrapMode.Truncate;
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = 12;
            }
            if (subtitleText != null) subtitleText.text = text.gameSubtitle;
        }

        private void OnCardClicked(int slot) => CardSlotClicked?.Invoke(slot);
        private void OnTargetClicked(int playerId) => CardTargetClicked?.Invoke(playerId);
        private void OnCardPlay() => CardPlayRequested?.Invoke();
        private void OnCardCancel() => CardCancelRequested?.Invoke();

        private void OnRoll() => RollRequested?.Invoke();
        private void OnChoose(int index) => DieChosen?.Invoke(index);
        private void OnPlayAgain() => PlayAgainRequested?.Invoke();
        private void OnStartGame() => StartGameRequested?.Invoke();

        private void OnMainMenu()
        {
            if (mainMenu != null) mainMenu.Show();
        }

        /// <summary>
        /// Normalized screen rect not covered by the HUD columns/top bar, so the board camera can frame
        /// the board inside it. Assumes a Screen Space - Overlay canvas (rect corners are screen pixels).
        /// </summary>
        public Rect GetBoardViewport()
        {
            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            float xMin = leftColumn != null ? ScreenRect(leftColumn).xMax / w : 0f;
            float xMax = rightColumn != null ? ScreenRect(rightColumn).xMin / w : 1f;
            float yMax = topBar != null ? ScreenRect(topBar).yMin / h : 1f;
            float yMin = bottomBar != null && bottomBar.gameObject.activeInHierarchy ? ScreenRect(bottomBar).yMax / h : 0f;
            xMin = Mathf.Clamp01(xMin);
            xMax = Mathf.Clamp(xMax, xMin + 0.1f, 1f);
            yMax = Mathf.Clamp(yMax, 0.2f, 1f);
            yMin = Mathf.Clamp(yMin, 0f, yMax - 0.15f);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static Rect ScreenRect(RectTransform rt)
        {
            rt.GetWorldCorners(Corners);
            return Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y);
        }
    }
}
