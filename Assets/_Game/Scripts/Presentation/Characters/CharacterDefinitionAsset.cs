using UnityEngine;

namespace DontTrustYourLuck.Presentation.Characters
{
    /// <summary>
    /// Designer-facing data of one playable animal (presentation only: no abilities, no rules).
    /// The cutout is the paper-standee texture shown on the board; the portrait is used by the HUD.
    /// New animals (arara, galo, serpente...) are added by creating another asset; no code change needed.
    /// </summary>
    [CreateAssetMenu(fileName = "Character", menuName = "Don't Trust Your Luck/Character Definition")]
    public sealed class CharacterDefinitionAsset : ScriptableObject
    {
        [Tooltip("Stable id (e.g. onca). Used for asset names and saved data, never shown to players.")]
        [SerializeField] private string id = "character";
        [SerializeField] private string displayName = "Character";
        [Tooltip("Paper cutout texture (RGBA, transparent background, point filtered).")]
        [SerializeField] private Texture2D cutout;
        [SerializeField] private Sprite portrait;
        [Tooltip("Default seat color when this animal is picked (players are never identified by color alone).")]
        [SerializeField] private Color accent = Color.white;
        [Tooltip("Standee height in world units (a board tile is 1 unit wide).")]
        [Min(0.1f)] [SerializeField] private float height = 0.95f;

        public string Id => id;
        public string DisplayName => displayName;
        public Texture2D Cutout => cutout;
        public Sprite Portrait => portrait;
        public Color Accent => accent;
        public float Height => height;

        /// <summary>Width / height of the cutout (1 when no texture is set).</summary>
        public float Aspect => cutout != null && cutout.height > 0 ? cutout.width / (float)cutout.height : 1f;

        /// <summary>Editor setup (Sprint 4 builder).</summary>
        public void Configure(string characterId, string name, Texture2D cutoutTexture, Sprite portraitSprite,
            Color accentColor, float standeeHeight)
        {
            id = characterId; displayName = name; cutout = cutoutTexture; portrait = portraitSprite;
            accent = accentColor; height = standeeHeight;
        }
    }
}
