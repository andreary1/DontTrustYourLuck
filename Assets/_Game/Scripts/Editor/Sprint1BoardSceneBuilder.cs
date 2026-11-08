using System.IO;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Config;
using DontTrustYourLuck.Presentation.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontTrustYourLuck.EditorTools
{
    /// <summary>
    /// Generates the Sprint 1 placeholder assets (materials, prefabs, config) and the GameBoard scene through
    /// Unity APIs, so GUIDs and serialized YAML are produced by the Editor instead of by hand.
    /// Re-running updates prefabs/materials in place (GUIDs preserved) and regenerates the scene.
    ///
    /// Menu: Tools > Don't Trust Your Luck > Build Sprint 1 Board Scene
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod DontTrustYourLuck.EditorTools.Sprint1BoardSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class Sprint1BoardSceneBuilder
    {
        private const string Root = "Assets/_Game";
        private const string MaterialsDir = Root + "/Art/Materials";
        private const string BoardPrefabsDir = Root + "/Prefabs/Board";
        private const string CharacterPrefabsDir = Root + "/Prefabs/Characters";
        private const string DataDir = Root + "/Data";
        private const string ScenesDir = Root + "/Scenes";
        private const string ScenePath = ScenesDir + "/GameBoard.unity";

        private static readonly Color[] PieceColors =
        {
            new Color(0.20f, 0.45f, 0.95f), // P1 blue
            new Color(1.00f, 0.55f, 0.10f), // P2 orange
            new Color(0.60f, 0.30f, 0.85f), // P3 purple
            new Color(0.10f, 0.80f, 0.70f), // P4 teal
        };

        [MenuItem("Tools/Don't Trust Your Luck/Build Sprint 1 Board Scene")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild GameBoard scene?",
                    $"{ScenePath} already exists and will be regenerated. Prefabs and materials are updated in place.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(BoardPrefabsDir);
            EnsureFolder(CharacterPrefabsDir);
            EnsureFolder(DataDir);
            EnsureFolder(ScenesDir);

            var materials = new BoardTileMaterials
            {
                even = GetOrCreateMaterial("M_Tile_Even", new Color(0.93f, 0.88f, 0.76f)),
                odd = GetOrCreateMaterial("M_Tile_Odd", new Color(0.84f, 0.74f, 0.58f)),
                start = GetOrCreateMaterial("M_Tile_Start", new Color(0.62f, 0.72f, 0.80f)),
                lucky = GetOrCreateMaterial("M_Tile_Lucky", new Color(0.957f, 0.725f, 0.259f)),   // #F4B942
                unlucky = GetOrCreateMaterial("M_Tile_Unlucky", new Color(0.847f, 0.227f, 0.208f)), // #D83A35
                connector = GetOrCreateMaterial("M_Path_Connector", new Color(0.45f, 0.36f, 0.28f)),
            };
            var pieceMaterial = GetOrCreateMaterial("M_PlayerPiece", Color.white);

            // BoardTile prefab
            var tileTemp = BoardTileView.CreateDefault();
            tileTemp.SetMaterial(materials.even, TileHighlight.None);
            var tilePrefab = SavePrefab(tileTemp.gameObject, BoardPrefabsDir + "/BoardTile.prefab").GetComponent<BoardTileView>();

            // PlayerPiece prefab
            var pieceTemp = PlayerPieceView.CreateDefault(pieceMaterial);
            var piecePrefab = SavePrefab(pieceTemp.gameObject, CharacterPrefabsDir + "/PlayerPiece.prefab").GetComponent<PlayerPieceView>();

            // Config asset
            var configPath = DataDir + "/DefaultMatchConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<MatchConfigAsset>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MatchConfigAsset>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            var rules = config.ToRulesConfig();

            // BoardView prefab (tiles are generated at Awake/Build, not stored in the prefab)
            var boardTemp = new GameObject("BoardView");
            boardTemp.AddComponent<BoardView>().Configure(tilePrefab, materials, rules.TileCount);
            var boardPrefab = SavePrefab(boardTemp, BoardPrefabsDir + "/BoardView.prefab").GetComponent<BoardView>();

            // Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            // NewScene(Single) unloads unused assets, so reload references by path before using them.
            boardPrefab = AssetDatabase.LoadAssetAtPath<BoardView>(BoardPrefabsDir + "/BoardView.prefab");
            piecePrefab = AssetDatabase.LoadAssetAtPath<PlayerPieceView>(CharacterPrefabsDir + "/PlayerPiece.prefab");
            config = AssetDatabase.LoadAssetAtPath<MatchConfigAsset>(configPath);

            var board = (BoardView)PrefabUtility.InstantiatePrefab(boardPrefab);
            board.Build(rules.TileCount); // pre-built in the scene so it is visible in Edit Mode

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.24f, 0.22f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<BoardCameraRig>();
            rig.Configure(board, 55f);
            rig.Frame();

            var piecesRoot = new GameObject("Pieces");
            var pieces = new PlayerPieceView[PieceColors.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = (PlayerPieceView)PrefabUtility.InstantiatePrefab(piecePrefab, piecesRoot.transform);
                piece.Configure(i + 1, PieceColors[i]);
                piece.SetLogicalPosition(0);
                pieces[i] = piece;
            }
            board.PlacePieces(pieces);

            var demoGo = new GameObject("BoardMovementDemo");
            demoGo.AddComponent<BoardMovementDemo>().Configure(config, board, rig, pieces);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Sprint1BoardSceneBuilder] Generated {ScenePath} with {board.TileCount} tiles and {pieces.Length} pieces.");
        }

        private static GameObject SavePrefab(GameObject temp, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
