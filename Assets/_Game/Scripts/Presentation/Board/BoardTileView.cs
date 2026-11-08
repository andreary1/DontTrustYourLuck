using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    public enum TileHighlight
    {
        None,
        Lucky,
        Unlucky
    }

    /// <summary>
    /// Visual tile. Its <see cref="Index"/> mirrors the logical tile index; the anchor is where pieces stand.
    /// Pure view: it never decides game state.
    /// </summary>
    public sealed class BoardTileView : MonoBehaviour
    {
        public const float SurfaceHeight = 0.15f;

        [SerializeField] private int index;
        [SerializeField] private Transform anchor;
        [SerializeField] private Renderer surfaceRenderer;
        [SerializeField] private TextMesh label;

        public int Index => index;
        public Transform Anchor => anchor != null ? anchor : transform;
        public Renderer SurfaceRenderer => surfaceRenderer;
        public TileHighlight Highlight { get; private set; }

        public void Initialize(int tileIndex)
        {
            index = tileIndex;
            gameObject.name = $"Tile_{tileIndex:00}";
            if (label != null) label.text = tileIndex.ToString();
        }

        public void SetMaterial(Material material, TileHighlight highlight)
        {
            Highlight = highlight;
            if (surfaceRenderer != null && material != null) surfaceRenderer.sharedMaterial = material;
        }

        /// <summary>Builds the placeholder tile hierarchy from Unity primitives (used by the editor tool and as runtime fallback).</summary>
        public static BoardTileView CreateDefault(Transform parent = null)
        {
            var root = new GameObject("BoardTile");
            if (parent != null) root.transform.SetParent(parent, false);

            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(root.transform, false);
            surface.transform.localScale = new Vector3(1f, SurfaceHeight, 1f);
            surface.transform.localPosition = new Vector3(0f, SurfaceHeight * 0.5f, 0f);

            // Number sits on the near (camera-facing) half so pieces standing on the anchor do not hide it.
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, SurfaceHeight + 0.005f, -0.24f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = labelGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.065f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.12f, 0.1f, 0.08f);
            text.text = "0";
            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (font != null) labelRenderer.sharedMaterial = font.material;
            labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var anchorGo = new GameObject("Anchor");
            anchorGo.transform.SetParent(root.transform, false);
            anchorGo.transform.localPosition = new Vector3(0f, SurfaceHeight, 0.12f);

            var view = root.AddComponent<BoardTileView>();
            view.anchor = anchorGo.transform;
            view.surfaceRenderer = surface.GetComponent<Renderer>();
            view.label = text;
            return view;
        }
    }
}
