using System;
using System.Collections.Generic;
using DontTrustYourLuck.Presentation.Characters;
using DontTrustYourLuck.Presentation.Match;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>
    /// Sprites/font reused from the main menu so the setup screens share its printed-paper look.
    /// Every field is optional: a missing sprite falls back to a flat palette color.
    /// </summary>
    public sealed class MenuSkin
    {
        public Sprite Window, Plaque, Button, Primary, Badge, Tile;
        public Sprite IconBack, IconPlay, IconStar, IconDie, IconClock, IconSun;
        public Font Font;
        /// <summary>Real bold face for bold labels (a synthesized bold smears pixel fonts).</summary>
        public Font FontBold;
    }

    /// <summary>Every player-facing setup string (English).</summary>
    [Serializable]
    public sealed class SetupText
    {
        public string characterTitle = "CHOOSE YOUR ANIMAL";
        public string characterSubtitle = "Pick your lottery card for a game of luck through the streets of Fortaleza.";
        public string you = "YOU";
        public string bot = "BOT";
        public string playerTag = "P{0}";
        public string cardNumber = "No. {0:00}";
        public string players = "Players: {0}";
        public string modeBots = "Mode: You vs Bots";
        public string modeLocal = "Mode: Local Hot-Seat";
        public string noCharacters = "No animals available";
        public string back = "Back";
        public string chooseBoard = "Choose Board";
        public string boardTitle = "CHOOSE THE BOARD";
        public string boardSubtitle = "One city, many laps. Stop exactly on your lucky number to win.";
        public string selected = "SELECTED";
        public string comingSoon = "COMING SOON";
        public string fortaleza = "FORTALEZA";
        public string fortalezaRegion = "Ceará, Brazil";
        public string fortalezaInfo = "A tropical plaza with a 30-tile circuit, action cards and debuffs.";
        public string fortalezaStats = "2-4 players\n10-20 minutes";
        public string salvador = "SALVADOR";
        public string salvadorRegion = "Bahia, Brazil";
        public string oaxaca = "OAXACA";
        public string oaxacaRegion = "Mexico";
        public string play = "Play";
    }

    /// <summary>
    /// Two-step setup modal (animal, then board), populated from the scene's character assets and drawn with the
    /// main menu's skin. Pure presentation: it only reports the chosen seats through the confirm callback.
    /// </summary>
    public sealed class MatchSetupView : MonoBehaviour
    {
        private const float WindowW = 1100f, WindowH = 620f;
        private const float CardW = 210f, CardH = 290f, CardGap = 30f, CardTop = 150f, CardLift = 14f;
        private const int CircuitTiles = 30;

        private static readonly Color Dim = new Color(0.10f, 0.06f, 0.04f, 0.55f);
        private static readonly Color Plaza = new Color(0.56f, 0.43f, 0.31f);
        private static readonly Color PlazaEdge = new Color(0.36f, 0.26f, 0.18f);
        private static readonly Color Locked = new Color(0.62f, 0.58f, 0.54f);
        private static readonly Color Unseated = new Color(0.72f, 0.68f, 0.63f);
        private static readonly Color TitleInk = new Color(0.55f, 0.13f, 0.10f);

        private sealed class CardWidget
        {
            public RectTransform Root;
            public Image Frame, Glow, Portrait;
            public GameObject Tag;
            public Text TagLabel;
        }

        private readonly SetupText _text = new SetupText();
        private readonly List<CharacterDefinitionAsset> _characters = new List<CharacterDefinitionAsset>();
        private readonly List<CardWidget> _cards = new List<CardWidget>();
        private MenuSkin _skin = new MenuSkin();
        private Font _font;
        private Action<IReadOnlyList<ParticipantConfig>> _confirm;
        private Action _cancel;
        private GameObject _characterPage, _boardPage;
        private Text _countLabel, _modeLabel;
        private Button _nextButton;
        private int _selected, _count = 4;
        private bool _local;

        public bool IsBoardSelection => _boardPage != null && _boardPage.activeSelf;
        public int SelectedCharacter => _selected;
        public int PlayerCount => _count;
        public bool AllLocal => _local;
        public SetupText Text => _text;

        public void Configure(IEnumerable<ParticipantConfig> seats, Action<IReadOnlyList<ParticipantConfig>> confirm, Action cancel,
            MenuSkin skin = null)
        {
            _confirm = confirm; _cancel = cancel;
            _skin = skin ?? new MenuSkin();
            _font = _skin.Font != null ? _skin.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var seat in seats)
                if (seat.character != null && !_characters.Contains(seat.character)) _characters.Add(seat.character);
            _count = Mathf.Min(4, _characters.Count);
            Build();
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _characterPage.SetActive(true); _boardPage.SetActive(false);
            FitToScreen();
            Refresh();
        }

        private void OnRectTransformDimensionsChange() => FitToScreen();

        /// <summary>Shrinks both pages when the canvas is shorter/narrower than the window (wide or small browser windows).</summary>
        private void FitToScreen()
        {
            if (_characterPage == null) return;
            var size = ((RectTransform)transform).rect.size;
            float s = Mathf.Min(1f, (size.x - 24f) / WindowW, (size.y - 24f) / WindowH);
            if (s <= 0f) return;
            var scale = new Vector3(s, s, 1f);
            _characterPage.transform.localScale = scale;
            _boardPage.transform.localScale = scale;
        }

        public void SelectCharacter(int index)
        {
            if (index < 0 || index >= _characters.Count) return;
            _selected = index; Refresh();
        }

        public void CyclePlayerCount()
        {
            int max = Mathf.Min(4, _characters.Count);
            _count = _count >= max ? Mathf.Min(2, max) : _count + 1;
            Refresh();
        }

        public void ToggleMode() { _local = !_local; Refresh(); }

        public void ShowBoards()
        {
            if (_characters.Count < 2) return;
            _characterPage.SetActive(false); _boardPage.SetActive(true);
        }

        public void Back()
        {
            if (IsBoardSelection) { _boardPage.SetActive(false); _characterPage.SetActive(true); }
            else { gameObject.SetActive(false); _cancel?.Invoke(); }
        }

        public void ConfirmFortaleza()
        {
            if (_characters.Count < 2) return;
            var seats = new List<ParticipantConfig>();
            for (int i = 0; i < _count; i++)
            {
                var character = _characters[(_selected + i) % _characters.Count];
                seats.Add(new ParticipantConfig(character.DisplayName, character.Accent, i != 0 && !_local, character));
            }
            gameObject.SetActive(false);
            _confirm?.Invoke(seats);
        }

        /// <summary>Seat a card fills this match (0 = the player), or -1 when it sits out.</summary>
        public int SeatOf(int cardIndex)
        {
            int n = _characters.Count;
            if (n == 0) return -1;
            int seat = (cardIndex - _selected + n) % n;
            return seat < _count ? seat : -1;
        }

        private void Refresh()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                int seat = SeatOf(i);
                bool chosen = i == _selected;
                card.Glow.enabled = chosen;
                card.Root.anchoredPosition = new Vector2(CardX(i), -(CardTop - (chosen ? CardLift : 0f)));
                card.Frame.color = seat < 0 ? Unseated : Color.white;
                card.Portrait.color = seat < 0 ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                card.Tag.SetActive(seat >= 0);
                if (seat >= 0)
                    card.TagLabel.text = seat == 0 ? (_local ? string.Format(_text.playerTag, 1) : _text.you)
                        : _local ? string.Format(_text.playerTag, seat + 1) : _text.bot;
            }
            if (_countLabel != null) _countLabel.text = string.Format(_text.players, _count);
            if (_modeLabel != null) _modeLabel.text = _local ? _text.modeLocal : _text.modeBots;
            if (_nextButton != null) _nextButton.interactable = _characters.Count >= 2;
        }

        private float CardX(int i)
        {
            int n = Mathf.Max(1, _characters.Count);
            float total = n * CardW + (n - 1) * CardGap;
            return (WindowW - total) / 2f + i * (CardW + CardGap);
        }

        // ================================================================== build

        private void Build()
        {
            if (GetComponent<CanvasGroup>() == null) gameObject.AddComponent<CanvasGroup>(); // panel pop fades it
            var dim = gameObject.AddComponent<Image>();
            dim.color = Dim;
            dim.raycastTarget = true;

            _characterPage = Window("Characters").gameObject;
            BuildCharacterPage(_characterPage.transform);
            _boardPage = Window("Boards").gameObject;
            BuildBoardPage(_boardPage.transform);
            _boardPage.SetActive(false);
        }

        private RectTransform Window(string name)
        {
            var window = MakeImage(name, transform, _skin.Window, HudPalette.Card);
            window.raycastTarget = true;
            var rt = window.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(WindowW, WindowH);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private void Header(Transform page, string title, string subtitle)
        {
            var plaque = MakeImage("TitlePlaque", page, _skin.Plaque, HudPalette.Card);
            Place(plaque.rectTransform, (WindowW - 480f) / 2f, 22f, 480f, 64f);
            var t = MakeText("Title", plaque.transform, title, 28, TitleInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(t.rectTransform, 12f, 4f);
            var s = MakeText("Subtitle", page, subtitle, 18, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(s.rectTransform, 60f, 92f, WindowW - 120f, 30f);
        }

        private void BuildCharacterPage(Transform page)
        {
            Header(page, _text.characterTitle, _text.characterSubtitle);

            if (_characters.Count == 0)
            {
                var none = MakeText("Empty", page, _text.noCharacters, 22, HudPalette.Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
                Place(none.rectTransform, 0f, 260f, WindowW, 60f);
            }
            for (int i = 0; i < _characters.Count; i++) _cards.Add(CharacterCard(page, i));

            WideButton(page, "PlayersButton", _skin.Button, _skin.IconDie, 280f, 470f, 250f, 52f, HudPalette.Ink, CyclePlayerCount, out _countLabel);
            WideButton(page, "ModeButton", _skin.Button, _skin.IconStar, 560f, 470f, 290f, 52f, HudPalette.Ink, ToggleMode, out _modeLabel);

            WideButton(page, "BackButton", _skin.Button, _skin.IconBack, 40f, 548f, 210f, 54f, HudPalette.Ink, Back, out var backLabel);
            backLabel.text = _text.back;
            _nextButton = WideButton(page, "NextButton", _skin.Primary, _skin.IconPlay, WindowW - 290f, 544f, 250f, 60f, Color.white, ShowBoards, out var nextLabel);
            nextLabel.text = _text.chooseBoard;
            LightLabel(nextLabel);
        }

        private CardWidget CharacterCard(Transform page, int index)
        {
            var character = _characters[index];
            var root = Rect(character.DisplayName, page);
            Place(root, CardX(index), CardTop, CardW, CardH);

            var glow = MakeImage("Glow", root, null, HudPalette.Lucky);
            Anchor(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-7f, -7f), new Vector2(7f, 7f));

            var frame = MakeImage("Frame", root, _skin.Window, HudPalette.Card);
            frame.raycastTarget = true;
            Anchor(frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            SetButtonColors(button);
            button.onClick.AddListener(() => SelectCharacter(index));

            // Lottery-card print details: number in the corner and an accent rule in the animal's color.
            var number = MakeText("Number", frame.transform, string.Format(_text.cardNumber, index + 1), 15, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(number.rectTransform, 18f, 14f, 100f, 22f);
            var rule = MakeImage("AccentRule", frame.transform, null, character.Accent);
            Place(rule.rectTransform, 18f, 38f, CardW - 36f, 4f);

            var portrait = MakeImage("Portrait", frame.transform, character.Portrait, Color.white);
            portrait.preserveAspect = true;
            Place(portrait.rectTransform, (CardW - 156f) / 2f, 48f, 156f, 156f);

            var plaque = MakeImage("NamePlaque", frame.transform, _skin.Plaque, HudPalette.Cream);
            Place(plaque.rectTransform, 14f, 216f, CardW - 28f, 56f);
            var name = MakeText("Name", plaque.transform, character.DisplayName.ToUpperInvariant(), 22, TitleInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(name.rectTransform, 8f, 4f);

            var tag = MakeImage("SeatTag", root, _skin.Primary, HudPalette.Danger);
            Place(tag.rectTransform, (CardW - 104f) / 2f, -18f, 104f, 36f);
            var tagLabel = MakeText("Label", tag.transform, string.Empty, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(tagLabel.rectTransform, 6f, 3f);
            LightLabel(tagLabel);

            return new CardWidget { Root = root, Frame = frame, Glow = glow, Portrait = portrait, Tag = tag.gameObject, TagLabel = tagLabel };
        }

        private void BuildBoardPage(Transform page)
        {
            Header(page, _text.boardTitle, _text.boardSubtitle);

            LockedBoard(page, _text.salvador, _text.salvadorRegion, 60f);
            LockedBoard(page, _text.oaxaca, _text.oaxacaRegion, WindowW - 60f - 210f);
            FeaturedBoard(page);

            var info = MakeImage("InfoStrip", page, _skin.Window, HudPalette.Card);
            Place(info.rectTransform, 150f, 464f, WindowW - 300f, 68f);
            var badge = MakeImage("Badge", info.transform, _skin.Badge, HudPalette.Cream);
            Place(badge.rectTransform, 18f, 9f, 50f, 50f);
            var sun = MakeImage("Icon", badge.transform, _skin.IconSun, Color.white);
            sun.preserveAspect = true;
            Inset(sun.rectTransform, 8f, 8f);
            var description = MakeText("Description", info.transform, _text.fortalezaInfo, 18, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            Place(description.rectTransform, 80f, 8f, 480f, 56f);
            var rule = MakeImage("Divider", info.transform, null, HudPalette.InkFaded);
            Place(rule.rectTransform, 574f, 14f, 2f, 44f);
            var stats = MakeText("Stats", info.transform, _text.fortalezaStats, 17, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(stats.rectTransform, 592f, 8f, 200f - 16f, 56f);

            WideButton(page, "BackButton", _skin.Button, _skin.IconBack, 40f, 548f, 210f, 54f, HudPalette.Ink, Back, out var backLabel);
            backLabel.text = _text.back;
            WideButton(page, "PlayButton", _skin.Primary, _skin.IconPlay, WindowW - 290f, 544f, 250f, 60f, Color.white, ConfirmFortaleza, out var playLabel);
            playLabel.text = _text.play;
            playLabel.fontSize = 26;
            LightLabel(playLabel);
        }

        private void FeaturedBoard(Transform page)
        {
            const float w = 500f, h = 320f, top = 134f;
            var glow = MakeImage("FortalezaGlow", page, null, HudPalette.Lucky);
            Place(glow.rectTransform, (WindowW - w) / 2f - 7f, top - 7f, w + 14f, h + 14f);
            var card = MakeImage("Fortaleza", page, _skin.Window, HudPalette.Card);
            Place(card.rectTransform, (WindowW - w) / 2f, top, w, h);

            var plaza = MakeImage("Preview", card.transform, null, PlazaEdge);
            Place(plaza.rectTransform, 20f, 20f, w - 40f, 206f);
            var inner = MakeImage("Plaza", plaza.transform, null, Plaza);
            Inset(inner.rectTransform, 3f, 3f);
            if (_skin.Tile != null)
            {
                var tiles = MakeImage("Azulejo", inner.transform, _skin.Tile, new Color(1f, 1f, 1f, 0.18f));
                tiles.type = Image.Type.Tiled;
                Inset(tiles.rectTransform, 0f, 0f);
            }
            MiniCircuit(inner.rectTransform, w - 46f, 200f);

            var plaque = MakeImage("NamePlaque", card.transform, _skin.Plaque, HudPalette.Cream);
            Place(plaque.rectTransform, 90f, 228f, w - 180f, 54f);
            var name = MakeText("Name", plaque.transform, _text.fortaleza, 26, TitleInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(name.rectTransform, 10f, 4f);
            var region = MakeText("Region", card.transform, _text.fortalezaRegion, 16, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(region.rectTransform, 20f, 283f, w - 40f, 22f);

            var ribbon = MakeImage("SelectedRibbon", card.transform, _skin.Primary, HudPalette.Danger);
            Place(ribbon.rectTransform, -16f, -12f, 180f, 38f);
            var star = MakeImage("Icon", ribbon.transform, _skin.IconStar, Color.white);
            star.preserveAspect = true;
            Place(star.rectTransform, 10f, 6f, 26f, 26f);
            var label = MakeText("Label", ribbon.transform, _text.selected, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(36f, 3f), new Vector2(-8f, -3f));
            LightLabel(label);
        }

        /// <summary>A loop of numbered tiles evenly spaced (by arc length) on an ellipse: the board's circuit at a glance.</summary>
        private void MiniCircuit(RectTransform area, float w, float h)
        {
            float a = w / 2f - 22f, b = h / 2f - 19f;
            const int samples = 360;
            var points = new Vector2[samples + 1];
            var length = new float[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.PI / 2f - i * 2f * Mathf.PI / samples; // starts at the top, runs clockwise
                points[i] = new Vector2(Mathf.Cos(t) * a, Mathf.Sin(t) * b);
                if (i > 0) length[i] = length[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }
            var colors = new[] { HudPalette.Card, HudPalette.Pink, HudPalette.Blue, HudPalette.Lucky, HudPalette.Green };
            int k = 0;
            for (int tile = 0; tile < CircuitTiles; tile++)
            {
                float target = length[samples] * tile / CircuitTiles;
                while (k < samples && length[k + 1] < target) k++;
                var p = points[k];
                var outline = MakeImage($"Tile{tile + 1}", area, null, HudPalette.Ink);
                var rt = outline.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(27f, 27f);
                rt.anchoredPosition = p;
                var face = MakeImage("Face", rt, null, colors[tile % colors.Length]);
                Inset(face.rectTransform, 2f, 2f);
                var n = MakeText("N", rt, (tile + 1).ToString(), 14, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                n.resizeTextMinSize = 12;
                Inset(n.rectTransform, 1f, 1f);
            }
            var center = MakeImage("Sun", area, _skin.IconSun, HudPalette.Lucky);
            center.preserveAspect = true;
            var crt = center.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(64f, 64f);
        }

        private void LockedBoard(Transform page, string city, string region, float x)
        {
            const float w = 210f, h = 250f;
            var card = MakeImage(city, page, _skin.Window, HudPalette.Card);
            card.color = _skin.Window != null ? Locked : HudPalette.CardDisabled;
            Place(card.rectTransform, x, 160f, w, h);
            var art = MakeImage("Preview", card.transform, null, new Color(0.40f, 0.35f, 0.31f));
            Place(art.rectTransform, 18f, 18f, w - 36f, 120f);
            if (_skin.Tile != null)
            {
                var tiles = MakeImage("Azulejo", art.transform, _skin.Tile, new Color(1f, 1f, 1f, 0.12f));
                tiles.type = Image.Type.Tiled;
                Inset(tiles.rectTransform, 0f, 0f);
            }
            var plaque = MakeImage("NamePlaque", card.transform, _skin.Plaque, HudPalette.Cream);
            plaque.color = _skin.Plaque != null ? Locked : HudPalette.CardDisabled;
            Place(plaque.rectTransform, 12f, 150f, w - 24f, 50f);
            var name = MakeText("Name", plaque.transform, city, 21, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(name.rectTransform, 8f, 4f);
            var where = MakeText("Region", card.transform, region, 15, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(where.rectTransform, 12f, 206f, w - 24f, 24f);

            var ribbon = MakeImage("SoonRibbon", card.transform, _skin.Primary, HudPalette.Danger);
            Place(ribbon.rectTransform, (w - 160f) / 2f, 60f, 160f, 34f);
            var label = MakeText("Label", ribbon.transform, _text.comingSoon, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(label.rectTransform, 6f, 3f);
            LightLabel(label);
        }

        private Button WideButton(Transform parent, string name, Sprite frame, Sprite icon, float x, float y, float w, float h,
            Color ink, Action click, out Text label)
        {
            var image = MakeImage(name, parent, frame, frame != null ? Color.white : HudPalette.Cream);
            image.raycastTarget = true;
            Place(image.rectTransform, x, y, w, h);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetButtonColors(button);
            button.onClick.AddListener(() => click());
            float s = h * 0.56f;
            float left = 14f;
            if (icon != null)
            {
                var iconImage = MakeImage("Icon", image.transform, icon, Color.white);
                iconImage.preserveAspect = true;
                Anchor(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -s / 2f + 2f), new Vector2(16f + s, s / 2f + 2f));
                left = 22f + s;
            }
            label = MakeText("Label", image.transform, string.Empty, 21, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(left, 4f), new Vector2(-14f, 0f));
            return button;
        }

        // ================================================================== helpers

        private static void LightLabel(Text t)
        {
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.36f, 0.08f, 0.06f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static void SetButtonColors(Selectable s)
        {
            var colors = s.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.8f);
            colors.pressedColor = new Color(0.85f, 0.78f, 0.68f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.65f, 0.62f, 0.58f, 0.6f);
            s.colors = colors;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color fallback)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            // An opaque color only stands in for a missing sprite; translucent colors tint the sprite.
            image.color = sprite != null && fallback.a >= 1f ? Color.white : fallback;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private Text MakeText(string name, Transform parent, string value, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            bool bold = style == FontStyle.Bold && _skin.FontBold != null && _skin.FontBold != _font;
            text.font = bold ? _skin.FontBold : _font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = bold ? FontStyle.Normal : style;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(13, size);
            text.resizeTextMaxSize = size;
            return text;
        }

        /// <summary>Top-left pixel placement inside the parent (y grows downward).</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void Inset(RectTransform rt, float x, float y) =>
            Anchor(rt, Vector2.zero, Vector2.one, new Vector2(x, y), new Vector2(-x, -y));

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
