using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>End-of-match overlay with winner(s), reason and Play Again.</summary>
    public sealed class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text reasonText;
        [SerializeField] private Image[] winnerSwatches = new Image[4];
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Text playAgainLabel;

        [SerializeField] private Button menuButton;
        [SerializeField] private Text menuLabel;

        public event Action PlayAgainClicked;
        public event Action MenuClicked;

        public bool IsVisible => root != null && root.activeSelf;
        public Button PlayAgainButton => playAgainButton;
        public Button MenuButton => menuButton;
        public string Title => titleText.text;

        public void Bind(GameObject rootObject, Text title, Text reason, Image[] swatches, Button playAgain, Text playAgainText)
        {
            root = rootObject; titleText = title; reasonText = reason;
            winnerSwatches = swatches; playAgainButton = playAgain; playAgainLabel = playAgainText;
        }

        /// <summary>Optional "Main Menu" button (main menu pass).</summary>
        public void BindMenuButton(Button button, Text label)
        {
            menuButton = button;
            menuLabel = label;
        }

        private void Awake()
        {
            playAgainButton.onClick.AddListener(OnPlayAgain);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);
        }

        private void OnDestroy()
        {
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
            if (menuButton != null) menuButton.onClick.RemoveListener(OnMenu);
        }

        private void OnPlayAgain() => PlayAgainClicked?.Invoke();
        private void OnMenu() => MenuClicked?.Invoke();

        public void ApplyLabels(HudText text)
        {
            playAgainLabel.text = text.playAgainButton;
            if (menuLabel != null) menuLabel.text = text.mainMenuButton;
        }

        public void Show(string title, string reason, IReadOnlyList<Color> winnerColors)
        {
            titleText.text = title;
            reasonText.text = reason;
            for (int i = 0; i < winnerSwatches.Length; i++)
            {
                bool on = winnerColors != null && i < winnerColors.Count;
                winnerSwatches[i].gameObject.SetActive(on);
                if (on) winnerSwatches[i].color = winnerColors[i];
            }
            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
