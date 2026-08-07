namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// One row of the searchable token list (specs/11-feature-dialogs.md §11.6): the assignable
    /// action, the parts a query is matched against, the composed item text the list shows, and
    /// the facts the token picker groups and labels rows by. Built by
    /// <see cref="KeySearchCatalog"/>; selection, acceptance and every caption belong to the UI.
    /// <para>
    /// The four positional members are the original shape and are load-bearing for existing call
    /// sites. Everything the picker added is an <c>init</c> property with a safe default, so
    /// constructing a row positionally stays legal.
    /// </para>
    /// </summary>
    /// <param name="Definition">The key-table entry the row selects (05 §3).</param>
    /// <param name="SearchName">
    /// The action's single-line name — the entry's default caption with two-line breaks flattened,
    /// or its file token when the caption is blank.
    /// </param>
    /// <param name="FileToken">The layout-file token of the entry in the catalog's dialect (05 §3).</param>
    /// <param name="DisplayText">
    /// The composed item text of §11.6: the search name, its display text when different, and
    /// <c>' (' + token + ')'</c> when the display text differs from the file token.
    /// </param>
    public sealed record KeySearchEntry(
        KeyDefinition Definition,
        string SearchName,
        string FileToken,
        string DisplayText)
    {
        /// <summary>
        /// The chip that reaches this row (docs/design/mockups.md §1e). Always concrete —
        /// <see cref="KeySearchCategory.All"/> is a filter value and is never assigned here.
        /// Rows no chip names carry <see cref="KeySearchCategory.Other"/>.
        /// </summary>
        public KeySearchCategory Category { get; init; } = KeySearchCategory.Other;

        /// <summary>
        /// Where the row lives on the board — the keypad, a device hotkey, a profile selector, or
        /// nowhere in particular. Derived from <see cref="KeyDefinition.Table"/>, never from a
        /// token prefix.
        /// </summary>
        public KeySearchScope Scope { get; init; } = KeySearchScope.None;

        /// <summary>
        /// The earlier row this one duplicates, or null when this row is the canonical one. Set by
        /// <see cref="KeySearchCatalog.LinkAliases"/>: two rows are the same action when the
        /// registry's first-match lookups cannot tell them apart (05 §7), and the earliest
        /// registration wins — exactly what <see cref="KeyRegistry.FindByCode"/> and
        /// <see cref="KeyRegistry.FindByToken(string?)"/> would answer. The chain is one level
        /// deep: a canonical row never points anywhere.
        /// </summary>
        public KeySearchEntry? AliasOf { get; init; }

        /// <summary>
        /// Whether an earlier row already offers this action — the picker's <c>alias of …</c>
        /// label. Aliases are listed, not hidden: both rows are assignable and both write a token
        /// the firmware accepts.
        /// </summary>
        public bool IsAlias => AliasOf is not null;
    }
}
