using System.Collections.Generic;
using System.Linq;
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
    /// Sprint 4 scene pass: Fortaleza praça diorama around the existing board (same 31 logical tiles), warm
    /// late-afternoon lighting, standee pieces with the four animals, lucky/unlucky markers, particles,
    /// audio and visual feedback objects. Runs after the Sprint 2/3 builders (they wire HUD + director).
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
                if (OwnedRoots.Contains(go.name)) Object.DestroyImmediate(go);
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
                connector = Load("M_Path_Connector"), beyondLucky = Load("M_Tile_Beyond"),
            };
            int tileCount = board.TileCount > 0 ? board.TileCount : 31;
            board.Configure(tilePrefab, materials, tileCount);
            board.Build(tileCount);

            Lighting(rig.Camera);
            Environment(env, board);
            var markers = Markers(ui, board);
            Pieces(director, board, rig, hud, characters);

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

        private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");

        private static void Lighting(Camera cam)
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
            cam.farClipPlane = 80f;
        }

        // ------------------------------------------------------------------ environment

        private sealed class Env
        {
            public Transform Root;
            public Material Cobble, Stone, Grass, Leaves, LeavesDark, Bark, Wood, Roof, Awning, Sand, Sky, Sail, Terracotta, Cream, Ink;
            public Material[] Facades, Flags, Fruit;
        }

        private static void Environment(Sprint4Art.EnvArt art, BoardView board)
        {
            var e = new Env
            {
                Root = new GameObject("Environment").transform,
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
                Sky = Sprint4Art.Mat("M_Env_Sky", Color.white, art.Sky, null, false, false, Sprint4Art.Unlit),
                Sail = Sprint4Art.Mat("M_Env_Sail", Color.white, art.Sail, null, true, true),
                Terracotta = Sprint4Art.Mat("M_Env_Terracotta", Sprint4Art.Terracotta),
                Cream = Sprint4Art.Mat("M_Env_Cream", Sprint4Art.PaperLight),
                Ink = Sprint4Art.Mat("M_Env_Ink", Sprint4Art.Ink),
                Facades = art.Facades.Select((t, i) => Sprint4Art.Mat($"M_Env_Facade_{i}", Color.white, t)).ToArray(),
                Flags = new[] { Sprint4Art.Gold, Sprint4Art.Red, Sprint4Art.Green, Sprint4Art.Blue, Sprint4Art.Pink, Sprint4Art.Purple }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Flag_{i}", c, art.Flag, null, true, true)).ToArray(),
                Fruit = new[] { Sprint4Art.Gold, Sprint4Art.Red, Sprint4Art.Green, PixelCanvas.Hex(0xF28C28) }
                    .Select((c, i) => Sprint4Art.Mat($"M_Env_Fruit_{i}", c)).ToArray(),
            };

            var b = board.GetBoardBounds(0f);
            float halfX = b.extents.x + 0.7f, halfZ = b.extents.z + 0.7f;
            var c0 = new Vector3(b.center.x, 0f, b.center.z);

            // Ground and raised stone stage (praça) ----------------------------------------
            Box(e, "Ground", e.Cobble, c0 + new Vector3(0f, -0.45f, 2f), new Vector3(44f, 0.3f, 30f), false);
            Box(e, "Stage_Border", e.Terracotta, c0 + new Vector3(0f, -0.2f, 0f), new Vector3(halfX * 2f + 0.5f, 0.3f, halfZ * 2f + 0.5f));
            Box(e, "Stage", e.Stone, c0 + new Vector3(0f, -0.08f, 0f), new Vector3(halfX * 2f, 0.16f, halfZ * 2f));
            // Steps on the near side.
            Box(e, "Step", e.Stone, c0 + new Vector3(0f, -0.25f, -halfZ - 0.45f), new Vector3(3.2f, 0.2f, 0.5f));

            // Back row: platibanda houses ----------------------------------------------------
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
            // Sky backdrop with the sea horizon and jangada sails (seen above the houses).
            Quad(e, "SkyBackdrop", e.Sky, c0 + new Vector3(0f, 7f, backZ + 9f), new Vector3(60f, 22.5f, 1f), Quaternion.Euler(0f, 0f, 0f), false);

            // Bandeirinhas (São João flags) over the back of the stage.
            float flagZ = c0.z + halfZ + 1.1f;
            Pole(e, new Vector3(c0.x - halfX - 0.3f, 0f, flagZ));
            Pole(e, new Vector3(c0.x + halfX + 0.3f, 0f, flagZ));
            Bunting(e, new Vector3(c0.x - halfX - 0.3f, 3.2f, flagZ), new Vector3(c0.x + halfX + 0.3f, 3.2f, flagZ), 0.8f);
            Bunting(e, new Vector3(c0.x - halfX - 0.3f, 3.0f, flagZ), new Vector3(c0.x - 1f, 4.4f, backZ - 1.2f), 0.4f);
            Bunting(e, new Vector3(c0.x + halfX + 0.3f, 3.0f, flagZ), new Vector3(c0.x + 1f, 4.4f, backZ - 1.2f), 0.4f);

            // Left: clock column (inspired by Praça do Ferreira's column clock, simplified, not a replica).
            ClockColumn(e, new Vector3(c0.x - halfX - 1.9f, 0f, c0.z + 1.2f));
            // Right: fruit stall (barraca) with striped awning.
            FruitStall(e, new Vector3(c0.x + halfX + 2f, 0f, c0.z + 1.0f));

            // Trees: cajueiro, coqueiros, carnaúba.
            Cajueiro(e, new Vector3(c0.x - halfX - 2.6f, 0f, c0.z + halfZ + 1.6f));
            Coqueiro(e, new Vector3(c0.x + halfX + 1.6f, 0f, c0.z + halfZ + 1.9f), 5.2f, 8f);
            Coqueiro(e, new Vector3(c0.x + halfX + 3.2f, 0f, c0.z + halfZ + 0.7f), 4.3f, -10f);
            Carnauba(e, new Vector3(c0.x - halfX - 3.6f, 0f, c0.z - 1.4f));
            Coqueiro(e, new Vector3(c0.x - halfX - 1.2f, 0f, c0.z - halfZ - 1.0f), 3.8f, 12f);

            // Front: planters with grass and benches flanking the steps.
            Planter(e, new Vector3(c0.x - 3.4f, 0f, c0.z - halfZ - 0.9f));
            Planter(e, new Vector3(c0.x + 3.4f, 0f, c0.z - halfZ - 0.9f));
            Bench(e, new Vector3(c0.x - 6.2f, 0f, c0.z - halfZ - 0.6f));
            Bench(e, new Vector3(c0.x + 6.2f, 0f, c0.z - halfZ - 0.6f));

            // Jangada on a sand pedestal (front right corner).
            Jangada(e, new Vector3(c0.x + halfX + 2.4f, 0f, c0.z - halfZ + 0.2f));

            foreach (var t in e.Root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        private static void Pole(Env e, Vector3 p)
        {
            Cyl(e, "Pole", e.Wood, p + new Vector3(0f, 1.7f, 0f), new Vector3(0.12f, 1.7f, 0.12f));
        }

        private static void Bunting(Env e, Vector3 a, Vector3 b, float sag)
        {
            var line = new GameObject("Bunting").transform;
            line.SetParent(e.Root, false);
            int n = Mathf.Max(4, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.55f));
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                var dir = (b - a).normalized;
                var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
                Quad(e, $"Flag_{k:00}", e.Flags[k % e.Flags.Length], p + Vector3.down * 0.18f, new Vector3(0.34f, 0.36f, 1f), rot, false, line);
            }
        }

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

        private static void FruitStall(Env e, Vector3 p)
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
                var s = Sphere(e, $"Fruit_{k}", e.Fruit[k % e.Fruit.Length], new Vector3(fx, 0.98f, fz), Vector3.one * (0.2f + (k % 3) * 0.03f), root);
                s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
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

        private static void Jangada(Env e, Vector3 p)
        {
            var root = Group(e, "Jangada", p);
            root.localRotation = Quaternion.Euler(0f, -30f, 0f);
            Box(e, "Sand", e.Sand, new Vector3(0f, 0.12f, 0f), new Vector3(2.4f, 0.24f, 1.4f), true, root);
            for (int k = 0; k < 4; k++)
                Cyl(e, $"Log_{k}", e.Wood, new Vector3(-0.3f + k * 0.2f, 0.34f, 0f), new Vector3(0.18f, 0.9f, 0.18f), root).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box(e, "Mast", e.Wood, new Vector3(0f, 1.3f, 0.1f), new Vector3(0.06f, 1.9f, 0.06f), true, root);
            Quad(e, "Sail", e.Sail, new Vector3(0.45f, 1.45f, 0.1f), new Vector3(1.0f, 1.6f, 1f), Quaternion.Euler(0f, 90f, 0f), true, root);
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
                new ParticipantConfig("Onça", HudPalette.Hex(0xF4B942), false, characters.ElementAtOrDefault(0)),
                new ParticipantConfig("Capivara", HudPalette.Hex(0x287FA3), true, characters.ElementAtOrDefault(1)),
                new ParticipantConfig("Macaco", HudPalette.Hex(0x438B52), true, characters.ElementAtOrDefault(2)),
                new ParticipantConfig("Tucano", HudPalette.Hex(0xED638B), true, characters.ElementAtOrDefault(3)),
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
