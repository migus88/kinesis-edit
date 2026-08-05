using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// One tracked layout line that could not be applied to the model
    /// (specs/04-layout-file-format.md §5): its position in the file, the layer it addressed,
    /// its verbatim text, and the per-segment validity spans a future dialog colours red
    /// (04 §5.2). Nothing is dropped silently on load — every unapplied line lands here — but
    /// <see cref="Keep"/> defaults to false, so a line the user does not mark kept is
    /// permanently dropped on the first save (04 §5.2).
    /// </summary>
    public sealed class LayoutInvalidLine
    {
        /// <summary>1-based line number in the loaded file (04 §4.2 step 2).</summary>
        public int LineNumber { get; }

        /// <summary>
        /// Index of the layer the line addressed (04 §4.2 step 2): the <c>fn </c>/<c>kp-</c>
        /// routing verdict, or the current <c>&lt;...&gt;</c> header layer on Gen2. A kept line
        /// is re-emitted after this layer's rules on save (04 §4.3 step 4).
        /// </summary>
        public int LayerIndex { get; }

        /// <summary>
        /// The original line exactly as loaded — original casing, any <c>fn </c> prefix, any
        /// trailing whitespace (04 §4.3: "Kept invalid lines must be preserved exactly as loaded").
        /// </summary>
        public string Text { get; }

        /// <summary>The validity spans of the line, tiling <see cref="Text"/> in order (04 §5.1).</summary>
        public IReadOnlyList<LayoutLineSegment> Segments { get; }

        /// <summary>
        /// Whether the line is written back verbatim on save. Defaults to false — unchecked
        /// invalid lines are silently discarded on the first save (04 §5.2).
        /// </summary>
        public bool Keep { get; set; }

        /// <summary>Creates a tracked line; see the property docs for the meaning of each value.</summary>
        public LayoutInvalidLine(int lineNumber, int layerIndex, string text, IReadOnlyList<LayoutLineSegment> segments)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(layerIndex);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(segments);

            LineNumber = lineNumber;
            LayerIndex = layerIndex;
            Text = text;
            Segments = new ReadOnlyCollection<LayoutLineSegment>([.. segments]);
        }
    }
}
