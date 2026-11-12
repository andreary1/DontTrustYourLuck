using System.Collections.Generic;
using System.Linq;
using DontTrustYourLuck.Presentation.Art;
using DontTrustYourLuck.Presentation.Audio;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Characters;
using DontTrustYourLuck.Presentation.Feedback;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontTrustYourLuck.EditorTools.Sprint4
{
    /// <summary>
    /// Sprint 4 scene pass: two city dioramas around the existing board (same 31 logical tiles) shown one at a time
    /// by a BoardThemeSwitcher (Oaxaca market plaza, Fortaleza beachfront at sunset), standee pieces with the four
    /// animals, lucky/unlucky markers, particles, audio and visual feedback objects. Runs after the Sprint 2/3
    /// builders (they wire HUD + director). <see cref="RebuildBoardThemes"/> rebuilds only the dioramas.
    /// </summary>
    public static class Sprint4Scene
    {
        public const string ScenePath = "Assets/_Game/Scenes/GameBoard.unity";
        public const string CharactersDataDir = "Assets/_Game/Data/Characters";
        private const string BoardPrefabPath = "Assets/_Game/Prefabs/Board/BoardView.prefab";
        private const string PiecePrefabPath = "Assets/_Game/Prefabs/Characters/PlayerPiece.prefab";
        private const string ConfigPath = "Assets/_Game/Data/DefaultMatchConfig.asset";
        private const string MaterialsDir = Sprint4Art.MaterialsDir;

        private static readonly string[] OwnedRoots = { "Environment", "BoardMarkers", "Feedback", "Audio", "BackgroundCamera" };
        private static readonly string[] ThemeRoots = { "Environment", OaxacaRoot, FortalezaRoot, "BoardThemes" };

        // ------------------------------------------------------------------ assets (before the scene)

        public static List<CharacterDefinitionAsset> BuildCharacterAssets(IReadOnlyList<Sprint4Art.CharacterArt> art)
        {
            Sprint4Art.EnsureFolder(CharactersDataDir);
            var list = new List<CharacterDefinitionAsset>();
            foreach (var a in art)
            {
                string path = $"{CharactersDataDir}/CHR_{char.ToUpperInvariant(a.Id[0])}{a.Id.Substring(1)}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<CharacterDefinitionAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CharacterDefinitionAsset>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.Configure(a.Id, a.Name, a.Cutout, a.Portrait, a.Accent, 0.95f);
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }
            return list;
        }

        /// <summary>Updates the Sprint 1 tile materials in place (GUIDs kept) and rebuilds the standee piece prefab.</summary>
        public static void BuildBoardAndPieceAssets(Sprint4Art.EnvArt env)
        {
            Tile("M_Tile_Even", env.TileEven);
            Tile("M_Tile_Odd", env.TileOdd);
            Tile("M_Tile_Start", env.TileStart);
            Tile("M_Tile_Lucky", env.TileLucky);
            Tile("M_Tile_Unlucky", env.TileUnlucky);
            var beyond = Sprint4Art.Mat("M_Tile_Beyond", Color.white, env.TileBeyond);
            var connector = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_Path_Connector.mat");
            if (connector != null) Sprint4Art.ConfigureSurface(connector, Color.white, env.Wood, new Vector2(1f, 2f), false, false);

            var board = AssetDatabase.LoadAssetAtPath<BoardView>(BoardPrefabPath);
            if (board != null)
            {
                var so = new SerializedObject(board);
                so.FindProperty("materials.beyondLucky").objectReferenceValue = beyond;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var ring = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_PlayerPiece.mat");
            var card = Sprint4Art.Mat("M_Char_Cutout", Color.white, null, null, true, true);
            var standBase = Sprint4Art.Mat("M_Char_Base", Color.white, env.Wood);
            var aura = Sprint4Art.TransparentMat("M_FX_ShieldAura", new Color(0.45f, 0.85f, 0.55f, 0.35f));
            var temp = PlayerPieceView.CreateDefault(ring, null, card, standBase, aura);
            PrefabUtility.SaveAsPrefabAsset(temp.gameObject, PiecePrefabPath);
            Object.DestroyImmediate(temp.gameObject);
        }

        private static void Tile(string name, Texture2D tex)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
            if (m == null) m = Sprint4Art.Mat(name, Color.white, tex);
            else Sprint4Art.ConfigureSurface(m, Color.white, tex, null, false, false);
        }

        // ------------------------------------------------------------------ scene

        public static void BuildScene(Sprint4Art.EnvArt env, Sprint4Art.UiArt ui, List<CharacterDefinitionAsset> characters, SoundEffectLibrary library)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (OwnedRoots.Contains(go.name) || ThemeRoots.Contains(go.name)) Object.DestroyImmediate(go);
            }

            var board = Object.FindAnyObjectByType<BoardView>();
            var rig = Object.FindAnyObjectByType<BoardCameraRig>();
            var hud = Object.FindAnyObjectByType<GameHUD>();
            var director = Object.FindAnyObjectByType<MatchDirector>();
            if (board == null || rig == null || hud == null || director == null)
            {
                Debug.LogError("[Sprint4Scene] GameBoard scene is missing BoardView/BoardCameraRig/GameHUD/MatchDirector. Run Sprint 1-3 builders.");
                return;
            }

            // Board: re-apply prefab materials (beyondLucky) and rebuild the 31 tiles.
            var boardPrefab = AssetDatabase.LoadAssetAtPath<BoardView>(BoardPrefabPath);
            var so = new SerializedObject(boardPrefab);
            var tilePrefab = (BoardTileView)so.FindProperty("tilePrefab").objectReferenceValue;
            var materials = new BoardTileMaterials
            {
                even = Load("M_Tile_Even"), odd = Load("M_Tile_Odd"), start = Load("M_Tile_Start"),
                lucky = Load("M_Tile_Lucky"), unlucky = Load("M_Tile_Unlucky"),
                connector = Load("M_Path_Connector"), beyondLucky = Load("M_Tile_Beyond"), label = Load("M_TileLabel"),
            };
            int tileCount = board.TileCount > 0 ? board.TileCount : 31;
            board.Configure(tilePrefab, materials, tileCount);
            board.Build(tileCount);

            var sun = Lighting(rig.Camera);
            var themes = BoardThemes(env, board, rig, sun);
            var markers = Markers(ui, board);
            Pieces(director, board, rig, hud, characters);
            director.SetBoardThemes(themes);

            rig.Configure(board, 52f);
            rig.SetFullScreenSafeArea(true, 0.02f);
            rig.Frame();
            EditorUtility.SetDirty(rig);

            // Feedback --------------------------------------------------------
            var feedbackGo = new GameObject("Feedback");
            var confetti = Particles("Confetti", feedbackGo.transform, ParticleMat("M_FX_Confetti", env.VfxSquare), Confetti);
            var puff = Particles("Puff", feedbackGo.transform, ParticleMat("M_FX_Puff", env.VfxDot), Puff);
            var sparkle = Particles("Sparkle", feedbackGo.transform, ParticleMat("M_FX_Sparkle", env.VfxStar), Sparkle);
            var feedback = feedbackGo.AddComponent<VisualFeedbackController>();
            feedback.Configure(director, hud, markers.active, confetti, puff, sparkle, ui.IconEye, ui.IconShield);

            // Audio -----------------------------------------------------------
            var audioGo = new GameObject("Audio");
            var manager = audioGo.AddComponent<AudioManager>();
            manager.Configure(library, true);
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(audioGo.transform, false);
            var musicSource = musicGo.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            var music = musicGo.AddComponent<MusicController>();
            music.Configure(manager);
            var router = audioGo.AddComponent<AudioEventRouter>();
            router.Configure(director, hud, manager, music);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Rebuilds only the city dioramas and the theme switcher in GameBoard (regenerates the environment textures;
        /// HUD, menu, cards and pieces are untouched).
        /// Batch: -executeMethod DontTrustYourLuck.EditorTools.Sprint4.Sprint4Scene.RebuildBoardThemes
        /// </summary>
        [MenuItem("Tools/Don't Trust Your Luck/Rebuild City Boards (Oaxaca + Fortaleza)")]
        public static void RebuildBoardThemes()
        {
            var env = Sprint4Art.BuildEnvironment();
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (ThemeRoots.Contains(go.name)) Object.DestroyImmediate(go);
            }
            var board = Object.FindAnyObjectByType<BoardView>();
            var rig = Object.FindAnyObjectByType<BoardCameraRig>();
            var director = Object.FindAnyObjectByType<MatchDirector>();
            if (board == null || rig == null || director == null)
            {
                Debug.LogError("[Sprint4Scene] GameBoard scene is missing BoardView/BoardCameraRig/MatchDirector.");
                return;
            }
            var sun = Lighting(rig.Camera);
            director.SetBoardThemes(BoardThemes(env, board, rig, sun));
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Sprint4Scene] City boards rebuilt (Oaxaca + Fortaleza).");
        }

        private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");

        private static Light Lighting(Camera cam)
        {
            var light = Object.FindObjectsByType<Light>(FindObjectsInactive.Include).FirstOrDefault(l => l.type == LightType.Directional);
            if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = PixelCanvas.Hex(0xFFD9A8);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            light.transform.rotation = Quaternion.Euler(38f, -38f, 0f); // low late-afternoon sun from the west/left

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = PixelCanvas.Hex(0xB9D3E0);
            RenderSettings.ambientEquatorColor = PixelCanvas.Hex(0xE6C8A0);
            RenderSettings.ambientGroundColor = PixelCanvas.Hex(0x7A6650);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = PixelCanvas.Hex(0xF2D3A6);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 60f;
            RenderSettings.skybox = null;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PixelCanvas.Hex(0xF2D3A6);
            cam.farClipPlane = 90f;
            return light;
        }

        // ------------------------------------------------------------------ board themes

        public const string OaxacaRoot = "Environment_Oaxaca";
        public const string FortalezaRoot = "Environment_Fortaleza";

        /// <summary>Builds both city dioramas and the switcher that shows one of them (Oaxaca by default).</summary>
        private static BoardThemeSwitcher BoardThemes(Sprint4Art.EnvArt art, BoardView board, BoardCameraRig rig, Light sun)
        {
            var e = Materials(art);
            var oaxaca = EnvironmentOaxaca(e, art, board);
            var fortaleza = EnvironmentFortaleza(e, art);
            foreach (var root in new[] { oaxaca, fortaleza })
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }

            var themes = new List<BoardTheme>
            {
                new BoardTheme
                {
                    id = "oaxaca", displayName = "Oaxaca", environment = oaxaca.gameObject, shape = CircuitShape.Plaza, cameraPitch = 38f,
                    sunColor = PixelCanvas.Hex(0xFFD9A8), sunIntensity = 1.35f, sunEuler = new Vector3(38f, -38f, 0f), // late afternoon, front right
                    ambientSky = PixelCanvas.Hex(0xB9D3E0), ambientEquator = PixelCanvas.Hex(0xE6C8A0), ambientGround = PixelCanvas.Hex(0x7A6650),
                    fogColor = PixelCanvas.Hex(0xF2D3A6), fogStart = 26f, fogEnd = 60f,
                },
                new BoardTheme
                {
                    id = "fortaleza", displayName = "Fortaleza", environment = fortaleza.gameObject, shape = CircuitShape.Bay,
                    cameraPitch = 31f, // lower than Oaxaca: more sea and sunset sky above the board
                    // Golden sunset. The sun comes from the front left so the standees stay lit (the backdrop sun is
                    // decorative); warmer and lower than Oaxaca, with lavender sky fill.
                    sunColor = PixelCanvas.Hex(0xFFC888), sunIntensity = 1.3f, sunEuler = new Vector3(32f, 40f, 0f),
                    ambientSky = PixelCanvas.Hex(0xB4B2CC), ambientEquator = PixelCanvas.Hex(0xE9C49A), ambientGround = PixelCanvas.Hex(0x6B5848),
                    fogColor = PixelCanvas.Hex(0xF4C590), fogStart = 30f, fogEnd = 72f,
                },
            };
            fortaleza.gameObject.SetActive(false);

            var go = new GameObject("BoardThemes");
            var switcher = go.AddComponent<BoardThemeSwitcher>();
            switcher.Configure(board, rig, sun, themes, "oaxaca");
            EditorUtility.SetDirty(switcher);
            return switcher;
        }

        private sealed class Env
        {
            public Transform Root;
            public Material Cobble, Stone, Grass, Leaves, LeavesDark, Bark, Wood, Roof, Awning, Sand, Sail, Terracotta, Cream, Ink;
            public Material[] Facades, Flags, Fruit;
            // Oaxaca
            public Material SkyOaxaca, Cantera, Water, Jacaranda, JacarandaDark, Cactus, Agave, Talavera, BarroNegro;
            public Material[] Papel;
            // Fortaleza
            public Material SkyFortaleza, Calcadao, Sea, Bronze, Granite, Lamp;
            public Material[] Towers, Coconut;
        }

        private static Env Materials(Sprint4Art.EnvArt art)
        {
            return new Env
            {
                Cobble = Sprint4Art.Mat("M_Env_Cobble", Color.white, art.Cobble, new Vector2(12f, 8f)),
                Stone = Sprint4Art.Mat("M_Env_Stone", Color.white, art.Stone, new Vector2(4f, 3f)),
                Grass = Sprint4Art.Mat("M_Env_Grass", Color.white, art.Grass, new Vector2(2f, 2f)),
                Leaves = Sprint4Art.Mat("M_Env_Leaves", Color.white, art.Leaves),
                LeavesDark = Sprint4Art.Mat("M_Env_LeavesDark", Color.white, art.LeavesDark),
                Bark = Sprint4Art.Mat("M_Env_Bark", Color.white, art.Bark, new Vector2(1f, 3f)),
                Wood = Sprint4Art.Mat("M_Env_Wood", Color.white, art.Wood),
                Roof = Sprint4Art.Mat("M_Env_Roof", Color.white, art.Roof, new Vector2(2f, 1f)),
                Awning = Sprint4Art.Mat("M_Env_Awning", Color.white, art.Awning, new Vector2(2f, 1f), false, true),
                Sand = Sprint4Art.Mat("M_Env_Sand", Color.white, art.Sand, new Vector2(3f, 3f)),
                Sail = Sprint4Art.Mat("M_Env_Sail", Color.white, art.Sail, null, true, true),
                Terracotta = Sprint4Art.Mat("M_Env_Terracotta", Sprint4Art.Terracotta),
                Cream = Sprint4Art.Mat("M_Env_Cream", Sprint4Art.PaperLight),
                Ink = Sprint4Art.Mat("M_Env_Ink", Sprint4Art.Ink),
                Facades = art.Facades.Select((t, i) => Sprint4Art.Mat($"M_Env_Facade_{i}", Color.white, t)).ToArray(),
                Flags = new[] { Sprint4Art.Gold, Sprint4Art.Red, Sprint4Art.Green, Sprint4Art.Blue, Sprint4Art.Pink, Sprint4Art.Purple }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Flag_{i}", c, art.Flag, null, true, true)).ToArray(),
                Fruit = new[] { Sprint4Art.Gold, Sprint4Art.Red, Sprint4Art.Green, PixelCanvas.Hex(0xF28C28) }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Fruit_{i}", c)).ToArray(),

                SkyOaxaca = Sprint4Art.Mat("M_Env_Sky", Color.white, art.Sky, null, false, false, Sprint4Art.Unlit),
                Cantera = Sprint4Art.Mat("M_Env_CanteraVerde", PixelCanvas.Hex(0x9DBB96), art.Stone, new Vector2(2f, 2f)),
                Water = Sprint4Art.Mat("M_Env_Water", PixelCanvas.Hex(0x5FA8C8), null, null, false, false, Sprint4Art.SimpleLit, 0.5f),
                Jacaranda = Sprint4Art.Mat("M_Env_Jacaranda", Color.white, art.LeavesPurple),
                JacarandaDark = Sprint4Art.Mat("M_Env_JacarandaDark", PixelCanvas.Hex(0xC9B4DC), art.LeavesPurple),
                Cactus = Sprint4Art.Mat("M_Env_Cactus", Color.white, art.Cactus, new Vector2(2f, 2f)),
                Agave = Sprint4Art.Mat("M_Env_Agave", PixelCanvas.Hex(0xA9D2C6), art.Cactus),
                Talavera = Sprint4Art.Mat("M_Env_Talavera", Color.white, art.Talavera, new Vector2(2f, 1f)),
                BarroNegro = Sprint4Art.Mat("M_Env_BarroNegro", PixelCanvas.Hex(0x2E2A27), null, null, false, false, Sprint4Art.SimpleLit, 0.45f),
                Papel = new[] { Sprint4Art.Pink, Sprint4Art.Gold, Sprint4Art.Green, PixelCanvas.Hex(0xF28C28), Sprint4Art.Blue, Sprint4Art.Purple }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Papel_{i}", c, art.PapelPicado, null, true, true)).ToArray(),

                SkyFortaleza = Sprint4Art.Mat("M_Env_Sky_Fortaleza", Color.white, art.SkyFortaleza, null, false, false, Sprint4Art.Unlit),
                Calcadao = Sprint4Art.Mat("M_Env_Calcadao", Color.white, art.Calcadao, new Vector2(28f, 8.5f)),
                Sea = Sprint4Art.Mat("M_Env_Sea", Color.white, art.Sea, new Vector2(40f, 8f), false, false, Sprint4Art.SimpleLit, 0.35f),
                Bronze = Sprint4Art.Mat("M_Env_Bronze", PixelCanvas.Hex(0xB3743F), null, null, false, false, Sprint4Art.SimpleLit, 0.35f),
                Granite = Sprint4Art.Mat("M_Env_Granite", PixelCanvas.Hex(0x4A4A50), art.Stone, new Vector2(1f, 1f)),
                Lamp = Sprint4Art.Mat("M_Env_Lamp", PixelCanvas.Hex(0xFFE9B0), null, null, false, false, Sprint4Art.Unlit),
                Towers = new[] { Color.white, (Color)PixelCanvas.Hex(0xF6D9C0), (Color)PixelCanvas.Hex(0xD6E2EA) }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Tower_{i}", c, art.Tower, new Vector2(1f, 2f))).ToArray(),
                Coconut = new[] { PixelCanvas.Hex(0x6E9F3E), PixelCanvas.Hex(0x8DB24A), PixelCanvas.Hex(0x5A8A34) }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Coconut_{i}", c)).ToArray(),
            };
        }

        private static Transform NewRoot(Env e, string name)
        {
            e.Root = new GameObject(name).transform;
            return e.Root;
        }

        // ------------------------------------------------------------------ Oaxaca (market plaza, inland valley)

        private static Transform EnvironmentOaxaca(Env e, Sprint4Art.EnvArt art, BoardView board)
        {
            var root = NewRoot(e, OaxacaRoot);
            var b = board.GetBoardBounds(0f);
            float halfX = b.extents.x + 0.7f, halfZ = b.extents.z + 0.7f;
            var c0 = new Vector3(b.center.x, 0f, b.center.z);

            // Ground and raised stone stage (zócalo) -----------------------------------------
            Box(e, "Ground", e.Cobble, c0 + new Vector3(0f, -0.45f, 2f), new Vector3(44f, 0.3f, 30f), false);
            Box(e, "Stage_Border", e.Terracotta, c0 + new Vector3(0f, -0.2f, 0f), new Vector3(halfX * 2f + 0.5f, 0.3f, halfZ * 2f + 0.5f));
            Box(e, "Stage", e.Stone, c0 + new Vector3(0f, -0.08f, 0f), new Vector3(halfX * 2f, 0.16f, halfZ * 2f));
            Box(e, "Step", e.Stone, c0 + new Vector3(0f, -0.25f, -halfZ - 0.45f), new Vector3(3.2f, 0.2f, 0.5f));

            // Back row: colorful single-storey houses -------------------------------------------
            float backZ = c0.z + halfZ + 3.4f;
            float x = -21f;
            int i = 0;
            while (x < 21f)
            {
                float w = 3.6f + (i % 3) * 0.6f;
                float h = 3.2f + ((i * 7) % 4) * 0.35f;
                var house = new GameObject($"House_{i:00}").transform;
                house.SetParent(e.Root, false);
                house.localPosition = new Vector3(x + w * 0.5f, -0.3f, backZ + (i % 2) * 0.4f);
                Box(e, "Body", e.Facades[i % e.Facades.Length], new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, 2.6f), true, house);
                Box(e, "Cornice", e.Cream, new Vector3(0f, h + 0.06f, -0.05f), new Vector3(w + 0.1f, 0.12f, 2.7f), true, house);
                var roof = Prism(e, "Roof", e.Roof, new Vector3(0f, h - 0.1f, 0.5f), new Vector3(w - 0.2f, 0.8f, 1.6f), house);
                roof.localRotation = Quaternion.identity;
                x += w + 0.05f;
                i++;
            }
            // Sky backdrop: warm valley afternoon with the Sierra behind the roofs.
            Quad(e, "SkyBackdrop", e.SkyOaxaca, c0 + new Vector3(0f, 7f, backZ + 9f), new Vector3(60f, 22.5f, 1f), Quaternion.identity, false);

            // Papel picado over the back of the stage.
            float flagZ = c0.z + halfZ + 1.1f;
            Pole(e, new Vector3(c0.x - halfX - 0.3f, 0f, flagZ));
            Pole(e, new Vector3(c0.x + halfX + 0.3f, 0f, flagZ));
            Banner(e, e.Papel, new Vector3(c0.x - halfX - 0.3f, 3.2f, flagZ), new Vector3(c0.x + halfX + 0.3f, 3.2f, flagZ), 0.8f, true);
            Banner(e, e.Papel, new Vector3(c0.x - halfX - 0.3f, 3.0f, flagZ), new Vector3(c0.x - 1f, 4.4f, backZ - 1.2f), 0.4f, true);
            Banner(e, e.Papel, new Vector3(c0.x + halfX + 0.3f, 3.0f, flagZ), new Vector3(c0.x + 1f, 4.4f, backZ - 1.2f), 0.4f, true);

            // Left: green-stone fountain. Right: market fruit stall.
            Fountain(e, new Vector3(c0.x - halfX - 1.9f, 0f, c0.z + 1.2f));
            FruitStall(e, new Vector3(c0.x + halfX + 2f, 0f, c0.z + 1.0f), e.Fruit);

            // Trees and succulents: jacaranda, organ-pipe cacti, agave.
            Jacaranda(e, new Vector3(c0.x - halfX - 2.6f, 0f, c0.z + halfZ + 1.6f));
            OrganCactus(e, new Vector3(c0.x + halfX + 1.6f, 0f, c0.z + halfZ + 1.9f), 3.6f);
            OrganCactus(e, new Vector3(c0.x + halfX + 3.2f, 0f, c0.z + halfZ + 0.7f), 2.8f);
            Agave(e, new Vector3(c0.x - halfX - 3.6f, 0f, c0.z - 1.4f), 1.1f);
            OrganCactus(e, new Vector3(c0.x - halfX - 1.2f, 0f, c0.z - halfZ - 1.0f), 2.4f);

            // Front: talavera planters and benches flanking the steps.
            TalaveraPot(e, new Vector3(c0.x - 3.4f, 0f, c0.z - halfZ - 0.9f));
            TalaveraPot(e, new Vector3(c0.x + 3.4f, 0f, c0.z - halfZ - 0.9f));
            Bench(e, new Vector3(c0.x - 6.2f, 0f, c0.z - halfZ - 0.6f));
            Bench(e, new Vector3(c0.x + 6.2f, 0f, c0.z - halfZ - 0.6f));

            // Black-clay pottery stand (front right corner).
            BarroNegroStand(e, new Vector3(c0.x + halfX + 2.4f, 0f, c0.z - halfZ + 0.2f));
            return root;
        }

        // ------------------------------------------------------------------ Fortaleza (beachfront promenade at sunset)

        private static Transform EnvironmentFortaleza(Env e, Sprint4Art.EnvArt art)
        {
            var root = NewRoot(e, FortalezaRoot);
            const float shoreZ = 4.7f, cityX = -9.5f;

            // Promenade (calçadão) the board sits on; the city block continues behind it on the left.
            Box(e, "Calcadao", e.Calcadao, new Vector3(0f, -0.17f, -5f), new Vector3(64f, 0.3f, 19.4f), false);
            // Plain stone pad under the circuit so the tiles read clearly over the wave mosaic.
            Box(e, "Pad_Border", e.Terracotta, new Vector3(0f, -0.06f, -0.75f), new Vector3(14.3f, 0.12f, 9.5f));
            Box(e, "Pad", e.Stone, new Vector3(0f, -0.03f, -0.75f), new Vector3(13.9f, 0.1f, 9.1f));
            Box(e, "CityBlock", e.Stone, new Vector3(cityX - 16f, -0.17f, 14f), new Vector3(32f, 0.3f, 16f), false);
            // Seawall, sand and sea along the bay side.
            float shoreLen = 40f - cityX;
            float shoreMid = (cityX + 40f) * 0.5f;
            Box(e, "Seawall", e.Cream, new Vector3(shoreMid, 0.1f, shoreZ), new Vector3(shoreLen, 0.4f, 0.4f));
            Box(e, "Sand", e.Sand, new Vector3(shoreMid, -0.25f, shoreZ + 1.5f), new Vector3(shoreLen, 0.3f, 2.8f), false);
            // Shallow foam line where the sea meets the sand.
            Box(e, "Foam", e.Cream, new Vector3(shoreMid, -0.33f, shoreZ + 2.95f), new Vector3(shoreLen, 0.1f, 0.25f), false);
            Box(e, "Sea", e.Sea, new Vector3(4f, -0.45f, 20.4f), new Vector3(110f, 0.3f, 30f), false);
            Box(e, "SandSpit", e.Sand, new Vector3(27f, -0.3f, 21f), new Vector3(20f, 0.3f, 10f), false);
            Quad(e, "SkyBackdrop", e.SkyFortaleza, new Vector3(4f, 6f, 35f), new Vector3(96f, 34f, 1f), Quaternion.identity, false);

            // Beachfront towers: tall blocks on the left and across the bay on the right.
            int k = 0;
            for (float z = 2.5f; z < 20f; z += 3.6f)
            {
                for (float tx = cityX - 1.8f; tx > -36f; tx -= 3.4f)
                {
                    float w = 2.4f + (k % 3) * 0.4f, h = 6f + ((k * 5) % 7) * 0.9f;
                    Tower(e, $"Tower_{k:00}", new Vector3(tx, 0f, z + (k % 2) * 0.6f), w, h, e.Towers[k % e.Towers.Length]);
                    k++;
                }
            }
            for (int j = 0; j < 6; j++)
                Tower(e, $"BayTower_{j}", new Vector3(20f + j * 2.9f, -0.15f, 22f + (j % 2) * 1.2f), 2.4f, 5f + (j * 3 % 5) * 1.1f, e.Towers[j % e.Towers.Length]);

            // Coconut palms along the seawall and on the beach.
            Coqueiro(e, new Vector3(-7.9f, 0f, 4.1f), 4.6f, 8f);
            Coqueiro(e, new Vector3(7.9f, 0f, 4.0f), 4.9f, -9f);
            Coqueiro(e, new Vector3(11.2f, 0f, 4.3f), 4.2f, 12f);
            Coqueiro(e, new Vector3(14.8f, -0.1f, 6.0f), 5.2f, -14f);
            Coqueiro(e, new Vector3(9.4f, 0f, -5.8f), 3.8f, -10f);

            // Lamps and São João bandeirinhas over the seawall.
            foreach (float lx in new[] { -5.2f, 0f, 5.2f }) Lamp(e, new Vector3(lx, 0f, 4.25f));
            Banner(e, e.Flags, new Vector3(-5.2f, 2.45f, 4.25f), new Vector3(0f, 2.45f, 4.25f), 0.35f, false);
            Banner(e, e.Flags, new Vector3(0f, 2.45f, 4.25f), new Vector3(5.2f, 2.45f, 4.25f), 0.35f, false);

            // Jangadas resting on the sand and sailing in the bay.
            Jangada(e, new Vector3(-6.6f, -0.1f, 6.3f), 18f, false);
            Jangada(e, new Vector3(12.5f, -0.1f, 6.4f), -40f, false);
            // Jangadas out at sea (log raft + sail) so the bay reads as water at a glance.
            Jangada(e, new Vector3(3.5f, -0.38f, 12f), -10f, false);
            Jangada(e, new Vector3(-4f, -0.38f, 17.5f), 25f, false);
            Jangada(e, new Vector3(10f, -0.38f, 20f), -35f, false);

            // Generic bronze-ring monument on a dark pedestal (a nod to the seafront's landmarks, not a replica).
            RingMonument(e, new Vector3(9.4f, 0f, 2.0f));

            // Inland side: coconut kiosk, clock column, cajueiro and carnaúba, benches and planters.
            FruitStall(e, new Vector3(-8.8f, 0f, -1.4f), e.Coconut);
            ClockColumn(e, new Vector3(-9.6f, 0f, -5.4f));
            Cajueiro(e, new Vector3(-12.6f, 0f, 2.2f));
            Carnauba(e, new Vector3(10.6f, 0f, -2.2f));
            Bench(e, new Vector3(-4.4f, 0f, -7.3f));
            Bench(e, new Vector3(4.4f, 0f, -7.3f));
            Planter(e, new Vector3(-7.4f, 0f, -7.6f));
            Planter(e, new Vector3(7.2f, 0f, -7.8f));
            return root;
        }

        private static void Pole(Env e, Vector3 p)
        {
            Cyl(e, "Pole", e.Wood, p + new Vector3(0f, 1.7f, 0f), new Vector3(0.12f, 1.7f, 0.12f));
        }

        /// <summary>A string of hanging flags: triangular bandeirinhas or rectangular papel picado.</summary>
        private static void Banner(Env e, Material[] mats, Vector3 a, Vector3 b, float sag, bool papel)
        {
            var line = new GameObject(papel ? "PapelPicado" : "Bunting").transform;
            line.SetParent(e.Root, false);
            float spacing = papel ? 0.5f : 0.55f;
            int n = Mathf.Max(4, Mathf.RoundToInt(Vector3.Distance(a, b) / spacing));
            var size = papel ? new Vector3(0.42f, 0.52f, 1f) : new Vector3(0.34f, 0.36f, 1f);
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                var dir = (b - a).normalized;
                var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
                Quad(e, $"Flag_{k:00}", mats[k % mats.Length], p + Vector3.down * (size.y * 0.5f), size, rot, false, line);
            }
        }

        private static void Fountain(Env e, Vector3 p)
        {
            var root = Group(e, "Fountain", p);
            Cyl(e, "Basin", e.Cantera, new Vector3(0f, 0.3f, 0f), new Vector3(1.9f, 0.3f, 1.9f), root);
            var water = Cyl(e, "Water", e.Water, new Vector3(0f, 0.58f, 0f), new Vector3(1.65f, 0.02f, 1.65f), root);
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cyl(e, "Column", e.Cantera, new Vector3(0f, 1.1f, 0f), new Vector3(0.3f, 0.6f, 0.3f), root);
            Cyl(e, "Bowl", e.Cantera, new Vector3(0f, 1.75f, 0f), new Vector3(0.95f, 0.1f, 0.95f), root);
            Cyl(e, "BowlWater", e.Water, new Vector3(0f, 1.86f, 0f), new Vector3(0.8f, 0.02f, 0.8f), root).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Sphere(e, "Finial", e.Cantera, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.32f, root);
        }

        private static void FruitStall(Env e, Vector3 p, Material[] fruit)
        {
            var root = Group(e, "FruitStall", p);
            root.localRotation = Quaternion.Euler(0f, -12f, 0f);
            Box(e, "Counter", e.Wood, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 0.9f), true, root);
            foreach (float sx in new[] { -0.85f, 0.85f })
                Box(e, "Post", e.Wood, new Vector3(sx, 1.25f, 0.4f), new Vector3(0.08f, 2.5f, 0.08f), true, root);
            var awning = Box(e, "Awning", e.Awning, new Vector3(0f, 2.35f, 0.05f), new Vector3(2.1f, 0.06f, 1.3f), true, root);
            awning.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            for (int k = 0; k < 9; k++)
            {
                float fx = -0.65f + (k % 5) * 0.32f, fz = -0.2f + (k / 5) * 0.3f;
                var s = Sphere(e, $"Fruit_{k}", fruit[k % fruit.Length], new Vector3(fx, 0.98f, fz), Vector3.one * (0.2f + (k % 3) * 0.03f), root);
                s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void Jacaranda(Env e, Vector3 p)
        {
            var root = Group(e, "Jacaranda", p);
            var trunk = Cyl(e, "Trunk", e.Bark, new Vector3(0f, 1.0f, 0f), new Vector3(0.32f, 1.0f, 0.32f), root);
            trunk.localRotation = Quaternion.Euler(0f, 0f, -6f);
            var branch = Cyl(e, "Branch", e.Bark, new Vector3(-0.45f, 2.0f, 0f), new Vector3(0.18f, 0.6f, 0.18f), root);
            branch.localRotation = Quaternion.Euler(0f, 0f, 50f);
            Sphere(e, "Crown_A", e.Jacaranda, new Vector3(0.1f, 2.7f, 0f), new Vector3(2.6f, 1.4f, 2.2f), root);
            Sphere(e, "Crown_B", e.JacarandaDark, new Vector3(-1.1f, 2.4f, 0.2f), new Vector3(1.8f, 1.1f, 1.7f), root);
            Sphere(e, "Crown_C", e.Jacaranda, new Vector3(0.6f, 3.2f, 0.3f), new Vector3(1.7f, 1.0f, 1.5f), root);
            for (int k = 0; k < 5; k++) // fallen blossoms
            {
                var petal = Cyl(e, $"Petals_{k}", e.Jacaranda, new Vector3(-1.2f + k * 0.6f, 0.01f, -0.6f + (k % 2) * 0.9f), new Vector3(0.5f, 0.005f, 0.4f), root);
                petal.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>Organ-pipe cactus (órgano): a cluster of ribbed columns.</summary>
        private static void OrganCactus(Env e, Vector3 p, float height)
        {
            var root = Group(e, "OrganCactus", p);
            var arms = new[] { (0f, 0f, 1f), (0.32f, 0.1f, 0.82f), (-0.3f, 0.12f, 0.74f), (0.08f, -0.3f, 0.66f) };
            for (int k = 0; k < arms.Length; k++)
            {
                var (ax, az, hs) = arms[k];
                float h = height * hs;
                Cyl(e, $"Column_{k}", e.Cactus, new Vector3(ax, h * 0.5f, az), new Vector3(0.26f, h * 0.5f, 0.26f), root);
                Sphere(e, $"Tip_{k}", e.Cactus, new Vector3(ax, h, az), new Vector3(0.26f, 0.2f, 0.26f), root);
            }
        }

        private static void Agave(Env e, Vector3 p, float size)
        {
            var root = Group(e, "Agave", p);
            for (int k = 0; k < 10; k++)
            {
                var leaf = Box(e, $"Leaf_{k}", e.Agave, Vector3.zero, new Vector3(0.22f * size, 0.05f, 1.0f * size), true, root);
                leaf.localRotation = Quaternion.Euler(-35f - (k % 3) * 15f, k * 36f, 0f);
                leaf.localPosition = new Vector3(0f, 0.15f, 0f) + leaf.localRotation * new Vector3(0f, 0f, 0.45f * size);
            }
        }

        private static void TalaveraPot(Env e, Vector3 p)
        {
            var root = Group(e, "TalaveraPot", p);
            Cyl(e, "Pot", e.Talavera, new Vector3(0f, 0.35f, 0f), new Vector3(0.75f, 0.35f, 0.75f), root);
            Cyl(e, "Rim", e.Talavera, new Vector3(0f, 0.72f, 0f), new Vector3(0.85f, 0.04f, 0.85f), root);
            Sphere(e, "Bush", e.Leaves, new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.55f, 0.8f), root);
            for (int k = 0; k < 3; k++)
                Sphere(e, $"Flower_{k}", e.Fruit[k == 1 ? 0 : 1], new Vector3(-0.2f + k * 0.2f, 1.18f, -0.15f), Vector3.one * 0.13f, root);
        }

        /// <summary>Black-clay (barro negro) pottery on a wooden table.</summary>
        private static void BarroNegroStand(Env e, Vector3 p)
        {
            var root = Group(e, "BarroNegroStand", p);
            root.localRotation = Quaternion.Euler(0f, -30f, 0f);
            Box(e, "Table", e.Wood, new Vector3(0f, 0.5f, 0f), new Vector3(2.0f, 0.08f, 1.0f), true, root);
            foreach (var (lx, lz) in new[] { (-0.9f, -0.4f), (0.9f, -0.4f), (-0.9f, 0.4f), (0.9f, 0.4f) })
                Box(e, "Leg", e.Wood, new Vector3(lx, 0.25f, lz), new Vector3(0.08f, 0.5f, 0.08f), true, root);
            var pots = new[] { (-0.6f, 0.0f, 0.36f), (-0.15f, 0.15f, 0.46f), (0.35f, -0.1f, 0.3f), (0.7f, 0.2f, 0.4f), (0.1f, -0.3f, 0.26f) };
            for (int k = 0; k < pots.Length; k++)
            {
                var (px, pz, r) = pots[k];
                Sphere(e, $"Pot_{k}", e.BarroNegro, new Vector3(px, 0.54f + r * 0.5f, pz), new Vector3(r, r * 0.95f, r), root);
                Cyl(e, $"Neck_{k}", e.BarroNegro, new Vector3(px, 0.54f + r * 1.0f, pz), new Vector3(r * 0.4f, r * 0.12f, r * 0.4f), root);
            }
            Box(e, "Cloth", e.Papel[0], new Vector3(0f, 0.55f, -0.45f), new Vector3(2.0f, 0.02f, 0.12f), false, root);
        }

        private static void Tower(Env e, string name, Vector3 p, float w, float h, Material facade)
        {
            var root = Group(e, name, p);
            var body = Box(e, "Body", facade, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, 2.4f), true, root);
            body.GetComponent<Renderer>().receiveShadows = false;
            Box(e, "Roof", e.Cream, new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.1f, 0.2f, 2.5f), false, root);
            Box(e, "Tank", e.Stone, new Vector3(w * 0.2f, h + 0.45f, 0.3f), new Vector3(w * 0.35f, 0.5f, 0.8f), false, root);
        }

        private static void Lamp(Env e, Vector3 p)
        {
            var root = Group(e, "Lamp", p);
            Cyl(e, "Post", e.Ink, new Vector3(0f, 1.3f, 0f), new Vector3(0.08f, 1.3f, 0.08f), root);
            var globe = Sphere(e, "Globe", e.Lamp, new Vector3(0f, 2.7f, 0f), Vector3.one * 0.3f, root);
            globe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Bronze ring standing on a tapered granite pedestal (generic seafront landmark).</summary>
        private static void RingMonument(Env e, Vector3 p)
        {
            var root = Group(e, "RingMonument", p);
            root.localRotation = Quaternion.Euler(0f, -28f, 0f);
            Box(e, "Base", e.Granite, new Vector3(0f, 0.1f, 0f), new Vector3(1.7f, 0.2f, 1.4f), true, root);
            Box(e, "Pedestal", e.Granite, new Vector3(0f, 0.65f, 0f), new Vector3(1.2f, 0.9f, 1.0f), true, root);
            Box(e, "Cap", e.Granite, new Vector3(0f, 1.14f, 0f), new Vector3(1.0f, 0.08f, 0.85f), true, root);
            var ring = new GameObject("Ring").transform;
            ring.SetParent(root, false);
            ring.localPosition = new Vector3(0f, 2.33f, 0f);
            ring.localRotation = Quaternion.Euler(0f, 0f, 14f);
            const int segments = 28;
            const float radius = 1.1f;
            for (int k = 0; k < segments; k++)
            {
                float a = k * Mathf.PI * 2f / segments;
                var seg = Box(e, $"Seg_{k:00}", e.Bronze, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f),
                    new Vector3(0.1f, 2f * Mathf.PI * radius / segments + 0.03f, 0.1f), true, ring);
                seg.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            Box(e, "Stem", e.Bronze, new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 0.1f, 0.14f), true, root);
        }

        // ------------------------------------------------------------------ Brazilian props (Fortaleza)

        private static void ClockColumn(Env e, Vector3 p)
        {
            var root = Group(e, "ClockColumn", p);
            Box(e, "Plinth", e.Stone, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 1.2f), true, root);
            Cyl(e, "Shaft", e.Cream, new Vector3(0f, 1.9f, 0f), new Vector3(0.45f, 1.3f, 0.45f), root);
            Box(e, "Capital", e.Stone, new Vector3(0f, 3.25f, 0f), new Vector3(0.8f, 0.15f, 0.8f), true, root);
            Box(e, "ClockBox", e.Cream, new Vector3(0f, 3.75f, 0f), new Vector3(0.9f, 0.9f, 0.9f), true, root);
            Box(e, "ClockRoof", e.Terracotta, new Vector3(0f, 4.3f, 0f), new Vector3(1.05f, 0.18f, 1.05f), true, root);
            var face = Cyl(e, "ClockFace", e.Ink, new Vector3(0f, 3.75f, -0.46f), new Vector3(0.62f, 0.02f, 0.62f), root);
            face.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var dial = Cyl(e, "Dial", e.Cream, new Vector3(0f, 3.75f, -0.48f), new Vector3(0.52f, 0.02f, 0.52f), root);
            dial.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box(e, "HandH", e.Ink, new Vector3(0.06f, 3.8f, -0.5f), new Vector3(0.16f, 0.04f, 0.01f), false, root);
            Box(e, "HandM", e.Ink, new Vector3(0f, 3.88f, -0.5f), new Vector3(0.04f, 0.22f, 0.01f), false, root);
        }

        private static void Cajueiro(Env e, Vector3 p)
        {
            var root = Group(e, "Cajueiro", p);
            var trunk = Cyl(e, "Trunk", e.Bark, new Vector3(0f, 0.9f, 0f), new Vector3(0.38f, 0.9f, 0.38f), root);
            trunk.localRotation = Quaternion.Euler(0f, 0f, 10f);
            var branch = Cyl(e, "Branch", e.Bark, new Vector3(0.5f, 1.8f, 0f), new Vector3(0.22f, 0.7f, 0.22f), root);
            branch.localRotation = Quaternion.Euler(0f, 0f, -55f);
            Sphere(e, "Crown_A", e.Leaves, new Vector3(-0.2f, 2.5f, 0f), new Vector3(2.6f, 1.5f, 2.2f), root);
            Sphere(e, "Crown_B", e.LeavesDark, new Vector3(1.2f, 2.2f, 0.2f), new Vector3(2.0f, 1.2f, 1.8f), root);
            Sphere(e, "Crown_C", e.Leaves, new Vector3(0.4f, 3.0f, 0.4f), new Vector3(1.8f, 1.1f, 1.6f), root);
            for (int k = 0; k < 6; k++) // cashew fruits
            {
                var f = Sphere(e, $"Caju_{k}", e.Fruit[k % 2 == 0 ? 1 : 0], new Vector3(-0.9f + k * 0.4f, 1.85f + (k % 2) * 0.2f, -0.9f), new Vector3(0.14f, 0.18f, 0.14f), root);
                f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void Coqueiro(Env e, Vector3 p, float height, float lean)
        {
            var root = Group(e, "Coqueiro", p);
            var trunkPivot = new GameObject("Lean").transform;
            trunkPivot.SetParent(root, false);
            trunkPivot.localRotation = Quaternion.Euler(0f, 0f, lean);
            int seg = 4;
            for (int k = 0; k < seg; k++)
            {
                float r = 0.26f - k * 0.025f;
                Cyl(e, $"Trunk_{k}", e.Bark, new Vector3(0f, height * (k + 0.5f) / seg, 0f), new Vector3(r, height * 0.5f / seg, r), trunkPivot);
            }
            var top = new Vector3(0f, height, 0f);
            for (int k = 0; k < 7; k++)
            {
                var frond = Box(e, $"Frond_{k}", k % 2 == 0 ? e.Leaves : e.LeavesDark, top, new Vector3(0.38f, 0.05f, 1.9f), true, trunkPivot);
                frond.localRotation = Quaternion.Euler(28f + (k % 3) * 6f, k * 360f / 7f, 0f);
                frond.localPosition = top + frond.localRotation * new Vector3(0f, 0f, 0.85f);
            }
            for (int k = 0; k < 3; k++)
                Sphere(e, $"Coco_{k}", e.Bark, top + new Vector3(Mathf.Cos(k * 2.1f) * 0.2f, -0.2f, Mathf.Sin(k * 2.1f) * 0.2f), Vector3.one * 0.22f, trunkPivot);
        }

        private static void Carnauba(Env e, Vector3 p)
        {
            var root = Group(e, "Carnauba", p);
            Cyl(e, "Trunk", e.Bark, new Vector3(0f, 1.6f, 0f), new Vector3(0.24f, 1.6f, 0.24f), root);
            for (int k = 0; k < 9; k++)
            {
                var fan = Box(e, $"Fan_{k}", e.LeavesDark, new Vector3(0f, 3.3f, 0f), new Vector3(0.9f, 0.04f, 1.1f), true, root);
                fan.localRotation = Quaternion.Euler(-20f - (k % 3) * 15f, k * 40f, 0f);
                fan.localPosition = new Vector3(0f, 3.3f, 0f) + fan.localRotation * new Vector3(0f, 0f, 0.55f);
            }
        }

        private static void Planter(Env e, Vector3 p)
        {
            var root = Group(e, "Planter", p);
            Box(e, "Box", e.Terracotta, new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.5f, 0.6f), true, root);
            Box(e, "Soil", e.Grass, new Vector3(0f, 0.52f, 0f), new Vector3(1.45f, 0.06f, 0.48f), true, root);
            for (int k = 0; k < 3; k++)
                Sphere(e, $"Bush_{k}", k == 1 ? e.LeavesDark : e.Leaves, new Vector3(-0.45f + k * 0.45f, 0.75f, 0f), new Vector3(0.55f, 0.45f, 0.5f), root);
            Sphere(e, "Flower", e.Fruit[1], new Vector3(0.2f, 0.98f, -0.1f), Vector3.one * 0.14f, root);
        }

        private static void Bench(Env e, Vector3 p)
        {
            var root = Group(e, "Bench", p);
            Box(e, "Seat", e.Wood, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 0.45f), true, root);
            Box(e, "Back", e.Wood, new Vector3(0f, 0.75f, 0.22f), new Vector3(1.6f, 0.4f, 0.06f), true, root);
            foreach (float sx in new[] { -0.7f, 0.7f })
                Box(e, "Leg", e.Ink, new Vector3(sx, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.4f), true, root);
        }

        private static void Jangada(Env e, Vector3 p, float yaw, bool sandBase)
        {
            var root = Group(e, "Jangada", p);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (sandBase) Box(e, "Sand", e.Sand, new Vector3(0f, 0.12f, 0f), new Vector3(2.4f, 0.24f, 1.4f), true, root);
            float y = sandBase ? 0.34f : 0.12f;
            for (int k = 0; k < 4; k++)
                Cyl(e, $"Log_{k}", e.Wood, new Vector3(-0.3f + k * 0.2f, y, 0f), new Vector3(0.18f, 0.9f, 0.18f), root).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box(e, "Mast", e.Wood, new Vector3(0f, y + 0.96f, 0.1f), new Vector3(0.06f, 1.9f, 0.06f), true, root);
            Quad(e, "Sail", e.Sail, new Vector3(0.45f, y + 1.11f, 0.1f), new Vector3(1.0f, 1.6f, 1f), Quaternion.Euler(0f, 90f, 0f), true, root);
        }

        // ------------------------------------------------------------------ markers and pieces

        private static (Transform lucky, Transform unlucky, Transform active) Markers(Sprint4Art.UiArt ui, BoardView board)
        {
            var root = new GameObject("BoardMarkers").transform;
            var lucky = Marker("LuckyMarker", root, IconMat("M_FX_IconSun", ui.IconSun), 0.9f, 0.1f, 0.08f);
            var unlucky = Marker("UnluckyMarker", root, IconMat("M_FX_IconEye", ui.IconEye), 0.85f, 0.06f, 0.1f);
            var active = Marker("ActivePlayerMarker", root, IconMat("M_FX_IconArrow", ui.IconArrow), 0.5f, 0.12f, 0f);
            active.gameObject.SetActive(false);
            board.SetMarkers(lucky, unlucky);
            board.SetHighlights(board.LuckyIndex, board.UnluckyIndex); // hides markers until the match sets numbers
            return (lucky, unlucky, active);
        }

        private static Transform Marker(string name, Transform parent, Material mat, float size, float bob, float pulse)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var holder = new GameObject("Bob").transform;
            holder.SetParent(root, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Icon";
            quad.transform.SetParent(holder, false);
            quad.transform.localScale = Vector3.one * size;
            quad.transform.localRotation = Quaternion.Euler(52f, 0f, 0f); // faces the fixed camera pitch
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            root.gameObject.AddComponent<BobAndSpin>().Configure(holder, bob, 0f, pulse, 2.2f);
            return root;
        }

        private static Material IconMat(string name, Sprite icon) =>
            Sprint4Art.Mat(name, Color.white, icon != null ? icon.texture : null, null, true, true, Sprint4Art.Unlit);

        private static void Pieces(MatchDirector director, BoardView board, BoardCameraRig rig, GameHUD hud, List<CharacterDefinitionAsset> characters)
        {
            var piecesRoot = GameObject.Find("Pieces")?.transform;
            if (piecesRoot == null) piecesRoot = new GameObject("Pieces").transform;
            for (int k = piecesRoot.childCount - 1; k >= 0; k--) Object.DestroyImmediate(piecesRoot.GetChild(k).gameObject);

            var seats = new List<ParticipantConfig>
            {
                new ParticipantConfig("Jaguar", GamePalette.SolarYellow, false, characters.ElementAtOrDefault(0)),
                new ParticipantConfig("Capybara", GamePalette.ColonialBlue, true, characters.ElementAtOrDefault(1)),
                new ParticipantConfig("Monkey", GamePalette.LeafGreen, true, characters.ElementAtOrDefault(2)),
                new ParticipantConfig("Toucan", GamePalette.HibiscusPink, true, characters.ElementAtOrDefault(3)),
            };

            var prefab = AssetDatabase.LoadAssetAtPath<PlayerPieceView>(PiecePrefabPath);
            var pieces = new List<PlayerPieceView>();
            for (int k = 0; k < seats.Count; k++)
            {
                var piece = (PlayerPieceView)PrefabUtility.InstantiatePrefab(prefab, piecesRoot);
                piece.Configure(k + 1, seats[k].color);
                piece.SetCharacter(seats[k].character);
                piece.SetLogicalPosition(0);
                pieces.Add(piece);
            }
            board.PlacePieces(pieces);

            var config = AssetDatabase.LoadAssetAtPath<DontTrustYourLuck.Presentation.Config.MatchConfigAsset>(ConfigPath);
            director.Configure(config, board, rig, hud, prefab, piecesRoot, pieces);
            director.SetParticipants(seats);
            EditorUtility.SetDirty(director);
        }

        // ------------------------------------------------------------------ particles

        private static Material ParticleMat(string name, Texture2D tex)
        {
            var m = Sprint4Art.Mat(name, Color.white, tex, null, false, false, Sprint4Art.ParticlesUnlit);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static ParticleSystem Particles(string name, Transform parent, Material mat, System.Action<ParticleSystem> setup)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true; // "playing" with zero emission so Emit() bursts simulate immediately
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            setup(ps);
            return ps;
        }

        private static void Confetti(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.9f;
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(Sprint4Art.Gold, 0f), new GradientColorKey(Sprint4Art.Red, 0.25f), new GradientColorKey(Sprint4Art.Green, 0.5f),
                new GradientColorKey(Sprint4Art.Blue, 0.75f), new GradientColorKey(Sprint4Art.Pink, 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            FadeOut(ps);
        }

        private static void Puff(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient((Color)Sprint4Art.PaperLight, (Color)PixelCanvas.Hex(0x9C8F80));
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
            FadeOut(ps);
        }

        private static void Sparkle(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient((Color)Sprint4Art.Gold, (Color)PixelCanvas.Hex(0xFFF1C6));
            main.gravityModifier = -0.1f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        }

        private static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // ------------------------------------------------------------------ primitive helpers

        private static Transform Group(Env e, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(e.Root, false);
            t.localPosition = pos;
            return t;
        }

        private static Transform Prim(Env e, PrimitiveType type, string name, Material mat, Vector3 pos, Vector3 scale, bool shadows, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent != null ? parent : e.Root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        private static Transform Box(Env e, string name, Material mat, Vector3 pos, Vector3 scale, bool shadows = true, Transform parent = null) =>
            Prim(e, PrimitiveType.Cube, name, mat, pos, scale, shadows, parent);

        private static Transform Cyl(Env e, string name, Material mat, Vector3 pos, Vector3 scale, Transform parent = null) =>
            Prim(e, PrimitiveType.Cylinder, name, mat, pos, scale, true, parent);

        private static Transform Sphere(Env e, string name, Material mat, Vector3 pos, Vector3 scale, Transform parent = null) =>
            Prim(e, PrimitiveType.Sphere, name, mat, pos, scale, true, parent);

        private static Transform Quad(Env e, string name, Material mat, Vector3 pos, Vector3 scale, Quaternion rot, bool shadows, Transform parent = null)
        {
            var t = Prim(e, PrimitiveType.Quad, name, mat, pos, scale, shadows, parent);
            t.localRotation = rot;
            return t;
        }

        private static Transform Prism(Env e, string name, Material mat, Vector3 pos, Vector3 scale, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Sprint4Art.RoofMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go.transform;
        }
    }
}
