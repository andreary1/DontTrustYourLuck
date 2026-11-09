using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.Tests.Presentation
{
    /// <summary>
    /// Asset contract of the graphics phase F1 (docs/qa/GFX-F1-base-tecnica.md), decided by captures: Web URP asset,
    /// matte Simple Lit materials, import of the generated textures and the Web canvas. Reads project files only, so
    /// a regenerated asset or a build that rewrites a profile cannot silently undo the base.
    /// </summary>
    public sealed class GraphicsBaseTests
    {
        private const string WebPipelineAsset = "Assets/Settings/Mobile_RPAsset.asset";
        private const string QualitySettingsFile = "ProjectSettings/QualitySettings.asset";
        private const string PlayerSettingsFile = "ProjectSettings/ProjectSettings.asset";
        private const string BuildProfilesDir = "Assets/Settings/Build Profiles";
        private const string CharactersDir = "Assets/_Game/Art/Characters";
        private const string EnvironmentDir = "Assets/_Game/Art/Environment";
        private const string SimpleLit = "Universal Render Pipeline/Simple Lit";

        [Test]
        public void WebRenderPipeline_UsesTheF1Base()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(WebPipelineAsset);
            Assert.IsNotNull(asset, WebPipelineAsset);
            var so = new SerializedObject(asset);
            Assert.AreEqual(1f, Prop(so, "m_RenderScale").floatValue, 1e-4f, "render scale");
            Assert.AreEqual(2, Prop(so, "m_MSAA").intValue, "MSAA (A/B: 2x removes the stair-steps of the tile frames; 4x adds little)");
            Assert.IsFalse(Prop(so, "m_SupportsHDR").boolValue, "HDR (no post-processing)");
            Assert.IsTrue(Prop(so, "m_SoftShadowsSupported").boolValue, "soft shadows");
            Assert.AreEqual(1, Prop(so, "m_SoftShadowQuality").intValue, "soft shadow quality (1 = Low)");
            Assert.AreEqual(35f, Prop(so, "m_ShadowDistance").floatValue, 1e-3f, "shadow distance");
            Assert.AreEqual(1, Prop(so, "m_ShadowCascadeCount").intValue, "shadow cascades");
            Assert.AreEqual(1024, Prop(so, "m_MainLightShadowmapResolution").intValue, "main light shadow map");
            Assert.AreEqual(0.5f, Prop(so, "m_ShadowDepthBias").floatValue, 1e-4f, "shadow depth bias");
            Assert.AreEqual(0.4f, Prop(so, "m_ShadowNormalBias").floatValue, 1e-4f, "shadow normal bias");
        }

        [Test]
        public void WebDefaultQualityLevel_UsesTheWebPipelineAsset()
        {
            string text = File.ReadAllText(QualitySettingsFile);
            int block = text.IndexOf("m_PerPlatformDefaultQuality:", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(block, 0, "m_PerPlatformDefaultQuality not found");
            var match = new Regex(@"\n\s+WebGL: (\d+)").Match(text, block);
            Assert.IsTrue(match.Success, "no WebGL default quality level");
            int level = int.Parse(match.Groups[1].Value);
            var pipeline = QualitySettings.GetRenderPipelineAssetAt(level);
            Assert.IsNotNull(pipeline, $"quality level {level} has no render pipeline asset");
            Assert.AreEqual(WebPipelineAsset, AssetDatabase.GetAssetPath(pipeline), $"pipeline of the WebGL default level {level}");
        }

        [Test]
        public void SimpleLitMaterials_HaveNoSpecular()
        {
            var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(m => m != null && m.shader != null && m.shader.name == SimpleLit)
                .ToList();
            Assert.IsNotEmpty(materials, "no Simple Lit material under Assets/_Game");
            // Simple Lit keeps SpecularSource in _SpecularHighlights: 0 = specular texture and colour, 1 = none.
            var specular = materials
                .Where(m => m.GetFloat("_SpecularHighlights") < 0.5f || m.IsKeywordEnabled("_SPECULAR_COLOR") ||
                            m.IsKeywordEnabled("_SPECGLOSSMAP"))
                .Select(m => m.name)
                .ToList();
            Assert.IsEmpty(specular, "Simple Lit materials with specular: " + string.Join(", ", specular));
        }

        [Test]
        public void CharacterCutouts_ArePointUncompressedWithoutMipmaps()
        {
            var cutouts = Importers(CharactersDir, "SPR_Char_*.png");
            Assert.IsNotEmpty(cutouts, "no SPR_Char_*.png");
            foreach (var pair in cutouts)
            {
                Assert.AreEqual(FilterMode.Point, pair.Value.filterMode, pair.Key);
                Assert.IsFalse(pair.Value.mipmapEnabled, pair.Key + ": mipmaps (prompt §5.2: none on sprites)");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, pair.Value.textureCompression, pair.Key);
            }
        }

        [Test]
        public void BoardTileTextures_ArePointWithoutMipmaps()
        {
            var tiles = Importers(EnvironmentDir, "T_Tile_*.png");
            Assert.IsNotEmpty(tiles, "no T_Tile_*.png");
            foreach (var pair in tiles)
            {
                Assert.AreEqual(FilterMode.Point, pair.Value.filterMode, pair.Key);
                Assert.IsFalse(pair.Value.mipmapEnabled, pair.Key + ": mipmaps (A/B: the back rows lose their pattern)");
            }
        }

        [Test]
        public void GeneratedTexturesUpTo256Px_AreUncompressed()
        {
            var textures = Importers(CharactersDir, "*.png").Concat(Importers(EnvironmentDir, "*.png")).ToList();
            Assert.IsNotEmpty(textures);
            foreach (var pair in textures)
            {
                pair.Value.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (Mathf.Max(width, height) > 256) continue;
                Assert.AreEqual(TextureImporterCompression.Uncompressed, pair.Value.textureCompression, $"{pair.Key} ({width}x{height})");
            }
        }

        [Test]
        public void WebCanvas_Is1280x720InPlayerSettingsAndEveryWebProfile()
        {
            var files = new List<string> { PlayerSettingsFile };
            files.AddRange(Directory.GetFiles(BuildProfilesDir, "Web*.asset"));
            Assert.Greater(files.Count, 1, "no Web build profile");
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                Assert.AreEqual("1280", Value(text, "defaultScreenWidthWeb"), file + ": width");
                Assert.AreEqual("720", Value(text, "defaultScreenHeightWeb"), file + ": height");
            }
        }

        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            Assert.IsNotNull(p, name + " not found");
            return p;
        }

        private static Dictionary<string, TextureImporter> Importers(string dir, string pattern) =>
            Directory.GetFiles(dir, pattern)
                .Select(p => p.Replace('\\', '/'))
                .ToDictionary(p => p, p => (TextureImporter)AssetImporter.GetAtPath(p));

        /// <summary>Value of a Player Settings key, also inside the "| key: value" lines of a build profile.</summary>
        private static string Value(string text, string key)
        {
            var m = Regex.Match(text, @"\b" + key + @": (\d+)");
            return m.Success ? m.Groups[1].Value : "missing";
        }
    }
}
