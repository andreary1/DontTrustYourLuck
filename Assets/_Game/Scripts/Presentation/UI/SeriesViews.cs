using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Display-only score row of one player (built by the director from series events).</summary>
    public struct ScoreDisplay
    {
        public int Id;
        public string Name;
        public Color Color;
        public int Score;
        /// <summary>Scored in the last finished round (shows "+1").</summary>
        public bool ScoredLastRound;
        /// <summary>Has the strictly highest score (or shares it) and at least one point.</summary>
        public bool Leader;
        /// <summary>False when sitting out a tiebreaker round.</summary>
        public bool InRound;
    }

    /// <summary>Lottery-ticket pips: one per point needed, filled with the player's color up to the score.</summary>
    internal static class ScoreTickets
    {
        public const int MaxPips = 9; // above this only the "3/12" text is shown

        public static void Fill(RectTransform container, List<Image> pips, int score, int pointsToWin, Color color, Vector2 size)
        {
            int count = pointsToWin <= MaxPips ? pointsToWin : 0;
            while (pips.Count < count)
            {
                var pip = RuntimeUi.Image("Ticket", container, Color.white);
                RuntimeUi.Layout(pip, size.x, size.y);
                var outline = pip.gameObject.AddComponent<Outline>();
                outline.effectColor = HudPalette.Ink;
                outline.effectDistance = new Vector2(1f, -1f);
                pips.Add(pip);
            }
            for (int i = 0; i < pips.Count; i++)
            {
                bool used = i < count;
                pips[i].gameObject.SetActive(used);
                if (used) pips[i].color = i < score ? color : HudPalette.CardDisabled;
            }
        }
    }

    /// <summary>
    /// Permanent series scoreboard (R1): a compact strip under the top bar with the round label and, per player,
    /// color swatch, name, ticket pips and "score/target". The leader is highlighted in gold; never color only.
    /// </summary>
    public sealed class ScoreboardStripView : MonoBehaviour
    {
        private sealed class Entry
        {
            public GameObject Root;
            public Image Background;
            public Image Swatch;
            public Text Name;
            public RectTransform Tickets;
            public readonly List<Image> Pips = new List<Image>();
            public Text Score;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private Font _font;
        private Text _title;
        private RectTransform _row;

        public RectTransform Rect => (RectTransform)transform;
        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => _title != null ? _title.text : string.Empty;
        /// <summary>Score label of the n-th entry (tests).</summary>
        public string ScoreText(int index) => index < _entries.Count && _entries[index].Root.activeSelf ? _entries[index].Score.text : null;

        /// <summary>Creates the strip under <paramref name="parent"/>, anchored to its bottom edge (pivot top).</summary>
        public static ScoreboardStripView Create(RectTransform parent, Font font)
        {
            var rt = RuntimeUi.Rect("SeriesScoreboard", parent);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(10f, 30f);
            var ignore = rt.gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true; // the top bar may have a layout group

            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(HudPalette.Card.r, HudPalette.Card.g, HudPalette.Card.b, 0.92f);
            bg.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = HudPalette.Ink;

            var view = rt.gameObject.AddComponent<ScoreboardStripView>();
            view._font = font;
            view._row = rt;
            RuntimeUi.Row(rt, 10f, TextAnchor.MiddleCenter, 3);
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            view._title = RuntimeUi.Text("Title", rt, font, 13, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            view._title.horizontalOverflow = HorizontalWrapMode.Overflow;
            RuntimeUi.Layout(view._title, -1f, 24f);
            rt.gameObject.SetActive(false);
            return view;
        }

        public void Show(string title, IReadOnlyList<ScoreDisplay> rows, int pointsToWin, string scoreFormat)
        {
            _title.text = title;
            int count = rows != null ? rows.Count : 0;
            while (_entries.Count < count) _entries.Add(CreateEntry());
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                bool used = i < count;
                e.Root.SetActive(used);
                if (!used) continue;
                var row = rows[i];
                e.Background.color = row.Leader ? HudPalette.CardWinner : HudPalette.Cream;
                e.Swatch.color = row.Color;
                e.Name.text = row.Name;
                e.Name.fontStyle = row.Leader ? FontStyle.Bold : FontStyle.Normal;
                e.Name.color = row.InRound ? HudPalette.Ink : HudPalette.InkFaded;
                ScoreTickets.Fill(e.Tickets, e.Pips, row.Score, pointsToWin, row.Color, new Vector2(7f, 12f));
                e.Score.text = string.Format(scoreFormat, row.Score, pointsToWin);
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private Entry CreateEntry()
        {
            var e = new Entry();
            e.Background = RuntimeUi.Image("Player", _row, HudPalette.Cream);
            e.Root = e.Background.gameObject;
            RuntimeUi.Row(e.Background, 4f, TextAnchor.MiddleLeft, 3);
            RuntimeUi.Layout(e.Background, -1f, 24f);

            e.Swatch = RuntimeUi.Image("Swatch", e.Background.transform, Color.white);
            RuntimeUi.Layout(e.Swatch, 10f, 16f);
            e.Name = RuntimeUi.Text("Name", e.Background.transform, _font, 12, HudPalette.Ink, TextAnchor.MiddleLeft);
            e.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            RuntimeUi.Layout(e.Name, -1f, 18f);

            e.Tickets = RuntimeUi.Rect("Tickets", e.Background.transform);
            RuntimeUi.Row(e.Tickets, 2f, TextAnchor.MiddleLeft);
            RuntimeUi.Layout(e.Tickets, -1f, 14f);

            e.Score = RuntimeUi.Text("Score", e.Background.transform, _font, 11, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            e.Score.horizontalOverflow = HorizontalWrapMode.Overflow;
            RuntimeUi.Layout(e.Score, -1f, 18f);
            return e;
        }
    }

    /// <summary>
    /// Between-rounds overlay (R1): who scored and why, the full scoreboard and a "Next round" button. Timing
    /// (auto-continue, minimum time before skipping) belongs to the director; this view only displays and
    /// forwards the click.
    /// </summary>
    public sealed class IntermissionView : MonoBehaviour
    {
        private sealed class Row
        {
            public GameObject Root;
            public Image Background;
            public Image Swatch;
            public Text Name;
            public RectTransform Tickets;
            public readonly List<Image> Pips = new List<Image>();
            public Text Score;
            public Text Gain;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Font _font;
        private Text _title;
        private Text _subtitle;
        private Text _note;
        private RectTransform _table;
        private Button _continue;
        private Text _continueLabel;
        private Text _countdown;

        public event Action ContinueClicked;

        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => _title.text;
        public string SubtitleText => _subtitle.text;
        public Button ContinueButton => _continue;

        public static IntermissionView Create(RectTransform parent, Font font)
        {
            var rt = RuntimeUi.Rect("SeriesIntermission", parent);
            RuntimeUi.Stretch(rt);
            var dim = rt.gameObject.AddComponent<Image>();
            dim.color = HudPalette.Overlay;
            dim.raycastTarget = true; // blocks board/HUD input while visible

            var view = rt.gameObject.AddComponent<IntermissionView>();
            view._font = font;

            var card = RuntimeUi.Image("Card", rt, HudPalette.Card);
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(440f, 10f);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HudPalette.Ink;
            outline.effectDistance = new Vector2(2f, -2f);
            RuntimeUi.Column(card, 8f, 18);
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            view._title = RuntimeUi.Text("Title", card.transform, font, 26, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            view._subtitle = RuntimeUi.Text("Subtitle", card.transform, font, 16, HudPalette.LuckyDark);
            view._table = RuntimeUi.Rect("Scores", card.transform);
            RuntimeUi.Column(view._table, 4f, 0);
            view._note = RuntimeUi.Text("Note", card.transform, font, 14, HudPalette.DangerDark, TextAnchor.MiddleCenter, FontStyle.Bold);

            var button = RuntimeUi.Image("NextRound", card.transform, HudPalette.Danger, raycast: true);
            RuntimeUi.Layout(button, -1f, 40f);
            view._continue = button.gameObject.AddComponent<Button>();
            view._continue.targetGraphic = button;
            view._continueLabel = RuntimeUi.Text("Label", button.transform, font, 18, HudPalette.Card, TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUi.Stretch(view._continueLabel.rectTransform);
            view._countdown = RuntimeUi.Text("Countdown", card.transform, font, 12, HudPalette.InkFaded);
            view._continue.onClick.AddListener(view.OnContinue);

            rt.gameObject.SetActive(false);
            return view;
        }

        private void OnDestroy()
        {
            if (_continue != null) _continue.onClick.RemoveListener(OnContinue);
        }

        private void OnContinue() => ContinueClicked?.Invoke();

        public void Show(string title, string subtitle, string note, IReadOnlyList<ScoreDisplay> rows, int pointsToWin,
            string scoreFormat, string gainLabel, string continueLabel)
        {
            _title.text = title;
            _subtitle.text = subtitle;
            _note.text = note ?? string.Empty;
            _note.gameObject.SetActive(!string.IsNullOrEmpty(note));
            _continueLabel.text = continueLabel;
            _countdown.text = string.Empty;

            int count = rows != null ? rows.Count : 0;
            while (_rows.Count < count) _rows.Add(CreateRow());
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool used = i < count;
                r.Root.SetActive(used);
                if (!used) continue;
                var row = rows[i];
                r.Background.color = row.Leader ? HudPalette.CardWinner : HudPalette.Cream;
                r.Swatch.color = row.Color;
                r.Name.text = row.Name;
                r.Name.fontStyle = row.Leader ? FontStyle.Bold : FontStyle.Normal;
                ScoreTickets.Fill(r.Tickets, r.Pips, row.Score, pointsToWin, row.Color, new Vector2(12f, 20f));
                r.Score.text = string.Format(scoreFormat, row.Score, pointsToWin);
                r.Gain.text = row.ScoredLastRound ? gainLabel : string.Empty;
            }
            SetContinueInteractable(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void SetContinueInteractable(bool value)
        {
            if (_continue != null) _continue.interactable = value;
        }

        public void SetCountdown(string value)
        {
            if (_countdown != null) _countdown.text = value ?? string.Empty;
        }

        public void Hide() => gameObject.SetActive(false);

        private Row CreateRow()
        {
            var r = new Row();
            r.Background = RuntimeUi.Image("Player", _table, HudPalette.Cream);
            r.Root = r.Background.gameObject;
            RuntimeUi.Row(r.Background, 8f, TextAnchor.MiddleLeft, 5);
            RuntimeUi.Layout(r.Background, -1f, 32f);

            r.Swatch = RuntimeUi.Image("Swatch", r.Background.transform, Color.white);
            RuntimeUi.Layout(r.Swatch, 14f, 22f);
            r.Name = RuntimeUi.Text("Name", r.Background.transform, _font, 16, HudPalette.Ink, TextAnchor.MiddleLeft);
            RuntimeUi.Layout(r.Name, 120f, 24f);
            r.Tickets = RuntimeUi.Rect("Tickets", r.Background.transform);
            RuntimeUi.Row(r.Tickets, 3f, TextAnchor.MiddleLeft);
            RuntimeUi.Layout(r.Tickets, -1f, 22f, 1f);
            r.Score = RuntimeUi.Text("Score", r.Background.transform, _font, 15, HudPalette.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            RuntimeUi.Layout(r.Score, 44f, 24f);
            r.Gain = RuntimeUi.Text("Gain", r.Background.transform, _font, 15, HudPalette.LuckyDark, TextAnchor.MiddleRight, FontStyle.Bold);
            RuntimeUi.Layout(r.Gain, 34f, 24f);
            return r;
        }
    }
}
