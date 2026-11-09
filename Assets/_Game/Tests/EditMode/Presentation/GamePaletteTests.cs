using System.Collections.Generic;
using DontTrustYourLuck.Presentation.Art;
using DontTrustYourLuck.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace DontTrustYourLuck.Tests.Presentation
{
    /// <summary>
    /// Graphics F1: the palette tokens keep the GDD §6.3 values, the HUD colours resolve to them, and the HUD text
    /// pairs keep WCAG 2.x contrast (4.5:1 body text, 3:1 large text). Expected hex values are literals from the GDD.
    /// Known gaps tracked for the HUD phase (not asserted): InkFaded on Card and Danger destination text on paper.
    /// </summary>
    public class GamePaletteTests
    {
        [Test]
        public void BaseTokens_KeepTheGddValues()
        {
            AssertRgb(0xF4B942, GamePalette.SolarYellow, "Amarelo solar");
            AssertRgb(0xD83A35, GamePalette.TropicalRed, "Vermelho tropical");
            AssertRgb(0x438B52, GamePalette.LeafGreen, "Verde folha");
            AssertRgb(0x287FA3, GamePalette.ColonialBlue, "Azul colonial");
            AssertRgb(0xED638B, GamePalette.HibiscusPink, "Rosa hibisco");
            AssertRgb(0x713E91, GamePalette.MysticPurple, "Roxo místico");
            AssertRgb(0xE9D7AC, GamePalette.AgedCream, "Creme envelhecido");
            AssertRgb(0x493329, GamePalette.InkBrown, "Marrom tinta");
        }

        [Test]
        public void HudColours_UseTheGddBaseColours()
        {
            AssertRgb(0xF4B942, HudPalette.Lucky, "HUD Lucky");
            AssertRgb(0xD83A35, HudPalette.Danger, "HUD Danger");
            AssertRgb(0x438B52, HudPalette.Green, "HUD Green");
            AssertRgb(0x287FA3, HudPalette.Blue, "HUD Blue");
            AssertRgb(0xED638B, HudPalette.Pink, "HUD Pink");
            AssertRgb(0x713E91, HudPalette.Purple, "HUD Purple");
            AssertRgb(0xE9D7AC, HudPalette.Cream, "HUD Cream");
            AssertRgb(0x493329, HudPalette.Ink, "HUD Ink");
        }

        [Test]
        public void HudBodyTextPairs_MeetWcagAA()
        {
            var failures = new List<string>();
            Check("Ink on Cream (panels)", HudPalette.Ink, HudPalette.Cream, 4.5, failures);
            Check("Ink on Card (player cards)", HudPalette.Ink, HudPalette.Card, 4.5, failures);
            Check("Ink on CardWinner", HudPalette.Ink, HudPalette.CardWinner, 4.5, failures);
            Check("LuckyDark on Card (lucky number)", HudPalette.LuckyDark, HudPalette.Card, 4.5, failures);
            Check("DangerDark on PaperRose (unlucky number)", HudPalette.DangerDark, GamePalette.PaperRose, 4.5, failures);
            Check("TropicalRedDark on PaperLight (titles)", GamePalette.TropicalRedDark, GamePalette.PaperLight, 4.5, failures);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void HudLargeText_PrimaryButtonLabel_MeetsWcagLargeTextMinimum()
        {
            var failures = new List<string>();
            Check("PaperWhite on Danger (30 px bold button)", GamePalette.PaperWhite, HudPalette.Danger, 3.0, failures);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ContrastHelper_MatchesWcagReferenceValues()
        {
            Assert.AreEqual(21.0, Contrast(Color.black, Color.white), 1e-3, "black on white");
            Assert.AreEqual(4.54, Contrast(Rgb(0x767676), Color.white), 0.01, "#767676 on white");
            Assert.AreEqual(4.48, Contrast(Rgb(0x777777), Color.white), 0.01, "#777777 on white");
            Assert.AreEqual(Contrast(Color.white, Rgb(0x777777)), Contrast(Rgb(0x777777), Color.white), 1e-9, "symmetric");
        }

        // ------------------------------------------------------------------ helpers (test-only)

        private static void AssertRgb(int expectedHex, Color32 actual, string name)
        {
            int rgb = (actual.r << 16) | (actual.g << 8) | actual.b;
            Assert.AreEqual(expectedHex.ToString("X6"), rgb.ToString("X6"), name);
            Assert.AreEqual(255, actual.a, name + " alpha");
        }

        private static void AssertRgb(int expectedHex, Color actual, string name) => AssertRgb(expectedHex, (Color32)actual, name);

        private static Color Rgb(int hex) =>
            new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        private static void Check(string name, Color text, Color background, double minimum, List<string> failures)
        {
            double ratio = Contrast(text, background);
            if (ratio < minimum) failures.Add($"{name}: {ratio:0.00}:1 < {minimum:0.0}:1");
        }

        /// <summary>WCAG 2.x contrast ratio from sRGB colours (relative luminance, (L1 + 0.05) / (L2 + 0.05)).</summary>
        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
        }

        private static double Luminance(Color c) => 0.2126 * Linear(c.r) + 0.7152 * Linear(c.g) + 0.0722 * Linear(c.b);

        private static double Linear(float channel)
        {
            double v = channel;
            return v <= 0.04045 ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
