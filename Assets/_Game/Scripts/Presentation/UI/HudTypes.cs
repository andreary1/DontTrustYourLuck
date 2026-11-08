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
    }

    public enum PlayerDisplayStatus
    {
        Active,
        Eliminated,
        Winner
    }

    /// <summary>Placeholder palette from GDD §6.3 (vintage paper, gold luck, red danger).</summary>
    public static class HudPalette
    {
        public static readonly Color Cream = Hex(0xE9D7AC);
        public static readonly Color Card = Hex(0xF4E9CC);
        public static readonly Color CardDisabled = Hex(0xBDB29A);
        public static readonly Color CardWinner = Hex(0xF9DE8E);
        public static readonly Color Ink = Hex(0x493329);
        public static readonly Color Lucky = Hex(0xF4B942);
        public static readonly Color LuckyDark = Hex(0x8A5A00);
        public static readonly Color Danger = Hex(0xD83A35);
        public static readonly Color Overlay = new Color(0.1f, 0.07f, 0.05f, 0.65f);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
