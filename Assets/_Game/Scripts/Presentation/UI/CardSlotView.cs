using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Display-only data of one card in the hand panel (built by the director).</summary>
    public struct CardSlotDisplay
    {
        public string InstanceId;
        public string Name;
        public string Description;
        public Sprite Art;
        /// <summary>Playable right now according to the rules layer.</summary>
        public bool Available;
        public bool Selected;
    }

    /// <summary>One printed-card slot: art, name, short description, availability and selection feedback.</summary>
    public sealed class CardSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image highlight;
        [SerializeField] private Image art;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [Tooltip("Sprint 4: selected cards rise by this many pixels (child 'Lift' content).")]
        [SerializeField] private RectTransform liftTarget;
        [SerializeField] private float liftPixels = 10f;

        public event Action Clicked;
        /// <summary>Pointer entered (true) or left (false) the card: the hand shows the description tooltip.</summary>
        public event Action<bool> Hovered;

        public Button Button => button;
        public bool IsSelected => highlight != null && highlight.enabled;
        public bool IsAvailable => button != null && button.interactable;
        public string NameText => nameText != null ? nameText.text : string.Empty;
        public string InstanceId { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public Sprite FrameSprite => background != null ? background.sprite : null;
        public Font Font => nameText != null ? nameText.font : null;

        public void Bind(Button slotButton, Image backgroundImage, Image highlightImage, Image artImage, Text name, Text description)
        {
            button = slotButton; background = backgroundImage; highlight = highlightImage;
            art = artImage; nameText = name; descriptionText = description;
        }

        public void BindLift(RectTransform target, float pixels)
        {
            liftTarget = target;
            liftPixels = pixels;
        }

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        private void OnClick() => Clicked?.Invoke();
        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(true);
        public void OnPointerExit(PointerEventData eventData) => Hovered?.Invoke(false);

        public void Show(CardSlotDisplay card)
        {
            gameObject.SetActive(true);
            InstanceId = card.InstanceId;
            nameText.text = card.Name;
            Description = card.Description ?? string.Empty;
            // The printed card is too small for the rules text: it lives in the hand's tooltip instead,
            // and the name takes the freed space under the art.
            descriptionText.text = Description;
            if (descriptionText.enabled)
            {
                descriptionText.enabled = false;
                var n = nameText.rectTransform;
                var d = descriptionText.rectTransform;
                n.anchorMin = new Vector2(n.anchorMin.x, d.anchorMin.y);
                n.offsetMin = new Vector2(n.offsetMin.x, d.offsetMin.y);
                nameText.resizeTextForBestFit = true;
                nameText.resizeTextMinSize = 12;
                nameText.resizeTextMaxSize = Mathf.Max(12, nameText.fontSize);
            }
            art.sprite = card.Art;
            art.enabled = card.Art != null;
            highlight.enabled = card.Selected;
            if (liftTarget != null) liftTarget.anchoredPosition = new Vector2(0f, card.Selected ? liftPixels : 0f);
            button.interactable = card.Available;
            // Unavailable cards look faded (aged, greyed paper) but stay readable.
            background.color = card.Available || card.Selected ? HudPalette.Card : HudPalette.CardDisabled;
            art.color = card.Available || card.Selected ? Color.white : new Color(0.75f, 0.72f, 0.66f, 0.8f);
            nameText.color = descriptionText.color = card.Available || card.Selected ? HudPalette.Ink : HudPalette.InkFaded;
        }

        public void Hide()
        {
            InstanceId = null;
            gameObject.SetActive(false);
        }
    }
}
