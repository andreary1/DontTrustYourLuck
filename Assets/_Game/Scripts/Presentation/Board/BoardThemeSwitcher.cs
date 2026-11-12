using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontTrustYourLuck.Presentation.Board
{
    /// <summary>
    /// One city board: its diorama root, circuit outline and lighting. Pure presentation: the rules are the same on
    /// every board, so switching never touches match state.
    /// </summary>
    [Serializable]
    public sealed class BoardTheme
    {
        public string id = "oaxaca";
        public string displayName = "Oaxaca";
        public GameObject environment;
        public CircuitShape shape = CircuitShape.Plaza;
        [Tooltip("Camera pitch in degrees for this board (lower shows more horizon).")]
        [Range(20f, 70f)] public float cameraPitch = 38f;

        [Header("Lighting")]
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1.3f;
        public Vector3 sunEuler = new Vector3(38f, -38f, 0f);
        public Color ambientSky = Color.gray;
        public Color ambientEquator = Color.gray;
        public Color ambientGround = Color.gray;
        public Color fogColor = Color.white;
        [Min(0f)] public float fogStart = 26f;
        [Min(0f)] public float fogEnd = 60f;
    }

    /// <summary>
    /// Shows one board theme at a time: enables its environment (the others are disabled, so they cost no draw calls),
    /// sets the circuit outline, the sun, ambient light, fog and camera background, then reframes the camera.
    /// </summary>
    public sealed class BoardThemeSwitcher : MonoBehaviour
    {
        [SerializeField] private BoardView board;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private Light sun;
        [SerializeField] private List<BoardTheme> themes = new List<BoardTheme>();
        [SerializeField] private string defaultTheme = "oaxaca";

        private BoardTheme _current;

        /// <summary>Raised after a different theme was applied.</summary>
        public event Action<BoardTheme> ThemeChanged;

        public BoardTheme Current => _current;
        public string CurrentId => _current != null ? _current.id : null;
        public IReadOnlyList<BoardTheme> Themes => themes;
        public string DefaultTheme => defaultTheme;

        public void Configure(BoardView boardView, BoardCameraRig rig, Light sunLight, IEnumerable<BoardTheme> list, string defaultId)
        {
            board = boardView; cameraRig = rig; sun = sunLight; defaultTheme = defaultId;
            themes = new List<BoardTheme>(list);
        }

        public BoardTheme Find(string id)
        {
            foreach (var theme in themes)
                if (theme != null && string.Equals(theme.id, id, StringComparison.OrdinalIgnoreCase)) return theme;
            return null;
        }

        private void Awake()
        {
            if (_current == null) Apply(defaultTheme);
        }

        /// <summary>Applies the theme with this id. Returns false (and keeps the current one) when it does not exist.</summary>
        /// <summary>Re-applies the current theme (or the default one), e.g. after another component reconfigured the camera.</summary>
        public void Reapply() => Apply(_current != null ? _current.id : defaultTheme);

        public bool Apply(string id)
        {
            var theme = Find(id);
            if (theme == null)
            {
                Debug.LogWarning($"[BoardThemeSwitcher] Unknown board theme '{id}'.", this);
                return false;
            }
            bool changed = theme != _current;
            _current = theme;

            foreach (var other in themes)
                if (other != null && other.environment != null && other != theme) other.environment.SetActive(false);
            if (theme.environment != null) theme.environment.SetActive(true);

            if (sun != null)
            {
                sun.color = theme.sunColor;
                sun.intensity = theme.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(theme.sunEuler);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.ambientSky;
            RenderSettings.ambientEquatorColor = theme.ambientEquator;
            RenderSettings.ambientGroundColor = theme.ambientGround;
            RenderSettings.fogColor = theme.fogColor;
            RenderSettings.fogStartDistance = theme.fogStart;
            RenderSettings.fogEndDistance = theme.fogEnd;

            if (board != null) board.SetCircuitShape(theme.shape);
            if (cameraRig != null)
            {
                if (board != null) cameraRig.Configure(board, theme.cameraPitch);
                cameraRig.Camera.backgroundColor = theme.fogColor;
                if (cameraRig.isActiveAndEnabled) cameraRig.Frame();
            }
            if (changed) ThemeChanged?.Invoke(theme);
            return true;
        }
    }
}
