using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    /// <summary>Outline of the closed circuit (presentation only: the logical order never depends on it).</summary>
    public enum CircuitShape
    {
        /// <summary>Rectangular plaza loop (Oaxaca).</summary>
        Plaza = 0,
        /// <summary>"D" loop: straight promenade in front, rounded bay side at the back (Fortaleza).</summary>
        Bay = 1,
    }

    /// <summary>
    /// Bay ("D") circuit: tile 1 sits on the front-left corner, tiles run right along the straight promenade, then
    /// around a superellipse arc at the back and down to tile 1 again. Tiles are spaced by equal arc length and
    /// turned to the path, with the yaw folded into [-45, 45] degrees so printed numbers never read upside down.
    /// </summary>
    public static class BayCircuit
    {
        public const float HalfWidth = 6.1f;
        public const float Depth = 6.7f;
        public const float FrontZ = -3.5f;
        /// <summary>Superellipse exponent: 2 is an ellipse, higher values push the arc towards a rectangle.</summary>
        public const float Exponent = 2.6f;
        public const float StartSpur = 1.2f;

        private const int Samples = 720;
        private static Vector3[] _points;
        private static float[] _lengths;

        public static float Perimeter
        {
            get
            {
                EnsureSampled();
                return _lengths[Samples];
            }
        }

        public static Pose Pose(int index, int length)
        {
            EnsureSampled();
            length = Mathf.Max(1, length);
            int tile = index == 0 ? 1 : index;
            float target = _lengths[Samples] * (tile - 1) / length;
            int k = Find(target);
            float segment = _lengths[k + 1] - _lengths[k];
            float t = segment > 1e-5f ? (target - _lengths[k]) / segment : 0f;
            var position = Vector3.Lerp(_points[k], _points[k + 1], t);
            var tangent = _points[Mathf.Min(k + 2, Samples)] - _points[Mathf.Max(k - 1, 0)];
            float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            yaw = Mathf.Repeat(yaw + 45f, 90f) - 45f;
            if (index == 0) return new Pose(position + Vector3.back * StartSpur, Quaternion.identity);
            return new Pose(position, Quaternion.Euler(0f, yaw, 0f));
        }

        private static int Find(float target)
        {
            int lo = 0, hi = Samples - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_lengths[mid] <= target) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        private static void EnsureSampled()
        {
            if (_points != null) return;
            _points = new Vector3[Samples + 1];
            _lengths = new float[Samples + 1];
            // Share of samples on the straight edge, proportional to its rough share of the perimeter.
            int straight = Samples / 3;
            for (int i = 0; i <= Samples; i++)
            {
                Vector3 p;
                if (i <= straight)
                {
                    p = new Vector3(Mathf.Lerp(-HalfWidth, HalfWidth, i / (float)straight), 0f, FrontZ);
                }
                else
                {
                    float a = (i - straight) / (float)(Samples - straight) * Mathf.PI; // 0 = right corner, PI = left corner
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float e = 2f / Exponent;
                    p = new Vector3(HalfWidth * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e), 0f,
                        FrontZ + Depth * Mathf.Pow(Mathf.Abs(s), e));
                }
                _points[i] = p;
                if (i > 0) _lengths[i] = _lengths[i - 1] + Vector3.Distance(_points[i - 1], p);
            }
        }
    }
}
