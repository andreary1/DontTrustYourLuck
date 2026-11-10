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
                slots[i].Hovered += over => { _hovered = over ? index : (_hovered == index ? -1 : _hovered); RefreshTooltip(); };
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
            RefreshTooltip();
        }

        // ------------------------------------------------------------------ description tooltip

        private const float TooltipWidth = 280f, TooltipHeight = 104f;
        private int _hovered = -1;
        private RectTransform _tooltip;
        private Text _tooltipTitle, _tooltipBody;

        /// <summary>Slot whose rules text is on show (hovered first, else the selected card), or -1.</summary>
        public int TooltipSlot { get; private set; } = -1;
        public string TooltipText => _tooltipBody != null && TooltipSlot >= 0 ? _tooltipBody.text : string.Empty;

        private void RefreshTooltip()
        {
            int slot = -1;
            if (_hovered >= 0 && _hovered < slots.Count && slots[_hovered].gameObject.activeSelf) slot = _hovered;
            else
                for (int i = 0; i < slots.Count; i++)
                    if (slots[i].gameObject.activeSelf && slots[i].IsSelected) { slot = i; break; }
            TooltipSlot = slot;
            if (slot < 0 || string.IsNullOrEmpty(slots[slot].Description))
            {
                if (_tooltip != null) _tooltip.gameObject.SetActive(false);
                return;
            }
            EnsureTooltip(slots[slot]);
            _tooltipTitle.text = slots[slot].NameText;
            _tooltipBody.text = slots[slot].Description;
            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();

            // Above the card, kept inside the hand panel's horizontal extent.
            var slotRt = (RectTransform)slots[slot].transform;
            var parent = (RectTransform)_tooltip.parent;
            var corners = new Vector3[4];
            slotRt.GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
            Vector2 local = parent.InverseTransformPoint(topCenter);
            var bounds = parent.rect;
            float x = Mathf.Clamp(local.x, bounds.xMin + TooltipWidth / 2f + 6f, bounds.xMax - TooltipWidth / 2f - 6f);
            _tooltip.anchoredPosition = Vector2.zero;
            _tooltip.localPosition = new Vector3(x, local.y + 18f, 0f);
        }

        private void EnsureTooltip(CardSlotView source)
        {
            if (_tooltip != null) return;
            var go = new GameObject("CardTooltip", typeof(RectTransform));
            go.layer = gameObject.layer;
            _tooltip = (RectTransform)go.transform;
            _tooltip.SetParent(transform, false);
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.pivot = new Vector2(0.5f, 0f);
            _tooltip.sizeDelta = new Vector2(TooltipWidth, TooltipHeight);
            go.AddComponent<LayoutElement>().ignoreLayout = true; // the hand panel may use a layout group
            var frame = go.AddComponent<Image>();
            frame.sprite = source.FrameSprite;
            frame.type = frame.sprite != null && frame.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            frame.color = frame.sprite != null ? Color.white : HudPalette.Card;
            frame.raycastTarget = false; // never steals the hover from the card under it
            // Same faces as the printed card: its name font (bold face) for the title, the hint's for the body.
            var bodyFont = hint != null && hint.font != null ? hint.font : title.font;
            var titleFont = source.Font != null ? source.Font : bodyFont;
            var titleStyle = titleFont != bodyFont ? FontStyle.Normal : FontStyle.Bold;
            _tooltipTitle = MakeTooltipText(_tooltip, titleFont, 17, titleStyle, new Vector2(12f, -40f), new Vector2(-12f, -10f));
            _tooltipBody = MakeTooltipText(_tooltip, bodyFont, 15, FontStyle.Normal, new Vector2(12f, 10f), new Vector2(-12f, -40f));
            _tooltipBody.alignment = TextAnchor.UpperLeft;
            _tooltipBody.resizeTextMinSize = 13;
        }

        private static Text MakeTooltipText(RectTransform parent, Font font, int size, FontStyle style, Vector2 min, Vector2 max)
        {
            var go = new GameObject(min.y < 0f ? "Title" : "Body", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, min.y < 0f ? 1f : 0f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = min;
            rt.offsetMax = max;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = HudPalette.Ink;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 13;
            t.resizeTextMaxSize = size;
            return t;
        }
    }
}
