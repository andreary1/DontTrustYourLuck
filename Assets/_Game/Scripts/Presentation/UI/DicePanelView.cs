using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Roll button, two die faces and their choose buttons. Raises intents only.</summary>
    public sealed class DicePanelView : MonoBehaviour
    {
        [SerializeField] private Button rollButton;
        [SerializeField] private Text rollLabel;
        [SerializeField] private Text[] dieValues = new Text[2];
        [SerializeField] private Image[] dieFaces = new Image[2];
        [SerializeField] private Button[] chooseButtons = new Button[2];
        [SerializeField] private Text[] chooseLabels = new Text[2];
        [SerializeField] private Text statusText;

        public event Action RollClicked;
        public event Action<int> ChooseClicked;

        public Button RollButton => rollButton;
        public IReadOnlyList<Button> ChooseButtons => chooseButtons;
        public string DieText(int index) => dieValues[index].text;

        public void Bind(Button roll, Text rollText, Text[] values, Image[] faces, Button[] choose, Text[] chooseText, Text status)
        {
            rollButton = roll; rollLabel = rollText;
            dieValues = values; dieFaces = faces;
            chooseButtons = choose; chooseLabels = chooseText;
            statusText = status;
        }

        private void Awake()
        {
            rollButton.onClick.AddListener(OnRoll);
            for (int i = 0; i < chooseButtons.Length; i++)
            {
                int index = i;
                chooseButtons[i].onClick.AddListener(() => ChooseClicked?.Invoke(index));
            }
        }

        private void OnDestroy()
        {
            if (rollButton != null) rollButton.onClick.RemoveAllListeners();
            foreach (var b in chooseButtons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
        }

        private void OnRoll() => RollClicked?.Invoke();

        public void ApplyLabels(HudText text)
        {
            rollLabel.text = text.rollButton;
            string[] names = { text.dieA, text.dieB };
            for (int i = 0; i < chooseLabels.Length; i++)
            {
                chooseLabels[i].text = string.Format(text.chooseButton, i < names.Length ? names[i] : (i + 1).ToString());
            }
        }

        /// <param name="values">Dice to show; null shows empty faces.</param>
        /// <param name="selectedIndex">Die already chosen (highlighted), or -1.</param>
        public void Show(HudText text, IReadOnlyList<int> values, bool canRoll, bool canChoose, int selectedIndex, string status)
        {
            rollButton.interactable = canRoll;
            for (int i = 0; i < dieValues.Length; i++)
            {
                bool has = values != null && i < values.Count;
                dieValues[i].text = has ? values[i].ToString() : text.emptyValue;
                chooseButtons[i].interactable = canChoose && has;
                dieFaces[i].color = i == selectedIndex ? HudPalette.Lucky : HudPalette.Card;
            }
            statusText.text = status ?? string.Empty;
        }
    }
}
