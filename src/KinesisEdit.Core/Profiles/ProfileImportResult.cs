using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// Outcome of <see cref="ProfileSession.Import"/>: which half of the session the imported
    /// file replaced, and the invalid-line list as it stands afterwards
    /// (specs/04-layout-file-format.md §5). A layout import brings its own list; a lighting
    /// import leaves the layout's untouched and reports it unchanged, so a caller can always
    /// re-render the invalid-line surface from this one value.
    /// </summary>
    public sealed record ProfileImportResult
    {
        /// <summary>What the file was read as (specs/07-lighting.md §1.4).</summary>
        public required ImportedFileKind Kind { get; init; }

        /// <summary>The session's invalid layout lines after the import.</summary>
        public required IReadOnlyList<LayoutInvalidLine> InvalidLines { get; init; }
    }
}
