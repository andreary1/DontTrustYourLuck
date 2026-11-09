using UnityEngine;

namespace DontTrustYourLuck.Presentation.Art
{
    /// <summary>
    /// Single source of the colour tokens shared by the HUD (runtime) and the art generators (Editor).
    /// Base tokens are the GDD §6.3 palette; derived shades are the darker/lighter values the art and the HUD
    /// already used before centralisation (values unchanged). Change a colour here, not at the call sites.
    /// </summary>
    public static class GamePalette
    {
        // GDD §6.3 base palette.
        /// <summary>Amarelo solar: luck, highlights, Lucky tile.</summary>
        public static readonly Color32 SolarYellow = Rgb(0xF4B942);
        /// <summary>Vermelho tropical: bad luck, alerts, Unlucky tile.</summary>
        public static readonly Color32 TropicalRed = Rgb(0xD83A35);
        /// <summary>Verde folha: vegetation.</summary>
        public static readonly Color32 LeafGreen = Rgb(0x438B52);
        /// <summary>Azul colonial: architecture, water.</summary>
        public static readonly Color32 ColonialBlue = Rgb(0x287FA3);
        /// <summary>Rosa hibisco: flowers, ornaments.</summary>
        public static readonly Color32 HibiscusPink = Rgb(0xED638B);
        /// <summary>Roxo místico: magic events.</summary>
        public static readonly Color32 MysticPurple = Rgb(0x713E91);
        /// <summary>Creme envelhecido: cards, paper, die-cut margin of the animals.</summary>
        public static readonly Color32 AgedCream = Rgb(0xE9D7AC);
        /// <summary>Marrom tinta: outlines and text.</summary>
        public static readonly Color32 InkBrown = Rgb(0x493329);

        // Derived shades.
        public static readonly Color32 InkDeep = Rgb(0x2E211B);
        public static readonly Color32 InkFaded = Rgb(0x7D6A5C);
        public static readonly Color32 PaperLight = Rgb(0xF4E9CC);
        public static readonly Color32 PaperDark = Rgb(0xD4BF8E);
        public static readonly Color32 PaperWhite = Rgb(0xFBF6E9);
        /// <summary>Rosy paper of the Unlucky frame.</summary>
        public static readonly Color32 PaperRose = Rgb(0xF3DCCD);
        public static readonly Color32 CardDisabled = Rgb(0xBDB29A);
        public static readonly Color32 CardWinner = Rgb(0xF9DE8E);
        public static readonly Color32 SolarYellowDark = Rgb(0xB07A12);
        /// <summary>Gold dark enough for text on paper.</summary>
        public static readonly Color32 GoldInk = Rgb(0x8A5A00);
        public static readonly Color32 TropicalRedDark = Rgb(0x8E2320);
        /// <summary>Red dark enough for text on paper.</summary>
        public static readonly Color32 DangerInk = Rgb(0x7A1E1B);
        public static readonly Color32 LeafGreenDark = Rgb(0x2C5E37);
        public static readonly Color32 ColonialBlueDark = Rgb(0x1B5670);
        public static readonly Color32 Terracotta = Rgb(0xB5523B);

        /// <summary>Opaque colour from 0xRRGGBB.</summary>
        public static Color32 Rgb(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
