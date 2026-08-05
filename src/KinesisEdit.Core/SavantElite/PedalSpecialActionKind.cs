namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// What applying a <see cref="PedalSpecialAction"/> does to the entry being edited
    /// (specs/12-savant-elite.md §6). Most items simply contribute keystrokes; the last two rows of
    /// the §6 table are modes of the editor rather than content.
    /// </summary>
    public enum PedalSpecialActionKind
    {
        /// <summary>
        /// Contributes <see cref="PedalSpecialAction.CreateKeystrokes"/> — appended in macro mode,
        /// the single action in single mode.
        /// </summary>
        Keystrokes = 0,

        /// <summary>
        /// §6 "Different Press &amp;&amp; Release": flags the last keystroke of the macro so it
        /// serializes as <c>{-key}{ }{+key}</c>, splitting playback between pedal press and release.
        /// </summary>
        DifferentPressRelease = 1,

        /// <summary>
        /// §6 "Windows Combination (Win + _)": a checkbox item that toggles the held state of the
        /// Win/Cmd modifier, which then rides along with every keystroke entered afterwards.
        /// </summary>
        ToggleWinModifier = 2
    }
}
