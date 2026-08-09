using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Picks the colour a key cap's caption is drawn in when the Lighting board has painted that
    /// cap's <b>face</b>, from the relative luminance of the colour actually on it: the dark label
    /// on a light fill, the light label on a dark one.
    /// <para>
    /// It exists because the fill is <b>device data</b>, not a theme role. A key can be lit
    /// <c>#FFFFFF</c> or <c>#050505</c>, and either one erases a caption drawn in
    /// <c>TextPrimaryBrush</c> — under both variants, because the fill is the same colour in both.
    /// So the choice cannot live in a theme dictionary, and it cannot live in a style either: it
    /// depends on a value that only arrives at runtime.
    /// </para>
    /// <para>
    /// <b>The two brushes are handed in from markup</b> (<see cref="DarkLabel"/> and
    /// <see cref="LightLabel"/>, set to <c>KeycapLitLabelDarkBrush</c>/<c>KeycapLitLabelLightBrush</c>
    /// where the converter is declared) rather than looked up by key from here. A key resolved in
    /// C# is invisible to the markup guards, and a converter is not a <c>StyledElement</c>, so it
    /// has no resource host to walk — the same constraint the hatch tile records. Both roles are
    /// variant-independent, which is what makes a <c>{StaticResource}</c> at the call site correct.
    /// </para>
    /// <para>
    /// <b>An unlit cap converts to <see cref="AvaloniaProperty.UnsetValue"/></b>, not to a third
    /// colour: the binding then produces no value at all and the caption falls back to the ordinary
    /// token its style names, which is what "the hatch means off, and off is an ordinary cap"
    /// requires. A cap under a fill too faint to matter is treated as unlit for the same reason —
    /// see <see cref="MinimumFillAlpha"/>.
    /// </para>
    /// <para>
    /// <b>The surface's own answer is the fifth value, and it is a value rather than a style</b>
    /// because a local binding on <c>Foreground</c> outranks every style setter: a caption gated by
    /// a class would keep whatever this converter last said. So the picture's <c>ShowsLighting</c>
    /// is handed in beside the four layer values, and a board that draws no lit face converts to
    /// <see cref="AvaloniaProperty.UnsetValue"/> whatever colour the key carries. The Keys tab shares
    /// its cap view models with the Lighting tab and its keys are painted at load, so without that
    /// gate a caption was coloured for a fill nothing draws — <c>#14181B</c> on the dark theme's
    /// raised cap is 1.15:1, and the legends went nearly invisible in both variants (issue #131).
    /// </para>
    /// </summary>
    public sealed class LitLabelBrushConverter : IMultiValueConverter
    {
        /// <summary>
        /// The sRGB relative luminance at which the label flips. It is not a taste threshold: at
        /// this value a fill's WCAG contrast against black and against white are <b>equal</b> —
        /// <c>(L + 0.05) / 0.05 == 1.05 / (L + 0.05)</c>, so <c>L = sqrt(1.05 * 0.05) - 0.05</c>.
        /// Above it the dark label wins, below it the light one, and the caption is therefore
        /// always drawn in whichever of the two has more contrast with the colour under it.
        /// </summary>
        public const double LabelFlipLuminance = 0.1791;

        /// <summary>
        /// How much of the cap's face has to be covered before the fill decides the caption. Under
        /// half, what the eye reads is mostly the hatch and the cap's own face — the cap is barely
        /// lit — so the caption keeps its ordinary token colour rather than being flipped by a
        /// wash it can hardly see. This is the case a paint layer at 40% under a mode that ignores
        /// paint lands in.
        /// </summary>
        private const double MinimumFillAlpha = 0.5;

        /// <summary>Relative-luminance coefficient of the red channel (WCAG 2.x).</summary>
        private const double RedWeight = 0.2126;

        /// <inheritdoc cref="RedWeight" />
        private const double GreenWeight = 0.7152;

        /// <inheritdoc cref="RedWeight" />
        private const double BlueWeight = 0.0722;

        /// <summary>Below this the sRGB transfer function is linear; above it, a 2.4 power.</summary>
        private const double LinearSegmentEnd = 0.03928;

        /// <summary>
        /// The relative luminance of <paramref name="color"/>, as WCAG 2.x defines it. Exposed so
        /// the tests can assert the flip against the same arithmetic the app uses rather than
        /// against a second copy of it.
        /// </summary>
        public static double RelativeLuminance(Color color)
        {
            return (RedWeight * ToLinear(color.R))
                + (GreenWeight * ToLinear(color.G))
                + (BlueWeight * ToLinear(color.B));
        }

        /// <summary>
        /// Composites the paint layer under the effect layer and returns the colour that ends up on
        /// the cap, or null when between them they cover less than <see cref="MinimumFillAlpha"/> of
        /// it. The order is the order the two are drawn in: paint on the hatch, effect over paint.
        /// </summary>
        public static Color? TryComposeFill(
            string? paintHex,
            double paintOpacity,
            string? effectHex,
            double effectIntensity)
        {
            var hasPaint = TryReadLayer(paintHex, paintOpacity, out var paint, out var paintAlpha);
            var hasEffect = TryReadLayer(effectHex, effectIntensity, out var effect, out var effectAlpha);

            if (!hasPaint)
            {
                paintAlpha = 0;
            }

            if (!hasEffect)
            {
                effectAlpha = 0;
            }

            var covered = effectAlpha + (paintAlpha * (1 - effectAlpha));

            if (covered < MinimumFillAlpha)
            {
                return null;
            }

            var underneath = paintAlpha * (1 - effectAlpha);

            return Color.FromRgb(
                Blend(effect.R, effectAlpha, paint.R, underneath, covered),
                Blend(effect.G, effectAlpha, paint.G, underneath, covered),
                Blend(effect.B, effectAlpha, paint.B, underneath, covered));
        }

        /// <summary>The caption's colour on a <b>light</b> fill: the app's light-theme primary.</summary>
        public IBrush? DarkLabel { get; set; }

        /// <summary>The caption's colour on a <b>dark</b> fill: the app's dark-theme primary.</summary>
        public IBrush? LightLabel { get; set; }

        /// <summary>
        /// Returns the label brush for the fill described by
        /// <c>[PaintColorHex, PaintOpacity, EffectColorHex, EffectIntensity, ShowsLighting]</c>, or
        /// <see cref="AvaloniaProperty.UnsetValue"/> when the cap is unlit — or when the surface
        /// draws no lit face at all, whatever colour the key carries.
        /// </summary>
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(values);

            // The surface first: a board that draws no fill can have no fill to read a label off.
            // Anything but a `true` there — a missing value, a binding that has not resolved yet —
            // means "not the lighting board", which is the side that leaves the style in charge.
            if (values.Count < 5 || values[4] is not true)
            {
                return AvaloniaProperty.UnsetValue;
            }

            var fill = TryComposeFill(
                values[0] as string,
                values[1] as double? ?? 0,
                values[2] as string,
                values[3] as double? ?? 0);

            if (fill is not { } color)
            {
                return AvaloniaProperty.UnsetValue;
            }

            return RelativeLuminance(color) > LabelFlipLuminance ? DarkLabel : LightLabel;
        }

        /// <summary>One layer's colour and how much of the cap it covers.</summary>
        private static bool TryReadLayer(string? hex, double opacity, out Color color, out double alpha)
        {
            color = default;
            alpha = 0;

            if (string.IsNullOrWhiteSpace(hex) || !Color.TryParse(hex.Trim(), out color))
            {
                return false;
            }

            alpha = Math.Clamp(opacity, 0, 1) * (color.A / 255d);

            return alpha > 0;
        }

        /// <summary>Source-over of two premultiplied contributions, normalised by the coverage.</summary>
        private static byte Blend(byte over, double overAlpha, byte under, double underAlpha, double covered)
        {
            return (byte)Math.Clamp(Math.Round(((over * overAlpha) + (under * underAlpha)) / covered), 0, 255);
        }

        /// <summary>One 0..255 channel, linearised out of sRGB.</summary>
        private static double ToLinear(byte channel)
        {
            var value = channel / 255d;

            return value <= LinearSegmentEnd ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }
}
