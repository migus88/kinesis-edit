namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Parses the text lines of a <c>lighting/ledN.txt</c> file into an in-memory lighting
    /// model, per device dialect (specs/07-lighting.md §1.4, §2, §5): key backlight for the
    /// Freestyle Edge RGB, key backlight + edge for the TKO, indicators for the Advantage360.
    /// Pure lines-in/model-out — file I/O lives in the v-Drive services, not here. Parsing is
    /// case-insensitive, starts from a fully reset state, and never rejects input: Freestyle
    /// is the fallback interpretation (§2.4).
    /// </summary>
    public static class LedFileParser
    {
        /// <summary>Parses a Freestyle Edge RGB led file (key-backlight dialect, §2).</summary>
        public static LightingModel ParseRgb(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            return LightingSectionParser.Parse(lines, LightingSectionContext.RgbKeyBacklight);
        }

        /// <summary>
        /// Parses a TKO led file: edge-lighting lines are first separated from the
        /// key-backlight lines, and the two sections are parsed independently (§1.4, §2.3).
        /// </summary>
        public static TkoLightingModel ParseTko(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var keyBacklightLines = new List<string>();
            var edgeLines = new List<string>();

            foreach (var line in lines)
            {
                if (TkoEdgeLineClassifier.IsEdgeLine(line))
                {
                    edgeLines.Add(line);
                }
                else
                {
                    keyBacklightLines.Add(line);
                }
            }

            var model = new TkoLightingModel();

            LightingSectionParser.ParseInto(model.KeyBacklight, keyBacklightLines, LightingSectionContext.TkoKeyBacklight);
            LightingSectionParser.ParseInto(model.Edge, edgeLines, LightingSectionContext.TkoEdge);

            return model;
        }

        /// <summary>Parses an Advantage360 led file (indicator dialect, §5).</summary>
        public static Advantage360LightingModel ParseAdvantage360(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            return IndicatorFileEngine.Parse(lines);
        }
    }
}
