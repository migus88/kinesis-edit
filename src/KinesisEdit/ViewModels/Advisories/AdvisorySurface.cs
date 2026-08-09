namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// Which surface <em>inside</em> a section reviews an advisory. It is the second half of
    /// <see cref="AdvisoryAnchor"/>'s address: <see cref="AdvisoryAnchor.Tab"/> says which section
    /// shows the advisory, this says what <c>Review N</c> opens once it is there.
    /// <para>
    /// It exists because the Layout tab now shows both kinds (issue #140): a duplicate token is
    /// about a key cap on the board, a macro budget is about the key inspector's Macro panel, and
    /// both are anchored at <see cref="EditorTab.Keys"/>. The tab alone can no longer tell them
    /// apart.
    /// </para>
    /// <para>
    /// <b>It is not inferred from <see cref="AdvisoryAnchor.MacroIndex"/>.</b> A flat-list board
    /// (Advantage 360) reports its macro findings with no slot at all (06 §1), so "has a macro
    /// index" would send exactly those advisories to key selection — the surface is stated by the
    /// source instead of guessed at by the consumer.
    /// </para>
    /// </summary>
    public enum AdvisorySurface
    {
        /// <summary>The keyboard picture — <c>Review</c> selects the anchored cap.</summary>
        Board = 0,

        /// <summary>The key inspector's Macro panel — <c>Review</c> opens the anchored macro.</summary>
        MacroPanel = 1
    }
}
