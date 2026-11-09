using DontTrustYourLuck.Presentation.Characters;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Board
{
    public enum PieceVisualState
    {
        Normal,
        Eliminated,
        Winner
    }

    /// <summary>
    /// Player token: a paper-cutout animal standing on a small base ringed with the player color.
    /// Holds a player id and the logical tile it represents; the root Transform is only a projection of that
    /// value (never the source of truth) and is positioned by BoardView/MatchDirector. All procedural
    /// animation (idle sway, victory bounce, elimination fall, shield aura) runs on the child "Visual"
    /// transform only, so it can never move the piece off its anchor nor change logical state.
    /// </summary>
    public sealed class PlayerPieceView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] private int playerId;
        [Min(0)] [SerializeField] private int logicalPosition;
        [SerializeField] private Color color = Color.white;
        [Tooltip("Base ring tinted with the player color.")]
        [SerializeField] private Renderer bodyRenderer;
        [Tooltip("Quad showing the character cutout (optional).")]
        [SerializeField] private Renderer cardRenderer;
        [Tooltip("Animated child; the root never moves for animation.")]
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject shieldAura;
        [Tooltip("Backward lean of the cutout so it reads better from the elevated camera (partial billboard).")]
        [Range(0f, 40f)] [SerializeField] private float tilt = 18f;
        [SerializeField] private float cardHeight = 0.95f;

        [Header("Animation (presentation only)")]
        [SerializeField] private float idleSwayDegrees = 2.5f;
        [SerializeField] private float idleSpeed = 1.6f;
        [SerializeField] private float stateBlendSpeed = 6f;

        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _cardBlock;
        [Tooltip("Animal shown on the card (set by the MatchDirector; serialized so scene pieces keep it).")]
        [SerializeField] private CharacterDefinitionAsset character;
        private float _phase;
        private float _fall; // 0 = standing, 1 = lying (eliminated)
        private float _grow; // 0 = normal, 1 = winner

        public int PlayerId => playerId;
        public int LogicalPosition => logicalPosition;
        public Color Color => color;
        public CharacterDefinitionAsset Character => character;
        public bool IsShielded => shieldAura != null && shieldAura.activeSelf;
        public Transform Visual => visual != null ? visual : transform;
        public PieceVisualState VisualState { get; private set; }

        public void Configure(int id, Color pieceColor)
        {
            playerId = id;
            color = pieceColor;
            gameObject.name = $"PlayerPiece_P{id}";
            _phase = id * 1.37f;
            ApplyTint();
        }

        /// <summary>Shows the animal cutout (null keeps a blank paper card).</summary>
        public void SetCharacter(CharacterDefinitionAsset animal)
        {
            character = animal;
            if (cardRenderer == null) return;
            float height = character != null ? character.Height : cardHeight;
            float aspect = character != null ? character.Aspect : 0.75f;
            var card = cardRenderer.transform;
            card.localScale = new Vector3(height * aspect, height, 1f);
            card.localPosition = new Vector3(0f, BaseHeight + height * 0.5f, 0f);

            _cardBlock ??= new MaterialPropertyBlock();
            cardRenderer.GetPropertyBlock(_cardBlock);
            if (character != null && character.Cutout != null)
            {
                _cardBlock.SetTexture(BaseMapId, character.Cutout);
                _cardBlock.SetTexture(MainTexId, character.Cutout);
            }
            cardRenderer.SetPropertyBlock(_cardBlock);
            if (character != null) gameObject.name = $"PlayerPiece_P{playerId}_{character.Id}";
            ApplyTint();
        }

        public void SetLogicalPosition(int tileIndex)
        {
            logicalPosition = tileIndex;
        }

        /// <summary>Visual-only state: eliminated cutouts fall back into a grey card, winners grow and bounce.</summary>
        public void SetVisualState(PieceVisualState state)
        {
            VisualState = state;
            if (state != PieceVisualState.Normal) SetShielded(false);
            if (!Application.isPlaying)
            {
                // Edit Mode / tests: no Update, so snap to the final pose.
                _fall = state == PieceVisualState.Eliminated ? 1f : 0f;
                _grow = state == PieceVisualState.Winner ? 1f : 0f;
                ApplyPose(0f);
            }
            ApplyTint();
        }

        public void SetShielded(bool shielded)
        {
            if (shieldAura != null) shieldAura.SetActive(shielded);
        }

        private const float BaseHeight = 0.06f;

        private void Awake()
        {
            if (character != null) SetCharacter(character); // property blocks are not serialized
            ApplyTint();
            _phase = playerId * 1.37f;
        }

        private void OnValidate() => ApplyTint();

        private void Update()
        {
            float targetFall = VisualState == PieceVisualState.Eliminated ? 1f : 0f;
            float targetGrow = VisualState == PieceVisualState.Winner ? 1f : 0f;
            float k = 1f - Mathf.Exp(-stateBlendSpeed * Time.deltaTime);
            _fall = Mathf.Lerp(_fall, targetFall, k);
            _grow = Mathf.Lerp(_grow, targetGrow, k);
            ApplyPose(Time.time);
        }

        private void ApplyPose(float time)
        {
            if (visual == null) return;
            float t = time * idleSpeed + _phase;
            float sway = Mathf.Sin(t) * idleSwayDegrees * (1f - _fall);
            float bounce = _grow * Mathf.Abs(Mathf.Sin(time * 5f)) * 0.18f;
            // Lean back for readability; when eliminated the card tips over backwards and lies face up.
            float pitch = Mathf.Lerp(tilt, 90f, _fall);
            visual.localRotation = Quaternion.Euler(pitch, 0f, sway);
            visual.localPosition = new Vector3(0f, bounce, 0f);
            visual.localScale = Vector3.one * (1f + 0.3f * _grow);
        }

        private void ApplyTint()
        {
            var tint = VisualState == PieceVisualState.Eliminated ? Color.Lerp(color, Color.gray, 0.75f) : color;
            if (bodyRenderer != null)
            {
                // Per-instance tint without duplicating materials.
                _block ??= new MaterialPropertyBlock();
                bodyRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                _block.SetColor(ColorId, tint);
                bodyRenderer.SetPropertyBlock(_block);
            }
            if (cardRenderer != null)
            {
                var cardTint = VisualState == PieceVisualState.Eliminated ? new Color(0.62f, 0.6f, 0.58f, 1f) : Color.white;
                _cardBlock ??= new MaterialPropertyBlock();
                cardRenderer.GetPropertyBlock(_cardBlock);
                _cardBlock.SetColor(BaseColorId, cardTint);
                _cardBlock.SetColor(ColorId, cardTint);
                cardRenderer.SetPropertyBlock(_cardBlock);
            }
        }

        /// <summary>
        /// Builds the standee hierarchy from primitives (Sprint 4 builder and runtime fallback):
        /// Root (anchor) / Visual (animated) / Base, Ring, Card, ShieldAura.
        /// </summary>
        public static PlayerPieceView CreateDefault(Material ringMaterial = null, Transform parent = null,
            Material cardMaterial = null, Material baseMaterial = null, Material auraMaterial = null)
        {
            var root = new GameObject("PlayerPiece");
            if (parent != null) root.transform.SetParent(parent, false);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);

            // Wooden/cardboard stand (shared material, untinted).
            var stand = Primitive(PrimitiveType.Cylinder, "Base", visualGo.transform);
            stand.transform.localScale = new Vector3(0.36f, BaseHeight * 0.5f, 0.36f);
            stand.transform.localPosition = new Vector3(0f, BaseHeight * 0.5f, 0f);
            var standRenderer = stand.GetComponent<Renderer>();
            if (baseMaterial != null) standRenderer.sharedMaterial = baseMaterial;

            // Player-color ring around the base (identification never relies on color alone: animal + HUD too).
            var ring = Primitive(PrimitiveType.Cylinder, "Ring", visualGo.transform);
            ring.transform.localScale = new Vector3(0.44f, BaseHeight * 0.3f, 0.44f);
            ring.transform.localPosition = new Vector3(0f, BaseHeight * 0.3f, 0f);
            var ringRenderer = ring.GetComponent<Renderer>();
            if (ringMaterial != null) ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var card = Primitive(PrimitiveType.Quad, "Card", visualGo.transform);
            var cardRenderer = card.GetComponent<Renderer>();
            if (cardMaterial != null) cardRenderer.sharedMaterial = cardMaterial;
            cardRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var aura = Primitive(PrimitiveType.Sphere, "ShieldAura", visualGo.transform);
            aura.transform.localScale = new Vector3(0.75f, 1.25f, 0.75f);
            aura.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var auraRenderer = aura.GetComponent<Renderer>();
            if (auraMaterial != null) auraRenderer.sharedMaterial = auraMaterial;
            auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraRenderer.receiveShadows = false;
            aura.SetActive(false);

            var view = root.AddComponent<PlayerPieceView>();
            view.bodyRenderer = ringRenderer;
            view.cardRenderer = cardRenderer;
            view.visual = visualGo.transform;
            view.shieldAura = aura;
            view.SetCharacter(null);
            view.ApplyPose(0f);
            view.ApplyTint();
            return view;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
