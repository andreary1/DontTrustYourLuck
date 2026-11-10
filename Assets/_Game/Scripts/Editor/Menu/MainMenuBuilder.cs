using System.Collections.Generic;
using DontTrustYourLuck.EditorTools.Sprint4;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.EditorTools.Menu
{
    /// <summary>
    /// Main menu pass (runs after the Sprint 4 HUD restyle): imports the logo, draws the menu icons, builds the
    /// title screen inside the GameHUD prefab (over the diorama), adds "Main Menu" to the result screen and
    /// makes the GameBoard scene wait for Play instead of auto-starting the match.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string LogoPath = "Assets/_Game/Art/UI/Branding/UI_Logo_Main.png";
        public const string MenuArtDir = "Assets/_Game/Art/UI/Menu";

        public sealed class MenuArt
        {
            public Sprite Logo, ButtonPrimary, Azulejo;
            public Sprite IconPlay, IconBook, IconGear, IconBack, IconDie, IconCard, IconBounce;
        }

        private static Font _font;

        [MenuItem("Tools/Don't Trust Your Luck/Build Main Menu (after Sprint 4)")]
        public static void BuildFromMenu()
        {
            Build(Sprint4Art.BuildUi());
            ConfigureScene();
        }

        public static void Build(Sprint4Art.UiArt ui)
        {
            var art = BuildArt();
            BuildHud(ui, art);
        }

        // ================================================================== art

        public static MenuArt BuildArt()
        {
            Sprint4Art.EnsureFolder(MenuArtDir);
            var art = new MenuArt
            {
                Logo = ImportLogo(),
                ButtonPrimary = Save("SPR_UI_ButtonPrimary.png", PrimaryButton(), new Vector4(6, 6, 6, 6)),
                Azulejo = Save("SPR_UI_Azulejo.png", Azulejo(), Vector4.zero),
                IconPlay = Save("SPR_Icon_Play.png", Icon(DrawPlay), Vector4.zero, 100f),
                IconBook = Save("SPR_Icon_Book.png", Icon(DrawBook), Vector4.zero, 100f),
                IconGear = Save("SPR_Icon_Gear.png", Icon(DrawGear), Vector4.zero, 100f),
                IconBack = Save("SPR_Icon_Back.png", Icon(DrawBack), Vector4.zero, 100f),
                IconDie = Save("SPR_Icon_Die.png", Icon(DrawDie), Vector4.zero, 100f),
                IconCard = Save("SPR_Icon_Card.png", Icon(DrawCard), Vector4.zero, 100f),
                IconBounce = Save("SPR_Icon_Bounce.png", Icon(DrawBounce), Vector4.zero, 100f),
            };
            return art;
        }

        /// <summary>User-supplied illustration: smooth filtering (not pixel art), compressed, no mipmaps.</summary>
        private static Sprite ImportLogo()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(LogoPath);
            if (importer == null)
            {
                Debug.LogError($"[MainMenuBuilder] Logo not found at {LogoPath}.");
                return null;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        }

        private static Sprite Save(string file, PixelCanvas c, Vector4 border, float ppu = 50f)
        {
            string path = $"{MenuArtDir}/{file}";
            c.SavePng(path);
            return Sprint4Art.ImportSprite(path, ppu, border, 64);
        }

        private static PixelCanvas Icon(System.Action<PixelCanvas> draw)
        {
            var c = new PixelCanvas(32, 32, 21u);
            c.Fill(Sprint4Art.Clear);
            draw(c);
            return c;
        }

        private static PixelCanvas PrimaryButton()
        {
            var c = new PixelCanvas(24, 24, 13u);
            c.Fill(Sprint4Art.Clear);
            c.FillRect(1, 0, 22, 23, Sprint4Art.RedDark); // drop edge
            c.FillRect(0, 2, 24, 22, Sprint4Art.InkDeep);
            c.FillRect(1, 3, 22, 20, Sprint4Art.Red);
            c.FillRect(1, 3, 22, 2, Sprint4Art.RedDark);
            c.FillRect(1, 20, 22, 2, PixelCanvas.Mix(Sprint4Art.Red, Sprint4Art.White, 0.3f));
            c.Set(0, 2, Sprint4Art.Clear); c.Set(23, 2, Sprint4Art.Clear); c.Set(0, 23, Sprint4Art.Clear); c.Set(23, 23, Sprint4Art.Clear);
            return c;
        }

        /// <summary>Blue-and-cream tile (azulejo) repeated along the menu edges, as in the UI concept sheet.</summary>
        private static PixelCanvas Azulejo()
        {
            var c = new PixelCanvas(32, 32, 17u);
            c.Fill(Sprint4Art.White);
            foreach (var (x, y) in new[] { (0, 0), (32, 0), (0, 32), (32, 32) })
            {
                c.FillCircle(x, y, 9f, Sprint4Art.Blue);
                c.FillCircle(x, y, 6f, Sprint4Art.White);
                c.FillCircle(x, y, 3f, Sprint4Art.BlueDark);
            }
            c.FillPolygon(new[] { new Vector2(16, 5), new Vector2(27, 16), new Vector2(16, 27), new Vector2(5, 16) }, Sprint4Art.Blue);
            c.FillPolygon(new[] { new Vector2(16, 9.5f), new Vector2(22.5f, 16), new Vector2(16, 22.5f), new Vector2(9.5f, 16) }, Sprint4Art.White);
            c.FillCircle(16, 16, 2.6f, Sprint4Art.Gold);
            c.Border(0, 0, 32, 32, 1, Sprint4Art.PaperDark); // grout line between tiles
            return c;
        }

        private static void DrawPlay(PixelCanvas c)
        {
            c.FillPolygon(new[] { new Vector2(7, 3), new Vector2(29, 16), new Vector2(7, 29) }, Sprint4Art.InkDeep);
            c.FillPolygon(new[] { new Vector2(10, 8), new Vector2(23.5f, 16), new Vector2(10, 24) }, Sprint4Art.PaperLight);
        }

        private static void DrawBook(PixelCanvas c)
        {
            c.FillRect(1, 5, 30, 22, Sprint4Art.InkDeep);
            c.FillRect(3, 7, 12, 18, Sprint4Art.White);
            c.FillRect(17, 7, 12, 18, Sprint4Art.White);
            c.FillRect(15, 4, 2, 24, Sprint4Art.Red);
            for (int i = 0; i < 4; i++)
            {
                c.Line(5, 21 - i * 4, 13, 21 - i * 4, 1f, Sprint4Art.Ink);
                c.Line(19, 21 - i * 4, 27, 21 - i * 4, 1f, Sprint4Art.Ink);
            }
        }

        private static void DrawGear(PixelCanvas c)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Line(16 + Mathf.Cos(a) * 8, 16 + Mathf.Sin(a) * 8, 16 + Mathf.Cos(a) * 15, 16 + Mathf.Sin(a) * 15, 6f, Sprint4Art.InkDeep);
            }
            c.FillCircle(16, 16, 11.5f, Sprint4Art.InkDeep);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Line(16 + Mathf.Cos(a) * 8, 16 + Mathf.Sin(a) * 8, 16 + Mathf.Cos(a) * 13.5f, 16 + Mathf.Sin(a) * 13.5f, 3.4f, Sprint4Art.Gold);
            }
            c.FillCircle(16, 16, 10f, Sprint4Art.Gold);
            c.FillCircle(16, 16, 4.5f, Sprint4Art.InkDeep);
            c.FillCircle(16, 16, 2.5f, Sprint4Art.PaperLight);
        }

        private static void DrawBack(PixelCanvas c)
        {
            c.FillPolygon(new[] { new Vector2(2, 16), new Vector2(15, 3), new Vector2(15, 10), new Vector2(29, 10), new Vector2(29, 22), new Vector2(15, 22), new Vector2(15, 29) }, Sprint4Art.InkDeep);
            c.FillPolygon(new[] { new Vector2(5.5f, 16), new Vector2(13, 8.5f), new Vector2(13, 12), new Vector2(27, 12), new Vector2(27, 20), new Vector2(13, 20), new Vector2(13, 23.5f) }, Sprint4Art.Gold);
        }

        private static void DrawDie(PixelCanvas c)
        {
            c.FillRect(3, 2, 27, 27, Sprint4Art.InkDeep);
            c.FillRect(5, 4, 23, 23, Sprint4Art.White);
            c.FillRect(5, 4, 23, 3, Sprint4Art.PaperDark);
            foreach (var (x, y) in new[] { (10.5f, 21.5f), (16.5f, 15.5f), (22.5f, 9.5f) }) c.FillCircle(x, y, 2.6f, Sprint4Art.InkDeep);
        }

        private static void DrawCard(PixelCanvas c)
        {
            c.FillRect(6, 1, 20, 30, Sprint4Art.InkDeep);
            c.FillRect(8, 3, 16, 26, Sprint4Art.PaperLight);
            c.Border(10, 5, 12, 22, 1, Sprint4Art.Gold);
            c.Star(16, 16, 6.5f, 2.8f, Sprint4Art.Gold, Sprint4Art.InkDeep);
        }

        private static void DrawBounce(PixelCanvas c)
        {
            c.Arc(16, 11, 11, 3.2f, 0f, 180f, Sprint4Art.InkDeep);
            c.Arc(16, 11, 11, 1.4f, 8f, 172f, Sprint4Art.Gold);
            c.FillPolygon(new[] { new Vector2(0, 12), new Vector2(10, 12), new Vector2(5, 3) }, Sprint4Art.InkDeep);
            c.FillPolygon(new[] { new Vector2(2.5f, 11), new Vector2(7.5f, 11), new Vector2(5, 6) }, Sprint4Art.Gold);
            c.FillRect(25, 6, 3, 10, Sprint4Art.InkDeep); // the lucky "post" the move bounces from
            c.FillCircle(26.5f, 18f, 3.4f, Sprint4Art.Gold);
            c.Ring(26.5f, 18f, 3.4f, 1f, Sprint4Art.InkDeep);
        }

        // ================================================================== HUD prefab

        public static void BuildHud(Sprint4Art.UiArt ui, MenuArt art)
        {
            _font = DontTrustYourLuck.EditorTools.GameFonts.Ui();
            var root = PrefabUtility.LoadPrefabContents(Sprint4Hud.HudPrefabPath);
            try
            {
                var hud = root.GetComponent<GameHUD>();
                var old = root.transform.Find("MainMenu");
                if (old != null) Object.DestroyImmediate(old.gameObject); // idempotent on its own

                var hudGroups = new List<CanvasGroup>();
                foreach (var name in new[] { "TopBar", "PlayersPanel", "RightColumn", "CardHand", "AudioSettings", "ResultScreen" })
                {
                    var t = root.transform.Find(name);
                    if (t == null) continue;
                    var g = t.GetComponent<CanvasGroup>();
                    if (g == null) g = t.gameObject.AddComponent<CanvasGroup>();
                    hudGroups.Add(g);
                }

                ResultMenuButton(root.transform, ui);
                var menu = BuildMenu(root.transform, ui, art, hudGroups.ToArray(), out var menuAudio);
                menu.transform.SetAsLastSibling();
                hud.BindMainMenu(menu, menuAudio);
                hud.ApplyLabels();
                PrefabUtility.SaveAsPrefabAsset(root, Sprint4Hud.HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ResultMenuButton(Transform root, Sprint4Art.UiArt ui)
        {
            var card = root.Find("ResultScreen/Overlay/Card");
            var playAgain = card != null ? (RectTransform)card.Find("PlayAgainButton") : null;
            if (playAgain == null) return;
            var existing = card.Find("MenuButton");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            playAgain.sizeDelta = new Vector2(220f, playAgain.sizeDelta.y);
            playAgain.anchoredPosition = new Vector2(-118f, playAgain.anchoredPosition.y);
            var menuButton = (RectTransform)Object.Instantiate(playAgain.gameObject, card).transform;
            menuButton.name = "MenuButton";
            menuButton.anchoredPosition = new Vector2(118f, playAgain.anchoredPosition.y);
            var button = menuButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            var image = menuButton.GetComponent<Image>();
            if (image != null) image.sprite = ui.Button;
            var label = menuButton.Find("Label").GetComponent<Text>();
            root.Find("ResultScreen").GetComponent<ResultScreenView>().BindMenuButton(button, label);
        }

        private static MainMenuView BuildMenu(Transform root, Sprint4Art.UiArt ui, MenuArt art, CanvasGroup[] hudGroups, out AudioSettingsView menuAudio)
        {
            var menuRt = Rect("MainMenu", root);
            Stretch(menuRt);
            var rootGroup = menuRt.gameObject.AddComponent<CanvasGroup>();

            // Backdrop: warm dim over the diorama + azulejo bands (concept sheet), fades out on Play.
            var backdropRt = Rect("Backdrop", menuRt);
            Stretch(backdropRt);
            var backdropGroup = backdropRt.gameObject.AddComponent<CanvasGroup>();
            var dim = MakeImage("Dim", backdropRt, new Color(0.16f, 0.09f, 0.05f, 0.5f), null);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true; // the HUD underneath is not clickable while the menu is up
            foreach (var top in new[] { true, false })
            {
                var band = MakeImage(top ? "TopBand" : "BottomBand", backdropRt, Color.white, art.Azulejo);
                band.type = Image.Type.Tiled;
                band.pixelsPerUnitMultiplier = 1.4f;
                Anchor(band.rectTransform, new Vector2(0f, top ? 1f : 0f), new Vector2(1f, top ? 1f : 0f),
                    new Vector2(0f, top ? -26f : 0f), new Vector2(0f, top ? 0f : 26f));
                var edge = MakeImage("Edge", band.transform, Sprint4Art.InkDeep, null);
                Anchor(edge.rectTransform, new Vector2(0f, top ? 0f : 1f), new Vector2(1f, top ? 0f : 1f),
                    new Vector2(0f, top ? -3f : 0f), new Vector2(0f, top ? 0f : 3f));
            }

            // Logo (left), dropped in by the entrance animation.
            var logoRt = Rect("Logo", menuRt);
            Anchor(logoRt, new Vector2(0.03f, 0.1f), new Vector2(0.6f, 0.95f), Vector2.zero, Vector2.zero);
            logoRt.gameObject.AddComponent<CanvasGroup>();
            var logo = MakeImage("Image", logoRt, Color.white, art.Logo);
            logo.preserveAspect = true;
            Stretch(logo.rectTransform);

            // Menu card (right).
            var card = MakeImage("Card", menuRt, Color.white, ui.Banner);
            card.raycastTarget = true;
            Anchor(card.rectTransform, new Vector2(0.63f, 0.13f), new Vector2(0.965f, 0.88f), Vector2.zero, Vector2.zero);
            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            var plaque = MakeImage("TitlePlaque", card.transform, Color.white, ui.Title);
            Anchor(plaque.rectTransform, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -86f), new Vector2(0f, -22f));
            var menuTitle = MakeText("Title", plaque.transform, 30, Sprint4Art.RedDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(menuTitle.rectTransform);
            menuTitle.rectTransform.offsetMin = new Vector2(10f, 4f);
            menuTitle.rectTransform.offsetMax = new Vector2(-10f, -4f);
            FitText(menuTitle, 18, 30);

            var column = Rect("Buttons", card.transform);
            Anchor(column, new Vector2(0.09f, 0f), new Vector2(0.91f, 1f), new Vector2(0f, 34f), new Vector2(0f, -104f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var groups = new CanvasGroup[4];
            var play = MenuButton(column, "PlayButton", art.ButtonPrimary, art.IconPlay, Sprint4Art.White, 30, 72f, out var playLabel, out groups[0]);
            var howTo = MenuButton(column, "HowToButton", ui.Button, art.IconBook, HudPalette.Ink, 24, 60f, out var howToLabel, out groups[1]);
            var options = MenuButton(column, "OptionsButton", ui.Button, art.IconGear, HudPalette.Ink, 24, 60f, out var optionsLabel, out groups[2]);
            var credits = MenuButton(column, "CreditsButton", ui.Button, ui.IconStar, HudPalette.Ink, 24, 60f, out var creditsLabel, out groups[3]);
            playLabel.GetComponent<Shadow>().effectColor = new Color(0.36f, 0.08f, 0.06f, 0.9f);

            // Footer.
            var footerRt = Rect("Footer", menuRt);
            Anchor(footerRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 30f), new Vector2(-24f, 62f));
            var footerGroup = footerRt.gameObject.AddComponent<CanvasGroup>();
            footerGroup.blocksRaycasts = false;
            var hint = MakeText("Hint", footerRt, 17, Sprint4Art.PaperLight, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(hint.rectTransform);
            AddShadow(hint);
            var jam = MakeText("Event", footerRt, 17, Sprint4Art.PaperLight, TextAnchor.MiddleRight, FontStyle.Italic);
            Stretch(jam.rectTransform);
            AddShadow(jam);

            var view = menuRt.gameObject.AddComponent<MainMenuView>();
            view.Bind(rootGroup, backdropGroup, logoRt, card.rectTransform, cardGroup, groups, footerGroup, hudGroups,
                play, howTo, options, credits, playLabel, howToLabel, optionsLabel, creditsLabel, menuTitle, hint, jam);

            // Panels.
            var backs = new List<Button>();
            var backLabels = new List<Text>();
            var howToPanel = Window("HowToPanel", menuRt, ui, new Vector2(800f, 580f), out var howToTitle);
            var stepIcons = new[] { ui.IconSun, ui.IconEye, art.IconDie, art.IconBounce, art.IconCard, ui.IconClock };
            var steps = new Text[stepIcons.Length];
            for (int i = 0; i < stepIcons.Length; i++)
            {
                float top = -100f - i * 62f;
                var row = Rect($"Step{i + 1}", howToPanel.transform);
                Anchor(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, top - 58f), new Vector2(-40f, top));
                var badge = MakeImage("Badge", row, Color.white, ui.Die);
                Anchor(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -27f), new Vector2(54f, 27f));
                var icon = MakeImage("Icon", badge.transform, Color.white, stepIcons[i]);
                icon.preserveAspect = true;
                Anchor(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 10f), new Vector2(-10f, -8f));
                steps[i] = MakeText("Text", row, 20, Sprint4Art.InkDeep, TextAnchor.MiddleLeft, FontStyle.Normal);
                Anchor(steps[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(70f, 0f), Vector2.zero);
                steps[i].supportRichText = true;
                FitText(steps[i], 14, 20);
            }
            backs.Add(BackButton(howToPanel.transform, ui, art, out var l1)); backLabels.Add(l1);

            var optionsPanel = Window("OptionsPanel", menuRt, ui, new Vector2(540f, 400f), out var optionsTitle);
            menuAudio = OptionsContent(menuRt, optionsPanel.transform, ui);
            backs.Add(BackButton(optionsPanel.transform, ui, art, out var l2)); backLabels.Add(l2);

            var creditsPanel = Window("CreditsPanel", menuRt, ui, new Vector2(600f, 540f), out var creditsTitle);
            var body = MakeText("Body", creditsPanel.transform, 19, Sprint4Art.InkDeep, TextAnchor.UpperCenter, FontStyle.Normal);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 92f), new Vector2(-40f, -100f));
            body.supportRichText = true;
            FitText(body, 13, 19);
            backs.Add(BackButton(creditsPanel.transform, ui, art, out var l3)); backLabels.Add(l3);

            view.BindPanels(howToPanel, howToTitle, steps, optionsPanel, optionsTitle, creditsPanel, creditsTitle, body,
                backs.ToArray(), backLabels.ToArray());
            howToPanel.SetActive(false);
            optionsPanel.SetActive(false);
            creditsPanel.SetActive(false);
            view.ApplyLabels();
            return view;
        }

        private static Button MenuButton(Transform parent, string name, Sprite frame, Sprite iconSprite, Color ink, int size,
            float height, out Text label, out CanvasGroup group)
        {
            var image = MakeImage(name, parent, Color.white, frame);
            image.raycastTarget = true;
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            group = image.gameObject.AddComponent<CanvasGroup>();
            var button = image.gameObject.AddComponent<Button>();
            SetButtonColors(button);
            var icon = MakeImage("Icon", image.transform, Color.white, iconSprite);
            icon.preserveAspect = true;
            float s = height * 0.56f;
            Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -s / 2f + 2f), new Vector2(18f + s, s / 2f + 2f));
            label = MakeText("Label", image.transform, size, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f + s, 4f), new Vector2(-18f, 0f));
            FitText(label, 16, size);
            AddShadow(label).effectColor = new Color(1f, 1f, 1f, 0.35f);
            return button;
        }

        private static GameObject Window(string name, Transform parent, Sprint4Art.UiArt ui, Vector2 size, out Text title)
        {
            var window = MakeImage(name, parent, Color.white, ui.Banner);
            window.raycastTarget = true;
            var rt = window.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            window.gameObject.AddComponent<CanvasGroup>();
            var plaque = MakeImage("TitlePlaque", rt, Color.white, ui.Title);
            Anchor(plaque.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190f, -84f), new Vector2(190f, -22f));
            title = MakeText("Title", plaque.transform, 28, Sprint4Art.RedDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(10f, 4f);
            title.rectTransform.offsetMax = new Vector2(-10f, -4f);
            FitText(title, 16, 28);
            return window.gameObject;
        }

        private static Button BackButton(Transform window, Sprint4Art.UiArt ui, MenuArt art, out Text label)
        {
            var image = MakeImage("BackButton", window, Color.white, ui.Button);
            image.raycastTarget = true;
            Anchor(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 22f), new Vector2(110f, 76f));
            var button = image.gameObject.AddComponent<Button>();
            SetButtonColors(button);
            var icon = MakeImage("Icon", image.transform, Color.white, art.IconBack);
            icon.preserveAspect = true;
            Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -14f), new Vector2(46f, 16f));
            label = MakeText("Label", image.transform, 22, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 4f), new Vector2(-14f, 0f));
            return button;
        }

        /// <summary>Second AudioSettingsView (same model, kept in sync by the audio router through GameHUD).</summary>
        private static AudioSettingsView OptionsContent(Transform menuRoot, Transform window, Sprint4Art.UiArt ui)
        {
            var section = MakeText("SoundTitle", window, 20, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(section.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -128f), new Vector2(-48f, -96f));

            Slider Row(string name, float y, out Text label)
            {
                label = MakeText(name + "Label", window, 19, Sprint4Art.InkDeep, TextAnchor.MiddleLeft, FontStyle.Bold);
                Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, y - 32f), new Vector2(170f, y));
                var slider = Sprint4Hud.BuildSlider(name + "Slider", window, ui);
                Anchor((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(176f, y - 28f), new Vector2(-48f, y - 4f));
                return slider;
            }

            var master = Row("Master", -136f, out var masterLabel);
            var music = Row("Music", -180f, out var musicLabel);
            var sfx = Row("Sfx", -224f, out var sfxLabel);

            var toggleRoot = Rect("MuteToggle", window);
            Anchor(toggleRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -312f), new Vector2(-48f, -272f));
            var box = MakeImage("Box", toggleRoot, Color.white, ui.Die);
            box.type = Image.Type.Sliced;
            box.raycastTarget = true;
            Anchor(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -17f), new Vector2(34f, 17f));
            var check = MakeImage("Check", box.transform, Color.white, ui.Check);
            Anchor(check.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var muteLabel = MakeText("Label", toggleRoot, 19, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(muteLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(46f, 0f), Vector2.zero);
            var mute = toggleRoot.gameObject.AddComponent<Toggle>();
            mute.targetGraphic = box;
            mute.graphic = check;
            mute.isOn = false;

            // Lives on an always-active child so its Awake runs even while the panel is closed.
            var host = Rect("OptionsAudio", menuRoot);
            var view = host.gameObject.AddComponent<AudioSettingsView>();
            view.Bind(null, null, window.gameObject, section, master, music, sfx, masterLabel, musicLabel, sfxLabel, mute, muteLabel);
            return view;
        }

        // ================================================================== scene

        /// <summary>GameBoard waits for the menu's Play instead of starting a match on load.</summary>
        public static void ConfigureScene()
        {
            var scene = EditorSceneManager.OpenScene(Sprint4Scene.ScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<MatchDirector>();
            if (director == null)
            {
                Debug.LogError("[MainMenuBuilder] MatchDirector not found in GameBoard.");
                return;
            }
            var so = new SerializedObject(director);
            so.FindProperty("autoStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ================================================================== helpers

        private static void FitText(Text t, int min, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = max;
        }

        private static Shadow AddShadow(Text t)
        {
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.07f, 0.04f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return shadow;
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
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image MakeImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(string name, Transform parent, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
