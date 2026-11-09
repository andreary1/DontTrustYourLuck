using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DontTrustYourLuck.EditorTools.Sprint4;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Graphics
{
    /// <summary>
    /// Graphics phase F1 (technical base), see docs/qa/GFX-F1-base-tecnica.md.
    /// Apply: Web URP asset (Mobile_RPAsset: render scale 1, MSAA 2x, HDR off, soft shadows Low, 35 m / 1 cascade /
    /// 1024, bias 0.5/0.4), matte Simple Lit materials (specular fix), the generator's import policy re-applied to the
    /// generated textures (board tiles without mipmaps), Web canvas 1280x720 (project Player Settings, the active
    /// build profile and every Web build profile). Values chosen by the F1 pipeline captures; locked by
    /// GraphicsBaseTests.
    /// Pipeline: captures before, Apply, captures after + single-factor A/B variants (each variant is restored).
    /// Menu: Tools > Don't Trust Your Luck > Graphics.
    /// </summary>
    public static class GfxF1
    {
        private const string Menu = "Tools/Don't Trust Your Luck/Graphics/";
        public const string WebRpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        public const string BuildProfilesDir = "Assets/Settings/Build Profiles";
        public const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        public const int WebCanvasWidth = 1280;
        public const int WebCanvasHeight = 720;
        private const string PendingRestorePath = GfxCaptureRunner.CaptureRoot + "/pending_restore.json";

        /// <summary>Web URP asset (pipeline of the Mobile quality level, the WebGL default).</summary>
        private static readonly (string prop, float value)[] WebRpSettings =
        {
            ("m_RenderScale", 1f),
            ("m_MSAA", 2f), // A/B: 2x removes the stair-steps of the tile frames, 4x adds little; prompt §6 asks 2x
            ("m_SupportsHDR", 0f), // no post-processing: HDR only added an intermediate target
            ("m_SoftShadowsSupported", 1f),
            ("m_SoftShadowQuality", 1f), // Low
            ("m_ShadowDistance", 35f),
            ("m_ShadowCascadeCount", 1f),
            ("m_MainLightShadowmapResolution", 1024f),
            ("m_ShadowDepthBias", 0.5f),
            ("m_ShadowNormalBias", 0.4f),
        };

        // ------------------------------------------------------------------ menus

        [MenuItem(Menu + "F1 - Run Pipeline (before, apply, after + A-B)")]
        public static void RunPipelineMenu()
        {
            if (!EditorUtility.DisplayDialog("Graphics F1 pipeline",
                    "1. Captures the current look (Web quality at 960x600 and 1280x720).\n" +
                    "2. Applies the F1 technical base (Web URP asset, matte materials, texture import, Web canvas 1280x720).\n" +
                    "3. Captures the result and single-factor A/B variants (MSAA, shadows, character and tile filtering); " +
                    "every variant is restored.\n\nTakes a few minutes and enters Play mode many times. Keep the Game view " +
                    "visible and Unity in the foreground. Output: Logs/GfxCaptures/.", "Run", "Cancel"))
                return;
            RunPipeline();
        }

        [MenuItem(Menu + "F1 - Apply Technical Base")]
        public static void ApplyMenu()
        {
            if (!EditorUtility.DisplayDialog("Graphics F1 - apply",
                    "Applies the F1 technical base (Web URP asset, matte Simple Lit materials, generated texture import " +
                    "policy, Web canvas 1280x720). Idempotent.", "Apply", "Cancel"))
                return;
            Debug.Log("[GfxF1] Technical base applied:\n" + Apply());
        }

        [MenuItem(Menu + "F1 - Verify (apply, final capture, tests, Web build)")]
        public static void VerifyMenu()
        {
            if (!EditorUtility.DisplayDialog("Graphics F1 - verify",
                    "Re-applies the F1 technical base (idempotent), captures the final look at 1280x720, runs the EditMode " +
                    "and PlayMode tests, then builds the Web player with the 'Web - Desktop - Development' profile to " +
                    "Builds/Web. Takes several minutes; keep the Game view visible.", "Run", "Cancel"))
                return;
            Verify();
        }

        [MenuItem(Menu + "Capture Showcase (Web quality, 1280x720)")]
        public static void CaptureShowcase() =>
            GfxCaptureRunner.Run("showcase", new[] { new GfxCaptureJob { Name = "web1280x720", Shots = GfxShots.Showcase } });

        [MenuItem(Menu + "Restore Pending A-B Changes")]
        public static void RestorePendingMenu() => Debug.Log("[GfxF1] " + RestorePending());

        // ------------------------------------------------------------------ pipeline

        public static void RunPipeline()
        {
            GfxCaptureRunner.Run("F1_pipeline", PipelineJobs(), (dir, ok) =>
            {
                // Last: reimporting the build profiles must not interrupt the capture session.
                string canvas = ApplyWebCanvas(Path.Combine(dir, "canvas.txt"));
                Debug.Log($"[GfxF1] Pipeline {(ok ? "done" : "FAILED")}: {Path.GetFullPath(dir)}\n{canvas}");
            });
        }

        public static IEnumerable<GfxCaptureJob> PipelineJobs()
        {
            yield return new GfxCaptureJob
            {
                Name = "A0_before_web960x600", Width = 960, Height = 600, Shots = GfxShots.Showcase,
                Note = "current Web canvas and Web URP asset (render scale 0.8)"
            };
            yield return new GfxCaptureJob { Name = "A1_before_web1280x720", Shots = GfxShots.Showcase, Note = "current Web URP asset at the target size" };
            // No "Editor at PC quality" job: in the F1 run, Play mode switched to Mobile (the WebGL default level) even
            // with PC selected before entering it, so Play mode in the Editor already shows the Web quality level.
            yield return new GfxCaptureJob { Name = "B_apply", EditStep = () => GfxCaptureRunner.Note(ApplyAssets()), Note = "F1 technical base (assets)" };
            yield return new GfxCaptureJob { Name = "C0_after_web1280x720", Shots = GfxShots.Showcase, Note = "F1 defaults" };
            yield return RpVariant("C1_ab_msaa4x", "m_MSAA", 4f);
            yield return RpVariant("C2_ab_msaa_off", "m_MSAA", 1f);
            yield return RpVariant("C3_ab_shadowmap2048", "m_MainLightShadowmapResolution", 2048f);
            yield return RpVariant("C4_ab_softshadow_medium", "m_SoftShadowQuality", 2f);
            yield return ImportVariant("C5_ab_chars_bilinear_mips", "characters: Bilinear + mipmaps (alpha coverage 0.5)", CharacterCutouts, i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.mipmapEnabled = true;
                i.mipMapsPreserveCoverage = true;
                i.alphaTestReferenceValue = 0.5f;
            });
            yield return ImportVariant("C6_ab_chars_point_mips", "characters: Point + mipmaps (alpha coverage 0.5)", CharacterCutouts, i =>
            {
                i.mipmapEnabled = true;
                i.mipMapsPreserveCoverage = true;
                i.alphaTestReferenceValue = 0.5f;
            });
            yield return ImportVariant("C7_ab_tiles_point_mips", "tiles: Point + mipmaps", TileTextures, i => i.mipmapEnabled = true);
            yield return ImportVariant("C8_ab_tiles_bilinear_aniso4", "tiles: Bilinear + mipmaps + aniso 4", TileTextures, i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.mipmapEnabled = true;
                i.anisoLevel = 4;
            });
            yield return new GfxCaptureJob { Name = "C9_after_web1920x1080", Width = 1920, Height = 1080, Note = "F1 defaults, large window" };
        }

        public static void Verify()
        {
            string applied = Apply();
            var jobs = new[]
            {
                new GfxCaptureJob { Name = "D_apply", EditStep = () => GfxCaptureRunner.Note(applied), Note = "F1 technical base (idempotent)" },
                new GfxCaptureJob { Name = "D0_final_web1280x720", Shots = GfxShots.Showcase, Note = "final F1 state" },
            };
            GfxCaptureRunner.Run("F1_verify", jobs, (dir, ok) =>
            {
                if (!ok) Debug.LogError("[GfxF1] Final capture failed; running tests and build anyway.");
                GfxTestChain.Run(Path.Combine(dir, "tests.txt"), testsOk => GfxWebBuild.Build(dir));
            });
        }

        // ------------------------------------------------------------------ apply

        /// <summary>Applies everything (assets and Web canvas). Returns a log of what changed.</summary>
        public static string Apply()
        {
            string assets = ApplyAssets();
            return assets + ApplyWebCanvas(null);
        }

        /// <summary>Web URP asset, matte Simple Lit materials and generated texture import policy.</summary>
        public static string ApplyAssets()
        {
            var log = new StringBuilder();
            ApplyWebRenderPipeline(log);
            ApplyMatteMaterials(log);
            ApplyTextureImportPolicy(log);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static void ApplyWebRenderPipeline(StringBuilder log)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(WebRpAssetPath);
            if (asset == null)
            {
                log.AppendLine("ERROR missing " + WebRpAssetPath);
                return;
            }
            var so = new SerializedObject(asset);
            foreach (var (prop, value) in WebRpSettings)
            {
                var p = so.FindProperty(prop);
                if (p == null)
                {
                    log.AppendLine($"WARNING {asset.name}.{prop} not found");
                    continue;
                }
                string before = Describe(p);
                SetValue(p, value);
                string after = Describe(p);
                if (before != after) log.AppendLine($"{asset.name}.{prop}: {before} -> {after}");
            }
            if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(asset);
        }

        private static void ApplyMatteMaterials(StringBuilder log)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader == null || m.shader.name != Sprint4Art.SimpleLit) continue;
                bool specular = m.IsKeywordEnabled("_SPECULAR_COLOR") ||
                                (m.HasProperty("_SpecularHighlights") && m.GetFloat("_SpecularHighlights") < 0.5f);
                if (!specular) continue;
                Sprint4Art.DisableSpecular(m);
                EditorUtility.SetDirty(m);
                count++;
            }
            log.AppendLine($"Simple Lit materials made matte (specular off): {count}");
        }

        private static void ApplyTextureImportPolicy(StringBuilder log)
        {
            foreach (string path in CharacterCutouts())
            {
                string before = ReadMeta(path);
                Sprint4Art.ImportCharacterCutout(path);
                if (ReadMeta(path) != before) log.AppendLine("reimported " + path + " " + ImportSummary(path));
            }
            foreach (string path in EnvironmentTextures())
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                string before = ReadMeta(path);
                Sprint4Art.ImportEnvironmentTexture(path, w, h, importer.wrapMode == TextureWrapMode.Repeat);
                if (ReadMeta(path) != before) log.AppendLine("reimported " + path + " " + ImportSummary(path));
            }
        }

        /// <summary>
        /// Web canvas 1280x720 in Player Settings and in every Web build profile (each profile carries its own Player
        /// Settings copy). Optional log file is written before the profiles are reimported.
        /// </summary>
        public static string ApplyWebCanvas(string logFile)
        {
            var log = new StringBuilder();
            int w0 = PlayerSettings.defaultWebScreenWidth, h0 = PlayerSettings.defaultWebScreenHeight;
            PlayerSettings.defaultWebScreenWidth = WebCanvasWidth;
            PlayerSettings.defaultWebScreenHeight = WebCanvasHeight;
            log.AppendLine($"PlayerSettings API web canvas: {w0}x{h0} -> {PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight}");
            var active = BuildProfile.GetActiveBuildProfile();
            log.AppendLine("active build profile: " + (active != null ? AssetDatabase.GetAssetPath(active) : "none (platform settings)"));
            ApplyProjectPlayerSettingsCanvas(log);
            var changed = new List<string>();
            foreach (string file in Directory.GetFiles(BuildProfilesDir, "Web*.asset"))
            {
                string path = file.Replace('\\', '/');
                string text = File.ReadAllText(path);
                string next = Regex.Replace(text, @"(\|   defaultScreenWidthWeb: )\d+", "${1}" + WebCanvasWidth);
                next = Regex.Replace(next, @"(\|   defaultScreenHeightWeb: )\d+", "${1}" + WebCanvasHeight);
                if (next == text) continue;
                File.WriteAllText(path, next);
                changed.Add(path);
                log.AppendLine($"{path}: web canvas -> {WebCanvasWidth}x{WebCanvasHeight}");
            }
            if (changed.Count == 0) log.AppendLine("Web build profiles already at the target canvas");
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(logFile)) File.WriteAllText(logFile, log.ToString());
            foreach (string path in changed) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            string after = $"PlayerSettings after profile reimport: {PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight}";
            log.AppendLine(after);
            if (!string.IsNullOrEmpty(logFile)) File.AppendAllText(logFile, after + Environment.NewLine);
            return log.ToString();
        }

        /// <summary>
        /// While a build profile overrides Player Settings, the PlayerSettings API writes to that profile (the F1
        /// pipeline left ProjectSettings.asset at 960x600). The project-wide object is set through its serialized
        /// properties, so a build without a custom profile also gets the 1280x720 canvas.
        /// </summary>
        private static void ApplyProjectPlayerSettingsCanvas(StringBuilder log)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath).OfType<PlayerSettings>().FirstOrDefault();
            var so = settings != null ? new SerializedObject(settings) : null;
            var width = so?.FindProperty("defaultScreenWidthWeb");
            var height = so?.FindProperty("defaultScreenHeightWeb");
            if (width == null || height == null)
            {
                log.AppendLine($"WARNING web canvas not found in the project Player Settings ({ProjectSettingsPath})");
                return;
            }
            string before = $"{width.intValue}x{height.intValue}";
            width.intValue = WebCanvasWidth;
            height.intValue = WebCanvasHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"{ProjectSettingsPath} web canvas: {before} -> {width.intValue}x{height.intValue}");
        }

        // ------------------------------------------------------------------ A/B variants (always restored)

        private static GfxCaptureJob RpVariant(string name, string prop, float value) => new GfxCaptureJob
        {
            Name = name,
            Note = $"{prop} = {value.ToString(CultureInfo.InvariantCulture)}",
            Setup = () =>
            {
                var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(WebRpAssetPath));
                var p = so.FindProperty(prop);
                var pending = new PendingRestore();
                pending.rp.Add(new RpEntry { prop = prop, value = GetValue(p) });
                SavePending(pending);
                SetValue(p, value);
                so.ApplyModifiedPropertiesWithoutUndo();
            },
            Teardown = () => GfxCaptureRunner.Note(RestorePending())
        };

        private static GfxCaptureJob ImportVariant(string name, string note, Func<IEnumerable<string>> paths, Action<TextureImporter> change) =>
            new GfxCaptureJob
            {
                Name = name,
                Note = note,
                Setup = () =>
                {
                    var list = paths().ToList();
                    var pending = new PendingRestore();
                    foreach (string path in list) pending.metas.Add(new MetaEntry { path = path, text = ReadMeta(path) });
                    SavePending(pending);
                    foreach (string path in list)
                    {
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        change(importer);
                        importer.SaveAndReimport();
                    }
                },
                Teardown = () => GfxCaptureRunner.Note(RestorePending())
            };

        [Serializable]
        private sealed class PendingRestore
        {
            public List<MetaEntry> metas = new List<MetaEntry>();
            public List<RpEntry> rp = new List<RpEntry>();
        }

        [Serializable]
        private sealed class MetaEntry
        {
            public string path;
            public string text;
        }

        [Serializable]
        private sealed class RpEntry
        {
            public string prop;
            public float value;
        }

        private static void SavePending(PendingRestore pending)
        {
            Directory.CreateDirectory(GfxCaptureRunner.CaptureRoot);
            File.WriteAllText(PendingRestorePath, JsonUtility.ToJson(pending, true));
        }

        /// <summary>Restores what an A/B variant changed (texture .meta files, Web URP asset values).</summary>
        public static string RestorePending()
        {
            if (!File.Exists(PendingRestorePath)) return "nothing to restore";
            var pending = JsonUtility.FromJson<PendingRestore>(File.ReadAllText(PendingRestorePath));
            var log = new StringBuilder();
            foreach (var entry in pending.metas)
            {
                if (ReadMeta(entry.path) == entry.text) continue;
                File.WriteAllText(entry.path + ".meta", entry.text);
                AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
                log.AppendLine((ReadMeta(entry.path) == entry.text ? "restored " : "WARNING restored but differs: ") + entry.path);
            }
            if (pending.rp.Count > 0)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(WebRpAssetPath);
                var so = new SerializedObject(asset);
                foreach (var entry in pending.rp)
                {
                    SetValue(so.FindProperty(entry.prop), entry.value);
                    log.AppendLine($"restored {asset.name}.{entry.prop} = {entry.value.ToString(CultureInfo.InvariantCulture)}");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            File.Delete(PendingRestorePath);
            return log.Length > 0 ? log.ToString() : "restore: nothing changed";
        }

        // ------------------------------------------------------------------ helpers

        public static IEnumerable<string> CharacterCutouts() => AssetFiles(Sprint4Art.CharactersDir, "SPR_Char_*.png");

        public static IEnumerable<string> TileTextures() => AssetFiles(Sprint4Art.EnvDir, Sprint4Art.BoardTilePrefix + "*.png");

        public static IEnumerable<string> EnvironmentTextures() => AssetFiles(Sprint4Art.EnvDir, "*.png");

        private static IEnumerable<string> AssetFiles(string dir, string pattern) =>
            Directory.Exists(dir)
                ? Directory.GetFiles(dir, pattern).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        private static string ReadMeta(string assetPath)
        {
            string meta = assetPath + ".meta";
            return File.Exists(meta) ? File.ReadAllText(meta) : null;
        }

        private static string ImportSummary(string path)
        {
            var i = (TextureImporter)AssetImporter.GetAtPath(path);
            var web = i.GetPlatformTextureSettings("WebGL");
            return $"(filter {i.filterMode}, mips {i.mipmapEnabled}, compression {i.textureCompression}, max {i.maxTextureSize}" +
                   $"{(web.overridden ? $", WebGL override {web.format}" : "")})";
        }

        private static float GetValue(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return p.floatValue;
                case SerializedPropertyType.Boolean: return p.boolValue ? 1f : 0f;
                default: return p.intValue;
            }
        }

        private static void SetValue(SerializedProperty p, float value)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    p.floatValue = value;
                    break;
                case SerializedPropertyType.Boolean:
                    p.boolValue = value != 0f;
                    break;
                default:
                    p.intValue = Mathf.RoundToInt(value);
                    break;
            }
        }

        private static string Describe(SerializedProperty p) => GetValue(p).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
