using DontTrustYourLuck.Presentation.Art;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>Display-only snapshot of a player for the HUD (built by the director from domain events).</summary>
    public struct PlayerDisplay
    {
        public int Id;
        public string Name;
        public Color Color;
        public bool IsBot;
        public int Position;
        public PlayerDisplayStatus Status;
        /// <summary>Shield active for the next move (from ShieldActivated/ShieldConsumed events).</summary>
        public bool Shielded;
        /// <summary>Character portrait (Sprint 4), or null.</summary>
        public Sprite Portrait;
    }

    public enum PlayerDisplayStatus
    {
        Active,
        Eliminated,
        Winner
    }

    /// <summary>HUD names for the <see cref="GamePalette"/> tokens (GDD §6.3: vintage paper, gold luck, red danger).</summary>
    public static class HudPalette
    {
        public static readonly Color Cream = GamePalette.AgedCream;
        public static readonly Color Card = GamePalette.PaperLight;
        public static readonly Color CardDisabled = GamePalette.CardDisabled;
        public static readonly Color CardWinner = GamePalette.CardWinner;
        public static readonly Color Ink = GamePalette.InkBrown;
        public static readonly Color InkFaded = GamePalette.InkFaded;
        public static readonly Color Lucky = GamePalette.SolarYellow;
        public static readonly Color LuckyDark = GamePalette.GoldInk;
        public static readonly Color Danger = GamePalette.TropicalRed;
        public static readonly Color Overlay = new Color(0.1f, 0.07f, 0.05f, 0.65f);
        public static readonly Color Green = GamePalette.LeafGreen;
        public static readonly Color Blue = GamePalette.ColonialBlue;
        public static readonly Color Pink = GamePalette.HibiscusPink;
        public static readonly Color Purple = GamePalette.MysticPurple;
        public static readonly Color DangerDark = GamePalette.DangerInk;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
