using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Sprint4
{
    /// <summary>
    /// Sprint 4 placeholder art painted from code (original pixel art, no third-party assets):
    /// character cutouts and portraits, UI frames/icons, environment textures, VFX textures and materials.
    /// Re-running overwrites the PNGs in place (GUIDs and references are preserved).
    /// Final illustrations replace these files 1:1 (same names, see docs/ART_DIRECTION.md).
    /// </summary>
    public static class Sprint4Art
    {
        public const string CharactersDir = "Assets/_Game/Art/Characters";
        public const string UiDir = "Assets/_Game/Art/UI";
        public const string EnvDir = "Assets/_Game/Art/Environment";
        public const string VfxDir = "Assets/_Game/Art/VFX";
        public const string MaterialsDir = "Assets/_Game/Art/Materials";

        // Palette (GDD §6.3) plus a few derived shades.
        public static readonly Color32 Ink = PixelCanvas.Hex(0x493329);
        public static readonly Color32 InkDeep = PixelCanvas.Hex(0x2E211B);
        public static readonly Color32 Paper = PixelCanvas.Hex(0xE9D7AC);
        public static readonly Color32 PaperLight = PixelCanvas.Hex(0xF4E9CC);
        public static readonly Color32 PaperDark = PixelCanvas.Hex(0xD4BF8E);
        public static readonly Color32 Gold = PixelCanvas.Hex(0xF4B942);
        public static readonly Color32 GoldDark = PixelCanvas.Hex(0xB07A12);
        public static readonly Color32 Red = PixelCanvas.Hex(0xD83A35);
        public static readonly Color32 RedDark = PixelCanvas.Hex(0x8E2320);
        public static readonly Color32 Green = PixelCanvas.Hex(0x438B52);
        public static readonly Color32 GreenDark = PixelCanvas.Hex(0x2C5E37);
        public static readonly Color32 Blue = PixelCanvas.Hex(0x287FA3);
        public static readonly Color32 BlueDark = PixelCanvas.Hex(0x1B5670);
        public static readonly Color32 Pink = PixelCanvas.Hex(0xED638B);
        public static readonly Color32 Purple = PixelCanvas.Hex(0x713E91);
        public static readonly Color32 White = PixelCanvas.Hex(0xFBF6E9);
        public static readonly Color32 Terracotta = PixelCanvas.Hex(0xB5523B);
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        // ================================================================== characters

        public sealed class CharacterArt
        {
            public string Id, Name;
            public Color Accent;
            public Texture2D Cutout;
            public Sprite Portrait;
        }

        public const int CharW = 96, CharH = 128;

        public static List<CharacterArt> BuildCharacters()
        {
            EnsureFolder(CharactersDir);
            var list = new List<CharacterArt>
            {
                Character("onca", "Onça", Gold, DrawOnca, new RectInt(23, 60, 52, 52)),
                Character("capivara", "Capivara", Blue, DrawCapivara, new RectInt(46, 50, 48, 48)),
                Character("macaco", "Macaco", Green, DrawMacaco, new RectInt(21, 60, 52, 52)),
                Character("tucano", "Tucano", Pink, DrawTucano, new RectInt(22, 58, 70, 70)),
            };
            return list;
        }

        private static CharacterArt Character(string id, string name, Color32 accent, System.Action<PixelCanvas> draw, RectInt headCrop)
        {
            var c = new PixelCanvas(CharW, CharH, (uint)(id.GetHashCode() & 0x7FFFFFFF) | 1u);
            draw(c);
            c.OutlineSilhouette(Ink, 1);
            c.OutlineSilhouette(PaperLight, 3); // die-cut paper margin
            c.OutlineSilhouette(InkDeep, 1);
            c.Grain(0.18f, 0.08f);

            string cutoutPath = $"{CharactersDir}/SPR_Char_{Cap(id)}.png";
            c.SavePng(cutoutPath);
            ImportTexture(cutoutPath, TextureWrapMode.Clamp, false, 128, true);

            // Portrait: head crop on a colored medallion.
            var p = new PixelCanvas(48, 48, 7u);
            var light = PixelCanvas.Mix(accent, White, 0.55f);
            p.FillCircle(23.5f, 23.5f, 22.5f, InkDeep);
            p.FillCircle(23.5f, 23.5f, 21f, accent);
            p.FillCircle(23.5f, 23.5f, 19f, light);
            var head = new PixelCanvas(48, 48);
            head.Blit(c, headCrop, new RectInt(2, 0, 44, 44));
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 48; x++)
                {
                    float dx = x - 23.5f, dy = y - 23.5f;
                    var h = head.Get(x, y);
                    if (h.a > 0 && dx * dx + dy * dy <= 19f * 19f) p.Set(x, y, h);
                }
            }
            string portraitPath = $"{CharactersDir}/SPR_Portrait_{Cap(id)}.png";
            p.SavePng(portraitPath);
            var portrait = ImportSprite(portraitPath, 100f, Vector4.zero, 64);

            return new CharacterArt
            {
                Id = id, Name = name, Accent = accent,
                Cutout = AssetDatabase.LoadAssetAtPath<Texture2D>(cutoutPath),
                Portrait = portrait
            };
        }

        private static void InkEllipse(PixelCanvas c, float x, float y, float rx, float ry, Color32 fill)
        {
            c.FillEllipse(x, y, rx + 1.3f, ry + 1.3f, Ink);
            c.FillEllipse(x, y, rx, ry, fill);
        }

        private static void InkLine(PixelCanvas c, float x0, float y0, float x1, float y1, float t, Color32 fill)
        {
            c.Line(x0, y0, x1, y1, t + 2.4f, Ink);
            c.Line(x0, y0, x1, y1, t, fill);
        }

        private static void InkCurve(PixelCanvas c, Vector2 a, Vector2 b, Vector2 d, float t, Color32 fill)
        {
            const int n = 16;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    var p0 = Bezier(a, b, d, i / (float)n);
                    var p1 = Bezier(a, b, d, (i + 1) / (float)n);
                    c.Line(p0.x, p0.y, p1.x, p1.y, pass == 0 ? t + 2.4f : t, pass == 0 ? Ink : fill);
                }
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 d, float t) =>
            (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * d;

        private static void Eye(PixelCanvas c, float x, float y, float r, Color32 iris)
        {
            if (iris.a > 0) c.FillEllipse(x, y, r + 1f, r + 0.6f, iris);
            c.FillCircle(x, y, r * 0.6f, InkDeep);
            c.Set(Mathf.RoundToInt(x + r * 0.3f), Mathf.RoundToInt(y + r * 0.35f), White);
        }

        private static void DrawOnca(PixelCanvas c)
        {
            var orange = PixelCanvas.Hex(0xE59A2F);
            var orangeDark = PixelCanvas.Hex(0xB86B1C);
            var cream = PixelCanvas.Hex(0xF7E9C8);
            InkCurve(c, new Vector2(64, 18), new Vector2(92, 28), new Vector2(82, 60), 6f, orange); // tail
            c.FillCircle(82, 60, 3.5f, InkDeep);
            InkEllipse(c, 30, 26, 12, 14, orange); // haunch
            InkEllipse(c, 48, 40, 23, 26, orange); // body
            InkEllipse(c, 50, 34, 11, 17, cream); // belly
            InkEllipse(c, 40, 20, 6, 15, orange); // front legs
            InkEllipse(c, 59, 20, 6, 15, orange);
            InkEllipse(c, 40, 7, 7, 4, cream);
            InkEllipse(c, 59, 7, 7, 4, cream);
            InkEllipse(c, 32, 99, 7, 7, orange); // ears
            InkEllipse(c, 66, 99, 7, 7, orange);
            c.FillCircle(32, 99, 3.2f, InkDeep);
            c.FillCircle(66, 99, 3.2f, InkDeep);
            InkEllipse(c, 49, 84, 21, 18, orange); // head
            InkEllipse(c, 49, 76, 11, 8, cream); // muzzle
            c.FillPolygon(new[] { new Vector2(44, 83), new Vector2(54, 83), new Vector2(49, 77) }, InkDeep); // nose
            c.FillPolygon(new[] { new Vector2(45.5f, 82), new Vector2(52.5f, 82), new Vector2(49, 78.5f) }, PixelCanvas.Hex(0xC4675A));
            c.Line(49, 77, 49, 73, 1.2f, InkDeep);
            c.Line(49, 73, 45, 71, 1.2f, InkDeep);
            c.Line(49, 73, 53, 71, 1.2f, InkDeep);
            Eye(c, 41, 89, 3.2f, PixelCanvas.Hex(0x9BC53D));
            Eye(c, 57, 89, 3.2f, PixelCanvas.Hex(0x9BC53D));
            foreach (var (x, y) in new[] { (28, 46), (68, 46), (37, 58), (61, 58), (24, 30), (72, 30), (30, 16), (66, 36) })
            {
                c.Ring(x, y, 2.6f, 1.4f, InkDeep);
                c.FillCircle(x, y, 1.1f, orangeDark);
            }
            foreach (var (x, y) in new[] { (37, 96), (61, 96), (49, 99), (44, 101), (54, 101), (40, 24), (59, 24), (85, 40), (88, 50) })
                c.FillCircle(x, y, 1.2f, InkDeep);
            c.Line(39, 77, 29, 79, 1f, InkDeep);
            c.Line(39, 75, 29, 73, 1f, InkDeep);
            c.Line(59, 77, 69, 79, 1f, InkDeep);
            c.Line(59, 75, 69, 73, 1f, InkDeep);
        }

        private static void DrawCapivara(PixelCanvas c)
        {
            var brown = PixelCanvas.Hex(0x9A6A43);
            var brownDark = PixelCanvas.Hex(0x6E4A2E);
            InkEllipse(c, 20, 18, 7, 13, brownDark); // legs (behind)
            InkEllipse(c, 54, 18, 7, 13, brownDark);
            InkEllipse(c, 38, 44, 29, 23, brown); // body
            InkEllipse(c, 64, 70, 17, 17, brown); // head
            InkEllipse(c, 76, 64, 13, 12, brown); // snout
            c.FillEllipse(64, 70, 15.5f, 15.5f, brown); // merge head + snout (single contour)
            InkEllipse(c, 56, 87, 5, 4, brownDark); // ear
            c.FillEllipse(85, 66, 2.5f, 4f, InkDeep); // nose
            c.Line(78, 56, 84, 58, 1.2f, InkDeep); // mouth
            Eye(c, 68, 77, 2.4f, Clear);
            c.FillEllipse(74, 66, 3.5f, 2f, PixelCanvas.Hex(0xC98466)); // cheek
            for (int i = 0; i < 26; i++)
            {
                float x = 14 + c.Rand() * 50, y = 30 + c.Rand() * 28;
                c.Line(x, y, x + 2, y + 3, 1f, brownDark);
            }
            // Hibiscus flower behind the ear.
            var center = new Vector2(62, 94);
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + 0.3f;
                c.FillCircle(center.x + Mathf.Cos(a) * 4.2f, center.y + Mathf.Sin(a) * 4.2f, 3.6f, Ink);
            }
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + 0.3f;
                c.FillCircle(center.x + Mathf.Cos(a) * 4.2f, center.y + Mathf.Sin(a) * 4.2f, 2.6f, Pink);
            }
            c.FillCircle(center.x, center.y, 1.6f, Gold);
        }

        private static void DrawMacaco(PixelCanvas c)
        {
            var brown = PixelCanvas.Hex(0x7B4B2A);
            var tan = PixelCanvas.Hex(0xE2B98A);
            InkCurve(c, new Vector2(58, 26), new Vector2(92, 26), new Vector2(84, 58), 5f, brown); // tail
            InkCurve(c, new Vector2(84, 58), new Vector2(80, 70), new Vector2(72, 62), 5f, brown);
            InkEllipse(c, 38, 20, 6, 14, brown); // legs
            InkEllipse(c, 56, 20, 6, 14, brown);
            InkEllipse(c, 36, 7, 7, 3.5f, tan);
            InkEllipse(c, 58, 7, 7, 3.5f, tan);
            InkEllipse(c, 47, 44, 17, 22, brown); // body
            InkEllipse(c, 47, 40, 10, 14, tan);
            InkLine(c, 33, 56, 24, 34, 6f, brown); // arm down
            InkEllipse(c, 23, 32, 4.5f, 4.5f, tan);
            InkLine(c, 60, 58, 76, 86, 6f, brown); // arm waving
            InkEllipse(c, 78, 90, 5, 5, tan);
            InkEllipse(c, 27, 86, 8, 8, brown); // ears
            InkEllipse(c, 67, 86, 8, 8, brown);
            c.FillCircle(27, 86, 4.5f, tan);
            c.FillCircle(67, 86, 4.5f, tan);
            InkEllipse(c, 47, 84, 18, 18, brown); // head
            c.FillPolygon(new[] { new Vector2(41, 101), new Vector2(47, 108), new Vector2(53, 101) }, brown);
            c.FillCircle(41, 87, 7.5f, tan);
            c.FillCircle(53, 87, 7.5f, tan);
            c.FillEllipse(47, 78, 12, 8.5f, tan);
            Eye(c, 41, 88, 2.4f, Clear);
            Eye(c, 53, 88, 2.4f, Clear);
            c.Set(45, 81, InkDeep); c.Set(49, 81, InkDeep);
            c.Arc(47, 77, 5, 1.3f, 200f, 340f, InkDeep);
        }

        private static void DrawTucano(PixelCanvas c)
        {
            var black = PixelCanvas.Hex(0x231E1E);
            var blackSoft = PixelCanvas.Hex(0x3E3534);
            var beak = PixelCanvas.Hex(0xF28C28);
            var beakTop = PixelCanvas.Hex(0xF6C343);
            InkLine(c, 4, 22, 92, 18, 5f, PixelCanvas.Hex(0x7A5534)); // branch
            InkEllipse(c, 12, 28, 6, 3, Green);
            InkEllipse(c, 84, 25, 6, 3, Green);
            InkEllipse(c, 76, 13, 5, 3, Green);
            c.FillPolygon(new[] { new Vector2(25, 46), new Vector2(16, 10), new Vector2(34, 10), new Vector2(40, 42) }, Ink);
            c.FillPolygon(new[] { new Vector2(27, 44), new Vector2(19, 12), new Vector2(32, 12), new Vector2(38, 42) }, black);
            InkEllipse(c, 38, 56, 19, 28, black); // body
            c.FillEllipse(32, 52, 11, 20, blackSoft); // wing
            for (int i = 0; i < 4; i++) c.Line(26 + i * 3, 40 + i, 30 + i * 3, 66 - i * 2, 1f, black);
            c.FillEllipse(38, 31, 7, 4, Red); // undertail
            InkEllipse(c, 42, 88, 15, 15, black); // head
            InkEllipse(c, 48, 68, 10, 16, White); // chest
            c.FillEllipse(48, 79, 8, 7, White);
            c.FillEllipse(49, 76, 6, 4, PixelCanvas.Hex(0xF7E3A0));
            c.FillCircle(49, 90, 5.5f, PixelCanvas.Hex(0x3E9BD1)); // eye patch
            Eye(c, 49, 90, 2.4f, Clear);
            var beakPts = new[]
            {
                new Vector2(54, 98), new Vector2(66, 101), new Vector2(80, 99), new Vector2(90, 93),
                new Vector2(92, 86), new Vector2(86, 84), new Vector2(70, 84), new Vector2(55, 80)
            };
            var outer = new Vector2[beakPts.Length];
            for (int i = 0; i < beakPts.Length; i++) outer[i] = beakPts[i] + (beakPts[i] - new Vector2(72, 91)).normalized * 1.4f;
            c.FillPolygon(outer, Ink);
            c.FillPolygon(beakPts, beak);
            c.Line(57, 96, 82, 96, 3f, beakTop);
            c.FillPolygon(new[] { new Vector2(85, 96), new Vector2(90, 93), new Vector2(92, 86), new Vector2(86, 85) }, InkDeep);
            c.Line(56, 88, 88, 88, 1f, InkDeep);
            c.FillRect(35, 20, 3, 6, PixelCanvas.Hex(0x6C7A8A));
            c.FillRect(43, 20, 3, 6, PixelCanvas.Hex(0x6C7A8A));
        }

        // ================================================================== UI

        public sealed class UiArt
        {
            public Sprite Paper, PaperDark, Lucky, Unlucky, Button, Card, Highlight, Die, Title, Banner, Bar, Knob, Check;
            public Sprite IconSun, IconEye, IconShield, IconSpeaker, IconArrow, IconStar, IconClock, IconLock;
        }

        public static UiArt BuildUi()
        {
            EnsureFolder(UiDir);
            var ui = new UiArt
            {
                Paper = Frame("Paper", Paper, Ink, Gold, Gold, 1),
                PaperDark = Frame("PaperDark", PaperDark, InkDeep, Terracotta, Green, 2),
                Lucky = Frame("FrameLucky", PaperLight, GoldDark, Green, Pink, 3, thick: 2),
                Unlucky = Frame("FrameUnlucky", PixelCanvas.Hex(0xF3DCCD), RedDark, Purple, Red, 4, thick: 2),
                Title = Frame("TitlePlaque", PaperLight, InkDeep, Red, Pink, 5, thick: 2),
                Banner = Frame("Banner", PaperLight, InkDeep, Gold, Red, 6, thick: 2),
                Card = Frame("CardFrame", White, InkDeep, Ink, Gold, 7, lace: false),
                Button = ButtonSprite(),
                Highlight = HighlightSprite(),
                Die = DieSprite(),
                Bar = BarSprite(),
                Knob = KnobSprite(),
                Check = CheckSprite(),
                IconSun = Icon("Sun", DrawSun),
                IconEye = Icon("Eye", DrawEye),
                IconShield = Icon("Shield", DrawShieldIcon),
                IconSpeaker = Icon("Speaker", DrawSpeaker),
                IconArrow = Icon("Arrow", DrawArrow),
                IconStar = Icon("Star", c => c.Star(16, 15, 13, 5.5f, Gold, InkDeep)),
                IconClock = Icon("Clock", DrawClock),
                IconLock = Icon("Lock", DrawLock),
            };
            return ui;
        }

        /// <summary>32x32 nine-slice paper frame (8 px border), drawn at 2x (PPU 50) for a crisp printed look.</summary>
        private static Sprite Frame(string name, Color32 fill, Color32 outer, Color32 inner, Color32 corner, uint seed, int thick = 1, bool lace = true)
        {
            const int S = 32;
            var c = new PixelCanvas(S, S, seed);
            c.Fill(fill); // no grain: the 9-slice center is stretched, grain would turn into blotches
            c.Border(0, 0, S, S, thick, outer);
            c.Border(thick + 1, thick + 1, S - 2 * (thick + 1), S - 2 * (thick + 1), 1, inner);
            // Bobbin-lace scallops (renda de bilro) just inside the accent line.
            int ins = thick + 3;
            for (int i = ins + 1; lace && i < S - ins - 1; i += 3)
            {
                c.Set(i, ins, PixelCanvas.Mix(fill, outer, 0.35f));
                c.Set(i, S - 1 - ins, PixelCanvas.Mix(fill, outer, 0.35f));
                c.Set(ins, i, PixelCanvas.Mix(fill, outer, 0.35f));
                c.Set(S - 1 - ins, i, PixelCanvas.Mix(fill, outer, 0.35f));
            }
            foreach (var (x, y) in new[] { (thick + 3, thick + 3), (S - thick - 4, thick + 3), (thick + 3, S - thick - 4), (S - thick - 4, S - thick - 4) })
            {
                c.Set(x, y, outer);
                c.Set(x + 1, y, corner); c.Set(x - 1, y, corner); c.Set(x, y + 1, corner); c.Set(x, y - 1, corner);
            }
            return SaveUi($"SPR_UI_{name}.png", c, new Vector4(8, 8, 8, 8), 50f);
        }

        private static Sprite ButtonSprite()
        {
            var c = new PixelCanvas(24, 24, 11u);
            c.Fill(Clear);
            c.FillRect(1, 0, 22, 23, PaperDark); // drop edge
            c.FillRect(0, 2, 24, 22, InkDeep);
            c.FillRect(1, 3, 22, 20, White);
            c.FillRect(1, 3, 22, 2, PixelCanvas.Mix(White, PaperDark, 0.6f));
            c.Set(0, 2, Clear); c.Set(23, 2, Clear); c.Set(0, 23, Clear); c.Set(23, 23, Clear);
            return SaveUi("SPR_UI_Button.png", c, new Vector4(6, 6, 6, 6), 50f);
        }

        private static Sprite HighlightSprite()
        {
            var c = new PixelCanvas(24, 24, 3u);
            c.Fill(Clear);
            c.Border(0, 0, 24, 24, 3, Gold);
            c.Border(1, 1, 22, 22, 1, PixelCanvas.Hex(0xFFE7A0));
            return SaveUi("SPR_UI_Highlight.png", c, new Vector4(6, 6, 6, 6), 50f);
        }

        private static Sprite DieSprite()
        {
            var c = new PixelCanvas(24, 24, 5u);
            c.Fill(Clear);
            c.FillRect(2, 0, 21, 21, PaperDark);
            c.FillRect(0, 2, 22, 22, InkDeep);
            c.FillRect(1, 3, 20, 20, White);
            c.Set(0, 2, Clear); c.Set(21, 2, Clear); c.Set(0, 23, Clear); c.Set(21, 23, Clear);
            return SaveUi("SPR_UI_DieFace.png", c, new Vector4(6, 6, 6, 6), 50f);
        }

        private static Sprite BarSprite()
        {
            var c = new PixelCanvas(12, 8, 2u);
            c.Fill(InkDeep);
            c.FillRect(1, 1, 10, 6, PaperDark);
            return SaveUi("SPR_UI_Bar.png", c, new Vector4(3, 3, 3, 3), 50f);
        }

        private static Sprite KnobSprite()
        {
            var c = new PixelCanvas(16, 16, 2u);
            c.Fill(Clear);
            c.FillCircle(7.5f, 7.5f, 7.5f, InkDeep);
            c.FillCircle(7.5f, 7.5f, 6f, Gold);
            c.FillCircle(6f, 9f, 2f, PixelCanvas.Hex(0xFFE7A0));
            return SaveUi("SPR_UI_Knob.png", c, Vector4.zero, 50f);
        }

        private static Sprite CheckSprite()
        {
            var c = new PixelCanvas(16, 16, 2u);
            c.Fill(Clear);
            c.Line(3, 8, 6, 4, 2.4f, Red);
            c.Line(6, 4, 13, 12, 2.4f, Red);
            return SaveUi("SPR_UI_Check.png", c, Vector4.zero, 50f);
        }

        private static Sprite Icon(string name, System.Action<PixelCanvas> draw)
        {
            var c = new PixelCanvas(32, 32, 9u);
            c.Fill(Clear);
            draw(c);
            return SaveUi($"SPR_Icon_{name}.png", c, Vector4.zero, 100f);
        }

        private static void DrawSun(PixelCanvas c)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                c.Line(16 + Mathf.Cos(a) * 9, 16 + Mathf.Sin(a) * 9, 16 + Mathf.Cos(a) * 15, 16 + Mathf.Sin(a) * 15, 2.6f, InkDeep);
                c.Line(16 + Mathf.Cos(a) * 9, 16 + Mathf.Sin(a) * 9, 16 + Mathf.Cos(a) * 14, 16 + Mathf.Sin(a) * 14, 1.2f, Gold);
            }
            c.FillCircle(16, 16, 9.5f, InkDeep);
            c.FillCircle(16, 16, 8.3f, Gold);
            c.FillCircle(13, 18, 1.2f, InkDeep);
            c.FillCircle(19, 18, 1.2f, InkDeep);
            c.Arc(16, 15, 4, 1.2f, 200f, 340f, InkDeep);
        }

        private static void DrawEye(PixelCanvas c)
        {
            var almond = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                almond.Add(new Vector2(2 + t * 28, 16 + Mathf.Sin(t * Mathf.PI) * 10));
            }
            for (int i = 16; i >= 0; i--)
            {
                float t = i / 16f;
                almond.Add(new Vector2(2 + t * 28, 16 - Mathf.Sin(t * Mathf.PI) * 10));
            }
            var pts = almond.ToArray();
            var outer = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) outer[i] = pts[i] + (pts[i] - new Vector2(16, 16)).normalized * 1.5f;
            c.FillPolygon(outer, InkDeep);
            c.FillPolygon(pts, White);
            c.FillCircle(16, 16, 7f, Red);
            c.FillCircle(16, 16, 3.5f, InkDeep);
            c.Set(18, 18, White);
            for (int i = 0; i < 5; i++) c.Line(8 + i * 4, 26, 6 + i * 5, 30, 1.3f, InkDeep); // lashes
        }

        private static void DrawShieldIcon(PixelCanvas c)
        {
            var outline = new[] { new Vector2(5, 26), new Vector2(16, 30), new Vector2(27, 26), new Vector2(27, 15), new Vector2(16, 2), new Vector2(5, 15) };
            var inner = new[] { new Vector2(7.5f, 24.5f), new Vector2(16, 27.5f), new Vector2(24.5f, 24.5f), new Vector2(24.5f, 15.5f), new Vector2(16, 5.5f), new Vector2(7.5f, 15.5f) };
            c.FillPolygon(outline, InkDeep);
            c.FillPolygon(inner, Green);
            c.FillRect(15, 7, 2, 18, Gold);
            c.FillRect(9, 17, 14, 2, Gold);
        }

        private static void DrawSpeaker(PixelCanvas c)
        {
            c.FillRect(4, 11, 8, 10, InkDeep);
            c.FillPolygon(new[] { new Vector2(10, 11), new Vector2(18, 4), new Vector2(18, 28), new Vector2(10, 21) }, InkDeep);
            c.FillRect(5, 12, 6, 8, Gold);
            c.Arc(18, 16, 6, 1.6f, 300f, 360f, InkDeep);
            c.Arc(18, 16, 6, 1.6f, 0f, 60f, InkDeep);
            c.Arc(18, 16, 10, 1.6f, 305f, 360f, InkDeep);
            c.Arc(18, 16, 10, 1.6f, 0f, 55f, InkDeep);
        }

        private static void DrawArrow(PixelCanvas c)
        {
            // Points down (texture origin is bottom-left): used above the active piece.
            var pts = new[] { new Vector2(16, 2), new Vector2(29, 17), new Vector2(21, 17), new Vector2(21, 30), new Vector2(11, 30), new Vector2(11, 17), new Vector2(3, 17) };
            var inner = new[] { new Vector2(16, 5.5f), new Vector2(25.5f, 15.5f), new Vector2(19.5f, 15.5f), new Vector2(19.5f, 28.5f), new Vector2(12.5f, 28.5f), new Vector2(12.5f, 15.5f), new Vector2(6.5f, 15.5f) };
            c.FillPolygon(pts, InkDeep);
            c.FillPolygon(inner, Gold);
            c.Line(14, 18, 14, 27, 1.4f, PixelCanvas.Hex(0xFFE7A0));
        }

        private static void DrawClock(PixelCanvas c)
        {
            c.FillCircle(16, 16, 15f, InkDeep);
            c.FillCircle(16, 16, 13.5f, White);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                c.Set(Mathf.RoundToInt(16 + Mathf.Cos(a) * 11), Mathf.RoundToInt(16 + Mathf.Sin(a) * 11), InkDeep);
            }
            c.Line(16, 16, 16, 25, 1.6f, InkDeep);
            c.Line(16, 16, 22, 13, 1.6f, InkDeep);
            c.FillCircle(16, 16, 1.5f, Red);
        }

        private static void DrawLock(PixelCanvas c)
        {
            c.Arc(16, 19, 6, 2.2f, 0f, 180f, InkDeep);
            c.FillRect(8, 6, 16, 13, InkDeep);
            c.FillRect(9, 7, 14, 11, PaperDark);
            c.FillCircle(16, 13, 1.6f, InkDeep);
        }

        private static Sprite SaveUi(string file, PixelCanvas c, Vector4 border, float ppu)
        {
            string path = $"{UiDir}/{file}";
            c.SavePng(path);
            return ImportSprite(path, ppu, border, 64);
        }

        // ================================================================== environment

        public sealed class EnvArt
        {
            public Texture2D Cobble, Stone, Grass, Leaves, LeavesDark, Bark, Wood, Roof, Awning, Sky, Sail, Flag, Sand;
            public Texture2D[] Facades;
            public Texture2D TileEven, TileOdd, TileStart, TileLucky, TileUnlucky, TileBeyond;
            public Texture2D VfxDot, VfxSquare, VfxStar;
        }

        public static EnvArt BuildEnvironment()
        {
            EnsureFolder(EnvDir);
            EnsureFolder(VfxDir);
            var env = new EnvArt
            {
                Cobble = Tex("T_Env_Cobble", 64, 64, DrawCobble, true),
                Stone = Tex("T_Env_Stone", 32, 32, c => Noise(c, PixelCanvas.Hex(0xE6D6B4), 0.5f, 0.07f), true),
                Grass = Tex("T_Env_Grass", 32, 32, DrawGrass, true),
                Leaves = Tex("T_Env_Leaves", 32, 32, c => DrawLeaves(c, Green, GreenDark), true),
                LeavesDark = Tex("T_Env_LeavesDark", 32, 32, c => DrawLeaves(c, GreenDark, PixelCanvas.Hex(0x1E4428)), true),
                Bark = Tex("T_Env_Bark", 16, 32, DrawBark, true),
                Wood = Tex("T_Env_Wood", 32, 32, DrawWood, true),
                Roof = Tex("T_Env_Roof", 32, 32, DrawRoof, true),
                Awning = Tex("T_Env_Awning", 32, 32, DrawAwning, true),
                Sand = Tex("T_Env_Sand", 32, 32, c => Noise(c, PixelCanvas.Hex(0xEBD3A0), 0.6f, 0.08f), true),
                Sky = Tex("T_Env_SkyBackdrop", 256, 96, DrawSky, false),
                Sail = Tex("T_Env_Sail", 32, 32, DrawSail, false),
                Flag = Tex("T_Env_Bandeirinha", 16, 16, DrawFlag, false),
                Facades = new[]
                {
                    Tex("T_Env_Facade_Yellow", 64, 64, c => DrawFacade(c, PixelCanvas.Hex(0xF2C14E), Blue, 1), false),
                    Tex("T_Env_Facade_Blue", 64, 64, c => DrawFacade(c, PixelCanvas.Hex(0x5FA8C8), PixelCanvas.Hex(0xF2C14E), 2), false),
                    Tex("T_Env_Facade_Pink", 64, 64, c => DrawFacade(c, PixelCanvas.Hex(0xEE9AA9), Green, 3), false),
                    Tex("T_Env_Facade_Green", 64, 64, c => DrawFacade(c, PixelCanvas.Hex(0x7FB87E), Red, 4), false),
                    Tex("T_Env_Facade_Cream", 64, 64, c => DrawFacade(c, PixelCanvas.Hex(0xF1E3C2), Purple, 5), false),
                    Tex("T_Env_Facade_Lilac", 64, 64, c => DrawFacade(c, PixelCanvas.Hex(0xB59AC9), Blue, 6), false),
                },
                TileEven = Tex("T_Tile_Even", 64, 64, c => DrawTile(c, Blue, Gold, 0), false),
                TileOdd = Tex("T_Tile_Odd", 64, 64, c => DrawTile(c, Terracotta, Green, 1), false),
                TileStart = Tex("T_Tile_Start", 64, 64, c => DrawTile(c, Green, White, 2), false),
                TileLucky = Tex("T_Tile_Lucky", 64, 64, c => DrawTile(c, GoldDark, Gold, 3), false),
                TileUnlucky = Tex("T_Tile_Unlucky", 64, 64, c => DrawTile(c, RedDark, Purple, 4), false),
                TileBeyond = Tex("T_Tile_Beyond", 64, 64, c => DrawTile(c, PixelCanvas.Hex(0x7E7468), PixelCanvas.Hex(0x9C9185), 5), false),
                VfxDot = Vfx("T_VFX_Dot", c => c.FillCircle(7.5f, 7.5f, 7f, White)),
                VfxSquare = Vfx("T_VFX_Square", c => c.FillRect(2, 4, 12, 8, White)),
                VfxStar = Vfx("T_VFX_Star", c => c.FillPolygon(PixelCanvas.StarPoints(7.5f, 7.5f, 7.5f, 3f), White)),
            };
            return env;
        }

        private static Texture2D Tex(string name, int w, int h, System.Action<PixelCanvas> draw, bool repeat)
        {
            var c = new PixelCanvas(w, h, (uint)(name.GetHashCode() & 0x7FFFFFFF) | 1u);
            draw(c);
            string path = $"{EnvDir}/{name}.png";
            c.SavePng(path);
            ImportTexture(path, repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, true, Mathf.Max(w, h), false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Vfx(string name, System.Action<PixelCanvas> draw)
        {
            var c = new PixelCanvas(16, 16);
            c.Fill(Clear);
            draw(c);
            string path = $"{VfxDir}/{name}.png";
            c.SavePng(path);
            ImportTexture(path, TextureWrapMode.Clamp, false, 16, true);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Noise(PixelCanvas c, Color32 baseColor, float amount, float strength)
        {
            c.Fill(baseColor);
            c.Grain(amount, strength);
        }

        private static void DrawCobble(PixelCanvas c)
        {
            c.Fill(PixelCanvas.Hex(0x8A7660)); // mortar
            var stones = new[] { PixelCanvas.Hex(0xCDB896), PixelCanvas.Hex(0xBFA684), PixelCanvas.Hex(0xD8C7A6), PixelCanvas.Hex(0xB59C80) };
            for (int gy = 0; gy < 6; gy++)
            {
                for (int gx = 0; gx < 6; gx++)
                {
                    float x = gx * 10.7f + 5 + (gy % 2) * 5 + (c.Rand() - 0.5f) * 2f;
                    float y = gy * 10.7f + 5 + (c.Rand() - 0.5f) * 2f;
                    var col = stones[(int)(c.Rand() * stones.Length) % stones.Length];
                    for (int ox = -1; ox <= 1; ox++)
                        for (int oy = -1; oy <= 1; oy++)
                            c.FillEllipse(x + ox * 64, y + oy * 64, 4.6f + c.Rand(), 4.2f + c.Rand(), col); // wraps (tileable)
                }
            }
            c.Grain(0.3f, 0.06f);
        }

        private static void DrawGrass(PixelCanvas c)
        {
            c.Fill(PixelCanvas.Hex(0x5E9A4F));
            for (int i = 0; i < 90; i++)
            {
                int x = (int)(c.Rand() * 32), y = (int)(c.Rand() * 32);
                c.Set(x, y, c.Rand() > 0.5f ? PixelCanvas.Hex(0x4A803F) : PixelCanvas.Hex(0x78B35E));
                c.Set(x, y + 1, PixelCanvas.Hex(0x4A803F));
            }
        }

        private static void DrawLeaves(PixelCanvas c, Color32 a, Color32 b)
        {
            c.Fill(a);
            for (int i = 0; i < 28; i++)
            {
                float x = c.Rand() * 32, y = c.Rand() * 32;
                c.FillEllipse(x, y, 2.5f, 1.6f, b);
                c.Set((int)x, (int)y + 2, PixelCanvas.Mix(a, White, 0.25f));
            }
        }

        private static void DrawBark(PixelCanvas c)
        {
            c.Fill(PixelCanvas.Hex(0x7A5534));
            for (int y = 0; y < 32; y += 4)
                for (int x = 0; x < 16; x++) c.Set(x, y + ((x / 4) % 2), PixelCanvas.Hex(0x5C3E25));
        }

        private static void DrawWood(PixelCanvas c)
        {
            c.Fill(PixelCanvas.Hex(0x9C6B42));
            for (int y = 0; y < 32; y += 8) c.FillRect(0, y, 32, 1, PixelCanvas.Hex(0x6E4A2E));
            for (int i = 0; i < 20; i++) c.Set((int)(c.Rand() * 32), (int)(c.Rand() * 32), PixelCanvas.Hex(0x80552F));
        }

        private static void DrawRoof(PixelCanvas c)
        {
            c.Fill(Terracotta);
            for (int y = 0; y < 32; y += 6)
            {
                c.FillRect(0, y, 32, 1, PixelCanvas.Hex(0x7D3424));
                int off = (y / 6) % 2 * 4;
                for (int x = off; x < 32; x += 8)
                {
                    c.FillRect(x, y + 1, 1, 5, PixelCanvas.Hex(0x8F3E2C));
                    c.FillRect(x + 2, y + 4, 3, 1, PixelCanvas.Hex(0xD27257));
                }
            }
        }

        private static void DrawAwning(PixelCanvas c)
        {
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++) c.Set(x, y, (x / 8) % 2 == 0 ? Red : White);
            c.FillRect(0, 0, 32, 2, InkDeep);
        }

        private static void DrawSky(PixelCanvas c)
        {
            var top = PixelCanvas.Hex(0x8FB9CF);
            var mid = PixelCanvas.Hex(0xF5C79A);
            var low = PixelCanvas.Hex(0xF9DFA6);
            for (int y = 0; y < c.Height; y++)
            {
                float t = y / (float)(c.Height - 1);
                var col = t > 0.55f ? PixelCanvas.Mix(mid, top, (t - 0.55f) / 0.45f) : PixelCanvas.Mix(low, mid, t / 0.55f);
                for (int x = 0; x < c.Width; x++) c.Set(x, y, col);
            }
            c.FillCircle(196, 30, 13, PixelCanvas.Hex(0xFFF0C4)); // low afternoon sun
            c.FillCircle(196, 30, 10, PixelCanvas.Hex(0xFFE08A));
            for (int i = 0; i < 7; i++)
            {
                float x = 12 + i * 37 + c.Rand() * 10, y = 58 + c.Rand() * 26;
                c.FillEllipse(x, y, 16, 4, PixelCanvas.Hex(0xFBEBD6));
                c.FillEllipse(x + 6, y + 3, 9, 4, PixelCanvas.Hex(0xFFF6EA));
            }
            // Sea strip with jangada sails on the horizon (Fortaleza coast).
            c.FillRect(0, 0, c.Width, 12, PixelCanvas.Hex(0x3F86A6));
            for (int i = 0; i < 40; i++) c.FillRect((int)(c.Rand() * c.Width), (int)(c.Rand() * 11), 3, 1, PixelCanvas.Hex(0x7FB9CF));
            foreach (int x in new[] { 40, 118, 170 })
            {
                c.FillPolygon(new[] { new Vector2(x, 11), new Vector2(x + 7, 11), new Vector2(x + 1, 24) }, White);
                c.FillRect(x - 2, 10, 11, 1, InkDeep);
            }
        }

        private static void DrawSail(PixelCanvas c)
        {
            c.Fill(Clear);
            var pts = new[] { new Vector2(2, 2), new Vector2(30, 2), new Vector2(4, 31) };
            c.FillPolygon(pts, InkDeep);
            c.FillPolygon(new[] { new Vector2(4, 3.5f), new Vector2(27, 3.5f), new Vector2(5, 28) }, White);
            c.FillPolygon(new[] { new Vector2(4, 9), new Vector2(21, 9), new Vector2(18, 12), new Vector2(4, 12) }, Red);
        }

        private static void DrawFlag(PixelCanvas c)
        {
            c.Fill(Clear);
            c.FillPolygon(new[] { new Vector2(0, 16), new Vector2(16, 16), new Vector2(16, 0), new Vector2(8, 5), new Vector2(0, 0) }, White);
        }

        /// <summary>Platibanda house: parapet with ornaments hiding the roof, wooden door, two shuttered windows.</summary>
        private static void DrawFacade(PixelCanvas c, Color32 wall, Color32 shutter, int variant)
        {
            c.Fill(wall);
            c.Grain(0.35f, 0.06f);
            var trim = White;
            var shade = PixelCanvas.Mix(wall, InkDeep, 0.25f);
            c.FillRect(0, 0, 64, 5, shade); // barrado
            c.FillRect(0, 46, 64, 2, trim); // cornice
            c.FillRect(0, 48, 64, 1, shade);
            // Parapet ornaments.
            for (int x = 4; x < 60; x += 8)
            {
                c.FillRect(x, 51, 4, 6, PixelCanvas.Mix(wall, White, 0.35f));
                c.Border(x, 51, 4, 6, 1, shade);
            }
            c.FillRect(0, 60, 64, 4, trim);
            if (variant % 2 == 0) c.FillPolygon(new[] { new Vector2(24, 60), new Vector2(40, 60), new Vector2(32, 64) }, trim);
            // Door with arched transom.
            var wood = variant % 3 == 0 ? Blue : variant % 3 == 1 ? PixelCanvas.Hex(0x7A4E2E) : GreenDark;
            c.FillRect(25, 5, 14, 26, trim);
            c.FillEllipse(32, 31, 7, 5, trim);
            c.FillRect(26, 5, 12, 25, wood);
            c.FillEllipse(32, 30, 6, 4, PixelCanvas.Hex(0x38506A));
            c.FillRect(32, 5, 1, 25, InkDeep);
            // Windows with shutters.
            foreach (int x in new[] { 7, 45 })
            {
                c.FillRect(x - 1, 17, 14, 20, trim);
                c.FillRect(x, 18, 12, 18, shutter);
                for (int y = 20; y < 36; y += 3) c.FillRect(x, y, 12, 1, PixelCanvas.Mix(shutter, InkDeep, 0.35f));
                c.FillRect(x + 6, 18, 1, 18, InkDeep);
                c.FillRect(x - 2, 16, 16, 1, shade);
            }
        }

        /// <summary>
        /// Ladrilho hidráulico tile: patterned band around a light center where the number is printed, so the number
        /// keeps the same contrast on every tile type (luck is also shown by shape/icons, not color only).
        /// </summary>
        private static void DrawTile(PixelCanvas c, Color32 band, Color32 accent, int kind)
        {
            var center = kind == 3 ? PixelCanvas.Hex(0xFFF1C6) : kind == 4 ? PixelCanvas.Hex(0xF8DAD2) : kind == 5 ? PixelCanvas.Hex(0xD9D2C4) : PaperLight;
            c.Fill(band);
            c.FillRect(9, 9, 46, 46, center);
            c.Border(0, 0, 64, 64, 2, InkDeep);
            c.Border(8, 8, 48, 48, 1, InkDeep);
            // Corner quarter-circles and band dots (hydraulic tile motif).
            foreach (var (x, y) in new[] { (2, 2), (61, 2), (2, 61), (61, 61) }) c.FillCircle(x, y, 6, accent);
            for (int i = 12; i < 54; i += 6)
            {
                c.FillCircle(i, 4.5f, 1.3f, accent);
                c.FillCircle(i, 59.5f, 1.3f, accent);
                c.FillCircle(4.5f, i, 1.3f, accent);
                c.FillCircle(59.5f, i, 1.3f, accent);
            }
            switch (kind)
            {
                case 2: // start: chevrons pointing along the path
                    for (int k = 0; k < 2; k++)
                        c.FillPolygon(new[] { new Vector2(40 + k * 6, 32), new Vector2(46 + k * 6, 32), new Vector2(40 + k * 6, 42), new Vector2(34 + k * 6, 42) }, Green);
                    break;
                case 3: // lucky: sun rays
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        c.Line(32 + Mathf.Cos(a) * 14, 32 + Mathf.Sin(a) * 14, 32 + Mathf.Cos(a) * 22, 32 + Mathf.Sin(a) * 22, 1.4f, Gold);
                    }
                    break;
                case 4: // unlucky: eye motif ring
                    c.Ring(32, 32, 19, 1.5f, Red);
                    c.Ring(32, 32, 22, 1f, Purple);
                    break;
                case 5: // beyond the lucky number (unreachable): diagonal hatching
                    for (int d = -64; d < 64; d += 6)
                        for (int x = 9; x < 55; x++)
                        {
                            int y = x + d;
                            if (y >= 9 && y < 55) c.Set(x, y, PixelCanvas.Hex(0xBDB4A5));
                        }
                    break;
            }
            c.Grain(0.2f, 0.05f);
        }

        // ================================================================== materials & meshes

        public const string SimpleLit = "Universal Render Pipeline/Simple Lit";
        public const string Unlit = "Universal Render Pipeline/Unlit";
        public const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        public static Material Mat(string name, Color color, Texture tex = null, Vector2? tiling = null,
            bool alphaClip = false, bool twoSided = false, string shaderName = SimpleLit, float smoothness = 0.08f)
        {
            EnsureFolder(MaterialsDir);
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(shaderName) ?? Shader.Find(SimpleLit) ?? Shader.Find("Standard");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            ConfigureSurface(m, color, tex, tiling, alphaClip, twoSided, smoothness);
            return m;
        }

        /// <summary>Updates an existing material in place (keeps its GUID) with the given look.</summary>
        public static void ConfigureSurface(Material m, Color color, Texture tex, Vector2? tiling, bool alphaClip, bool twoSided, float smoothness = 0.08f)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            var scale = tiling ?? Vector2.one;
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", scale);
            if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", scale);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (alphaClip)
            {
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = -1;
            }
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        /// <summary>URP Unlit transparent (alpha blend) material: used only for the shield aura.</summary>
        public static Material TransparentMat(string name, Color color)
        {
            var m = Mat(name, color, null, null, false, false, Unlit);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Triangular prism (gable roof), 1x1x1, ridge along X, base at y=0. Saved as an asset.</summary>
        public static Mesh RoofMesh()
        {
            string path = $"{EnvDir}/Mesh_RoofPrism.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "RoofPrism" };
            mesh.Clear();
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 1), new Vector2(2, 0) });
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count;
                v.AddRange(new[] { a, b, c });
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0) });
                tris.AddRange(new[] { i, i + 1, i + 2 });
            }
            var l0 = new Vector3(-0.5f, 0, -0.5f); var r0 = new Vector3(0.5f, 0, -0.5f);
            var l1 = new Vector3(-0.5f, 0, 0.5f); var r1 = new Vector3(0.5f, 0, 0.5f);
            var lt = new Vector3(-0.5f, 1, 0); var rt = new Vector3(0.5f, 1, 0);
            Quad(l0, lt, rt, r0); // front slope
            Quad(r1, rt, lt, l1); // back slope
            Tri(l1, lt, l0); // left gable
            Tri(r0, rt, r1); // right gable
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ================================================================== import helpers

        public static Sprite ImportSprite(string path, float ppu, Vector4 border, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // tiny files; avoids block artifacts
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void ImportTexture(string path, TextureWrapMode wrap, bool mips, int maxSize, bool alpha)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = mips;
            importer.alphaIsTransparency = alpha;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.wrapMode = wrap;
            importer.sRGBTexture = true;
            importer.textureCompression = maxSize <= 64 ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            importer.maxTextureSize = Mathf.ClosestPowerOfTwo(Mathf.Max(32, maxSize)) < maxSize ? Mathf.NextPowerOfTwo(maxSize) : Mathf.ClosestPowerOfTwo(Mathf.Max(32, maxSize));
            importer.SaveAndReimport();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Cap(string id) => char.ToUpperInvariant(id[0]) + id.Substring(1);
    }
}
