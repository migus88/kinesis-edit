namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Why <see cref="PedalEditSession.Apply"/> refused a special action. Only "Different Press
    /// &amp;&amp; Release" can be refused (specs/12-savant-elite.md §6) — it flags an existing
    /// keystroke rather than adding one.
    /// </summary>
    public enum PedalSpecialActionFailure
    {
        /// <summary>The action was applied.</summary>
        None = 0,

        /// <summary>There is no keystroke to flag — the entry is empty (§6: the split marks "the last entered key").</summary>
        EmptyEntry = 1,

        /// <summary>
        /// The last keystroke is a modifier key. <c>{-lshift}{ }{+lshift}</c> reads back as a
        /// Shift-modified space (§4.4's <c>{ }</c> ambiguity), so the flag would not survive a save,
        /// and §6 never appends a bare <c>{ }</c> here.
        /// </summary>
        ModifierKeystroke = 2,

        /// <summary>
        /// The entry is in a mode the item does not support, and switching to the one it needs would
        /// clear the entry (§5 step 2) — leaving the split nothing to flag. The user switches mode
        /// deliberately instead of losing what is in the box.
        /// </summary>
        ModeNotSupported = 3
    }
}
