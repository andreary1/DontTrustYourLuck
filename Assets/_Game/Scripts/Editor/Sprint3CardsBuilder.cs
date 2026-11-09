using System.Collections.Generic;
using System.IO;
using DontTrustYourLuck.Domain.Cards;
using DontTrustYourLuck.Presentation.Art;
using DontTrustYourLuck.Presentation.Cards;
using DontTrustYourLuck.Presentation.Config;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools
{
    /// <summary>
    /// Sprint 3: generates the four placeholder card illustrations (64×64 pixel art, aged lottery-card frame,
    /// GDD §6.3 palette), creates/updates the <see cref="CardDefinitionAsset"/>s, assigns them to the default
    /// match config and rebuilds the HUD prefab + GameBoard scene (Sprint 2 builder, now with the hand panel).
    /// Idempotent; art is procedural and original, meant to be replaced.
    ///
    /// Menu: Tools > Don't Trust Your Luck > Build Sprint 3 Cards (Art + Assets + HUD)
    /// Batch: -executeMethod DontTrustYourLuck.EditorTools.Sprint3CardsBuilder.BuildFromCommandLine
    /// </summary>
    public static class Sprint3CardsBuilder
    {
        private const string ArtDir = "Assets/_Game/Art/Cards";
        private const string DataDir = "Assets/_Game/Data/Cards";
        private const string ConfigPath = "Assets/_Game/Data/DefaultMatchConfig.asset";
        private const int Size = 64;

        private static readonly Color32 Paper = GamePalette.AgedCream;
        private static readonly Color32 PaperLight = GamePalette.PaperLight;
        private static readonly Color32 PaperDark = GamePalette.PaperDark;
        private static readonly Color32 Ink = GamePalette.InkBrown;
        private static readonly Color32 Gold = GamePalette.SolarYellow;
        private static readonly Color32 GoldDark = GamePalette.GoldInk;
        private static readonly Color32 Red = GamePalette.TropicalRed;
        private static readonly Color32 Green = GamePalette.LeafGreen;
        private static readonly Color32 Blue = GamePalette.ColonialBlue;
        private static readonly Color32 Pink = GamePalette.HibiscusPink;
        private static readonly Color32 Purple = GamePalette.MysticPurple;

        private sealed class CardSpec
        {
            public string Id, Name, Description;
            public CardEffectType Effect;
            public CardTiming Timing;
            public CardTargeting Targeting;
            public int Amount;
            public Color32 Accent;
            public System.Action<PixelCanvas> DrawIcon;
        }

        private static readonly CardSpec[] Specs =
        {
            new CardSpec
            {
                Id = CardIds.LuckyRoll, Name = "Lucky Roll", Description = "After rolling: reroll both dice.",
                Effect = CardEffectType.Reroll, Timing = CardTiming.AfterRoll, Targeting = CardTargeting.None,
                Accent = Gold, DrawIcon = DrawLuckyRoll
            },
            new CardSpec
            {
                Id = CardIds.Swap, Name = "Swap", Description = "Before rolling: swap tiles with a player.",
                Effect = CardEffectType.SwapPositions, Timing = CardTiming.BeforeRoll, Targeting = CardTargeting.OtherActivePlayer,
                Accent = Blue, DrawIcon = DrawSwap
            },
            new CardSpec
            {
                Id = CardIds.Retreat, Name = "Retreat", Description = "Before rolling: move back 3 tiles.",
                Effect = CardEffectType.MoveBack, Timing = CardTiming.BeforeRoll, Targeting = CardTargeting.None, Amount = 3,
                Accent = Red, DrawIcon = DrawRetreat
            },
            new CardSpec
            {
                Id = CardIds.Shield, Name = "Shield", Description = "Before rolling: survive the unlucky tile once.",
                Effect = CardEffectType.Shield, Timing = CardTiming.BeforeRoll, Targeting = CardTargeting.None,
                Accent = Green, DrawIcon = DrawShield
            },
        };

        [MenuItem("Tools/Don't Trust Your Luck/Build Sprint 3 Cards (Art + Assets + HUD)")]
        public static void BuildFromMenu()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine() => Build();

        private static void Build()
        {
            EnsureFolder(ArtDir);
            EnsureFolder(DataDir);

            var assets = new List<CardDefinitionAsset>();
            foreach (var spec in Specs)
            {
                var sprite = WriteArt(spec);
                assets.Add(WriteDefinition(spec, sprite));
            }

            var config = AssetDatabase.LoadAssetAtPath<MatchConfigAsset>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[Sprint3CardsBuilder] {ConfigPath} not found.");
                return;
            }
            config.SetCards(assets);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Sprint2GameSceneBuilder.BuildFromCommandLine(); // HUD prefab (with hand panel) + scene wiring
            Debug.Log("[Sprint3CardsBuilder] Card art, definitions, config and HUD updated.");
        }

        // ------------------------------------------------------------------ assets

        private static Sprite WriteArt(CardSpec spec)
        {
            var canvas = new PixelCanvas(Size, Size);
            int seed = 17;
            foreach (char ch in spec.Id) seed = seed * 31 + ch; // stable across runtimes (string.GetHashCode is not guaranteed)
            DrawFrame(canvas, spec.Accent, seed);
            spec.DrawIcon(canvas);

            string path = $"{ArtDir}/Card_{spec.Id}.png";
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(canvas.Pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; // crisp pixel-art look
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 16 KB each; avoids artifacts
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static CardDefinitionAsset WriteDefinition(CardSpec spec, Sprite sprite)
        {
            string path = $"{DataDir}/Card_{spec.Id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CardDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(spec.Id, spec.Effect, spec.Timing, spec.Targeting, spec.Amount, spec.Name, spec.Description, sprite);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ------------------------------------------------------------------ art

        /// <summary>Aged paper with speckles, ink border, accent inner border and corner ornaments.</summary>
        private static void DrawFrame(PixelCanvas c, Color32 accent, int seed)
        {
            uint s = (uint)seed;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    s = s * 1664525u + 1013904223u;
                    uint r = (s >> 24) & 0xFF;
                    Color32 col = r < 14 ? PaperDark : r > 245 ? PaperLight : Paper;
                    // Slight darkening towards the edges (old print).
                    int edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                    if (edge < 6 && r < 60) col = PaperDark;
                    c.Set(x, y, col);
                }
            }
            c.Border(0, 0, Size, Size, 2, Ink);
            c.Border(4, 4, Size - 8, Size - 8, 1, accent);
            // Corner ornaments (small diamonds).
            foreach (var (x, y) in new[] { (6, 6), (Size - 7, 6), (6, Size - 7), (Size - 7, Size - 7) })
            {
                c.Set(x, y, Ink); c.Set(x + 1, y, accent); c.Set(x - 1, y, accent); c.Set(x, y + 1, accent); c.Set(x, y - 1, accent);
            }
        }

        private static void DrawLuckyRoll(PixelCanvas c)
        {
            Die(c, 12, 16, 22, 3);
            Die(c, 30, 26, 22, 6);
            c.Star(15, 49, 6, 2.5f, Gold, GoldDark);
            c.Star(50, 14, 5, 2f, Gold, GoldDark);
            c.Star(52, 53, 4, 1.8f, Gold, GoldDark);
        }

        private static void Die(PixelCanvas c, int x, int y, int size, int pips)
        {
            c.FillRect(x + 1, y - 1, size, size, PaperDark); // shadow
            c.FillRect(x, y, size, size, PaperLight);
            c.Border(x, y, size, size, 1, Ink);
            int lo = x + size / 4, mid = x + size / 2, hi = x + size - 1 - size / 4;
            int bl = y + size / 4, bm = y + size / 2, bh = y + size - 1 - size / 4;
            var spots = pips == 3
                ? new[] { (lo, bh), (mid, bm), (hi, bl) }
                : new[] { (lo, bl), (lo, bm), (lo, bh), (hi, bl), (hi, bm), (hi, bh) };
            foreach (var (px, py) in spots) c.FillCircle(px, py, 1.6f, Ink);
        }

        private static void DrawSwap(PixelCanvas c)
        {
            // Two circular arrows around two pieces.
            c.Arc(32, 32, 19, 3, 25f, 155f, Blue);
            c.Arc(32, 32, 19, 3, 205f, 335f, Pink);
            ArrowHead(c, 32, 32, 19, 155f, true, Blue);
            ArrowHead(c, 32, 32, 19, 335f, true, Pink);
            Piece(c, 22, 32, Green);
            Piece(c, 42, 32, Purple);
        }

        private static void Piece(PixelCanvas c, int x, int y, Color32 color)
        {
            c.FillCircle(x, y + 3, 4.5f, color);
            c.FillRect(x - 4, y - 7, 9, 7, color);
            c.Border(x - 4, y - 7, 9, 7, 1, Ink);
            c.Ring(x, y + 3, 4.5f, 1f, Ink);
        }

        /// <summary>Arrow head at the end of an arc, pointing along the arc direction (counter-clockwise).</summary>
        private static void ArrowHead(PixelCanvas c, int cx, int cy, float radius, float angleDeg, bool ccw, Color32 color)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var tip = new Vector2(cx + Mathf.Cos(a + (ccw ? 0.35f : -0.35f)) * radius, cy + Mathf.Sin(a + (ccw ? 0.35f : -0.35f)) * radius);
            var outer = new Vector2(cx + Mathf.Cos(a) * (radius + 6f), cy + Mathf.Sin(a) * (radius + 6f));
            var inner = new Vector2(cx + Mathf.Cos(a) * (radius - 6f), cy + Mathf.Sin(a) * (radius - 6f));
            c.FillPolygon(new[] { tip, outer, inner }, color);
        }

        private static void DrawRetreat(PixelCanvas c)
        {
            // Footprints walking back (towards the left).
            Foot(c, 46, 46); Foot(c, 38, 40); Foot(c, 30, 46); Foot(c, 22, 40);
            // Back arrow.
            c.FillRect(22, 17, 28, 6, Red);
            c.Border(22, 17, 28, 6, 1, Ink);
            c.FillPolygon(new[] { new Vector2(9, 20), new Vector2(23, 30), new Vector2(23, 10) }, Ink);
            c.FillPolygon(new[] { new Vector2(12, 20), new Vector2(22, 27), new Vector2(22, 13) }, Red);
        }

        private static void Foot(PixelCanvas c, int x, int y)
        {
            c.FillEllipse(x, y, 2.6f, 3.6f, Ink);
            c.FillCircle(x - 3, y + 4, 1.1f, Ink);
            c.FillCircle(x - 1, y + 5, 1.1f, Ink);
            c.FillCircle(x + 1, y + 5, 1.1f, Ink);
        }

        private static void DrawShield(PixelCanvas c)
        {
            var outline = new[]
            {
                new Vector2(13, 52), new Vector2(32, 56), new Vector2(51, 52), new Vector2(51, 32),
                new Vector2(32, 9), new Vector2(13, 32)
            };
            var inner = new[]
            {
                new Vector2(16, 49), new Vector2(32, 52.5f), new Vector2(48, 49), new Vector2(48, 32.5f),
                new Vector2(32, 13.5f), new Vector2(16, 32.5f)
            };
            var core = new[]
            {
                new Vector2(19, 46.5f), new Vector2(32, 49.5f), new Vector2(45, 46.5f), new Vector2(45, 33),
                new Vector2(32, 17.5f), new Vector2(19, 33)
            };
            c.FillPolygon(outline, Ink);
            c.FillPolygon(inner, Gold);
            c.FillPolygon(core, Green);
            // Four-leaf clover: protection symbol.
            c.FillCircle(29, 37, 3.6f, PaperLight);
            c.FillCircle(35, 37, 3.6f, PaperLight);
            c.FillCircle(29, 31, 3.6f, PaperLight);
            c.FillCircle(35, 31, 3.6f, PaperLight);
            c.FillRect(32, 22, 2, 8, PaperLight);
            c.Set(32, 34, Gold); c.Set(31, 34, Gold); c.Set(32, 33, Gold); c.Set(31, 33, Gold);
        }

        // ------------------------------------------------------------------ helpers

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Tiny raster for placeholder art. Origin bottom-left (Texture2D convention).</summary>
        private sealed class PixelCanvas
        {
            public readonly Color32[] Pixels;
            private readonly int _w, _h;

            public PixelCanvas(int w, int h)
            {
                _w = w; _h = h;
                Pixels = new Color32[w * h];
            }

            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= _w || y >= _h) return;
                Pixels[y * _w + x] = c;
            }

            public void FillRect(int x, int y, int w, int h, Color32 c)
            {
                for (int j = y; j < y + h; j++)
                    for (int i = x; i < x + w; i++) Set(i, j, c);
            }

            public void Border(int x, int y, int w, int h, int t, Color32 c)
            {
                FillRect(x, y, w, t, c);
                FillRect(x, y + h - t, w, t, c);
                FillRect(x, y, t, h, c);
                FillRect(x + w - t, y, t, h, c);
            }

            public void FillCircle(float cx, float cy, float r, Color32 c) => FillEllipse(cx, cy, r, r, c);

            public void FillEllipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                {
                    for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                    }
                }
            }

            public void Ring(float cx, float cy, float r, float t, Color32 c) => Arc(cx, cy, r, t, 0f, 360f, c);

            public void Arc(float cx, float cy, float r, float t, float fromDeg, float toDeg, Color32 c)
            {
                int R = Mathf.CeilToInt(r + t);
                for (int y = (int)cy - R; y <= (int)cy + R; y++)
                {
                    for (int x = (int)cx - R; x <= (int)cx + R; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < r - t * 0.5f || d > r + t * 0.5f) continue;
                        float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (a < 0f) a += 360f;
                        if (a >= fromDeg && a <= toDeg) Set(x, y, c);
                    }
                }
            }

            public void FillPolygon(Vector2[] pts, Color32 c)
            {
                float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
                foreach (var p in pts)
                {
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
                for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                {
                    for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                    {
                        if (Inside(pts, x + 0.5f, y + 0.5f)) Set(x, y, c);
                    }
                }
            }

            public void Star(float cx, float cy, float rOuter, float rInner, Color32 fill, Color32 outline)
            {
                var outer = StarPoints(cx, cy, rOuter + 1f, rInner + 0.8f);
                var inner = StarPoints(cx, cy, rOuter, rInner);
                FillPolygon(outer, outline);
                FillPolygon(inner, fill);
            }

            private static Vector2[] StarPoints(float cx, float cy, float ro, float ri)
            {
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    float r = i % 2 == 0 ? ro : ri;
                    pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                }
                return pts;
            }

            private static bool Inside(Vector2[] pts, float x, float y)
            {
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    if ((pts[i].y > y) != (pts[j].y > y) &&
                        x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                        inside = !inside;
                }
                return inside;
            }
        }
    }
}
