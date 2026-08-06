using System.Collections.ObjectModel;
using System.Text;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Backs the Search Keys dialog of specs/11-feature-dialogs.md §11.6: the searchable list of
    /// every assignable action in one dialect, plus the incremental filter over it. UI-free — no
    /// list box, no selection, no captions beyond the item text §11.6 specifies.
    /// <para>
    /// §11.6 skips entries "flagged as non-searchable (numpad duplicates, delay tokens,
    /// hotkeys)". The flag that carries that intent here is
    /// <see cref="KeyDefinitionFlags.HiddenFromSearch"/> (the legacy <c>SKIP_SEARCH</c> sentinel
    /// of specs/05-key-model.md §1.1), which the key table sets on the §3.12 speed/delay
    /// pseudo-keys, the §3.13 edge zones, and the <c>Fn</c> action. The Legacy keypad duplicates
    /// of §3.6 and the <c>hk0</c>..<c>hk10</c> hotkeys of §3.11 carry no such flag in the table,
    /// so they are listed; flagging them is a key-table change, not a search-catalog one.
    /// </para>
    /// </summary>
    public static class KeySearchCatalog
    {
        private const char CaptionLineBreak = '\n';
        private const string TokenPrefix = " (";
        private const string TokenSuffix = ")";

        /// <summary>
        /// Every assignable action of <paramref name="dialect"/> in key-table registration order
        /// (05 §3, §7), minus the entries flagged
        /// <see cref="KeyDefinitionFlags.HiddenFromSearch"/> and the entries the dialect does not
        /// name — an action with no file token there can never be written to that device's files.
        /// <see cref="TokenDialect.None"/> lists every action under the first token that names it,
        /// mirroring <see cref="KeyRegistry.FindByToken(string?)"/>'s all-dialect lookup.
        /// </summary>
        public static IReadOnlyList<KeySearchEntry> Build(TokenDialect dialect)
        {
            var entries = new List<KeySearchEntry>();

            foreach (var definition in KeyRegistry.Entries)
            {
                if ((definition.Flags & KeyDefinitionFlags.HiddenFromSearch) != 0)
                {
                    continue;
                }

                var token = ResolveToken(definition, dialect);

                if (token.Length == 0)
                {
                    continue;
                }

                entries.Add(Compose(definition, dialect, token));
            }

            return new ReadOnlyCollection<KeySearchEntry>(entries);
        }

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

        private static bool Matches(KeySearchEntry entry, string query)
        {
            return entry.DisplayText.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || entry.FileToken.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Composes the item text of §11.6. The key table carries no <c>SearchText</c> column
        /// (05 §1.1 documents it on the legacy model only), so the entry's dialect-neutral
        /// caption plays the search name and the dialect caption plays the display text — the two
        /// differ exactly where a §3 table gives a per-dialect override.
        /// </summary>
        private static KeySearchEntry Compose(KeyDefinition definition, TokenDialect dialect, string token)
        {
            var searchName = ResolveCaption(definition.DisplayText, token);
            var displayText = ResolveCaption(definition.GetDisplayText(dialect), token);
            var text = new StringBuilder(searchName);

            if (!Equal(displayText, searchName))
            {
                text.Append(' ').Append(displayText);
            }

            if (!Equal(displayText, token))
            {
                text.Append(TokenPrefix).Append(token).Append(TokenSuffix);
            }

            return new KeySearchEntry(definition, searchName, token, text.ToString());
        }

        /// <summary>
        /// Flattens a key-cap caption to one line (a <c>\n</c> is a two-line cap, 05 §1.1) and
        /// falls back to the file token for the captions the §3 tables leave blank — the nine
        /// unlabelled Freestyle hotkeys of §3.11 would otherwise be indistinguishable rows.
        /// </summary>
        private static string ResolveCaption(string caption, string token)
        {
            var flattened = caption.Replace(CaptionLineBreak, ' ').Trim();

            return flattened.Length > 0 ? flattened : token;
        }

        private static string ResolveToken(KeyDefinition definition, TokenDialect dialect)
        {
            if (dialect != TokenDialect.None)
            {
                return definition.GetToken(dialect);
            }

            if (definition.LegacyToken.Length > 0)
            {
                return definition.LegacyToken;
            }

            return definition.Gen1Token.Length > 0 ? definition.Gen1Token : definition.Gen2Token;
        }

        private static bool Equal(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
