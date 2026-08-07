namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Turns an effect's 0..1 intensity into a cell, in the two shapes the modes come in: over a
    /// background colour, and over the layer's per-key paint.
    /// </summary>
    internal static class LightingCellComposer
    {
        /// <summary>
        /// A cell for a mode that ignores the paint. With a non-black base colour every key is
        /// lit — the base <i>is</i> the board's background — and the effect blends over it; with a
        /// black base, black is "no colour" (specs/07-lighting.md §2.1), so the key is lit only
        /// where the effect reaches it and stays hatched everywhere else.
        /// </summary>
        public static LightingPreviewCell? Compose(LedColor effectColor, LedColor baseColor, double intensity)
        {
            var weight = LightingPreviewMath.Clamp01(intensity);

            if (!baseColor.IsBlack)
            {
                return new LightingPreviewCell(LightingPreviewMath.Lerp(baseColor, effectColor, weight), 1.0);
            }

            return weight < LightingPreviewCell.MinimumVisibleIntensity
                ? null
                : new LightingPreviewCell(effectColor, weight);
        }

        /// <summary>
        /// A cell for a mode that renders the paint directly: the key's own painted colour at the
        /// effect's intensity. A key with no paint on file has no colour to animate, so it stays
        /// unlit — hatched, not black.
        /// </summary>
        public static LightingPreviewCell? ComposePaint(LayerLightingState state, int keyCode, double intensity)
        {
            if (!state.KeyColors.TryGetValue(keyCode, out var color))
            {
                return null;
            }

            var weight = LightingPreviewMath.Clamp01(intensity);

            return weight < LightingPreviewCell.MinimumVisibleIntensity
                ? null
                : new LightingPreviewCell(color, weight);
        }
    }
}
