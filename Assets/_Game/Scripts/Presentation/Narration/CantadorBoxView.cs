using System.Collections.Generic;
using DontTrustYourLuck.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.Narration
{
    /// <summary>
    /// Paper speech box of the Cantador at the top of the HUD. Never blocks input (no raycasts) and never delays
    /// the match: lines queue up briefly and fade. Update runs only while something is showing or queued.
    /// Created at runtime (RuntimeUi), like the series views.
    /// </summary>
    public sealed class CantadorBoxView : MonoBehaviour
    {
        private const int MaxQueued = 2;
        private const float FadeTime = 0.2f;

        [Min(0.5f)] [SerializeField] private float minSeconds = 2.6f;
        [Min(0f)] [SerializeField] private float secondsPerChar = 0.035f;
        [Min(0.5f)] [SerializeField] private float maxSeconds = 5.5f;
        [Min(0.5f)] [SerializeField] private float finaleSeconds = 8f;

        private readonly List<CantadorLine> _queue = new List<CantadorLine>(MaxQueued + 1);
        private CanvasGroup _group;
        private RectTransform _card;
        private Text _speaker;
        private Text _body;
        private bool _showing;
        private CantadorLine _current;
        private float _shownAt;
        private float _duration;

        public bool IsShowing => _showing;
        public string CurrentText => _showing ? _current.Text : string.Empty;
        public int QueuedCount => _queue.Count;

        public static CantadorBoxView Create(RectTransform parent, Font font, string speaker)
        {
            var rt = RuntimeUi.Rect("CantadorBox", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -88f);
            rt.sizeDelta = new Vector2(500f, 10f);

            var view = rt.gameObject.AddComponent<CantadorBoxView>();
            view._group = rt.gameObject.AddComponent<CanvasGroup>();
            view._group.alpha = 0f;
            view._group.blocksRaycasts = false;
            view._group.interactable = false;

            var card = RuntimeUi.Image("Card", rt, HudPalette.Cream);
            view._card = card.rectTransform;
            view._card.anchorMin = new Vector2(0f, 1f);
            view._card.anchorMax = new Vector2(1f, 1f);
            view._card.pivot = new Vector2(0.5f, 1f);
            view._card.anchoredPosition = Vector2.zero;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HudPalette.Ink;
            outline.effectDistance = new Vector2(2f, -2f);
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(4f, -5f);
            var column = RuntimeUi.Column(card, 2f, 10);
            column.padding = new RectOffset(18, 18, 8, 12);
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            view._speaker = RuntimeUi.Text("Speaker", card.transform, font, 14, HudPalette.DangerDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            view._speaker.text = speaker;
            view._body = RuntimeUi.Text("Line", card.transform, font, 21, HudPalette.Ink, TextAnchor.MiddleCenter);
            view._body.lineSpacing = 1.05f;
            view._body.supportRichText = true;

            view.enabled = false;
            return view;
        }

        /// <summary>Queues a line. A higher-priority line cuts a lower one short; bumps are dropped when busy.</summary>
        public void Say(CantadorLine line)
        {
            if (string.IsNullOrEmpty(line.Text)) return;
            if (_showing && line.Priority > _current.Priority)
            {
                _queue.Insert(0, line);
                StartNext();
                return;
            }
            if (_showing && line.Priority == 0) return; // flavor only; never pile up bump lines
            _queue.Add(line);
            while (_queue.Count > MaxQueued) DropLowest();
            if (!_showing) StartNext();
        }

        public void Clear()
        {
            _queue.Clear();
            _showing = false;
            if (_group != null) _group.alpha = 0f;
            enabled = false;
        }

        private void DropLowest()
        {
            int worst = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                if (_queue[i].Priority < _queue[worst].Priority) worst = i;
            }
            _queue.RemoveAt(worst);
        }

        private void StartNext()
        {
            if (_queue.Count == 0)
            {
                Clear();
                return;
            }
            _current = _queue[0];
            _queue.RemoveAt(0);
            _body.text = _current.Text;
            _duration = _current.IsFinale ? finaleSeconds
                : Mathf.Clamp(minSeconds + secondsPerChar * _current.Text.Length, minSeconds, maxSeconds);
            _shownAt = Time.unscaledTime;
            _showing = true;
            transform.SetAsLastSibling(); // above overlays created later (result screen, intermission)
            enabled = true;
            Tick();
        }

        private void Update() => Tick();

        private void Tick()
        {
            if (!_showing) return;
            float t = Time.unscaledTime - _shownAt;
            if (t >= _duration)
            {
                StartNext();
                return;
            }
            float fadeIn = Mathf.Clamp01(t / FadeTime);
            float fadeOut = Mathf.Clamp01((_duration - t) / FadeTime);
            _group.alpha = Mathf.Min(fadeIn, _queue.Count > 0 ? 1f : fadeOut);
            float pop = 1f + 0.05f * (1f - fadeIn);
            _card.localScale = new Vector3(pop, pop, 1f);
        }
    }
}
