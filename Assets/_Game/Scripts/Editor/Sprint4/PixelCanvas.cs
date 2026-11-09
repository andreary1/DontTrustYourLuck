using System.IO;
using UnityEditor;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Sprint4
{
    /// <summary>
    /// Small RGBA raster used to paint original placeholder pixel art from code (origin bottom-left, Texture2D
    /// convention). Deterministic: the same code always produces the same image, so regenerating assets is safe.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly Color32[] Pixels;
        public readonly int Width, Height;
        private uint _seed;

        public PixelCanvas(int w, int h, uint seed = 12345u)
        {
            Width = w; Height = h;
            Pixels = new Color32[w * h];
            _seed = seed == 0 ? 1u : seed;
        }

        public static Color32 Hex(int rgb, byte a = 255) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), a);

        public static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        /// <summary>Deterministic LCG in [0,1).</summary>
        public float Rand()
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 8) & 0xFFFFFF) / 16777216f;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 Get(int x, int y) => InBounds(x, y) ? Pixels[y * Width + x] : new Color32(0, 0, 0, 0);

        public void Set(int x, int y, Color32 c)
        {
            if (InBounds(x, y)) Pixels[y * Width + x] = c;
        }

        public void Fill(Color32 c)
        {
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = c;
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
                    float dx = (x - cx) / Mathf.Max(0.01f, rx), dy = (y - cy) / Mathf.Max(0.01f, ry);
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

        /// <summary>Thick line made of discs.</summary>
        public void Line(float x0, float y0, float x1, float y1, float thickness, Color32 c)
        {
            float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                FillCircle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), thickness * 0.5f, c);
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

        public void Star(float cx, float cy, float rOuter, float rInner, Color32 fill, Color32 outline, int points = 5)
        {
            FillPolygon(StarPoints(cx, cy, rOuter + 1f, rInner + 0.8f, points), outline);
            FillPolygon(StarPoints(cx, cy, rOuter, rInner, points), fill);
        }

        public static Vector2[] StarPoints(float cx, float cy, float ro, float ri, int points = 5)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? ro : ri;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        /// <summary>Paints <paramref name="c"/> on transparent pixels touching an opaque one (sticker outline).</summary>
        public void OutlineSilhouette(Color32 c, int thickness = 1)
        {
            for (int pass = 0; pass < thickness; pass++)
            {
                var copy = (Color32[])Pixels.Clone();
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (copy[y * Width + x].a > 0) continue;
                        if (Opaque(copy, x - 1, y) || Opaque(copy, x + 1, y) || Opaque(copy, x, y - 1) || Opaque(copy, x, y + 1))
                            Pixels[y * Width + x] = c;
                    }
                }
            }
        }

        /// <summary>Draws ink on opaque pixels that border a different color region darker (inner contour).</summary>
        public void InkEdges(Color32 ink, int colorThreshold = 60)
        {
            var copy = (Color32[])Pixels.Clone();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = copy[y * Width + x];
                    if (p.a == 0 || Same(p, ink)) continue;
                    // Only mark the pixel when a lighter... any neighbor differs strongly and is to the right/top,
                    // giving 1-px lines without doubling.
                    if (Differs(p, At(copy, x + 1, y), colorThreshold) || Differs(p, At(copy, x, y + 1), colorThreshold))
                        Pixels[y * Width + x] = ink;
                }
            }
        }

        private Color32 At(Color32[] src, int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? src[y * Width + x] : new Color32(0, 0, 0, 0);

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        private static bool Differs(Color32 a, Color32 b, int threshold)
        {
            if (b.a == 0) return false; // the silhouette outline handles the outside
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > threshold;
        }

        private bool Opaque(Color32[] src, int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && src[y * Width + x].a > 0;

        /// <summary>Random darker/lighter specks on opaque pixels (printed-paper grain).</summary>
        public void Grain(float amount, float strength)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                if (Pixels[i].a == 0) continue;
                float r = Rand();
                if (r > amount) continue;
                float k = (Rand() - 0.5f) * 2f * strength;
                var p = Pixels[i];
                Pixels[i] = new Color32(Sh(p.r, k), Sh(p.g, k), Sh(p.b, k), p.a);
            }
        }

        private static byte Sh(byte v, float k) => (byte)Mathf.Clamp(v + v * k, 0, 255);

        /// <summary>Copies a scaled (nearest) region of another canvas into this one.</summary>
        public void Blit(PixelCanvas src, RectInt from, RectInt to)
        {
            for (int y = 0; y < to.height; y++)
            {
                for (int x = 0; x < to.width; x++)
                {
                    int sx = from.x + Mathf.FloorToInt(x * from.width / (float)to.width);
                    int sy = from.y + Mathf.FloorToInt(y * from.height / (float)to.height);
                    var c = src.Get(sx, sy);
                    if (c.a > 0) Set(to.x + x, to.y + y, c);
                }
            }
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

        /// <summary>Writes the PNG and returns the asset path (import settings are applied by the caller).</summary>
        public string SavePng(string assetPath)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(Pixels);
            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return assetPath;
        }
    }
}
