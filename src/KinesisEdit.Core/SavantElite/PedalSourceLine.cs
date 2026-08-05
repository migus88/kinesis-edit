namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One line of the loaded <c>pedals.txt</c> and the input that owns it — the record the save
    /// merge of specs/12-savant-elite.md §4.6 runs on: "rewrites only the lines whose prefix
    /// matches a pedal token, leaving all other lines (the factory instruction block) untouched".
    /// </summary>
    /// <param name="Text">The line exactly as loaded, original casing and spacing included.</param>
    /// <param name="Input">
    /// The input whose prefix the line carries, or <see cref="PedalInputId.None"/> for a line that
    /// names no input — those are written back byte-for-byte.
    /// </param>
    public sealed record PedalSourceLine(string Text, PedalInputId Input);
}
