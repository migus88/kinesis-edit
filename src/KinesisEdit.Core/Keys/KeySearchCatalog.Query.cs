using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// The query side of the searchable token list: the incremental filter of
    /// specs/11-feature-dialogs.md §11.6, the category filter behind the token picker's chips, and
    /// the counted grouping behind its result headers. Every one of them is a pure function over a
    /// row list, so the picker can compose them in any order and re-run them on each keystroke.
    /// </summary>
    public static partial class KeySearchCatalog
    {
        /// <summary>
        /// The incremental filter of §11.6: the rows of <paramref name="entries"/> whose item text
        /// or file token contains <paramref name="query"/>, compared case-insensitively so a user
        /// can search "by either name or file token". A null, empty, or whitespace query matches
        /// everything and <paramref name="entries"/> is returned unchanged.
        /// </summary>
        public static IReadOnlyList<KeySearchEntry> Filter(IReadOnlyList<KeySearchEntry> entries, string? query)
        {
            ArgumentNullException.ThrowIfNull(entries);

            if (string.IsNullOrWhiteSpace(query))
            {
                return entries;
            }

            var matches = new List<KeySearchEntry>();

            foreach (var entry in entries)
            {
                if (Matches(entry, query))
                {
                    matches.Add(entry);
                }
            }

            return new ReadOnlyCollection<KeySearchEntry>(matches);
        }

        /// <summary>
        /// The rows of <paramref name="entries"/> in <paramref name="category"/> — the token
        /// picker's chips. <see cref="KeySearchCategory.All"/> is "no filter" and returns
        /// <paramref name="entries"/> unchanged, matching the blank-query rule; every other value
        /// compares against <see cref="KeySearchEntry.Category"/>. Composes with
        /// <see cref="Filter(IReadOnlyList{KeySearchEntry}, string?)"/> in either order.
        /// </summary>
        public static IReadOnlyList<KeySearchEntry> Filter(
            IReadOnlyList<KeySearchEntry> entries,
            KeySearchCategory category)
        {
            ArgumentNullException.ThrowIfNull(entries);

            if (category == KeySearchCategory.All)
            {
                return entries;
            }

            var matches = new List<KeySearchEntry>();

            foreach (var entry in entries)
            {
                if (entry.Category == category)
                {
                    matches.Add(entry);
                }
            }

            return new ReadOnlyCollection<KeySearchEntry>(matches);
        }

        /// <summary>
        /// Groups <paramref name="entries"/> by the specs/05-key-model.md §3 sub-table each row
        /// comes from, keeping input order both between groups (first appearance) and within them.
        /// Empty groups are never produced, so <see cref="KeySearchGroup.Count"/> is always at
        /// least one and the header's count is exactly the rows beneath it.
        /// </summary>
        public static IReadOnlyList<KeySearchGroup> Group(IReadOnlyList<KeySearchEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var order = new List<KeyTable>();
            var rowsByTable = new Dictionary<KeyTable, List<KeySearchEntry>>();

            foreach (var entry in entries)
            {
                var table = entry.Definition.Table;

                if (!rowsByTable.TryGetValue(table, out var rows))
                {
                    rows = new List<KeySearchEntry>();
                    rowsByTable.Add(table, rows);
                    order.Add(table);
                }

                rows.Add(entry);
            }

            var groups = new List<KeySearchGroup>(order.Count);

            foreach (var table in order)
            {
                groups.Add(new KeySearchGroup(table, new ReadOnlyCollection<KeySearchEntry>(rowsByTable[table])));
            }

            return new ReadOnlyCollection<KeySearchGroup>(groups);
        }

        private static bool Matches(KeySearchEntry entry, string query)
        {
            return entry.DisplayText.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || entry.FileToken.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
