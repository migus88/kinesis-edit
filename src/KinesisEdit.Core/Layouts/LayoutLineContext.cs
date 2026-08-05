using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Everything one layout line's parse needs (specs/04-layout-file-format.md §4.2): the
    /// model under construction, the dialect rules, the processed (lowercased, right-trimmed)
    /// line with its content offset and separator position, the scanned config/value parts,
    /// and the segment recorder. <see cref="LayerIndex"/> starts at the line-level verdict
    /// (<c>fn </c> prefix, Gen2 header) and is updated by the Advantage2 per-token routing.
    /// </summary>
    internal sealed class LayoutLineContext
    {
        /// <summary>The model rules are applied to.</summary>
        public required KeyboardLayout Layout { get; init; }

        /// <summary>The dialect knobs of the parse.</summary>
        public required LayoutDialectRules Rules { get; init; }

        /// <summary>The lowercased, right-trimmed line.</summary>
        public required string Processed { get; init; }

        /// <summary>Content start — 3 on a Gen1 bottom-layer line, 0 otherwise (04 §3.1).</summary>
        public required int Offset { get; init; }

        /// <summary>Index of the first <c>&gt;</c> — the rule separator (04 §1.3).</summary>
        public required int SeparatorIndex { get; init; }

        /// <summary>Scanned parts of the config side, in line order.</summary>
        public required List<LayoutLinePart> ConfigParts { get; init; }

        /// <summary>Scanned parts of the value side, in line order.</summary>
        public required List<LayoutLinePart> ValueParts { get; init; }

        /// <summary>The validity spans of the line (04 §5.1).</summary>
        public required LayoutLineSegmentRecorder Segments { get; init; }

        /// <summary>The layer the line targets (04 §3); updated by Advantage2 token routing.</summary>
        public int LayerIndex { get; set; }

        /// <summary>The addressed layer, or null when <see cref="LayerIndex"/> names none.</summary>
        public KeyboardLayer? FindLayer()
        {
            return Layout.FindLayer(LayerIndex);
        }

        /// <summary>
        /// Poisons the line without a visible span — for structural failures no segment can
        /// carry, like a missing tap-and-hold group (04 §5.1 "missing tap/hold action").
        /// </summary>
        public void MarkInvalidWithoutSegment()
        {
            Segments.Add(Processed.Length, 0, false);
        }
    }
}
