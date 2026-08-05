namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// One contiguous span of a tracked layout line with its validity verdict
    /// (specs/04-layout-file-format.md §5.1: "Each parsed line is decomposed into segments, each
    /// flagged valid or invalid"). The segments of a line tile its verbatim text in order, so a
    /// UI can rebuild the line and colour the invalid spans red (04 §5.2).
    /// </summary>
    /// <param name="Text">Verbatim slice of the original line, original casing included.</param>
    /// <param name="IsValid">Whether the segment parsed; a line is valid only if all segments are (04 §5.1).</param>
    public sealed record LayoutLineSegment(string Text, bool IsValid);
}
