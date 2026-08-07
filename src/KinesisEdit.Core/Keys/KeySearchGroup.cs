namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// One counted group of the token picker's result list — the <c>Navigation · 3</c> headers of
    /// docs/design/mockups.md §1e. Groups are keyed by the specs/05-key-model.md §3 sub-table the
    /// rows come from, which is finer than <see cref="KeySearchCategory"/> and is the real datum;
    /// the header wording is the app layer's business. Built by
    /// <see cref="KeySearchCatalog.Group"/>.
    /// </summary>
    /// <param name="Table">The §3 sub-table every row in the group belongs to.</param>
    /// <param name="Entries">The group's rows, in catalog order.</param>
    public sealed record KeySearchGroup(KeyTable Table, IReadOnlyList<KeySearchEntry> Entries)
    {
        /// <summary>The number the header shows beside the group name.</summary>
        public int Count => Entries.Count;
    }
}
