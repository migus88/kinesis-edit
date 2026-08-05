using System.Collections.ObjectModel;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// The outcome of parsing one <c>active/pedals.txt</c> (specs/12-savant-elite.md §4.2): the
    /// seven inputs the file programmed, the lines that addressed an input but could not be
    /// applied, and every source line with its owner — the input the save merge of §4.6 rewrites
    /// it from.
    /// </summary>
    public sealed class PedalParseResult
    {
        /// <summary>The model the file was loaded into; parsing always starts from a fresh configuration.</summary>
        public PedalConfiguration Configuration { get; }

        /// <summary>Every line that addressed an input but could not be applied, in file order (12 §4.2).</summary>
        public IReadOnlyList<PedalInvalidLine> InvalidLines { get; }

        /// <summary>
        /// Every line of the file in order, each tagged with the input that owns it — hand this to
        /// <see cref="PedalFileSerializer.Merge"/> to rewrite the pedal lines in place and leave
        /// the factory instruction block byte-identical (12 §4.6).
        /// </summary>
        public IReadOnlyList<PedalSourceLine> SourceLines { get; }

        internal PedalParseResult(
            PedalConfiguration configuration,
            List<PedalInvalidLine> invalidLines,
            List<PedalSourceLine> sourceLines)
        {
            Configuration = configuration;
            InvalidLines = new ReadOnlyCollection<PedalInvalidLine>(invalidLines);
            SourceLines = new ReadOnlyCollection<PedalSourceLine>(sourceLines);
        }
    }
}
