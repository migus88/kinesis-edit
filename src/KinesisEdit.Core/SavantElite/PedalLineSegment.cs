namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One contiguous span of a tracked pedal line with its validity verdict — the pedal twin of
    /// <see cref="Layouts.LayoutLineSegment"/> (specs/04-layout-file-format.md §5.1). The segments
    /// of a line tile its verbatim text in order, so a UI can rebuild the line and colour the
    /// invalid spans red.
    /// </summary>
    /// <param name="Text">Verbatim slice of the original line, original casing included.</param>
    /// <param name="IsValid">Whether the segment parsed; a line applies only if all segments are valid.</param>
    public sealed record PedalLineSegment(string Text, bool IsValid);
}
