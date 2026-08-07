using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Softens an <see cref="LedColor"/> for the <b>preview</b> on the keyboard picture: the colour
    /// a lit cap's face is painted with, and nothing else
    /// ([#124](https://github.com/migus88/kinesis-edit/issues/124)).
    /// <para>
    /// <b>Why.</b> The led file stores full-gamut sRGB triples and the board draws ~95 caps of them
    /// side by side, so a freshly painted layer of <c>#FF0000</c>/<c>#00FF00</c> renders as a wall
    /// of the display's most saturated primaries — an effect that is fine on a keyboard's diffused
    /// LEDs and painful on a monitor. The preview is a <b>representative approximation, never a
    /// simulation</b> (docs/app/lighting.md, "Preview"), so it is free to render the same value
    /// more legibly: what it must get right is which colour is in play, not the photometry of the
    /// hardware.
    /// </para>
    /// <para>
    /// <b>What.</b> Two fixed steps, in this order: pull every channel toward the colour's own luma
    /// (<see cref="LumaPull"/>) to cut saturation, then scale all three
    /// (<see cref="BrightnessScale"/>) to take the glare off. Both are affine maps applied to all
    /// three channels with the <i>same</i> constants, so every channel <i>difference</i> is scaled
    /// by the same factor and the hue is preserved exactly — red stays red, only quieter. There is
    /// no preference and no per-mode variation: one board-wide look, tuned against rendered frames
    /// in both theme variants.
    /// </para>
    /// <para>
    /// <b>Black is a fixed point of both steps</b>, which is load-bearing rather than incidental:
    /// black is "no colour" (docs/app/lighting.md, invariant 5) and an unlit cap is *absence* — a
    /// null colour that never reaches here — while a cap Pitch Black lights black must still read
    /// black. A softening that lifted toward mid-grey would turn one into a dim-grey lit key and
    /// the other into a board of them, so nothing here adds a constant term.
    /// </para>
    /// <para>
    /// <b>Applied at exactly one seam</b> — <c>KeyboardKeyViewModel.ApplyPaint</c>/<c>ApplyEffect</c>,
    /// where a <see cref="LedColor"/> becomes the cap's <c>#RRGGBB</c> face string. Not in
    /// <c>KeyColorOverlay.ToHex</c> and not in a converter: both are shared with the rail's colour
    /// slots and the colour picker, which show the value that is <i>on file</i>, verbatim. And
    /// because the cap's own hex is what <c>LitLabelBrushConverter</c> reads, the lit caption's
    /// light/dark flip is decided by the face actually drawn rather than by a colour nothing
    /// paints. This is presentation only: no <see cref="LedColor"/> written to the lighting model
    /// passes through here, so a profile saves the same bytes it did before.
    /// </para>
    /// <para>
    /// It is deliberately <b>not idempotent</b> — softening a softened colour softens it again —
    /// which is the other reason for the single seam.
    /// </para>
    /// </summary>
    public static class LedPreviewTint
    {
        /// <summary>
        /// How far each channel is pulled toward the colour's luma, 0 = untouched, 1 = grey. At
        /// this value a full primary keeps its identity while the wall of saturation is gone:
        /// <c>#FF0000</c> becomes <c>#A41010</c>, which reads as a lit red rather than as a glowing
        /// filament. Chosen by capturing a board painted in every premixed swatch, in both theme
        /// variants, at four settings: at 0.20 the primaries still glare, and by 0.40 (with a
        /// heavier scale) blue has gone navy and yellow olive — a different colour rather than a
        /// quieter one, which is the failure mode on the other side.
        /// </summary>
        public const double LumaPull = 0.33;

        /// <summary>
        /// What the desaturated colour is then scaled by. Small on purpose, and smaller than the
        /// pull: the face has to stay clearly brighter than the hatch under it in the <b>light</b>
        /// theme too, where a cap darkened much further starts reading as a shadow rather than as
        /// a light.
        /// </summary>
        public const double BrightnessScale = 0.87;

        /// <summary>Relative-luminance coefficient of the red channel (WCAG 2.x / Rec. 709).</summary>
        private const double RedWeight = 0.2126;

        /// <inheritdoc cref="RedWeight" />
        private const double GreenWeight = 0.7152;

        /// <inheritdoc cref="RedWeight" />
        private const double BlueWeight = 0.0722;

        /// <summary>The largest value a channel can hold.</summary>
        private const double MaxChannel = 255;

        /// <summary>
        /// The colour <paramref name="color"/> is <i>previewed</i> as. Pure and allocation-free: it
        /// runs for every painted cap of a layer and again for every lit cap of every sampled frame
        /// (~30 times a second), so it does no more arithmetic than the two steps need.
        /// <para>
        /// The weights are applied to the sRGB-encoded channels rather than to linearised ones.
        /// That is not the WCAG luminance and is not meant to be: this is a viewing adjustment, and
        /// working in the encoded space is what keeps the pull's effect on a mid-tone the size the
        /// eye expects instead of collapsing every dark colour toward black.
        /// </para>
        /// </summary>
        public static LedColor Soften(LedColor color)
        {
            var luma = (RedWeight * color.Red) + (GreenWeight * color.Green) + (BlueWeight * color.Blue);

            return new LedColor(
                Adjust(color.Red, luma),
                Adjust(color.Green, luma),
                Adjust(color.Blue, luma));
        }

        /// <summary>One channel through both steps, rounded back into a byte.</summary>
        private static byte Adjust(byte channel, double luma)
        {
            var desaturated = channel + ((luma - channel) * LumaPull);
            var scaled = desaturated * BrightnessScale;

            return (byte)Math.Clamp(Math.Round(scaled, MidpointRounding.AwayFromZero), 0, MaxChannel);
        }
    }
}
