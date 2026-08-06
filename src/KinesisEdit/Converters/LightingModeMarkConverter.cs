using System.Globalization;
using Avalonia.Data.Converters;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Turns a <see cref="LightingMode"/> into its mark from Themes/LightingMarks.axaml — the
    /// drawing beside a caption on the Lighting tab's mode rail, which "draws the motion rather
    /// than naming it twice" (docs/design/handoff.md).
    /// <para>
    /// <b>Why a converter parameter.</b> Thirteen of the sixteen marks are outlines and three —
    /// Freestyle's key grid, Monochrome's disc, Breathe's concentric rings — are solids, and an
    /// <c>Icon</c> takes its outline and its solid from two different properties. A single
    /// geometry binding could not say which. So the call site puts two <c>Icon</c>s in one cell,
    /// one asking for <see cref="StrokeParameter"/> and one for <see cref="FillParameter"/>, and
    /// exactly one of them resolves for any given mode; the other gets null and draws nothing.
    /// Painting a stroke-only mark with a fill would turn every ring into a disc, which is why
    /// the two are never both answered.
    /// </para>
    /// </summary>
    public sealed class LightingModeMarkConverter : IValueConverter
    {
        /// <summary>Prefix every mode mark's resource key carries in Themes/LightingMarks.axaml.</summary>
        public const string KeyPrefix = "LightingMark";

        /// <summary>Converter parameter asking for the mode's mark only if it is an outline.</summary>
        public const string StrokeParameter = "Stroke";

        /// <summary>Converter parameter asking for the mode's mark only if it is a solid.</summary>
        public const string FillParameter = "Fill";

        /// <summary>
        /// Whether <paramref name="mode"/>'s mark is drawn as a solid rather than as an outline.
        /// The three that are describe a lit surface rather than a movement, so they read as
        /// filled shapes at 16px where an outline of the same idea would not.
        /// </summary>
        public static bool IsFilled(LightingMode mode)
        {
            return mode is LightingMode.Freestyle or LightingMode.Monochrome or LightingMode.Breathe;
        }

        /// <summary>
        /// The resource key of <paramref name="mode"/>'s mark. Every member of the enum has one —
        /// the catalog is complete by construction, and a coverage test reflects over
        /// <see cref="LightingMode"/> to keep it that way — so this is the member name under the
        /// prefix rather than a per-member table with sixteen identical rows.
        /// </summary>
        public static string GetResourceKey(LightingMode mode)
        {
            return KeyPrefix + mode;
        }

        /// <summary>
        /// Returns the mark for a <see cref="LightingMode"/> when <paramref name="parameter"/>
        /// names the half it is drawn in, and null otherwise — including for the half it is not
        /// drawn in, and for a parameter that names neither half.
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not LightingMode mode || parameter is not string requestedHalf)
            {
                return null;
            }

            var wantsFill = string.Equals(requestedHalf, FillParameter, StringComparison.OrdinalIgnoreCase);
            var wantsStroke = string.Equals(requestedHalf, StrokeParameter, StringComparison.OrdinalIgnoreCase);

            if (!wantsFill && !wantsStroke)
            {
                return null;
            }

            return wantsFill == IsFilled(mode) ? GeometryResources.Find(GetResourceKey(mode)) : null;
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(LightingModeMarkConverter)} is a one-way converter.");
        }
    }
}
