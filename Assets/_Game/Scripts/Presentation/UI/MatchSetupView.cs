using System;
using System.Collections.Generic;
using DontTrustYourLuck.Presentation.Board;
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
        public string characterSubtitle = "Pick your lottery card for a game of luck through the streets of Latin America.";
        public string you = "YOU";
        public string bot = "BOT";
        public string playerTag = "P{0}";
        public string cardNumber = "No. {0:00}";
        public string players = "Players: {0}";
        public string modeBots = "Mode: You vs Bots";
        public string modeLocal = "Mode: Local Hot-Seat";
        public string gameQuick = "Quick Match";
        public string gameSeries = "Series: first to {0}";
        public string noCharacters = "No animals available";
        public string back = "Back";
        public string chooseBoard = "Choose Board";
        public string boardTitle = "CHOOSE THE BOARD";
        public string boardSubtitle = "One city, many laps. Stop exactly on your lucky number to win.";
        public string selected = "SELECTED";
        public string comingSoon = "COMING SOON";
        public string oaxaca = "OAXACA";
        public string oaxacaRegion = "Oaxaca, Mexico";
        public string oaxacaInfo = "A colorful market plaza with a 30-tile square circuit, action cards and debuffs.";
        public string fortaleza = "FORTALEZA";
        public string fortalezaRegion = "Ceará, Brazil";
        public string fortalezaInfo = "A seaside promenade at sunset with a 30-tile bay-shaped circuit, action cards and debuffs.";
        public string boardStats = "2-4 players\n10-20 minutes";
        public string salvador = "SALVADOR";
        public string salvadorRegion = "Bahia, Brazil";
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
        private static readonly Color Calcadao = new Color(0.86f, 0.80f, 0.68f);
        private static readonly Color Sand = new Color(0.92f, 0.82f, 0.60f);
        private static readonly Color Sea = new Color(0.25f, 0.52f, 0.66f);
        private static readonly Color Locked = new Color(0.62f, 0.58f, 0.54f);
        private static readonly Color Unseated = new Color(0.72f, 0.68f, 0.63f);
        private static readonly Color TitleInk = new Color(0.55f, 0.13f, 0.10f);

        public const string OaxacaId = "oaxaca";
        public const string FortalezaId = "fortaleza";

        private sealed class BoardWidget
        {
            public string Id;
            public Image Glow;
            public GameObject Ribbon;
        }

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
        private readonly List<BoardWidget> _boards = new List<BoardWidget>();
        private Action<string> _boardSelected;
        private string _board = OaxacaId;
        private Text _boardInfo;
        private MenuSkin _skin = new MenuSkin();
        private Font _font;
        private Action<IReadOnlyList<ParticipantConfig>> _confirm;
        private Action _cancel;
        private GameObject _characterPage, _boardPage;
        private Text _countLabel, _modeLabel, _gameLabel;
        private Button _nextButton;
        private int _selected, _count = 4;
        private bool _local;
        private int _seriesPoints; // 0 = quick match (single round)

        /// <summary>Series options offered by the game-type button, after the quick match.</summary>
        public static readonly int[] SeriesOptions = { 3, 5, 7 };

        public bool IsBoardSelection => _boardPage != null && _boardPage.activeSelf;
        public int SelectedCharacter => _selected;
        public int PlayerCount => _count;
        public bool AllLocal => _local;
        /// <summary>Points to win the series, or 0 for a quick (single-round) match.</summary>
        public int SeriesPoints => _seriesPoints;
        public bool IsSeries => _seriesPoints > 0;
        public SetupText Text => _text;
        /// <summary>Id of the highlighted board ("oaxaca" or "fortaleza").</summary>
        public string SelectedBoard => _board;

        public void Configure(IEnumerable<ParticipantConfig> seats, Action<IReadOnlyList<ParticipantConfig>> confirm, Action cancel,
            MenuSkin skin = null, Action<string> boardSelected = null, string initialBoard = null)
        {
            _confirm = confirm; _cancel = cancel; _boardSelected = boardSelected;
            if (initialBoard == FortalezaId || initialBoard == OaxacaId) _board = initialBoard;
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

        /// <summary>Quick match, then series to 3, 5 and 7 points, then back to quick.</summary>
        public void CycleGameType()
        {
            int next = Array.IndexOf(SeriesOptions, _seriesPoints) + 1;
            _seriesPoints = next < SeriesOptions.Length ? SeriesOptions[next] : 0;
            Refresh();
        }

        public void ShowBoards()
        {
            if (_characters.Count < 2) return;
            _characterPage.SetActive(false); _boardPage.SetActive(true);
            RefreshBoards();
        }

        public void Back()
        {
            if (IsBoardSelection) { _boardPage.SetActive(false); _characterPage.SetActive(true); }
            else { gameObject.SetActive(false); _cancel?.Invoke(); }
        }

        /// <summary>Highlights a board and previews it behind the modal. Unknown ids are ignored.</summary>
        public void SelectBoard(string id)
        {
            if (id != OaxacaId && id != FortalezaId) return;
            _board = id;
            RefreshBoards();
            _boardSelected?.Invoke(id);
        }

        /// <summary>Starts the match on the selected board.</summary>
        public void ConfirmBoard()
        {
            if (_characters.Count < 2) return;
            _boardSelected?.Invoke(_board);
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
            if (_gameLabel != null) _gameLabel.text = IsSeries ? string.Format(_text.gameSeries, _seriesPoints) : _text.gameQuick;
            if (_nextButton != null) _nextButton.interactable = _characters.Count >= 2;
        }

        private void RefreshBoards()
        {
            foreach (var b in _boards)
            {
                bool on = b.Id == _board;
                b.Glow.enabled = on;
                b.Ribbon.SetActive(on);
            }
            if (_boardInfo != null) _boardInfo.text = _board == FortalezaId ? _text.fortalezaInfo : _text.oaxacaInfo;
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

            WideButton(page, "PlayersButton", _skin.Button, _skin.IconDie, 120f, 470f, 230f, 52f, HudPalette.Ink, CyclePlayerCount, out _countLabel);
            WideButton(page, "ModeButton", _skin.Button, _skin.IconStar, 370f, 470f, 300f, 52f, HudPalette.Ink, ToggleMode, out _modeLabel);
            WideButton(page, "GameTypeButton", _skin.Button, _skin.IconClock, 690f, 470f, 290f, 52f, HudPalette.Ink, CycleGameType, out _gameLabel);

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

            const float w = 380f, gap = 20f;
            float x0 = (WindowW - (2f * w + 210f + 2f * gap)) / 2f;
            BoardCard(page, OaxacaId, _text.oaxaca, _text.oaxacaRegion, CircuitShape.Plaza, x0, w);
            BoardCard(page, FortalezaId, _text.fortaleza, _text.fortalezaRegion, CircuitShape.Bay, x0 + w + gap, w);
            LockedBoard(page, _text.salvador, _text.salvadorRegion, x0 + 2f * (w + gap));

            var info = MakeImage("InfoStrip", page, _skin.Window, HudPalette.Card);
            Place(info.rectTransform, 150f, 464f, WindowW - 300f, 68f);
            var badge = MakeImage("Badge", info.transform, _skin.Badge, HudPalette.Cream);
            Place(badge.rectTransform, 18f, 9f, 50f, 50f);
            var sun = MakeImage("Icon", badge.transform, _skin.IconSun, Color.white);
            sun.preserveAspect = true;
            Inset(sun.rectTransform, 8f, 8f);
            _boardInfo = MakeText("Description", info.transform, _text.oaxacaInfo, 18, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            Place(_boardInfo.rectTransform, 80f, 8f, 480f, 56f);
            var rule = MakeImage("Divider", info.transform, null, HudPalette.InkFaded);
            Place(rule.rectTransform, 574f, 14f, 2f, 44f);
            var stats = MakeText("Stats", info.transform, _text.boardStats, 17, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(stats.rectTransform, 592f, 8f, 200f - 16f, 56f);

            WideButton(page, "BackButton", _skin.Button, _skin.IconBack, 40f, 548f, 210f, 54f, HudPalette.Ink, Back, out var backLabel);
            backLabel.text = _text.back;
            WideButton(page, "PlayButton", _skin.Primary, _skin.IconPlay, WindowW - 290f, 544f, 250f, 60f, Color.white, ConfirmBoard, out var playLabel);
            playLabel.text = _text.play;
            playLabel.fontSize = 26;
            LightLabel(playLabel);
            RefreshBoards();
        }

        private void BoardCard(Transform page, string id, string city, string region, CircuitShape shape, float x, float w)
        {
            const float h = 310f, top = 136f;
            var glow = MakeImage(city + "Glow", page, null, HudPalette.Lucky);
            Place(glow.rectTransform, x - 7f, top - 7f, w + 14f, h + 14f);
            var card = MakeImage(city, page, _skin.Window, HudPalette.Card);
            card.raycastTarget = true;
            Place(card.rectTransform, x, top, w, h);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            SetButtonColors(button);
            button.onClick.AddListener(() => SelectBoard(id));

            bool bay = shape == CircuitShape.Bay;
            var preview = MakeImage("Preview", card.transform, null, PlazaEdge);
            Place(preview.rectTransform, 18f, 18f, w - 36f, 196f);
            var inner = MakeImage("Ground", preview.transform, null, bay ? Calcadao : Plaza);
            Inset(inner.rectTransform, 3f, 3f);
            if (bay)
            {
                // Sea and sand behind the promenade, matching the board's bay side.
                var sea = MakeImage("Sea", inner.transform, null, Sea);
                Anchor(sea.rectTransform, new Vector2(0f, 0.86f), Vector2.one, Vector2.zero, Vector2.zero);
                var sand = MakeImage("Sand", inner.transform, null, Sand);
                Anchor(sand.rectTransform, new Vector2(0f, 0.78f), new Vector2(1f, 0.86f), Vector2.zero, Vector2.zero);
            }
            else if (_skin.Tile != null)
            {
                var tiles = MakeImage("Azulejo", inner.transform, _skin.Tile, new Color(1f, 1f, 1f, 0.18f));
                tiles.type = Image.Type.Tiled;
                Inset(tiles.rectTransform, 0f, 0f);
            }
            MiniCircuit(inner.rectTransform, w - 42f, 190f, shape);

            var plaque = MakeImage("NamePlaque", card.transform, _skin.Plaque, HudPalette.Cream);
            Place(plaque.rectTransform, 60f, 220f, w - 120f, 50f);
            var name = MakeText("Name", plaque.transform, city, 25, TitleInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Inset(name.rectTransform, 10f, 4f);
            var where = MakeText("Region", card.transform, region, 16, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(where.rectTransform, 20f, 274f, w - 40f, 22f);

            var ribbon = MakeImage("SelectedRibbon", card.transform, _skin.Primary, HudPalette.Danger);
            Place(ribbon.rectTransform, -16f, -12f, 180f, 38f);
            var star = MakeImage("Icon", ribbon.transform, _skin.IconStar, Color.white);
            star.preserveAspect = true;
            Place(star.rectTransform, 10f, 6f, 26f, 26f);
            var label = MakeText("Label", ribbon.transform, _text.selected, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(36f, 3f), new Vector2(-8f, -3f));
            LightLabel(label);

            _boards.Add(new BoardWidget { Id = id, Glow = glow, Ribbon = ribbon.gameObject });
        }

        /// <summary>The board's real circuit outline (same poses as the 3D tiles), scaled into the preview.</summary>
        private void MiniCircuit(RectTransform area, float w, float h, CircuitShape shape)
        {
            var points = new Vector2[CircuitTiles];
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int tile = 0; tile < CircuitTiles; tile++)
            {
                var p = BoardView.CircuitPose(tile + 1, CircuitTiles, shape).position;
                points[tile] = new Vector2(p.x, p.z);
                min = Vector2.Min(min, points[tile]);
                max = Vector2.Max(max, points[tile]);
            }
            const float tileSize = 22f;
            var size = max - min;
            float scale = Mathf.Min((w - tileSize - 12f) / Mathf.Max(0.01f, size.x), (h - tileSize - 12f) / Mathf.Max(0.01f, size.y));
            var mid = (min + max) * 0.5f;

            var colors = new[] { HudPalette.Card, HudPalette.Pink, HudPalette.Blue, HudPalette.Lucky, HudPalette.Green };
            for (int tile = 0; tile < CircuitTiles; tile++)
            {
                var outline = MakeImage($"Tile{tile + 1}", area, null, HudPalette.Ink);
                var rt = outline.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(tileSize, tileSize);
                rt.anchoredPosition = (points[tile] - mid) * scale;
                var face = MakeImage("Face", rt, null, colors[tile % colors.Length]);
                Inset(face.rectTransform, 2f, 2f);
                var n = MakeText("N", rt, (tile + 1).ToString(), 13, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                n.resizeTextMinSize = 10;
                Inset(n.rectTransform, 1f, 1f);
            }
            var center = MakeImage("Sun", area, _skin.IconSun, HudPalette.Lucky);
            center.preserveAspect = true;
            var crt = center.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(56f, 56f);
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
