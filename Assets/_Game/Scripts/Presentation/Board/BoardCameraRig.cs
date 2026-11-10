using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    /// <summary>
    /// Fixed elevated camera that frames the whole board. Re-frames only when the screen size or the HUD-free
    /// area changes (browser window resize), so it is cheap and needs no input.
    ///
    /// Two modes:
    /// - <see cref="fullScreenSafeArea"/> on (Sprint 4 default): the camera renders the whole screen so the
    ///   diorama shows behind the HUD, and an off-center projection places the board inside the HUD-free
    ///   "safe area" at the same scale it would have in a full-screen frame of that size.
    /// - off (Sprint 2 behavior): Camera.rect is restricted to the safe area.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraRig : MonoBehaviour
    {
        [SerializeField] private BoardView board;
        [Tooltip("Downward tilt in degrees. ~50-60 keeps numbers readable without an extreme angle.")]
        [Range(20f, 89f)] [SerializeField] private float pitch = 55f;
        [Range(-180f, 180f)] [SerializeField] private float yaw = 0f;
        [Tooltip("Viewport margin on each side (0.05 = 5%).")]
        [Range(0f, 0.3f)] [SerializeField] private float margin = 0.06f;
        [Tooltip("Render full screen and fit the board in the HUD-free area with an off-center projection.")]
        [SerializeField] private bool fullScreenSafeArea;

        private Camera _camera;
        private int _lastWidth;
        private int _lastHeight;
        private Rect _safeArea = new Rect(0f, 0f, 1f, 1f);
        private Transform _focus;
        private Vector3 _overviewPosition;
        private Vector3 _overviewCenter;
        private bool _hasOverview;
        public Transform FocusTarget => _focus;
        public void FocusOn(Transform target) => _focus = target;

        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());
        public Rect SafeArea => _safeArea;
        public bool FullScreenSafeArea => fullScreenSafeArea;

        public void Configure(BoardView target, float pitchDegrees)
        {
            board = target;
            pitch = pitchDegrees;
        }

        public void SetFullScreenSafeArea(bool enabled, float marginFraction)
        {
            fullScreenSafeArea = enabled;
            margin = Mathf.Clamp(marginFraction, 0f, 0.3f);
        }

        /// <summary>
        /// Area of the screen (normalized) not covered by the HUD. In safe-area mode the camera keeps
        /// rendering full screen; otherwise rendering is restricted to it (pixels outside must be cleared
        /// by another camera).
        /// </summary>
        public void SetViewport(Rect normalized)
        {
            if (fullScreenSafeArea)
            {
                if (_safeArea == normalized && Camera.rect == FullRect) return;
                _safeArea = normalized;
                Frame();
                return;
            }
            if (Camera.rect == normalized) return;
            Camera.rect = normalized;
            Frame();
        }

        private static readonly Rect FullRect = new Rect(0f, 0f, 1f, 1f);

        private void Start() => Frame();

        private void LateUpdate()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight) Frame();
            if (!_hasOverview) return;
            Vector3 desired = _overviewPosition;
            if (_focus != null)
            {
                // A modest dolly and partial follow retain the surrounding city and board context.
                desired += (_focus.position - _overviewCenter) * 0.35f;
                desired += transform.forward * Vector3.Distance(_overviewPosition, _overviewCenter) * 0.10f;
            }
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        }

        private void OnDisable()
        {
            if (_camera != null) _camera.ResetProjectionMatrix();
        }

        [ContextMenu("Frame Board")]
        public void Frame()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (board == null || board.TileCount == 0) return;

            var cam = Camera;
            var bounds = board.GetBoardBounds();
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var corners = GetCorners(bounds);
            float limit = 1f - 2f * margin;

            Rect safe = fullScreenSafeArea ? Sanitize(_safeArea) : FullRect;
            if (fullScreenSafeArea)
            {
                cam.rect = FullRect;
                cam.ResetProjectionMatrix(); // read the real aspect/FOV before overriding
            }
            float aspect = Mathf.Max(0.01f, cam.aspect) * safe.width / safe.height;

            if (cam.orthographic)
            {
                var inverse = Quaternion.Inverse(rotation);
                float halfHeight = 0f;
                foreach (var c in corners)
                {
                    var local = inverse * (c - bounds.center);
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y), Mathf.Abs(local.x) / aspect);
                }
                halfHeight /= Mathf.Max(0.1f, limit);
                cam.orthographicSize = halfHeight / safe.height;
                transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * (bounds.extents.magnitude + 10f), rotation);
                if (fullScreenSafeArea)
                {
                    var o = ComputeOffCenterExtents(safe, halfHeight * aspect, halfHeight);
                    cam.projectionMatrix = Matrix4x4.Ortho(o.x, o.y, o.z, o.w, cam.nearClipPlane, cam.farClipPlane);
                }
                return;
            }

            // Safe-area frustum: same scale as the full-screen FOV, cropped to the safe rect.
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * safe.height;
            float tanH = tanV * aspect;

            // Binary search for the closest distance where every corner fits in the safe frustum.
            float near = 0.5f, far = 500f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (near + far) * 0.5f;
                if (Fits(corners, bounds.center, rotation, mid, tanH * limit, tanV * limit, cam.nearClipPlane)) far = mid;
                else near = mid;
            }

            transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * far, rotation);
            _overviewPosition = transform.position;
            _overviewCenter = bounds.center;
            _hasOverview = true;

            if (fullScreenSafeArea)
            {
                float n = cam.nearClipPlane;
                var e = ComputeOffCenterExtents(safe, tanH, tanV);
                cam.projectionMatrix = Matrix4x4.Frustum(e.x * n, e.y * n, e.z * n, e.w * n, n, cam.farClipPlane);
            }
        }

        /// <summary>
        /// Full-screen frustum extents (left, right, bottom, top) such that the symmetric safe-area frustum
        /// [-halfW, halfW] x [-halfH, halfH] (centered on the optical axis) lands exactly on <paramref name="safe"/>.
        /// Values are tangents for perspective or world units for orthographic projection.
        /// </summary>
        public static Vector4 ComputeOffCenterExtents(Rect safe, float halfW, float halfH)
        {
            safe = Sanitize(safe);
            float unitX = 2f * halfW / safe.width;  // extent of the whole screen width
            float unitY = 2f * halfH / safe.height;
            float left = -halfW - safe.xMin * unitX;
            float right = halfW + (1f - safe.xMax) * unitX;
            float bottom = -halfH - safe.yMin * unitY;
            float top = halfH + (1f - safe.yMax) * unitY;
            return new Vector4(left, right, bottom, top);
        }

        private static Rect Sanitize(Rect r)
        {
            float xMin = Mathf.Clamp01(r.xMin), yMin = Mathf.Clamp01(r.yMin);
            float xMax = Mathf.Clamp(r.xMax, xMin + 0.05f, 1f), yMax = Mathf.Clamp(r.yMax, yMin + 0.05f, 1f);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool Fits(Vector3[] corners, Vector3 center, Quaternion rotation, float distance,
            float tanH, float tanV, float nearClip)
        {
            var position = center - rotation * Vector3.forward * distance;
            var inverse = Quaternion.Inverse(rotation);
            foreach (var c in corners)
            {
                var local = inverse * (c - position);
                if (local.z <= nearClip) return false;
                if (Mathf.Abs(local.x) > local.z * tanH) return false;
                if (Mathf.Abs(local.y) > local.z * tanV) return false;
            }
            return true;
        }

        private static Vector3[] GetCorners(Bounds b)
        {
            var min = b.min;
            var max = b.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
            };
        }
    }
}
