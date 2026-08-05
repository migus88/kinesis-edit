using System.Collections.ObjectModel;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// The outcome of parsing one layout file (specs/04-layout-file-format.md §4.2): a freshly
    /// built model carrying every rule that applied, plus the tracked lines that did not. Device
    /// limits the file breaches are not errors — <see cref="KeyboardLayout.Validate"/> reports
    /// them on <see cref="Layout"/>.
    /// </summary>
    public sealed class LayoutParseResult
    {
        /// <summary>The dialect the file was parsed as (04 §4.1).</summary>
        public LayoutDialect Dialect { get; }

        /// <summary>
        /// The model the file was loaded into. Parsing always starts from a fresh factory-state
        /// layout — the file fully replaces the model (04 §4.2 step 1).
        /// </summary>
        public KeyboardLayout Layout { get; }

        /// <summary>
        /// Every line that could not be applied, in file order (04 §5). Mark
        /// <see cref="LayoutInvalidLine.Keep"/> and hand the list to
        /// <see cref="LayoutFileSerializer.Serialize(KeyboardLayout, IEnumerable{LayoutInvalidLine})"/>
        /// to preserve a line verbatim across a save.
        /// </summary>
        public IReadOnlyList<LayoutInvalidLine> InvalidLines { get; }

        internal LayoutParseResult(LayoutDialect dialect, KeyboardLayout layout, List<LayoutInvalidLine> invalidLines)
        {
            Dialect = dialect;
            Layout = layout;
            InvalidLines = new ReadOnlyCollection<LayoutInvalidLine>(invalidLines);
        }
    }
}
