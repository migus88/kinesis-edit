using Avalonia.Headless.XUnit;
using Avalonia.Media;
using KinesisEdit.Core.Lighting;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The preview softening (issue #124). The claims are about the <b>shape</b> of the transform —
    /// the hue survives, the saturation and the brightness come down, black is a fixed point — and
    /// deliberately not about the two constants, which are a look tuned against rendered frames and
    /// are expected to move without any of these tests moving with them.
    /// <para>
    /// Hue and saturation are read through Avalonia's own <see cref="HsvColor"/> rather than out of
    /// a second copy of the arithmetic: a helper here that re-derived them from the channels could
    /// agree with a wrong implementation.
    /// </para>
    /// </summary>
    public class LedPreviewTintTests
    {
        /// <summary>
        /// How far a hue may move, in degrees. The maths preserves it exactly; the byte rounding at
        /// the end does not, and on a saturated colour that is worth well under a degree.
        /// </summary>
        private const double HueTolerance = 1.0;

        [AvaloniaTheory]
        [InlineData(255, 0, 0)]
        [InlineData(0, 255, 0)]
        [InlineData(0, 0, 255)]
        [InlineData(255, 255, 0)]
        [InlineData(0, 255, 255)]
        [InlineData(255, 0, 255)]
        [InlineData(255, 128, 0)]
        [InlineData(0, 128, 255)]
        [InlineData(87, 196, 216)]
        public void Soften_KeepsTheHue(byte red, byte green, byte blue)
        {
            // Both steps are affine maps applied to all three channels with the SAME constants, so
            // every channel difference is scaled by the same factor and the hue comes through
            // untouched. This is the whole reason for a luma anchor rather than, say, blending
            // toward a fixed grey: a user who painted a key red must still see red.
            var original = new LedColor(red, green, blue);
            var softened = LedPreviewTint.Soften(original);

            Assert.Equal(ToHsv(original).H, ToHsv(softened).H, HueTolerance);
        }

        [AvaloniaTheory]
        [InlineData(255, 0, 0)]
        [InlineData(0, 255, 0)]
        [InlineData(0, 0, 255)]
        [InlineData(255, 255, 0)]
        [InlineData(0, 255, 255)]
        [InlineData(255, 0, 255)]
        [InlineData(87, 196, 216)]
        public void Soften_CutsTheSaturationAndTheBrightness(byte red, byte green, byte blue)
        {
            // The defect the issue reports, in two numbers: a full-gamut triple drawn on ~95 caps
            // at once is a wall of the display's most saturated primaries. Both have to come down —
            // desaturating alone leaves a pastel that is still as bright as the screen can go.
            var original = ToHsv(new LedColor(red, green, blue));
            var softened = ToHsv(LedPreviewTint.Soften(new LedColor(red, green, blue)));

            Assert.True(softened.S < original.S, $"The saturation did not come down ({softened.S}).");
            Assert.True(softened.V < original.V, $"The brightness did not come down ({softened.V}).");
        }

        [AvaloniaTheory]
        [InlineData(255, 0, 0)]
        [InlineData(0, 255, 0)]
        [InlineData(0, 0, 255)]
        [InlineData(255, 255, 255)]
        [InlineData(87, 196, 216)]
        public void Soften_KeepsTheColourWellClearOfGrey(byte red, byte green, byte blue)
        {
            // The other side of the same coin, and the reason the pull is a third rather than a
            // half: the preview still has to say WHICH colour is on the key. A cap softened past
            // recognition would be a worse answer than a cap that glares.
            var original = ToHsv(new LedColor(red, green, blue));
            var softened = ToHsv(LedPreviewTint.Soften(new LedColor(red, green, blue)));

            Assert.True(softened.S >= original.S / 2, $"The colour washed out to {softened.S}.");
            Assert.True(softened.V >= original.V / 2, $"The colour went dark at {softened.V}.");
        }

        [AvaloniaFact]
        public void Soften_LeavesBlackBlack()
        {
            // BLACK IS "NO COLOUR" (docs/app/lighting.md, invariant 5) and an unlit cap is drawn as
            // the hatch. Neither may become a dim grey: a softening with a constant term would lift
            // every black key off the hatch and turn Pitch Black — which lights every key black at
            // full intensity, and must read differently from off — into a board of grey ones.
            Assert.Equal(LedColor.Black, LedPreviewTint.Soften(LedColor.Black));
        }

        [AvaloniaFact]
        public void Soften_LeavesAGreyGrey()
        {
            // A colour that is already its own luma has no saturation to cut, so only the brightness
            // step can touch it — and it must not acquire a cast on the way through.
            var softened = LedPreviewTint.Soften(new LedColor(128, 128, 128));

            Assert.Equal(softened.Red, softened.Green);
            Assert.Equal(softened.Green, softened.Blue);
            Assert.True(softened.Red < 128, "The brightness step did nothing.");
        }

        [AvaloniaFact]
        public void Soften_OfWhite_StaysInRange()
        {
            // The brightest input there is, which is where a clamp would be needed if the arithmetic
            // ever overshot. It must also stay neutral: white is the one swatch whose "hue" is
            // nothing at all.
            var softened = LedPreviewTint.Soften(new LedColor(255, 255, 255));

            Assert.Equal(softened.Red, softened.Green);
            Assert.Equal(softened.Green, softened.Blue);
            Assert.True(softened.Red < 255, "White came through untouched.");
        }

        [AvaloniaFact]
        public void Soften_IsNotIdempotent_WhichIsWhyItHasExactlyOneCallSite()
        {
            // Not a defect — a constraint, recorded so it cannot be forgotten. Softening a softened
            // colour softens it again, so the transform belongs at ONE seam
            // (KeyboardKeyViewModel.ApplyPaint/ApplyEffect). Put it in KeyColorOverlay.ToHex or in a
            // converter as well and a cap would fade a little further every time it was formatted.
            var once = LedPreviewTint.Soften(new LedColor(255, 0, 0));
            var twice = LedPreviewTint.Soften(once);

            Assert.NotEqual(once, twice);
            Assert.True(ToHsv(twice).V < ToHsv(once).V, "The second pass did nothing.");
        }

        [AvaloniaFact]
        public void Soften_MovesEveryPremixedSwatch_AndNoneOfThemOutOfItsFamily()
        {
            // Swept over the picker's own ten swatches rather than a fixture list of this test's
            // making, so a swatch added there is covered here the day it lands. White and black are
            // the two ends the sweep has to tolerate: black cannot move at all, and white has no hue.
            foreach (var swatch in ColorPickerViewModel.PremixedColors)
            {
                var softened = LedPreviewTint.Soften(swatch.Color);

                if (swatch.Color.IsBlack)
                {
                    Assert.Equal(swatch.Color, softened);

                    continue;
                }

                Assert.NotEqual(swatch.Color, softened);
                Assert.True(
                    ToHsv(softened).V < ToHsv(swatch.Color).V,
                    $"{swatch.Name} did not come down at all.");
            }
        }

        [AvaloniaFact]
        public void TheSharedHexFormatter_IsNotSoftened()
        {
            // WHERE THE SOFTENING MUST NOT BE. KeyColorOverlay.ToHex is the app's one LedColor →
            // "#RRGGBB" formatter and it is shared with the surfaces that show what is ON FILE —
            // the rail's colour slots and the colour picker. Softening it, or the hex-to-brush
            // converter under it, would have been the shorter change and would have made the rail
            // lie about the value the led file holds.
            Assert.Equal("#FF0000", KeyColorOverlay.ToHex(new LedColor(255, 0, 0)));
            Assert.NotEqual(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(255, 0, 0))),
                KeyColorOverlay.ToHex(new LedColor(255, 0, 0)));
        }

        [AvaloniaFact]
        public void ARailColourSlot_ShowsTheStoredValueVerbatim()
        {
            // The same claim read off the surface that makes it: a swatch is the number in the file,
            // and it is drawn beside that number as text. A softened swatch would disagree with the
            // caps AND with its own caption, which is a worse screen than a vivid one.
            var slot = LightingColorSlotViewModel.CreateEffectColor();

            Assert.Equal(KeyColorOverlay.ToHex(LedColor.DefaultEffectColor), slot.ColorHex);
            Assert.NotEqual(KeyColorOverlay.ToHex(LedPreviewTint.Soften(LedColor.DefaultEffectColor)), slot.ColorHex);
        }

        private static HsvColor ToHsv(LedColor color)
        {
            return Color.FromRgb(color.Red, color.Green, color.Blue).ToHsv();
        }
    }
}
