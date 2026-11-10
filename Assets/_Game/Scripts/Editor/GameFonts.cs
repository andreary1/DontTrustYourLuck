using DontTrustYourLuck.EditorTools.Sprint4;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.EditorTools
{
    /// <summary>
    /// The game's UI font (Jersey 25, SIL OFL 1.1 - license next to the file). Builders call
    /// <see cref="Ui"/> instead of the built-in LegacyRuntime font; <see cref="ApplyToProject"/> swaps the font on
    /// every uGUI Text already saved in the HUD prefab and the GameBoard scene.
    /// </summary>
    public static class GameFonts
    {
        public const string Dir = "Assets/_Game/Art/Fonts/Jersey25";
        public const string RegularPath = Dir + "/Jersey25-Regular.ttf";

        /// <summary>Jersey 25 Regular, or the built-in font if missing. Bold text uses <see cref="UiBold"/>.</summary>
        public static Font Ui()
        {
            EnsureImported();
            var font = AssetDatabase.LoadAssetAtPath<Font>(RegularPath);
            if (font != null) return font;
            Debug.LogWarning($"[GameFonts] {RegularPath} not found; using the built-in font.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void EnsureImported()
        {
            var importer = AssetImporter.GetAtPath(RegularPath) as TrueTypeFontImporter;
            if (importer == null) return;
            bool dirty = false;
            if (importer.fontTextureCase != FontTextureCase.Dynamic) { importer.fontTextureCase = FontTextureCase.Dynamic; dirty = true; }
            if (!importer.includeFontData) { importer.includeFontData = true; dirty = true; } // WebGL has no OS fonts
            // Pixel font: hard-edged glyphs stay legible at small sizes; smoothed ones blur "2" into "8".
            if (importer.fontRenderingMode != FontRenderingMode.HintedRaster) { importer.fontRenderingMode = FontRenderingMode.HintedRaster; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        [MenuItem("Tools/Don't Trust Your Luck/Apply UI Font (Jersey 25)")]
        public static void ApplyToProject()
        {
            var font = Ui();
            int prefabCount = ApplyToPrefab(Sprint4Hud.HudPrefabPath, font);
            var scene = EditorSceneManager.OpenScene(Sprint4Scene.ScenePath, OpenSceneMode.Single);
            int sceneCount = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Text>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsAddedComponentOverride(t)) continue; // comes from the prefab
                    if (!Assign(t, font, UiBold())) continue;
                    EditorUtility.SetDirty(t);
                    sceneCount++;
                }
            if (sceneCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[GameFonts] Jersey 25 applied: {prefabCount} texts in the HUD prefab, {sceneCount} in the scene.");
        }

        /// <summary>
        /// Jersey 25 ships a single weight, so bold text uses the Regular face (no synthesized bold).
        /// </summary>
        public static Font UiBold() => Ui();

        /// <summary>
        /// Sets the UI face on a Text: bold styles use <see cref="UiBold"/> with a Normal style (a synthesized bold
        /// smears pixel glyphs, e.g. "5" reads as "S"). Returns true when something changed.
        /// </summary>
        public static bool Assign(Text t, Font regular, Font bold)
        {
            bool wantsBold = t.fontStyle == FontStyle.Bold || t.fontStyle == FontStyle.BoldAndItalic || t.font == bold;
            var face = wantsBold && bold != null ? bold : regular;
            var style = t.fontStyle == FontStyle.BoldAndItalic || t.fontStyle == FontStyle.Italic ? FontStyle.Italic : FontStyle.Normal;
            if (!wantsBold) style = t.fontStyle;
            if (t.font == face && t.fontStyle == style) return false;
            t.font = face;
            t.fontStyle = style;
            return true;
        }

        private static int ApplyToPrefab(string path, Font font)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var bold = UiBold();
            int count = 0;
            try
            {
                foreach (var t in root.GetComponentsInChildren<Text>(true))
                    if (Assign(t, font, bold)) count++;
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return count;
        }
    }
}
