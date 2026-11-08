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

        public event Action PlayAgainClicked;

        public bool IsVisible => root != null && root.activeSelf;
        public Button PlayAgainButton => playAgainButton;
        public string Title => titleText.text;

        public void Bind(GameObject rootObject, Text title, Text reason, Image[] swatches, Button playAgain, Text playAgainText)
        {
            root = rootObject; titleText = title; reasonText = reason;
            winnerSwatches = swatches; playAgainButton = playAgain; playAgainLabel = playAgainText;
        }

        private void Awake() => playAgainButton.onClick.AddListener(OnPlayAgain);

        private void OnDestroy()
        {
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
        }

        private void OnPlayAgain() => PlayAgainClicked?.Invoke();

        public void ApplyLabels(HudText text) => playAgainLabel.text = text.playAgainButton;

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
