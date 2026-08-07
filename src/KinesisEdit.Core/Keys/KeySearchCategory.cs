namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// The category filter over the token picker's searchable list — the chips of
    /// docs/design/mockups.md §1e (<c>All · Letters · Nav · Media · Mouse · Hotkeys</c>). A
    /// category is a coarse grouping of the specs/05-key-model.md §3 sub-tables already carried by
    /// <see cref="KeyTable"/>; it is a filter, not a second taxonomy, and
    /// <see cref="KeySearchCatalog.CategoryFor(KeyTable)"/> is the whole mapping.
    /// <para>
    /// Only five of the thirteen §3 tables are named by a chip. Punctuation (§3.2), function keys
    /// (§3.4), modifiers (§3.5), keypad keys (§3.6), special actions (§3.9) and layer keys (§3.10)
    /// map to <see cref="Other"/> — a category with no chip of its own, reached through
    /// <see cref="All"/>. Nothing is dropped to make the mapping tidy: §11.6 specifies one flat
    /// list of every assignable action, and a row no chip could reach would be a regression
    /// against it.
    /// </para>
    /// <para>
    /// <see cref="All"/> is a filter value only. Rows built by <see cref="KeySearchCatalog"/>
    /// always carry one of the concrete categories, and
    /// <see cref="KeySearchCatalog.Filter(IReadOnlyList{KeySearchEntry}, KeySearchCategory)"/>
    /// treats <see cref="All"/> as "no filter" rather than as a value to compare against.
    /// </para>
    /// </summary>
    public enum KeySearchCategory
    {
        /// <summary>Every row. The neutral filter and the default; never assigned to a row.</summary>
        All = 0,

        /// <summary>Letters and digits — <see cref="KeyTable.LettersAndDigits"/> (§3.1).</summary>
        Letters = 1,

        /// <summary>
        /// Whitespace, editing and navigation — <see cref="KeyTable.Navigation"/> (§3.3).
        /// </summary>
        Nav = 2,

        /// <summary>Media and volume keys — <see cref="KeyTable.MediaKeys"/> (§3.7).</summary>
        Media = 3,

        /// <summary>Mouse actions — <see cref="KeyTable.MouseActions"/> (§3.8).</summary>
        Mouse = 4,

        /// <summary>
        /// Profile selectors and the board's own hotkeys —
        /// <see cref="KeyTable.ProfilesAndHotkeys"/> (§3.11). <see cref="KeySearchEntry.Scope"/>
        /// separates the two.
        /// </summary>
        Hotkeys = 5,

        /// <summary>
        /// Every §3 table no chip names: punctuation, function keys, modifiers, keypad keys,
        /// special actions and layer keys. Reachable through <see cref="All"/> and through the
        /// query, which is what keeps the list complete.
        /// </summary>
        Other = 6
    }
}
