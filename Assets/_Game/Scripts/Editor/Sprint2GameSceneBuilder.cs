using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Config;
using DontTrustYourLuck.Presentation.Demo;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DontTrustYourLuck.EditorTools
{
    /// <summary>
    /// Sprint 2: builds the GameHUD prefab (placeholder uGUI, vintage paper palette) and wires the playable match
    /// into the existing GameBoard scene (MatchDirector, HUD, EventSystem, background camera), removing the
    /// Sprint 1 movement demo. Idempotent: re-running replaces the HUD prefab contents and scene objects it owns.
    /// Run the Sprint 1 builder first if the scene does not exist.
    ///
    /// Menu: Tools > Don't Trust Your Luck > Build Sprint 2 Match (HUD + Scene)
    /// Batch: -executeMethod DontTrustYourLuck.EditorTools.Sprint2GameSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class Sprint2GameSceneBuilder
    {
        private const string Root = "Assets/_Game";
        private const string UiPrefabsDir = Root + "/Prefabs/UI";
        private const string HudPrefabPath = UiPrefabsDir + "/GameHUD.prefab";
        private const string ScenePath = Root + "/Scenes/GameBoard.unity";
        private const string BootstrapPath = Root + "/Scenes/Bootstrap.unity";
        private const string ConfigPath = Root + "/Data/DefaultMatchConfig.asset";
        private const string PiecePrefabPath = Root + "/Prefabs/Characters/PlayerPiece.prefab";

        private const float TopBarHeight = 76f;
        private const float Margin = 8f;
        private const float LeftWidth = 210f;
        private const float RightWidth = 260f;
        private const float HandHeight = 172f; // Sprint 3 card hand panel (bottom, between the columns)
        private const int HandSlots = 3;
        private const int TargetButtons = 4;

        private static Font _font;
        private static Sprite _panelSprite;

        [MenuItem("Tools/Don't Trust Your Luck/Build Sprint 2 Match (HUD + Scene)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine() => Build();

        private static void Build()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[Sprint2GameSceneBuilder] {ScenePath} not found. Run the Sprint 1 builder first.");
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            EnsureFolder(UiPrefabsDir);
            BuildHudPrefab();
            BuildScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Sprint2GameSceneBuilder] GameHUD prefab and GameBoard scene updated.");
        }

        // ------------------------------------------------------------------ HUD prefab

        private static void BuildHudPrefab()
        {
            var root = new GameObject("GameHUD", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var text = new HudText();

            // Top bar ---------------------------------------------------------
            var top = Rect("TopBar", root.transform);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(-2f * Margin, TopBarHeight);
            top.anchoredPosition = new Vector2(0f, -Margin);
            var topLayout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 8f;
            topLayout.childControlWidth = topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = topLayout.childForceExpandHeight = true;

            InfoCard(top, "LuckyCard", HudPalette.Lucky, HudPalette.Ink, out var luckyL, out var luckyV, 1.3f);
            InfoCard(top, "UnluckyCard", HudPalette.Danger, HudPalette.Card, out var unluckyL, out var unluckyV, 1.3f);
            InfoCard(top, "RoundCard", HudPalette.Card, HudPalette.Ink, out var roundL, out var roundV, 1f);
            var turnCard = InfoCard(top, "TurnCard", HudPalette.Card, HudPalette.Ink, out var turnL, out var turnV, 1.6f);
            var turnSwatch = Image("Swatch", turnCard, Color.white);
            Anchor(turnSwatch.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 6f), new Vector2(18f, -6f));
            var info = top.gameObject.AddComponent<MatchInfoView>();
            info.Bind(luckyL, luckyV, unluckyL, unluckyV, roundL, roundV, turnL, turnV, turnSwatch);

            // Left column: players --------------------------------------------
            var left = Panel("PlayersPanel", root.transform, HudPalette.Cream);
            Anchor(left.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(Margin, Margin), new Vector2(Margin + LeftWidth, -(TopBarHeight + 2f * Margin)));
            var playersTitle = Text("Title", left.transform, 16, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(playersTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -32f), new Vector2(-12f, -6f));
            var list = Rect("Cards", left.transform);
            Anchor(list, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -38f));
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10f;
            listLayout.childControlWidth = listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            var cards = new List<PlayerCardView>();
            for (int i = 0; i < 4; i++) cards.Add(PlayerCard(list, i + 1));
            var playerStatus = left.gameObject.AddComponent<PlayerStatusView>();
            playerStatus.Bind(playersTitle, cards);

            // Right column: dice + log ----------------------------------------
            var right = Rect("RightColumn", root.transform);
            Anchor(right, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-(Margin + RightWidth), Margin), new Vector2(-Margin, -(TopBarHeight + 2f * Margin)));
            var rightLayout = right.gameObject.AddComponent<VerticalLayoutGroup>();
            rightLayout.spacing = 8f;
            rightLayout.childControlWidth = rightLayout.childControlHeight = true;
            rightLayout.childForceExpandWidth = true;
            rightLayout.childForceExpandHeight = false;

            var dice = BuildDicePanel(right);
            var messages = BuildMessagePanel(right);

            // Bottom: card hand (Sprint 3) -------------------------------------
            var hand = BuildCardHand(root.transform, out var handRect);

            // Result overlay (last = drawn on top) ---------------------------
            var result = BuildResultScreen(root.transform);

            var hud = root.AddComponent<GameHUD>();
            hud.Bind(info, playerStatus, dice, messages, result, top, left.rectTransform, right);
            hud.BindCards(hand, handRect);
            hand.ApplyLabels(text);

            // Fill labels so the prefab reads correctly in Edit Mode.
            info.ApplyLabels(text);
            playerStatus.ApplyLabels(text);
            dice.ApplyLabels(text);
            messages.ApplyLabels(text);
            result.ApplyLabels(text);

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static RectTransform InfoCard(RectTransform parent, string name, Color bg, Color ink,
            out Text label, out Text value, float flexibleWidth)
        {
            var card = Panel(name, parent, bg);
            Outline(card.gameObject);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = flexibleWidth;
            label = Text("Label", card.transform, 15, ink, TextAnchor.UpperCenter, FontStyle.Bold);
            Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -24f), new Vector2(-6f, -4f));
            value = Text("Value", card.transform, 34, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 2f), new Vector2(-6f, -20f));
            value.resizeTextMaxSize = 34;
            value.resizeTextMinSize = 14;
            value.resizeTextForBestFit = true;
            return card.rectTransform;
        }

        private static PlayerCardView PlayerCard(RectTransform parent, int index)
        {
            var root = Rect($"PlayerCard_{index}", parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;

            var highlight = Image("Highlight", root, HudPalette.Lucky);
            Anchor(highlight.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            var background = Image("Background", root, HudPalette.Card);
            Stretch(background.rectTransform);
            Outline(background.gameObject);
            var swatch = Image("Swatch", root, Color.white);
            Anchor(swatch.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 6f), new Vector2(20f, -6f));

            var name = Text("Name", root, 18, HudPalette.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            Anchor(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 34f), new Vector2(-8f, -6f));
            var position = Text("Position", root, 15, HudPalette.Ink, TextAnchor.LowerLeft, FontStyle.Normal);
            Anchor(position.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 8f), new Vector2(-8f, -36f));
            var status = Text("Status", root, 15, HudPalette.Ink, TextAnchor.LowerRight, FontStyle.Bold);
            Anchor(status.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 8f), new Vector2(-10f, -36f));

            var view = root.gameObject.AddComponent<PlayerCardView>();
            view.Bind(swatch, highlight, background, name, position, status);
            return view;
        }

        private static DicePanelView BuildDicePanel(RectTransform parent)
        {
            var panel = Panel("DicePanel", parent, HudPalette.Cream);
            panel.gameObject.AddComponent<LayoutElement>().preferredHeight = 250f;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var status = Text("Status", panel.transform, 15, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            status.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            var row = Rect("DiceRow", panel.transform);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 132f;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = true;

            var values = new Text[2];
            var faces = new Image[2];
            var buttons = new Button[2];
            var labels = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                var column = Rect(i == 0 ? "DieA" : "DieB", row);
                var colLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
                colLayout.spacing = 6f;
                colLayout.childControlWidth = colLayout.childControlHeight = true;
                colLayout.childForceExpandWidth = true;
                colLayout.childForceExpandHeight = false;

                faces[i] = Panel("Face", column, HudPalette.Card);
                Outline(faces[i].gameObject);
                faces[i].gameObject.AddComponent<LayoutElement>().preferredHeight = 88f;
                values[i] = Text("Value", faces[i].transform, 52, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                Stretch(values[i].rectTransform);

                buttons[i] = Button("ChooseButton", column, HudPalette.Card, out labels[i], 16);
                buttons[i].gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            var roll = Button("RollButton", panel.transform, HudPalette.Lucky, out var rollLabel, 22);
            roll.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            var view = panel.gameObject.AddComponent<DicePanelView>();
            view.Bind(roll, rollLabel, values, faces, buttons, labels, status);
            return view;
        }

        /// <summary>
        /// Sprint 3 hand panel: three printed-card slots on the left; hint, target buttons (Swap) and Play/Cancel
        /// on the right. Sits between the player and dice columns so the board stays visible above it.
        /// </summary>
        private static CardHandView BuildCardHand(Transform parent, out RectTransform panelRect)
        {
            var panel = Panel("CardHand", parent, HudPalette.Cream);
            Outline(panel.gameObject);
            panelRect = panel.rectTransform;
            Anchor(panelRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(2f * Margin + LeftWidth, Margin), new Vector2(-(2f * Margin + RightWidth), Margin + HandHeight));

            var title = Text("Title", panel.transform, 15, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(12f, -26f), new Vector2(0f, -4f));

            const float slotWidth = 122f, spacing = 8f;
            float slotsWidth = HandSlots * slotWidth + (HandSlots - 1) * spacing;
            var slotArea = Rect("Slots", panel.transform);
            Anchor(slotArea, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 8f), new Vector2(10f + slotsWidth, -28f));
            var slotLayout = slotArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotLayout.spacing = spacing;
            slotLayout.childAlignment = TextAnchor.MiddleLeft;
            slotLayout.childControlWidth = slotLayout.childControlHeight = true;
            slotLayout.childForceExpandWidth = false;
            slotLayout.childForceExpandHeight = true;

            var slots = new List<CardSlotView>();
            for (int i = 0; i < HandSlots; i++) slots.Add(CardSlot(slotArea, i + 1, slotWidth));

            var empty = Text("Empty", panel.transform, 15, HudPalette.InkFaded, TextAnchor.MiddleCenter, FontStyle.Italic);
            Anchor(empty.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 8f), new Vector2(10f + slotsWidth, -28f));

            // Action area ------------------------------------------------------
            var actions = Rect("Actions", panel.transform);
            Anchor(actions, Vector2.zero, Vector2.one, new Vector2(20f + slotsWidth, 8f), new Vector2(-10f, -6f));

            var hint = Text("Hint", actions, 15, HudPalette.Ink, TextAnchor.UpperLeft, FontStyle.Italic);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -46f), Vector2.zero);

            var targetRow = Rect("Targets", actions);
            Anchor(targetRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 52f), new Vector2(0f, 90f));
            var targetLayout = targetRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            targetLayout.spacing = 6f;
            targetLayout.childControlWidth = targetLayout.childControlHeight = true;
            targetLayout.childForceExpandWidth = targetLayout.childForceExpandHeight = true;
            var targetButtons = new List<Button>();
            var targetLabels = new List<Text>();
            var targetSwatches = new List<Image>();
            for (int i = 0; i < TargetButtons; i++)
            {
                var b = Button($"Target_{i + 1}", targetRow, HudPalette.Card, out var label, 14);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(18f, 0f);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 14;
                var swatch = Image("Swatch", b.transform, Color.white);
                Anchor(swatch.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(5f, 6f), new Vector2(14f, -6f));
                targetButtons.Add(b);
                targetLabels.Add(label);
                targetSwatches.Add(swatch);
            }

            var buttonRow = Rect("Buttons", actions);
            Anchor(buttonRow, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 44f));
            var buttonLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 8f;
            buttonLayout.childControlWidth = buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = buttonLayout.childForceExpandHeight = true;
            var play = Button("PlayButton", buttonRow, HudPalette.Lucky, out var playLabel, 18);
            play.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.4f;
            var cancel = Button("CancelButton", buttonRow, HudPalette.Card, out var cancelLabel, 16);
            cancel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var view = panel.gameObject.AddComponent<CardHandView>();
            view.Bind(title, hint, empty, slots, targetRow.gameObject, targetButtons, targetLabels, targetSwatches,
                play, playLabel, cancel, cancelLabel);
            targetRow.gameObject.SetActive(false);
            return view;
        }

        private static CardSlotView CardSlot(RectTransform parent, int index, float width)
        {
            var root = Rect($"CardSlot_{index}", parent);
            root.gameObject.AddComponent<LayoutElement>().preferredWidth = width;

            var highlight = Image("Highlight", root, HudPalette.Lucky);
            Anchor(highlight.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));

            var background = Panel("Background", root, HudPalette.Card);
            Stretch(background.rectTransform);
            Outline(background.gameObject);
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            colors.disabledColor = Color.white; // the view fades the card itself (keeps the art readable)
            button.colors = colors;

            var art = Image("Art", root, Color.white);
            art.preserveAspect = true;
            var artRt = art.rectTransform;
            artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 1f);
            artRt.pivot = new Vector2(0.5f, 1f);
            artRt.sizeDelta = new Vector2(64f, 64f);
            artRt.anchoredPosition = new Vector2(0f, -5f);

            var name = Text("Name", root, 15, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -90f), new Vector2(-4f, -70f));
            var description = Text("Description", root, 12, HudPalette.Ink, TextAnchor.UpperCenter, FontStyle.Normal);
            Anchor(description.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 3f), new Vector2(-5f, -91f));
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 9;
            description.resizeTextMaxSize = 12;

            var view = root.gameObject.AddComponent<CardSlotView>();
            view.Bind(button, background, highlight, art, name, description);
            highlight.enabled = false;
            return view;
        }

        private static MessagePanelView BuildMessagePanel(RectTransform parent)
        {
            var panel = Panel("MessagePanel", parent, HudPalette.Cream);
            panel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var title = Text("Title", panel.transform, 16, HudPalette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-12f, -6f));
            var body = Text("Body", panel.transform, 15, HudPalette.Ink, TextAnchor.LowerLeft, FontStyle.Normal);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -34f));
            body.verticalOverflow = VerticalWrapMode.Truncate;
            var view = panel.gameObject.AddComponent<MessagePanelView>();
            view.Bind(title, body);
            return view;
        }

        private static ResultScreenView BuildResultScreen(Transform parent)
        {
            var host = Rect("ResultScreen", parent);
            Stretch(host);
            var overlay = Image("Overlay", host, HudPalette.Overlay);
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true; // blocks board/HUD clicks while visible

            var card = Panel("Card", overlay.transform, HudPalette.Card);
            Outline(card.gameObject);
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(540f, 320f);

            var title = Text("Title", card.transform, 38, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -86f), new Vector2(-16f, -20f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 38;

            var swatchRow = Rect("Winners", card.transform);
            swatchRow.anchorMin = swatchRow.anchorMax = new Vector2(0.5f, 1f);
            swatchRow.sizeDelta = new Vector2(220f, 34f);
            swatchRow.anchoredPosition = new Vector2(0f, -112f);
            var rowLayout = swatchRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            var swatches = new Image[4];
            for (int i = 0; i < swatches.Length; i++)
            {
                swatches[i] = Image($"Swatch_{i + 1}", swatchRow, Color.white);
                swatches[i].rectTransform.sizeDelta = new Vector2(34f, 34f);
                Outline(swatches[i].gameObject);
            }

            var reason = Text("Reason", card.transform, 19, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
            Anchor(reason.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(24f, -60f), new Vector2(-24f, 8f));

            var button = Button("PlayAgainButton", card.transform, HudPalette.Lucky, out var label, 22);
            var brt = (RectTransform)button.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(240f, 54f);
            brt.anchoredPosition = new Vector2(0f, 24f);

            var view = host.gameObject.AddComponent<ResultScreenView>();
            view.Bind(overlay.gameObject, title, reason, swatches, button, label);
            overlay.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ Scene

        private static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            // Remove Sprint 1 demo and objects previously created by this builder.
            foreach (var go in roots)
            {
                if (go.GetComponent<BoardMovementDemo>() != null || go.GetComponent<MatchDirector>() != null ||
                    go.GetComponent<GameHUD>() != null || go.GetComponent<EventSystem>() != null ||
                    go.name == "BackgroundCamera")
                {
                    Object.DestroyImmediate(go);
                }
            }

            var board = Object.FindAnyObjectByType<BoardView>();
            var rig = Object.FindAnyObjectByType<BoardCameraRig>();
            if (board == null || rig == null)
            {
                Debug.LogError("[Sprint2GameSceneBuilder] GameBoard scene lacks BoardView or BoardCameraRig. Run the Sprint 1 builder.");
                return;
            }

            var piecesRoot = GameObject.Find("Pieces")?.transform;
            if (piecesRoot == null) piecesRoot = new GameObject("Pieces").transform;
            var pieces = piecesRoot.GetComponentsInChildren<PlayerPieceView>(true).OrderBy(p => p.PlayerId).ToList();

            // Background camera clears the whole screen; the board camera renders only the HUD-free viewport.
            var mainCam = rig.Camera;
            var bgGo = new GameObject("BackgroundCamera");
            var bg = bgGo.AddComponent<Camera>();
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = mainCam.backgroundColor;
            bg.cullingMask = 0;
            bg.depth = mainCam.depth - 1f;
            bg.useOcclusionCulling = false;
            bgGo.transform.SetSiblingIndex(mainCam.transform.GetSiblingIndex());

            // EventSystem with the Input System UI module (project uses the new Input System only).
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            var moduleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) esGo.AddComponent(moduleType);
            else esGo.AddComponent<StandaloneInputModule>();

            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameHUD>(HudPrefabPath);
            var hud = (GameHUD)PrefabUtility.InstantiatePrefab(hudPrefab);

            var directorGo = new GameObject("MatchDirector");
            var director = directorGo.AddComponent<MatchDirector>();
            director.Configure(
                AssetDatabase.LoadAssetAtPath<MatchConfigAsset>(ConfigPath),
                board, rig, hud,
                AssetDatabase.LoadAssetAtPath<PlayerPieceView>(PiecePrefabPath),
                piecesRoot, pieces);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == ScenePath) continue;
                // Bootstrap (Sprint 0 smoke test) stays in the list but disabled so it is not shipped.
                scenes.Add(new EditorBuildSettingsScene(s.path, s.path == BootstrapPath ? false : s.enabled));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ UI helpers

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var image = Image(name, parent, color);
            image.sprite = _panelSprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        private static void Outline(GameObject go)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = HudPalette.Ink;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static Text Text(string name, Transform parent, int size, Color color, TextAnchor anchor, FontStyle style)
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

        private static Button Button(string name, Transform parent, Color color, out Text label, int fontSize)
        {
            var image = Panel(name, parent, color);
            image.raycastTarget = true;
            Outline(image.gameObject);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            colors.disabledColor = new Color(0.6f, 0.58f, 0.55f, 0.55f);
            button.colors = colors;
            label = Text("Label", image.transform, fontSize, HudPalette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform);
            return button;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
