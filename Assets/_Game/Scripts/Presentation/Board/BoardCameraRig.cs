using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    /// <summary>
    /// Fixed elevated camera that frames the whole board. Re-frames only when the screen size changes
    /// (browser window resize), so it is cheap and needs no input.
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

        private Camera _camera;
        private int _lastWidth;
        private int _lastHeight;

        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public void Configure(BoardView target, float pitchDegrees)
        {
            board = target;
            pitch = pitchDegrees;
        }

        private void Start() => Frame();

        private void LateUpdate()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight) Frame();
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
            float aspect = Mathf.Max(0.01f, cam.aspect);
            float limit = 1f - 2f * margin;

            if (cam.orthographic)
            {
                var inverse = Quaternion.Inverse(rotation);
                float halfHeight = 0f;
                foreach (var c in corners)
                {
                    var local = inverse * (c - bounds.center);
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y), Mathf.Abs(local.x) / aspect);
                }
                cam.orthographicSize = halfHeight / Mathf.Max(0.1f, limit);
                transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * (bounds.extents.magnitude + 10f), rotation);
                return;
            }

            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;

            // Binary search for the closest distance where every corner fits in the viewport.
            float near = 0.5f, far = 500f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (near + far) * 0.5f;
                if (Fits(corners, bounds.center, rotation, mid, tanH * limit, tanV * limit, cam.nearClipPlane)) far = mid;
                else near = mid;
            }

            transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * far, rotation);
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
