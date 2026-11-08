using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    /// <summary>
    /// Placeholder player token. Holds a player id and the logical tile it represents; the Transform is
    /// only a projection of that value (never the source of truth). Positioning is done by BoardView.
    /// </summary>
    public sealed class PlayerPieceView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private int playerId;
        [Min(0)] [SerializeField] private int logicalPosition;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Renderer bodyRenderer;

        private MaterialPropertyBlock _block;

        public int PlayerId => playerId;
        public int LogicalPosition => logicalPosition;
        public Color Color => color;

        public void Configure(int id, Color pieceColor)
        {
            playerId = id;
            color = pieceColor;
            gameObject.name = $"PlayerPiece_P{id}";
            ApplyColor();
        }

        public void SetLogicalPosition(int tileIndex)
        {
            logicalPosition = tileIndex;
        }

        private void Awake() => ApplyColor();

        private void OnValidate() => ApplyColor();

        private void ApplyColor()
        {
            if (bodyRenderer == null) return;
            // Per-instance tint without duplicating materials.
            _block ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(_block);
        }

        /// <summary>Builds the placeholder piece from a capsule primitive.</summary>
        public static PlayerPieceView CreateDefault(Material material = null, Transform parent = null)
        {
            var root = new GameObject("PlayerPiece");
            if (parent != null) root.transform.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.3f, 0.25f, 0.3f);
            body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var renderer = body.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;

            var view = root.AddComponent<PlayerPieceView>();
            view.bodyRenderer = renderer;
            view.ApplyColor();
            return view;
        }
    }
}
