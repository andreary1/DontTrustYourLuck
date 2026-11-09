using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>A player that can be picked as a card target.</summary>
    public struct CardTargetDisplay
    {
        public int PlayerId;
        public string Name;
        public Color Color;
    }

    /// <summary>
    /// Hand panel: up to three card slots, target buttons (Swap), Play and Cancel. Raises intents only; the
    /// director validates through the rules layer and decides what is enabled.
    /// </summary>
    public sealed class CardHandView : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text hint;
        [SerializeField] private Text emptyText;
        [SerializeField] private List<CardSlotView> slots = new List<CardSlotView>();
        [SerializeField] private GameObject targetRow;
        [SerializeField] private List<Button> targetButtons = new List<Button>();
        [SerializeField] private List<Text> targetLabels = new List<Text>();
        [SerializeField] private List<Image> targetSwatches = new List<Image>();
        [SerializeField] private Button playButton;
        [SerializeField] private Text playLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text cancelLabel;

        private readonly int[] _targetIds = { -1, -1, -1, -1 };

        public event Action<int> CardClicked;
        public event Action<int> TargetClicked;
        public event Action PlayClicked;
        public event Action CancelClicked;

        public IReadOnlyList<CardSlotView> Slots => slots;
        public IReadOnlyList<Button> TargetButtons => targetButtons;
        public Button PlayButton => playButton;
        public Button CancelButton => cancelButton;
        public string TitleText => title.text;
        public string HintText => hint.text;
        public bool TargetsVisible => targetRow != null && targetRow.activeSelf;

        /// <summary>Player id shown on a target button, or -1 when the button is hidden.</summary>
        public int TargetIdAt(int index) => index >= 0 && index < _targetIds.Length ? _targetIds[index] : -1;

        public void Bind(Text titleText, Text hintText, Text empty, List<CardSlotView> slotViews, GameObject targets,
            List<Button> targetButtonList, List<Text> targetLabelList, List<Image> targetSwatchList,
            Button play, Text playText, Button cancel, Text cancelText)
        {
            title = titleText; hint = hintText; emptyText = empty; slots = slotViews;
            targetRow = targets; targetButtons = targetButtonList; targetLabels = targetLabelList; targetSwatches = targetSwatchList;
            playButton = play; playLabel = playText; cancelButton = cancel; cancelLabel = cancelText;
        }

        private void Awake()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                int index = i;
                slots[i].Clicked += () => CardClicked?.Invoke(index);
            }
            for (int i = 0; i < targetButtons.Count; i++)
            {
                int index = i;
                targetButtons[i].onClick.AddListener(() =>
                {
                    if (_targetIds[index] >= 0) TargetClicked?.Invoke(_targetIds[index]);
                });
            }
            playButton.onClick.AddListener(() => PlayClicked?.Invoke());
            cancelButton.onClick.AddListener(() => CancelClicked?.Invoke());
        }

        private void OnDestroy()
        {
            foreach (var b in targetButtons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
            if (playButton != null) playButton.onClick.RemoveAllListeners();
            if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        }

        public void ApplyLabels(HudText text)
        {
            title.text = text.cardsTitle;
            playLabel.text = text.playCardButton;
            cancelLabel.text = text.cancelCardButton;
            emptyText.text = text.noCards;
        }

        /// <param name="targets">Eligible targets to show; null hides the target row.</param>
        /// <param name="selectedTarget">Highlighted target id, or -1.</param>
        public void Show(string titleValue, IReadOnlyList<CardSlotDisplay> cards, string hintValue,
            IReadOnlyList<CardTargetDisplay> targets, int selectedTarget, bool canPlay, bool canCancel)
        {
            title.text = titleValue;
            hint.text = hintValue ?? string.Empty;
            int count = cards?.Count ?? 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i < count) slots[i].Show(cards[i]);
                else slots[i].Hide();
            }
            emptyText.enabled = count == 0;

            bool showTargets = targets != null;
            targetRow.SetActive(showTargets);
            for (int i = 0; i < targetButtons.Count; i++)
            {
                bool has = showTargets && i < targets.Count;
                _targetIds[i] = has ? targets[i].PlayerId : -1;
                targetButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                targetLabels[i].text = targets[i].Name;
                targetSwatches[i].color = targets[i].Color;
                targetButtons[i].image.color = targets[i].PlayerId == selectedTarget ? HudPalette.Lucky : HudPalette.Card;
            }

            playButton.interactable = canPlay;
            cancelButton.interactable = canCancel;
        }
    }
}
