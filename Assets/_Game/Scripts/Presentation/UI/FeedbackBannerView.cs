using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>
    /// Short non-blocking banner (turn change, card played, unlucky reveal). Never intercepts clicks and never
    /// delays input: it only fades in and out. Update runs only while visible.
    /// </summary>
    public sealed class FeedbackBannerView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image background;
        [SerializeField] private Image accent;
        [SerializeField] private Image icon;
        [SerializeField] private Text title;
        [SerializeField] private Text subtitle;
        [Min(0.01f)] [SerializeField] private float fadeTime = 0.12f;

        private float _shownAt;
        private float _duration;

        public bool IsVisible => group != null && group.alpha > 0.01f;
        public string TitleText => title != null ? title.text : string.Empty;

        public void Bind(CanvasGroup canvasGroup, RectTransform panelRect, Image backgroundImage, Image accentImage,
            Image iconImage, Text titleText, Text subtitleText)
        {
            group = canvasGroup; panel = panelRect; background = backgroundImage; accent = accentImage;
            icon = iconImage; title = titleText; subtitle = subtitleText;
        }

        private void Awake()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            enabled = false;
        }

        public void Show(string titleValue, string subtitleValue, Color accentColor, Sprite iconSprite, float duration)
        {
            if (group == null || duration <= 0f) return;
            title.text = titleValue ?? string.Empty;
            subtitle.text = subtitleValue ?? string.Empty;
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitleValue));
            if (accent != null) accent.color = accentColor;
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }
            _shownAt = Time.unscaledTime;
            _duration = duration;
            enabled = true;
            Tick();
        }

        public void Hide()
        {
            if (group != null) group.alpha = 0f;
            enabled = false;
        }

        private void Update() => Tick();

        private void Tick()
        {
            float t = Time.unscaledTime - _shownAt;
            if (t >= _duration)
            {
                Hide();
                return;
            }
            float fadeIn = Mathf.Clamp01(t / fadeTime);
            float fadeOut = Mathf.Clamp01((_duration - t) / fadeTime);
            float a = Mathf.Min(fadeIn, fadeOut);
            group.alpha = a;
            if (panel != null)
            {
                float pop = 1f + 0.08f * (1f - fadeIn);
                panel.localScale = new Vector3(pop, pop, 1f);
            }
        }
    }
}
