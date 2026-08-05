namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Serializes an in-memory lighting model into the canonical text lines of a
    /// <c>lighting/ledN.txt</c> file, per device dialect (specs/07-lighting.md §2, §5).
    /// Saving regenerates the whole file; round-tripping through <see cref="LedFileParser"/>
    /// is semantic — the canonical output may reorder legacy inputs (e.g. the base
    /// <c>[mono]</c> line is always written first, §2.4 item 5) but parses to an equal model.
    /// Pure model-in/lines-out — file I/O lives in the v-Drive services, not here.
    /// </summary>
    public static class LedFileSerializer
    {
        /// <summary>Serializes a Freestyle Edge RGB led file (key-backlight dialect, §2).</summary>
        public static IReadOnlyList<string> SerializeRgb(LightingModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return LightingSectionSerializer.Serialize(model, LightingSectionContext.RgbKeyBacklight);
        }

        /// <summary>
        /// Serializes a TKO led file: the edge section is appended after the key-backlight
        /// section and written as one file (§1.4, §2.3).
        /// </summary>
        public static IReadOnlyList<string> SerializeTko(TkoLightingModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            var lines = LightingSectionSerializer.Serialize(model.KeyBacklight, LightingSectionContext.TkoKeyBacklight);

            lines.AddRange(LightingSectionSerializer.Serialize(model.Edge, LightingSectionContext.TkoEdge));

            return lines;
        }

        /// <summary>Serializes an Advantage360 led file (indicator dialect, §5).</summary>
        public static IReadOnlyList<string> SerializeAdvantage360(Advantage360LightingModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return IndicatorFileEngine.Serialize(model);
        }
    }
}
