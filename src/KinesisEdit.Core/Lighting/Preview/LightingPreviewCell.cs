namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// What one key shows on the effect layer of a preview frame.
    /// <para>
    /// <b>Absence means unlit.</b> A <see cref="LightingEffectFrame"/> holds a cell only for the
    /// keys the effect lights this frame; a key the effect leaves dark is simply missing from
    /// <see cref="LightingEffectFrame.Cells"/> rather than present at intensity 0, so the board
    /// draws it hatched (off is hatched, never black). Cells below
    /// <see cref="MinimumVisibleIntensity"/> are dropped for the same reason.
    /// </para>
    /// </summary>
    /// <param name="Color">The colour the effect layer paints on the key.</param>
    /// <param name="Intensity">The alpha the effect layer is drawn at, 0..1.</param>
    public readonly record struct LightingPreviewCell(LedColor Color, double Intensity)
    {
        /// <summary>
        /// The smallest intensity worth drawing — one step of an 8-bit alpha channel. A cell that
        /// would land below this is omitted from the frame instead.
        /// </summary>
        public const double MinimumVisibleIntensity = 1.0 / 255.0;
    }
}
