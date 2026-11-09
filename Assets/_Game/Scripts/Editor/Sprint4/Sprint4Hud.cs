using System.Collections.Generic;
using DontTrustYourLuck.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.EditorTools.Sprint4
{
    /// <summary>
    /// Sprint 4 HUD pass ("lottery card" look). Runs on top of the Sprint 2/3 GameHUD prefab: keeps every view
    /// component (GameHUD, MatchInfoView, PlayerStatusView, DicePanelView, CardHandView, MessagePanelView,
    /// ResultScreenView) and its bindings, reskins images with the paper sprites and adds the new pieces:
    /// title plaque, lucky/unlucky icons, portraits, destination previews, feedback banner and audio settings.
    /// Not idempotent by itself: always run after the Sprint 2 HUD builder (the "All" menu does that).
    /// </summary>
    public static class Sprint4Hud
    {
        public const string HudPrefabPath = "Assets/_Game/Prefabs/UI/GameHUD.prefab";
        public const float LeftWidth = 236f; // Sprint 2: 210 (+26 for the portraits)
        private const float Margin = 8f;
        private const float TopBarHeight = 76f;

        private static Font _font;
        private static Sprite _builtinPanel;

        public static void Restyle(Sprint4Art.UiArt ui, IReadOnlyList<Sprint4Art.CharacterArt> characters)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _builtinPanel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                var hud = root.GetComponent<GameHUD>();
                Reskin(root.transform, ui);
                TopBar(root.transform, ui, out var title, out var subtitle, out var audioButton, out var audioLabel);
                Players(root.transform, ui, characters);
                Dice(root.transform, ui);
                Cards(root.transform, ui);
                Result(root.transform, ui);
                var banner = Banner(root.transform, ui);
                var audioView = AudioPopover(root.transform, ui, audioButton, audioLabel);

                // Overlay ordering: banner and popover above the HUD, result screen always on top.
                var result = root.transform.Find("ResultScreen");
                if (result != null) result.SetAsLastSibling();

                hud.BindSprint4(banner, audioView, title, subtitle);
                hud.ApplyLabels();
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ passes

        private static void Reskin(Transform root, Sprint4Art.UiArt ui)
        {
            foreach (var outline in root.GetComponentsInChildren<Outline>(true))
            {
                if (outline.GetComponent<Text>() == null) Object.DestroyImmediate(outline); // frames carry their own ink line
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != _builtinPanel) continue;
                bool isButton = image.GetComponent<Button>() != null && image.name != "Background";
                image.sprite = isButton ? ui.Button : ui.Paper;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
                if (!isButton && (image.color == HudPalette.Cream)) image.color = Color.white;
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.fontSize == 15) text.fontSize = 16; // GDD §9 readability: nothing below 16 px except card rules
                if (text.resizeTextForBestFit && text.resizeTextMinSize < 10) text.resizeTextMinSize = 10;
            }
        }

        private static void TopBar(Transform root, Sprint4Art.UiArt ui, out Text title, out Text subtitle, out Button audioButton, out Text audioLabel)
        {
            var top = (RectTransform)root.Find("TopBar");

            var plaque = MakeImage("TitlePlaque", top, Color.white, ui.Title);
            plaque.rectTransform.SetAsFirstSibling();
            plaque.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2.1f;
            title = MakeText("Title", plaque.transform, 24, Sprint4Art.RedDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 0.38f), Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, -6f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 24;
            subtitle = MakeText("Subtitle", plaque.transform, 16, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            Anchor(subtitle.rectTransform, Vector2.zero, new Vector2(1f, 0.42f), new Vector2(12f, 8f), new Vector2(-12f, 0f));

            InfoCard(top.Find("LuckyCard"), ui.Lucky, ui.IconSun, HudPalette.LuckyDark);
            InfoCard(top.Find("UnluckyCard"), ui.Unlucky, ui.IconEye, HudPalette.DangerDark);
            InfoCard(top.Find("RoundCard"), ui.Paper, ui.IconClock, HudPalette.Ink);
            var turn = top.Find("TurnCard");
            if (turn != null) turn.GetComponent<Image>().sprite = ui.Paper;

            // Sound button (opens the audio popover).
            var buttonImage = MakeImage("SoundButton", top, Color.white, ui.Button);
            buttonImage.raycastTarget = true;
            var layout = buttonImage.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 0f;
            layout.preferredWidth = 104f;
            layout.minWidth = 104f;
            audioButton = buttonImage.gameObject.AddComponent<Button>();
            SetButtonColors(audioButton);
            var icon = MakeImage("Icon", buttonImage.transform, Color.white, ui.IconSpeaker);
            Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16f, 2f), new Vector2(16f, 34f));
            audioLabel = MakeText("Label", buttonImage.transform, 16, HudPalette.Ink, TextAnchor.LowerCenter, FontStyle.Bold);
            Anchor(audioLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 8f), new Vector2(-4f, -4f));
        }

        private static void InfoCard(Transform card, Sprite frame, Sprite iconSprite, Color ink)
        {
            if (card == null) return;
            var image = card.GetComponent<Image>();
            image.sprite = frame;
            image.color = Color.white;
            var icon = MakeImage("Icon", card, Color.white, iconSprite);
            icon.preserveAspect = true;
            Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -17f), new Vector2(44f, 17f));
            foreach (var name in new[] { "Label", "Value" })
            {
                var t = card.Find(name)?.GetComponent<Text>();
                if (t == null) continue;
                t.color = ink;
                t.rectTransform.offsetMin = new Vector2(46f, t.rectTransform.offsetMin.y);
                if (name == "Label") t.rectTransform.offsetMax = new Vector2(-10f, -8f); // clear of the printed frame
            }
        }

        private static void Players(Transform root, Sprint4Art.UiArt ui, IReadOnlyList<Sprint4Art.CharacterArt> characters)
        {
            var left = (RectTransform)root.Find("PlayersPanel");
            left.offsetMax = new Vector2(Margin + LeftWidth, left.offsetMax.y);
            var hand = (RectTransform)root.Find("CardHand");
            if (hand != null) hand.offsetMin = new Vector2(2f * Margin + LeftWidth, hand.offsetMin.y);

            var cards = left.GetComponentsInChildren<PlayerCardView>(true);
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i].transform;
                ((LayoutElement)card.GetComponent<LayoutElement>()).preferredHeight = 76f;
                var background = card.Find("Background")?.GetComponent<Image>();
                if (background != null)
                {
                    background.sprite = ui.Card;
                    background.type = Image.Type.Sliced; // Sprint 2 created it as a plain Image
                }
                var highlight = card.Find("Highlight")?.GetComponent<Image>();
                if (highlight != null)
                {
                    highlight.sprite = ui.Highlight;
                    highlight.type = Image.Type.Sliced;
                    highlight.color = Color.white;
                }

                var medallion = MakeImage("Portrait", card, Color.white, i < characters.Count ? characters[i].Portrait : null);
                medallion.preserveAspect = true;
                Anchor(medallion.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -30f), new Vector2(84f, 30f));
                medallion.transform.SetSiblingIndex(card.Find("Swatch").GetSiblingIndex() + 1);
                foreach (var name in new[] { "Name", "Position", "Status" })
                {
                    var t = card.Find(name)?.GetComponent<Text>();
                    if (t != null) t.rectTransform.offsetMin = new Vector2(90f, t.rectTransform.offsetMin.y);
                }
                var status = card.Find("Status")?.GetComponent<Text>();
                if (status != null)
                {
                    status.alignment = TextAnchor.LowerRight;
                    status.rectTransform.offsetMax = new Vector2(-14f, status.rectTransform.offsetMax.y);
                }
                var nameText = card.Find("Name")?.GetComponent<Text>();
                if (nameText != null)
                {
                    // One line: shrink long names ("Capivara (Bot)") instead of wrapping over the tile line.
                    nameText.rectTransform.offsetMin = new Vector2(90f, 44f);
                    nameText.rectTransform.offsetMax = new Vector2(-12f, -8f);
                    nameText.resizeTextForBestFit = true;
                    nameText.resizeTextMinSize = 12;
                    nameText.resizeTextMaxSize = 18;
                    nameText.verticalOverflow = VerticalWrapMode.Truncate;
                }
                cards[i].BindPortrait(medallion);
            }
        }

        private static void Dice(Transform root, Sprint4Art.UiArt ui)
        {
            var panel = root.Find("RightColumn/DicePanel");
            if (panel == null) return;
            ((LayoutElement)panel.GetComponent<LayoutElement>()).preferredHeight = 278f;
            var row = panel.Find("DiceRow");
            ((LayoutElement)row.GetComponent<LayoutElement>()).preferredHeight = 160f;

            var destinations = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                var column = row.Find(i == 0 ? "DieA" : "DieB");
                var face = column.Find("Face").GetComponent<Image>();
                face.sprite = ui.Die;
                face.type = Image.Type.Sliced;
                var value = face.transform.Find("Value").GetComponent<Text>();
                value.rectTransform.offsetMin = new Vector2(0f, 4f); // optical center above the printed drop edge
                destinations[i] = MakeText("Destination", column, 16, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                destinations[i].gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
                destinations[i].text = "-> 0";
            }
            panel.GetComponent<DicePanelView>().BindDestinations(destinations);
        }

        private static void Cards(Transform root, Sprint4Art.UiArt ui)
        {
            var hand = root.Find("CardHand");
            if (hand == null) return;
            foreach (var slot in hand.GetComponentsInChildren<CardSlotView>(true))
            {
                var t = slot.transform;
                var lift = Rect("Lift", t);
                Stretch(lift);
                var children = new List<Transform>();
                foreach (Transform c in t)
                {
                    if (c != lift) children.Add(c);
                }
                foreach (var c in children) c.SetParent(lift, false);

                var background = lift.Find("Background")?.GetComponent<Image>();
                if (background != null)
                {
                    background.sprite = ui.Card;
                    background.type = Image.Type.Sliced;
                }
                var highlight = lift.Find("Highlight")?.GetComponent<Image>();
                if (highlight != null)
                {
                    highlight.sprite = ui.Highlight;
                    highlight.type = Image.Type.Sliced;
                    highlight.color = Color.white;
                }
                slot.BindLift(lift, 10f);
            }
        }

        private static void Result(Transform root, Sprint4Art.UiArt ui)
        {
            var card = root.Find("ResultScreen/Overlay/Card")?.GetComponent<Image>();
            if (card == null) return;
            card.sprite = ui.Banner;
            card.color = Color.white;
            var title = card.transform.Find("Title")?.GetComponent<Text>();
            if (title != null) title.color = Sprint4Art.RedDark;
            var star = MakeImage("Star", card.transform, Color.white, ui.IconStar);
            Anchor(star.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-24f, -18f), new Vector2(24f, 30f));
        }

        private static FeedbackBannerView Banner(Transform root, Sprint4Art.UiArt ui)
        {
            var host = Rect("FeedbackBanner", root);
            host.anchorMin = host.anchorMax = new Vector2(0.5f, 1f);
            host.pivot = new Vector2(0.5f, 1f);
            host.sizeDelta = new Vector2(520f, 92f);
            host.anchoredPosition = new Vector2(0f, -(TopBarHeight + 2f * Margin + 10f));
            var group = host.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var panel = MakeImage("Panel", host, Color.white, ui.Banner);
            Stretch(panel.rectTransform);
            var accent = MakeImage("Accent", panel.transform, HudPalette.Lucky, null);
            Anchor(accent.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 10f), new Vector2(-14f, 16f));
            var icon = MakeImage("Icon", panel.transform, Color.white, ui.IconStar);
            icon.preserveAspect = true;
            Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -30f), new Vector2(76f, 30f));
            var title = MakeText("Title", panel.transform, 28, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 0.42f), Vector2.one, new Vector2(88f, 0f), new Vector2(-16f, -8f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 28;
            var sub = MakeText("Subtitle", panel.transform, 17, HudPalette.Ink, TextAnchor.UpperLeft, FontStyle.Italic);
            Anchor(sub.rectTransform, Vector2.zero, new Vector2(1f, 0.44f), new Vector2(88f, 18f), new Vector2(-16f, 0f));

            var view = host.gameObject.AddComponent<FeedbackBannerView>();
            view.Bind(group, panel.rectTransform, panel, accent, icon, title, sub);
            return view;
        }

        private static AudioSettingsView AudioPopover(Transform root, Sprint4Art.UiArt ui, Button toggle, Text toggleLabel)
        {
            var host = Rect("AudioSettings", root);
            Stretch(host);

            var popover = MakeImage("Popover", host, Color.white, ui.Paper);
            popover.raycastTarget = true;
            var rt = popover.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(300f, 226f);
            rt.anchoredPosition = new Vector2(-Margin, -(TopBarHeight + 2f * Margin));

            var title = MakeText("Title", popover.transform, 18, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -40f), new Vector2(-16f, -12f));

            Slider SliderRow(string name, float y, out Text label)
            {
                label = MakeText(name + "Label", popover.transform, 16, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
                Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y - 28f), new Vector2(110f, y));
                var slider = BuildSlider(name + "Slider", popover.transform, ui);
                Anchor((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(116f, y - 24f), new Vector2(-18f, y - 4f));
                return slider;
            }

            var master = SliderRow("Master", -48f, out var masterLabel);
            var music = SliderRow("Music", -86f, out var musicLabel);
            var sfx = SliderRow("Sfx", -124f, out var sfxLabel);

            // Mute toggle.
            var toggleRoot = Rect("MuteToggle", popover.transform);
            Anchor(toggleRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -206f), new Vector2(-16f, -166f));
            var box = MakeImage("Box", toggleRoot, Color.white, ui.Die);
            box.type = Image.Type.Sliced;
            box.raycastTarget = true;
            Anchor(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -16f), new Vector2(32f, 16f));
            var check = MakeImage("Check", box.transform, Color.white, ui.Check);
            Anchor(check.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var muteLabel = MakeText("Label", toggleRoot, 16, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(muteLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(42f, 0f), Vector2.zero);
            var mute = toggleRoot.gameObject.AddComponent<Toggle>();
            mute.targetGraphic = box;
            mute.graphic = check;
            mute.isOn = false;

            var view = host.gameObject.AddComponent<AudioSettingsView>();
            view.Bind(toggle, toggleLabel, popover.gameObject, title, master, music, sfx, masterLabel, musicLabel, sfxLabel, mute, muteLabel);
            popover.gameObject.SetActive(false);
            return view;
        }

        internal static Slider BuildSlider(string name, Transform parent, Sprint4Art.UiArt ui)
        {
            var rt = Rect(name, parent);
            var bg = MakeImage("Background", rt, Color.white, ui.Bar);
            bg.type = Image.Type.Sliced;
            Anchor(bg.rectTransform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);
            var fillArea = Rect("Fill Area", rt);
            Anchor(fillArea, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(4f, 0f), new Vector2(-10f, 0f));
            var fill = MakeImage("Fill", fillArea, HudPalette.Lucky, null);
            Anchor(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 2f), new Vector2(10f, -2f));
            var handleArea = Rect("Handle Slide Area", rt);
            Anchor(handleArea, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            var handle = MakeImage("Handle", handleArea, Color.white, ui.Knob);
            handle.raycastTarget = true;
            Anchor(handle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-10f, -2f), new Vector2(10f, 2f));

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            SetButtonColors(slider);
            return slider;
        }

        // ------------------------------------------------------------------ helpers

        private static void SetButtonColors(Selectable s)
        {
            var colors = s.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.58f, 0.55f, 0.55f);
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
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
