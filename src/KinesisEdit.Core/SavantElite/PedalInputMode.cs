namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// What one pedal input is programmed with (specs/12-savant-elite.md §4.2): the bracket style
    /// of the input name selects the mode for the whole line — <c>[input]&gt;</c> is a single
    /// action, <c>{input}&gt;</c> a macro.
    /// </summary>
    public enum PedalInputMode
    {
        /// <summary>
        /// Unassigned — the input has no action. Written as the empty single-action line
        /// <c>[input]&gt;</c>, the form a freshly created file uses for every input (12 §4.1).
        /// </summary>
        None = 0,

        /// <summary>One key or mouse button, written <c>[input]&gt;[token]</c> (12 §4.3).</summary>
        Single = 1,

        /// <summary>A keystroke sequence, written <c>{input}&gt;{...}{...}</c> (12 §4.3).</summary>
        Macro = 2
    }
}
