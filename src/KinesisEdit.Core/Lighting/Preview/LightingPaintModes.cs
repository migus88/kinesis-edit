using System.Collections.Frozen;

namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// The one place that says how a mode stands to the layer's per-key painted colours: whether
    /// it can be painted at all (<see cref="AcceptsPaint"/>) and, if it can, whether it consumes
    /// the paint or lets it show through (<see cref="RenderedDirectly"/>).
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
        /// Whether a paint gesture on a layer in <paramref name="mode"/> can reach the file at
        /// all — <b>false for Disable and Pitch Black, true for every other mode</b>.
        /// <para>
        /// It is the fact <see cref="LightingSectionSerializer"/> opens with: specs/07-lighting.md
        /// §2.2 gives those two modes no file body at all — choosing Disable writes an empty
        /// section (§6) and the reserved <c>[black]</c> token is never written — so nothing about
        /// such a layer is expressible, the per-key colour lines included. Everything else can
        /// carry paint, whether the effect consumes it (<see cref="RenderedDirectly"/>) or lets it
        /// show through underneath.
        /// </para>
        /// <para>
        /// <b>Named rather than derived</b>, unlike <see cref="RenderedDirectly"/>: no column of
        /// <see cref="LightingModeDefinition"/> separates this pair from the rest. Disable carries
        /// no token at all, while Pitch Black carries <c>[black]</c> and is excluded for being
        /// <see cref="LightingModeDefinition.IsReserved"/> — two different reasons, so any
        /// catalog-shaped derivation would be a third spelling of §2.2 rather than a reading of
        /// it. <b>Stating it once here is what the single place buys</b>: this is the only literal
        /// mention of the pair in the preview layer, and <see cref="PaintOpacityFor"/> and
        /// <see cref="LightingModeParameters.AcceptsPaint"/> both read it.
        /// </para>
        /// </summary>
        public static bool AcceptsPaint(LightingMode mode)
        {
            return mode is not (LightingMode.Disabled or LightingMode.PitchBlack);
        }

        /// <summary>
        /// The opacity the paint layer is drawn at under <paramref name="mode"/>'s effect —
        /// <see cref="LightingEffectFrame.PaintOpacityDirect"/>,
        /// <see cref="LightingEffectFrame.PaintOpacityDimmed"/> or
        /// <see cref="LightingEffectFrame.PaintOpacityHidden"/>.
        /// </summary>
        public static double PaintOpacityFor(LightingMode mode)
        {
            // Nothing to draw where nothing can be painted: the two modes that write no file body
            // light no LED either, so the cap stays hatched (§2.2).
            if (!AcceptsPaint(mode))
            {
                return LightingEffectFrame.PaintOpacityHidden;
            }

            return RendersPaintDirectly(mode)
                ? LightingEffectFrame.PaintOpacityDirect
                : LightingEffectFrame.PaintOpacityDimmed;
        }
    }
}
