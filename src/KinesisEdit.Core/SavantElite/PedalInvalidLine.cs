using System.Collections.ObjectModel;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One pedal line that addressed an input but could not be applied (specs/12-savant-elite.md
    /// §4.2): its position in the file, its verbatim text, and the per-segment validity spans a
    /// dialog can colour red. Nothing is dropped silently on load.
    /// <para>
    /// There is no <c>Keep</c> flag and no layer index here, unlike
    /// <see cref="Layouts.LayoutInvalidLine"/>: the pedal file has no layers, and a line whose
    /// prefix names an input is always rewritten in place from the model on save (§4.6), so the
    /// broken text is replaced rather than kept. Lines that name no input are preserved verbatim
    /// and never reach this type.
    /// </para>
    /// </summary>
    public sealed class PedalInvalidLine
    {
        /// <summary>1-based line number in the loaded file.</summary>
        public int LineNumber { get; }

        /// <summary>The input the line addressed (12 §4.2); never <see cref="PedalInputId.None"/>.</summary>
        public PedalInputId Input { get; }

        /// <summary>The original line exactly as loaded — original casing and spacing included.</summary>
        public string Text { get; }

        /// <summary>The validity spans of the line, tiling <see cref="Text"/> in order.</summary>
        public IReadOnlyList<PedalLineSegment> Segments { get; }

        /// <summary>Creates a tracked line; see the property docs for the meaning of each value.</summary>
        public PedalInvalidLine(int lineNumber, PedalInputId input, string text, IReadOnlyList<PedalLineSegment> segments)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(segments);

            LineNumber = lineNumber;
            Input = input;
            Text = text;
            Segments = new ReadOnlyCollection<PedalLineSegment>([.. segments]);
        }
    }
}
