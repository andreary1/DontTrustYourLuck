using DontTrustYourLuck.EditorTools.Menu;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Sprint4
{
    /// <summary>
    /// Sprint 4 entry point. Order: placeholder art and audio, data assets, board/piece assets,
    /// Sprint 3 builder (cards + Sprint 2 HUD/scene wiring), Sprint 4 HUD restyle, Sprint 4 scene pass.
    /// Idempotent: re-running regenerates the same files in place (GUIDs preserved).
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod DontTrustYourLuck.EditorTools.Sprint4.Sprint4Builder.BuildFromCommandLine
    /// </summary>
    public static class Sprint4Builder
    {
        [MenuItem("Tools/Don't Trust Your Luck/Build Sprint 4 Art Direction (All)")]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Sprint 4 builder",
                    "Regenerates placeholder art/audio, the GameHUD prefab and the GameBoard scene (Sprint 1-3 builders run first). Continue?",
                    "Build", "Cancel"))
                return;
            BuildFromCommandLine();
        }

        public static void BuildFromCommandLine()
        {
            var characters = Sprint4Art.BuildCharacters();
            var ui = Sprint4Art.BuildUi();
            var env = Sprint4Art.BuildEnvironment();
            var library = Sprint4Audio.Build();
            var characterAssets = Sprint4Scene.BuildCharacterAssets(characters);
            Sprint4Scene.BuildBoardAndPieceAssets(env);
            AssetDatabase.SaveAssets();

            Sprint3CardsBuilder.BuildFromCommandLine(); // cards + Sprint 2 HUD prefab and scene wiring
            Sprint4Hud.Restyle(ui, characters);
            MainMenuBuilder.Build(ui); // title screen over the diorama (after the HUD restyle)
            Sprint4Scene.BuildScene(env, ui, characterAssets, library);
            MainMenuBuilder.ConfigureScene(); // match waits for the menu's Play
            AssetDatabase.SaveAssets();
            Debug.Log("[Sprint4Builder] Art, audio, HUD and GameBoard scene generated.");
        }
    }
}
