using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The values themselves, against docs/design/handoff.md. This is the regression guard for the
    /// whole redesign: the completeness tests only prove that a role exists, and a role that exists
    /// with the wrong hex repaints the app just as thoroughly as one that is missing.
    /// <para>
    /// Both halves of each pair are checked, so a <c>Brush</c> that was edited without its
    /// <c>Color</c> — or the reverse — fails here as well as in the pairing test.
    /// </para>
    /// </summary>
    public class TokenValueTests
    {
        [AvaloniaTheory]
        // Surfaces: the elevation ramp (handoff.md "Surfaces").
        [InlineData("SurfaceCanvas", "#0F1214", "#F4F5F6")]
        [InlineData("SurfacePanel", "#16191C", "#FFFFFF")]
        [InlineData("SurfaceInset", "#131619", "#F4F5F6")]
        [InlineData("SurfaceBar", "#1C2024", "#FAFBFB")]
        [InlineData("SurfaceRaised", "#23272C", "#EEF0F1")]
        [InlineData("SurfaceLine", "#2C3136", "#DDE1E3")]
        [InlineData("SurfaceLineHigh", "#3A4046", "#C9CFD2")]
        [InlineData("SurfaceBorderRaised", "#31363B", "#C9CFD2")]
        [InlineData("SurfaceThumbnail", "#262B30", "#EEF0F1")]
        [InlineData("SurfaceKeySelected", "#2A2F35", "#E4E7E9")]
        // Text: six steps. Light deliberately collapses two pairs of them.
        [InlineData("TextPrimary", "#E8EBED", "#14181B")]
        [InlineData("TextSecondary", "#C6CCD1", "#4A5158")]
        [InlineData("TextBodyMuted", "#98A1A8", "#4A5158")]
        [InlineData("TextMuted", "#7B858C", "#6C757C")]
        [InlineData("TextFaint", "#6C757C", "#8A9298")]
        [InlineData("TextDisabled", "#545C62", "#8A9298")]
        // Accent: the hue is variant-independent; only what sits on top of it flips.
        [InlineData("Accent", "#5B9DF9", "#5B9DF9")]
        [InlineData("AccentText", "#0F1214", "#FFFFFF")]
        [InlineData("AccentLinkHover", "#8FBCFB", "#8FBCFB")]
        [InlineData("AccentSelectionFill", "#245B9DF9", "#245B9DF9")]
        [InlineData("AccentSelectedRing", "#3E6FA8", "#3E6FA8")]
        [InlineData("AccentFocusHalo", "#475B9DF9", "#475B9DF9")]
        [InlineData("AccentKeyHalo", "#4D5B9DF9", "#4D5B9DF9")]
        // The selected key cap's opaque ring (#128). `AccentKeyRing` is the accent itself at full
        // opacity and `KeycapRingEdge` the near-black hairline inside it — the one member of the
        // accent family that is not a blue, and deliberately so: a single hue cannot contrast with
        // an arbitrary led colour, so the ring carries two and one of them always does.
        [InlineData("AccentKeyRing", "#5B9DF9", "#5B9DF9")]
        [InlineData("KeycapRingEdge", "#14181B", "#14181B")]
        // Status: amber is the only warning colour, so it is identical in both variants.
        [InlineData("StatusOk", "#4FBF8B", "#35A26D")]
        [InlineData("StatusOkText", "#7ED3AC", "#1D7A52")]
        [InlineData("StatusOkTint", "#1F4FBF8B", "#1F4FBF8B")]
        [InlineData("StatusOkTintBorder", "#614FBF8B", "#6135A26D")]
        [InlineData("StatusAdvisory", "#DDA94E", "#DDA94E")]
        [InlineData("StatusAdvisoryStrong", "#C08A21", "#C08A21")]
        [InlineData("StatusAdvisoryText", "#C9B48A", "#7A5A11")]
        [InlineData("StatusAdvisoryTint", "#1FDDA94E", "#1FDDA94E")]
        [InlineData("StatusAdvisoryTintBorder", "#61C08A21", "#61C08A21")]
        [InlineData("StatusError", "#E4685E", "#B0453C")]
        [InlineData("StatusErrorText", "#EE9C94", "#B0453C")]
        [InlineData("StatusErrorTint", "#1FE4685E", "#1FB0453C")]
        [InlineData("StatusErrorTintBorder", "#61E4685E", "#61B0453C")]
        [InlineData("StatusDemo", "#B58CF6", "#7A4FD0")]
        [InlineData("StatusDemoTint", "#1FB58CF6", "#1F7A4FD0")]
        [InlineData("StatusDemoTintBorder", "#61B58CF6", "#617A4FD0")]
        // Badge hues sit on a keycap rather than on the window, so they do not flip.
        [InlineData("BadgeMacro", "#C77DD8", "#C77DD8")]
        [InlineData("BadgeTapHold", "#35A26D", "#35A26D")]
        [InlineData("BadgeLightingPreview", "#57C4D8", "#57C4D8")]
        [InlineData("BadgeLed", "#B58CF6", "#B58CF6")]
        // The key struck in a macro step row: the macro orchid, exactly `BadgeMacro` on dark and
        // darkened for text on light, where #C77DD8 reads at 2.9:1. Deliberately not the accent
        // (two sanctioned meanings, neither of them this) and not the advisory ramp.
        [InlineData("MacroStepKey", "#C77DD8", "#8E45A0")]
        // Dimming a light window uses the same near-black at the same alpha.
        [InlineData("Scrim", "#9E080A0C", "#9E080A0C")]
        public void Role_InEachVariant_IsTheHandoffValue(string role, string dark, string light)
        {
            AssertRole(role, ThemeVariant.Dark, dark);
            AssertRole(role, ThemeVariant.Light, light);
        }

        [AvaloniaTheory]
        [InlineData("ShadowPopover", "0 8 24 0 #80000000")]
        [InlineData("ShadowModal", "0 24 60 0 #99000000")]
        // Focus is a spread-only halo, never an offset outline (handoff.md "Elevation").
        [InlineData("ShadowFocusHalo", "0 0 0 3 #475B9DF9")]
        [InlineData("ShadowKeyHalo", "0 0 0 2 #4D5B9DF9")]
        // ...and the ring the key cap actually wears since #128, which the handoff does not
        // specify because the handoff's own value is the line above. It is pinned here for the
        // same reason every other value is: it repaints the board just as thoroughly if it drifts.
        [InlineData("ShadowKeyHaloStrong", "0 0 0 3 #5B9DF9, 0 0 0 1 #14181B")]
        public void Shadow_InEachVariant_IsTheHandoffValue(string role, string expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(BoxShadows.Parse(expected), (BoxShadows)DesignTokens.Resolve(role, variant));
            }
        }

        [AvaloniaFact]
        public void TheKeyCapsRing_IsOpaqueAndTwoToned_AndTheHandoffsWashIsStillDeclaredBesideIt()
        {
            // THE DEVIATION, stated as the two facts that make it one (issue #128).
            //
            // The handoff draws a selected keycap as "filled face + 1px accent border + 2px halo
            // rgba(91,157,249,0.3)", and `ShadowKeyHalo` above is that value to the letter. It is
            // legible on the Layout board's neutral cap and invisible on the Lighting board's: a
            // lit cap's face is an arbitrary saturated led colour drawn to the inside of the
            // border, `.paintSelected` paints no face of its own, and 30 % of blue over #A41010 is
            // a shade of red. The user could not see which keys were selected.
            //
            // So the ring the cap wears is a third token: opaque, and two-toned, because a single
            // hue cannot promise contrast against device data — a blue ring disappears on a
            // blue-lit key however opaque it is. Both facts are asserted rather than described:
            // every colour in the ring is fully opaque, and the ring holds two DIFFERENT colours.
            foreach (var variant in DesignTokens.Variants)
            {
                var ring = (BoxShadows)DesignTokens.Resolve("ShadowKeyHaloStrong", variant);
                var wash = (BoxShadows)DesignTokens.Resolve("ShadowKeyHalo", variant);

                Assert.Equal(2, ring.Count);
                Assert.All(Shadows(ring), shadow => Assert.Equal(255, shadow.Color.A));
                Assert.NotEqual(ring[0].Color, ring[1].Color);

                // ...and it is INWARD-ONLY and blur-free, like every halo in this file: an offset
                // or a blur would put the band in the 4px gap the focus ring owns.
                Assert.All(
                    Shadows(ring),
                    shadow =>
                    {
                        Assert.Equal(0, shadow.OffsetX);
                        Assert.Equal(0, shadow.OffsetY);
                        Assert.Equal(0, shadow.Blur);
                    });

                // The handoff's own value is untouched and still declared: this is a token beside
                // it, not an edit to it, and a later hand comparing the two can see the deviation.
                Assert.Equal(BoxShadows.Parse("0 0 0 2 #4D5B9DF9"), wash);
            }
        }

        /// <summary>The shadows of <paramref name="shadows"/>, which indexes but does not enumerate.</summary>
        private static IReadOnlyList<BoxShadow> Shadows(BoxShadows shadows)
        {
            var list = new List<BoxShadow>();

            for (var index = 0; index < shadows.Count; index++)
            {
                list.Add(shadows[index]);
            }

            return list;
        }

        [AvaloniaFact]
        public void StatusHues_OnLight_DarkenWhileKeepingTheirHue()
        {
            // Law 2a of docs/design/mockups.md, as an ordering rather than a value: whatever the
            // exact hex, the light member of a status pair must be the darker of the two.
            foreach (var role in new[] { "StatusOk", "StatusError", "StatusDemo" })
            {
                var dark = DesignTokens.ResolveColor(role + DesignTokens.ColorSuffix, ThemeVariant.Dark);
                var light = DesignTokens.ResolveColor(role + DesignTokens.ColorSuffix, ThemeVariant.Light);

                Assert.True(
                    Brightness(light) < Brightness(dark),
                    $"{role} is not darker on light: {light} vs {dark}.");
            }
        }

        [AvaloniaFact]
        public void TheDemoPurpleAndTheLedPurple_AreOneColourUnderTwoRoles()
        {
            // Deliberate double-naming: neither call site needs to know about the other. They part
            // company on light, where the status hue darkens for contrast and the badge does not.
            Assert.Equal(
                DesignTokens.ResolveColor("StatusDemoColor", ThemeVariant.Dark),
                DesignTokens.ResolveColor("BadgeLedColor", ThemeVariant.Dark));
        }

        [AvaloniaFact]
        public void TheMacroStepKeyTint_IsTheMacroBadgesOwnHue_AndPartsFromItOnLight()
        {
            // The same deliberate double-naming as the pair above, and the same reason it is worth
            // pinning: purple already means "macro" in this app, so the token in a step row and the
            // dot on the cap say one thing in one hue. They part company on light because a badge
            // is a shape on a keycap and this is type on a near-white row — #C77DD8 reads at 2.9:1
            // there, under any threshold worth having.
            Assert.Equal(
                DesignTokens.ResolveColor("BadgeMacroColor", ThemeVariant.Dark),
                DesignTokens.ResolveColor("MacroStepKeyColor", ThemeVariant.Dark));

            Assert.NotEqual(
                DesignTokens.ResolveColor("BadgeMacroColor", ThemeVariant.Light),
                DesignTokens.ResolveColor("MacroStepKeyColor", ThemeVariant.Light));
        }

        [AvaloniaTheory]
        // The surfaces a macro step row can sit on, per variant.
        [InlineData("SurfacePanel")]
        [InlineData("SurfaceInset")]
        [InlineData("SurfaceRaised")]
        public void TheMacroStepKeyTint_StaysLegibleOnEveryRowSurface(string surface)
        {
            // The design law the hue was picked against: "slightly different", not a shout, and
            // legible in both variants. So it must clear 4.5:1 on the faces a row is drawn on — and
            // stay under the primary text step, or it would be the loudest thing in the row rather
            // than a tint on it.
            foreach (var variant in DesignTokens.Variants)
            {
                var ground = DesignTokens.ResolveColor(surface + DesignTokens.ColorSuffix, variant);
                var tint = Contrast(DesignTokens.ResolveColor("MacroStepKeyColor", variant), ground);
                var primary = Contrast(DesignTokens.ResolveColor("TextPrimaryColor", variant), ground);

                Assert.True(tint >= 4.5, $"MacroStepKey on {surface} ({variant}) is {tint:F2}:1.");
                Assert.True(tint < primary, $"MacroStepKey on {surface} ({variant}) is louder than the primary text step.");
            }
        }

        /// <summary>WCAG relative-luminance contrast ratio between two opaque colours.</summary>
        private static double Contrast(Color first, Color second)
        {
            var a = Luminance(first);
            var b = Luminance(second);

            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        private static double Luminance(Color color)
        {
            return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
        }

        private static double Channel(byte value)
        {
            var channel = value / 255.0;

            return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        private static void AssertRole(string role, ThemeVariant variant, string expected)
        {
            var color = Color.Parse(expected);

            Assert.Equal(color, DesignTokens.ResolveColor(role + DesignTokens.ColorSuffix, variant));
            Assert.Equal(color, DesignTokens.ResolveBrushColor(role + DesignTokens.BrushSuffix, variant));
        }

        private static double Brightness(Color color)
        {
            return (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        }
    }
}
