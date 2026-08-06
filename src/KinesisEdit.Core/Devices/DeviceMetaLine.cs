namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Composes the one-line hardware summary a dashboard card shows under the device name —
    /// "Split contoured · 5 layers · 6 indicator LEDs" (docs/design/mockups.md §1b, §2e).
    /// Pure formatting over <see cref="DeviceDefinition"/>: every segment reads a catalog
    /// property, so no view ever needs per-device code, and the strings are testable without a UI.
    /// Segments the device has no data for are omitted rather than rendered empty.
    /// </summary>
    public static class DeviceMetaLine
    {
        /// <summary>Separator between segments: U+00B7 MIDDLE DOT, spaced (docs/design/mockups.md §1b).</summary>
        private const string SegmentSeparator = " · ";

        /// <summary>
        /// Returns the meta line for <paramref name="device"/>, or an empty string when the device
        /// carries none of the described facts (the CROSSFIRE keypad).
        /// </summary>
        public static string Describe(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var segments = new List<string>(capacity: 4);

            AppendSegment(segments, DescribeFormFactor(device.FormFactor));
            AppendSegment(segments, DescribeLayers(device.LayerCount));
            AppendSegment(segments, DescribeLighting(device.Lighting));
            AppendSegment(segments, device.AccessoryNote);

            return string.Join(SegmentSeparator, segments);
        }

        private static void AppendSegment(List<string> segments, string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return;
            }

            segments.Add(segment);
        }

        private static string? DescribeFormFactor(DeviceFormFactor formFactor)
        {
            return formFactor switch
            {
                DeviceFormFactor.SplitFlat => "Split flat",
                DeviceFormFactor.SplitContoured => "Split contoured",
                DeviceFormFactor.SixtyPercentGaming => "60% gaming",
                DeviceFormFactor.ThreeButtonFootPedal => "3-button foot pedal",
                _ => null
            };
        }

        private static string? DescribeLayers(int? layerCount)
        {
            if (layerCount is null or <= 0)
            {
                return null;
            }

            return DescribeCount(layerCount.Value, "layer");
        }

        private static string? DescribeLighting(LightingCapability lighting)
        {
            return lighting.Kind switch
            {
                // The mockups never show a blue-backlit board (the Freestyle Edge has no card in
                // any of the 20 screens), so the wording follows the spec's own phrase — specs/02
                // "blue backlight: brightness knob, Pitch Black and Breathe modes" — cast in the
                // shape of the sibling segments: lowercase words, acronyms left uppercase.
                LightingKind.BlueBacklight => "blue backlight",
                LightingKind.PerKeyRgb => lighting.HasEdgeLighting ? "per-key + edge RGB" : "per-key RGB",
                LightingKind.IndicatorLeds when lighting.IndicatorLedCount > 0 =>
                    DescribeCount(lighting.IndicatorLedCount, "indicator LED"),
                _ => null
            };
        }

        private static string DescribeCount(int count, string singularNoun)
        {
            return count == 1 ? $"{count} {singularNoun}" : $"{count} {singularNoun}s";
        }
    }
}
