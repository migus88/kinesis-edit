using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Collects the validity spans of one pedal line while it is parsed and builds its
    /// <see cref="PedalLineSegment"/> list — the pedal twin of
    /// <see cref="LayoutLineSegmentRecorder"/> (specs/04-layout-file-format.md §5.1, applied to
    /// the 12 §4.2 grammar). Spans are recorded against the processed (lowercased, right-trimmed)
    /// line but sliced from the verbatim original — per-character lowercasing preserves offsets —
    /// and every gap between recorded spans (the input name, the <c>&gt;</c> separator, trailing
    /// whitespace) becomes a valid segment, so the segments always tile the original text.
    /// </summary>
    internal sealed class PedalLineSegmentRecorder
    {
        /// <summary>Whether any recorded span was invalid — the line-validity verdict.</summary>
        public bool HasInvalidSegments { get; private set; }

        private readonly string _rawLine;
        private readonly List<(int Start, int Length, bool IsValid)> _spans = [];

        public PedalLineSegmentRecorder(string rawLine)
        {
            _rawLine = rawLine;
        }

        /// <summary>Records a scanned part with its verdict; parts must be added in line order.</summary>
        public void Add(LayoutLinePart part, bool isValid)
        {
            Add(part.Start, part.Length, isValid);
        }

        /// <summary>Records a span; spans must be added in line order and must not overlap.</summary>
        public void Add(int start, int length, bool isValid)
        {
            if (!isValid)
            {
                HasInvalidSegments = true;
            }

            if (length > 0)
            {
                _spans.Add((start, length, isValid));
            }
        }

        /// <summary>Discards all recorded spans and marks the whole line as one invalid segment.</summary>
        public void MarkWholeLineInvalid()
        {
            _spans.Clear();
            _spans.Add((0, _rawLine.Length, false));
            HasInvalidSegments = true;
        }

        /// <summary>Builds the segment list, filling every gap with a valid segment.</summary>
        public IReadOnlyList<PedalLineSegment> Build()
        {
            var segments = new List<PedalLineSegment>();
            var cursor = 0;

            foreach (var (start, length, isValid) in _spans)
            {
                if (start > cursor)
                {
                    segments.Add(new PedalLineSegment(_rawLine[cursor..start], true));
                }

                var end = Math.Min(start + length, _rawLine.Length);

                segments.Add(new PedalLineSegment(_rawLine[start..end], isValid));
                cursor = end;
            }

            if (cursor < _rawLine.Length)
            {
                segments.Add(new PedalLineSegment(_rawLine[cursor..], true));
            }

            return segments;
        }
    }
}
