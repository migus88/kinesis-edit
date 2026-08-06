namespace KinesisEdit.Core.Transfer
{
    /// <summary>
    /// One file the Export files dialog writes into the chosen directory
    /// (specs/11-feature-dialogs.md §11.5): the base name it keeps and the serialized text. The
    /// caller performs the write — and owns the <c>'Error exporting layout file: '</c> /
    /// <c>'Error exporting lighting file: '</c> / <c>'Files exported successfully!'</c> wording.
    /// </summary>
    /// <param name="FileName">Base name only, no directory (e.g. <c>layout1.txt</c>, <c>led1.txt</c>).</param>
    /// <param name="Lines">The file's lines, exactly as a save to the v-Drive would write them.</param>
    public sealed record ExportFile(string FileName, IReadOnlyList<string> Lines);
}
