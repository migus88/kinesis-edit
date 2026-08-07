using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// One sampled instant of a lighting preview: the effect layer's lit keys, plus the opacity
    /// the board should draw the <i>paint</i> layer (the layer's per-key colours) at underneath.
    /// <para>
    /// The paint opacity is the design's rule made explicit: a mode that renders the painted
    /// colours directly gets <see cref="PaintOpacityDirect"/>; a mode that ignores them gets
    /// <see cref="PaintOpacityDimmed"/>, so the colours stay visible as a reminder that they are
    /// still on file; and the two modes that light nothing from the file — Disable and Pitch
    /// Black — get <see cref="PaintOpacityHidden"/>, because a dimmed paint layer under an off or
    /// deliberately black board would be a lie.
    /// </para>
    /// </summary>
    /// <param name="Cells">The lit keys by key code; a key absent from the map is unlit this frame.</param>
    /// <param name="PaintOpacity">The opacity of the paint layer beneath the effect, 0..1.</param>
    public sealed record LightingEffectFrame(IReadOnlyDictionary<int, LightingPreviewCell> Cells, double PaintOpacity)
    {
        /// <summary>The paint layer is the picture: the mode renders the painted colours directly.</summary>
        public const double PaintOpacityDirect = 1.0;

        /// <summary>The mode ignores the painted colours; they show faintly under the effect.</summary>
        public const double PaintOpacityDimmed = 0.4;

        /// <summary>No paint layer at all (Disable, Pitch Black).</summary>
        public const double PaintOpacityHidden = 0.0;

        /// <summary>A frame that lights nothing and shows no paint — the shape of "no layer yet".</summary>
        public static LightingEffectFrame Empty { get; } =
            new(ReadOnlyDictionary<int, LightingPreviewCell>.Empty, PaintOpacityHidden);
    }
}
