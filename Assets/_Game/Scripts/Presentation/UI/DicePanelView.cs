using System;
using System.Collections.Generic;
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
        [Tooltip("Optional (Sprint 4): destination preview under each die (\"-> 17\").")]
        [SerializeField] private Text[] destinationTexts = Array.Empty<Text>();

        public event Action RollClicked;
        public event Action<int> ChooseClicked;
        /// <summary>The roll flicker ended (sound/feedback hook).</summary>
        public event Action RollAnimationFinished;

        private float _rollUntil;
        private float _nextFlicker;
        private readonly int[] _shownValues = { 0, 0 };
        private bool _hasValues;
        private string _emptyValue = "-";

        public Button RollButton => rollButton;
        public IReadOnlyList<Button> ChooseButtons => chooseButtons;
        public string DieText(int index) => dieValues[index].text;
        public bool IsRolling { get; private set; }
        public string DestinationText(int index) =>
            index >= 0 && index < destinationTexts.Length && destinationTexts[index] != null ? destinationTexts[index].text : string.Empty;

        public void Bind(Button roll, Text rollText, Text[] values, Image[] faces, Button[] choose, Text[] chooseText, Text status)
        {
            rollButton = roll; rollLabel = rollText;
            dieValues = values; dieFaces = faces;
            chooseButtons = choose; chooseLabels = chooseText;
            statusText = status;
        }

        public void BindDestinations(Text[] destinations) => destinationTexts = destinations ?? Array.Empty<Text>();

        private void Awake()
        {
            rollButton.onClick.AddListener(OnRoll);
            for (int i = 0; i < chooseButtons.Length; i++)
            {
                int index = i;
                chooseButtons[i].onClick.AddListener(() => ChooseClicked?.Invoke(index));
            }
            enabled = false; // Update only runs during the roll flicker
            EqualizeDieColumns();
        }

        /// <summary>
        /// Both die columns get the same width whatever they show: without this the row sizes each column by its
        /// text, so "-> 1 DANGER!" squeezed the other die. Long previews shrink instead of spilling over.
        /// </summary>
        private void EqualizeDieColumns()
        {
            for (int i = 0; i < dieFaces.Length; i++)
            {
                var column = dieFaces[i] != null ? dieFaces[i].transform.parent : null;
                if (column == null || column.parent == null || column.parent.GetComponent<HorizontalLayoutGroup>() == null) continue;
                var le = column.GetComponent<LayoutElement>();
                if (le == null) le = column.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 0f;
                le.preferredWidth = 0f;
                le.flexibleWidth = 1f;
            }
            foreach (var t in destinationTexts)
            {
                if (t == null) continue;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.resizeTextForBestFit = true;
                t.resizeTextMaxSize = t.fontSize;
                t.resizeTextMinSize = Mathf.Min(12, t.fontSize);
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
            _emptyValue = text.emptyValue;
            rollButton.interactable = canRoll;
            _hasValues = values != null;
            for (int i = 0; i < dieValues.Length; i++)
            {
                bool has = values != null && i < values.Count;
                _shownValues[Mathf.Min(i, 1)] = has ? values[i] : 0;
                if (!IsRolling) dieValues[i].text = has ? values[i].ToString() : text.emptyValue;
                chooseButtons[i].interactable = canChoose && has && !IsRolling;
                dieFaces[i].color = i == selectedIndex ? HudPalette.Lucky : HudPalette.Card;
            }
            statusText.text = status ?? string.Empty;
        }

        /// <summary>
        /// Destination preview per die: "-> 17", gold when it lands on the lucky number, red with a warning when
        /// it lands on the unlucky number. Null hides the preview. Display only: the rules resolve the real move.
        /// </summary>
        public void SetRollLabel(string label) { if (rollLabel != null) rollLabel.text = label; }

        public void ShowDestinations(HudText text, IReadOnlyList<int> destinations, int lucky, int unlucky, int sticky = -1)
        {
            for (int i = 0; i < destinationTexts.Length; i++)
            {
                var label = destinationTexts[i];
                if (label == null) continue;
                bool has = destinations != null && i < destinations.Count && !IsRolling;
                if (!has)
                {
                    label.text = string.Empty;
                    continue;
                }
                int tile = destinations[i];
                bool danger = tile == unlucky || tile == sticky;
                string format = tile == lucky ? text.destinationLucky : danger ? text.destinationUnlucky : text.destinationTile;
                label.text = string.Format(format, tile);
                label.color = tile == lucky ? HudPalette.LuckyDark : danger ? HudPalette.Danger : HudPalette.Ink;
            }
        }

        /// <summary>Cosmetic flicker of random faces; the real values are shown when it ends.</summary>
        public void PlayRollAnimation(float duration)
        {
            if (duration <= 0f) return;
            _rollUntil = Time.unscaledTime + duration + 0.5f; // safety stop if nobody calls StopRollAnimation
            _nextFlicker = 0f;
            IsRolling = true;
            foreach (var b in chooseButtons) b.interactable = false;
            foreach (var d in destinationTexts)
            {
                if (d != null) d.text = string.Empty;
            }
            enabled = true;
        }

        /// <summary>Ends the flicker and shows the stored real values. Buttons are restored by the next Show().</summary>
        public void StopRollAnimation()
        {
            bool wasRolling = IsRolling;
            IsRolling = false;
            enabled = false;
            for (int i = 0; i < dieValues.Length; i++)
            {
                int v = _shownValues[Mathf.Min(i, 1)];
                dieValues[i].text = _hasValues && v > 0 ? v.ToString() : _emptyValue;
                dieValues[i].rectTransform.localRotation = Quaternion.identity;
            }
            if (wasRolling) RollAnimationFinished?.Invoke();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (!IsRolling || now >= _rollUntil)
            {
                StopRollAnimation();
                return;
            }
            if (now < _nextFlicker) return;
            _nextFlicker = now + 0.06f;
            for (int i = 0; i < dieValues.Length; i++)
            {
                dieValues[i].text = UnityEngine.Random.Range(1, 7).ToString(); // display only, not gameplay RNG
                dieValues[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-12f, 12f));
            }
        }
    }
}
