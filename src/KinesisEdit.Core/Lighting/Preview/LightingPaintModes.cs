using System.Collections.Frozen;

namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// The one place that says which modes consume the layer's per-key painted colours.
    /// <para>
    /// <b>Derived, never listed.</b> A mode renders the paint directly exactly when its
    /// <see cref="LightingModeDefinition.HasPerKeyColors"/> is set — that flag <i>is</i> the
    /// question, because it says the mode's file body is per-key colour lines
    /// (specs/07-lighting.md §2.2, §4). Building the set from <see cref="LightingModeCatalog"/>
    /// rather than naming members keeps it from drifting from the file format. The answer is
    /// Freestyle, Breathe and Frozen Wave.
    /// </para>
    /// <para>
    /// <b>This deliberately contradicts design mockup 2f</b>, whose sentence "Solid, Reactive,
    /// Ripple and Starlight render the paint directly" is wrong about the hardware: those four
    /// modes write no per-key colour line at all — Monochrome writes one effect colour, and
    /// Reactive/Ripple/Starlight an effect colour over a base — so the firmware never reads a
    /// painted colour in them. It is wrong in the same way the mockup's direction lists are, and
    /// the rule for this screen is the same: the catalog wins parameters, the mockup wins labels.
    /// Their painted colours stay on file and show dimmed under the effect, which is exactly what
    /// <see cref="LightingEffectFrame.PaintOpacityDimmed"/> is for.
    /// </para>
    /// </summary>
    public static class LightingPaintModes
    {
        /// <summary>
        /// The modes whose preview draws the layer's per-key colours: every catalog row with
        /// <see cref="LightingModeDefinition.HasPerKeyColors"/>. Nothing else in the app may
        /// restate this set.
        /// </summary>
        public static IReadOnlySet<LightingMode> RenderedDirectly { get; } = LightingModeCatalog.All
            .Where(definition => definition.HasPerKeyColors)
            .Select(definition => definition.Mode)
            .ToFrozenSet();

        /// <summary>Whether <paramref name="mode"/>'s preview consumes the per-key painted colours.</summary>
        public static bool RendersPaintDirectly(LightingMode mode)
        {
            return RenderedDirectly.Contains(mode);
        }

        /// <summary>
        /// The opacity the paint layer is drawn at under <paramref name="mode"/>'s effect —
        /// <see cref="LightingEffectFrame.PaintOpacityDirect"/>,
        /// <see cref="LightingEffectFrame.PaintOpacityDimmed"/> or
        /// <see cref="LightingEffectFrame.PaintOpacityHidden"/>.
        /// </summary>
        public static double PaintOpacityFor(LightingMode mode)
        {
            if (mode is LightingMode.Disabled or LightingMode.PitchBlack)
            {
                return LightingEffectFrame.PaintOpacityHidden;
            }

            return RendersPaintDirectly(mode)
                ? LightingEffectFrame.PaintOpacityDirect
                : LightingEffectFrame.PaintOpacityDimmed;
        }
    }
}
