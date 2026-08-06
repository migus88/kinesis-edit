using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The other two token files: <c>Themes/Geometry.axaml</c> and the resources of
    /// <c>Themes/Typography.axaml</c>. Neither lives in a theme dictionary — a corner radius does
    /// not change when the OS flips — so the guard here is that they resolve in <b>both</b>
    /// variants anyway, which is what a view relying on them needs.
    /// </summary>
    public class ShapeAndTypeTokenTests
    {
        [AvaloniaTheory]
        // Radii: panels and cards 8, controls 5, keycaps 4, kbd chips 3, pills round.
        [InlineData("RadiusPanel", 8)]
        [InlineData("RadiusControl", 5)]
        [InlineData("RadiusKeycap", 4)]
        [InlineData("RadiusChip", 3)]
        [InlineData("RadiusPill", 999)]
        public void Radius_InEachVariant_IsTheHandoffValue(string key, double expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(new CornerRadius(expected), (CornerRadius)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        // The 4px grid: the six-step spacing scale, the fixed chrome heights, the rails.
        [InlineData("Space4", 4)]
        [InlineData("Space8", 8)]
        [InlineData("Space12", 12)]
        [InlineData("Space16", 16)]
        [InlineData("Space24", 24)]
        [InlineData("Space32", 32)]
        [InlineData("HeightToolbar", 46)]
        [InlineData("HeightTabBar", 38)]
        [InlineData("HeightAdvisoryStrip", 30)]
        [InlineData("WidthInspectorRail", 268)]
        [InlineData("WidthInspectorRailWide", 300)]
        [InlineData("GutterSplit", 26)]
        [InlineData("CardGridGap", 12)]
        [InlineData("WidthCardStatusRail", 2)]
        [InlineData("IconSize", 16)]
        [InlineData("IconStrokeThickness", 1.5)]
        [InlineData("IconSizeDialog", 24)]
        [InlineData("SpinnerSize", 14)]
        [InlineData("SpinnerStrokeThickness", 1.5)]
        [InlineData("HatchPitch", 4)]
        [InlineData("HatchAngle", 45)]
        public void Measure_InEachVariant_IsTheHandoffValue(string key, double expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(expected, (double)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        // Avalonia's two-value Thickness is horizontal,vertical — the reverse of the CSS shorthand
        // the handoff writes its "8,13" button padding in.
        [InlineData("PaddingCard", 14, 14)]
        [InlineData("PaddingInspectorSection", 12, 12)]
        [InlineData("PaddingButton", 13, 8)]
        [InlineData("PaddingTab", 13, 0)]
        public void Padding_InEachVariant_IsTheHandoffValue(string key, double horizontal, double vertical)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(new Thickness(horizontal, vertical), (Thickness)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        // The type scale of handoff.md, in the order it lists it.
        [InlineData("FontSizeDeviceHeadline", 24)]
        [InlineData("FontSizePageTitle", 18)]
        [InlineData("FontSizeCardTitle", 15)]
        [InlineData("FontSizeModalTitle", 14)]
        [InlineData("FontSizeToolbarDevice", 13)]
        [InlineData("FontSizeControl", 12)]
        [InlineData("FontSizeModalBody", 12)]
        [InlineData("FontSizeBody", 11)]
        [InlineData("FontSizeMeta", 11)]
        [InlineData("FontSizeMonoValue", 11)]
        [InlineData("FontSizeMonoValueSmall", 10)]
        [InlineData("FontSizeSectionLabel", 10)]
        [InlineData("FontSizeKeycapLabel", 9)]
        // 0.12em of tracking on the 10px uppercase section labels — the only tracking in the app,
        // and Avalonia's LetterSpacing is in pixels.
        [InlineData("LetterSpacingSectionLabel", 1.2)]
        public void TypeStep_InEachVariant_IsTheHandoffValue(string key, double expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(expected, (double)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        [InlineData("FontSans", "IBM Plex Sans")]
        [InlineData("FontMono", "IBM Plex Mono")]
        public void Family_IsTheEmbeddedIbmPlex(string key, string expected)
        {
            var family = Assert.IsType<FontFamily>(DesignTokens.Resolve(key, ThemeVariant.Dark));

            Assert.Equal(expected, family.Name);
            Assert.NotNull(family.Key);
            Assert.Contains("KinesisEdit/Assets/Fonts", family.Key!.ToString(), StringComparison.Ordinal);
        }

        [AvaloniaTheory]
        [InlineData("FontSans", "IBM Plex Sans")]
        [InlineData("FontMono", "IBM Plex Mono")]
        public void Family_LoadsRatherThanFallingBackToASystemFont(string key, string expected)
        {
            // The families are shipped in the assembly, so this must not depend on what the machine
            // has installed. If the embedded collection failed to register, the typeface would fall
            // back to the OS default and the whole app would render in the wrong face.
            var family = (FontFamily)DesignTokens.Resolve(key, ThemeVariant.Dark);

            Assert.True(
                FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface),
                $"No glyph typeface for {expected}.");
            Assert.Equal(expected, typeface.FamilyName);
        }

        [AvaloniaTheory]
        [InlineData(FontWeight.Normal)]
        [InlineData(FontWeight.Medium)]
        [InlineData(FontWeight.SemiBold)]
        public void PlexSans_CarriesEveryWeightTheScaleUses(FontWeight weight)
        {
            var family = (FontFamily)DesignTokens.Resolve("FontSans", ThemeVariant.Dark);

            Assert.True(
                FontManager.Current.TryGetGlyphTypeface(new Typeface(family, FontStyle.Normal, weight), out var typeface),
                $"IBM Plex Sans has no {weight} face.");
            Assert.Equal(weight, typeface.Weight);
        }
    }
}
