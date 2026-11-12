using System;
using DontTrustYourLuck.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.Narration
{
    /// <summary>
    /// Opening cordel: one stanza per page over a dimmed screen, Next / Skip. Blocks clicks behind it while open
    /// (it sits over the main menu, never over a running turn). Created at runtime (RuntimeUi).
    /// </summary>
    public sealed class StoryIntroView : MonoBehaviour
    {
        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };

        private CantadorLines _lines;
        private Text _numeral;
        private Text _stanza;
        private Text _page;
        private Button _next;
        private Text _nextLabel;
        private Button _skip;
        private int _index;

        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;
        public int PageIndex => _index;
        public string StanzaText => _stanza.text;
        public Button NextButton => _next;
        public Button SkipButton => _skip;

        public static StoryIntroView Create(RectTransform parent, Font font, CantadorLines lines)
        {
            var rt = RuntimeUi.Rect("StoryIntro", parent);
            RuntimeUi.Stretch(rt);
            var dim = rt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.08f, 0.05f, 0.03f, 0.86f);
            dim.raycastTarget = true;

            var view = rt.gameObject.AddComponent<StoryIntroView>();
            view._lines = lines;

            var card = RuntimeUi.Image("Card", rt, HudPalette.Cream);
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 10f);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HudPalette.Ink;
            outline.effectDistance = new Vector2(3f, -3f);
            var column = RuntimeUi.Column(card, 10f, 24);
            column.padding = new RectOffset(36, 36, 22, 22);
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = RuntimeUi.Text("Title", card.transform, font, 18, HudPalette.DangerDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.text = lines.introTitle;
            view._numeral = RuntimeUi.Text("Numeral", card.transform, font, 30, HudPalette.LuckyDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            view._stanza = RuntimeUi.Text("Stanza", card.transform, font, 24, HudPalette.Ink, TextAnchor.MiddleCenter);
            view._stanza.lineSpacing = 1.1f;
            view._page = RuntimeUi.Text("Page", card.transform, font, 14, HudPalette.InkFaded);

            var row = RuntimeUi.Rect("Buttons", card.transform);
            RuntimeUi.Layout(row, -1f, 44f);
            var rowGroup = RuntimeUi.Row(row, 16f);
            rowGroup.childForceExpandWidth = true;

            view._skip = MakeButton(row, font, "Skip", HudPalette.CardDisabled, HudPalette.Ink, out var skipLabel);
            skipLabel.text = lines.introSkip;
            view._next = MakeButton(row, font, "Next", HudPalette.Danger, HudPalette.Card, out view._nextLabel);

            view._next.onClick.AddListener(view.OnNext);
            view._skip.onClick.AddListener(view.Close);
            rt.gameObject.SetActive(false);
            return view;
        }

        private static Button MakeButton(RectTransform parent, Font font, string name, Color fill, Color ink, out Text label)
        {
            var image = RuntimeUi.Image(name, parent, fill, raycast: true);
            RuntimeUi.Layout(image, 160f, 44f, 1f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = RuntimeUi.Text("Label", image.transform, font, 20, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUi.Stretch(label.rectTransform);
            return button;
        }

        private void OnDestroy()
        {
            if (_next != null) _next.onClick.RemoveListener(OnNext);
            if (_skip != null) _skip.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (_lines.introStanzas == null || _lines.introStanzas.Length == 0) return;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            ShowPage(0);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void OnNext()
        {
            if (_index + 1 >= _lines.introStanzas.Length) Close();
            else ShowPage(_index + 1);
        }

        private void ShowPage(int index)
        {
            int count = _lines.introStanzas.Length;
            _index = Mathf.Clamp(index, 0, count - 1);
            _numeral.text = _index < Numerals.Length ? Numerals[_index] : (_index + 1).ToString();
            _stanza.text = _lines.introStanzas[_index];
            _page.text = (_index + 1) + " / " + count;
            bool last = _index == count - 1;
            _nextLabel.text = last ? _lines.introBegin : _lines.introNext;
            _skip.gameObject.SetActive(!last);
        }
    }
}
