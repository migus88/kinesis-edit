using System.Collections.ObjectModel;
using System.Text;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Backs the searchable token list of specs/11-feature-dialogs.md §11.6: every assignable
    /// action in one dialect, classified into the token picker's categories and scopes, with the
    /// duplicate registrations linked to the row they duplicate. UI-free — no list box, no
    /// selection, no caption beyond the item text §11.6 specifies.
    /// <para>
    /// §11.6 skips entries "flagged as non-searchable (numpad duplicates, delay tokens,
    /// hotkeys)". The flag that carries that intent here is
    /// <see cref="KeyDefinitionFlags.HiddenFromSearch"/> (the legacy <c>SKIP_SEARCH</c> sentinel
    /// of specs/05-key-model.md §1.1), which the key table sets on the §3.12 speed/delay
    /// pseudo-keys, the §3.13 edge zones, and the <c>Fn</c> action. The Legacy keypad duplicates
    /// of §3.6 and the <c>hk0</c>..<c>hk10</c> hotkeys of §3.11 carry no such flag in the table,
    /// so they are listed; flagging them is a key-table change, not a search-catalog one. They are
    /// instead labelled — see <see cref="KeySearchScope"/> and
    /// <see cref="KeySearchEntry.AliasOf"/>.
    /// </para>
    /// <para>
    /// Query-side members (<see cref="Filter(IReadOnlyList{KeySearchEntry}, string?)"/>,
    /// <see cref="Filter(IReadOnlyList{KeySearchEntry}, KeySearchCategory)"/> and
    /// <see cref="Group"/>) live in <c>KeySearchCatalog.Query.cs</c>.
    /// </para>
    /// </summary>
    public static partial class KeySearchCatalog
    {
        private const char CaptionLineBreak = '\n';
        private const string TokenPrefix = " (";
        private const string TokenSuffix = ")";
        private const string HotkeyTokenPrefix = "hk";
        private const string ProfileTokenPrefix = "pro";

        /// <summary>
        /// Every assignable action of <paramref name="dialect"/> in key-table registration order
        /// (05 §3, §7), minus the entries flagged
        /// <see cref="KeyDefinitionFlags.HiddenFromSearch"/> and the entries the dialect does not
        /// name — an action with no file token there can never be written to that device's files.
        /// <see cref="TokenDialect.None"/> lists every action under the first token that names it,
        /// mirroring <see cref="KeyRegistry.FindByToken(string?)"/>'s all-dialect lookup.
        /// <para>
        /// Every row carries its <see cref="KeySearchEntry.Category"/> and
        /// <see cref="KeySearchEntry.Scope"/>, and the duplicate registrations carry
        /// <see cref="KeySearchEntry.AliasOf"/>. The row count is the picker's denominator and is
        /// always derived from this call — it differs per dialect and is never a literal.
        /// </para>
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

            return LinkAliases(entries);
        }

        /// <summary>
        /// The chip that reaches <paramref name="definition"/>. Delegates to
        /// <see cref="CategoryFor(KeyTable)"/> — the §3 sub-table is the only input.
        /// </summary>
        public static KeySearchCategory CategoryFor(KeyDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return CategoryFor(definition.Table);
        }

        /// <summary>
        /// Maps a §3 sub-table onto the token picker's chips. Five tables are named by a chip;
        /// every other table — including <see cref="KeyTable.None"/> and the two tables that never
        /// reach a catalog (<see cref="KeyTable.MacroTiming"/>,
        /// <see cref="KeyTable.EdgeZones"/>) — answers <see cref="KeySearchCategory.Other"/>,
        /// which <see cref="KeySearchCategory.All"/> still reaches. Nothing becomes unreachable.
        /// </summary>
        public static KeySearchCategory CategoryFor(KeyTable table)
        {
            return table switch
            {
                KeyTable.LettersAndDigits => KeySearchCategory.Letters,
                KeyTable.Navigation => KeySearchCategory.Nav,
                KeyTable.MediaKeys => KeySearchCategory.Media,
                KeyTable.MouseActions => KeySearchCategory.Mouse,
                KeyTable.ProfilesAndHotkeys => KeySearchCategory.Hotkeys,
                _ => KeySearchCategory.Other
            };
        }

        /// <summary>
        /// Where <paramref name="definition"/> lives on the board. Derived from
        /// <see cref="KeyDefinition.Table"/>; §3.11 is the one table that holds two things, and
        /// the <c>hk</c>/<c>pro</c> token stems the spec fixes for its rows separate them.
        /// </summary>
        public static KeySearchScope ScopeFor(KeyDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return definition.Table switch
            {
                KeyTable.KeypadKeys => KeySearchScope.Keypad,
                KeyTable.ProfilesAndHotkeys => ProfileOrHotkeyScope(definition),
                _ => KeySearchScope.None
            };
        }

        /// <summary>
        /// Whether duplicate registrations of <paramref name="definition"/> may be reported as
        /// aliases of one another. Every table but <see cref="KeyTable.MacroTiming"/> answers
        /// true, and §3.12 is the only place in the key table where a numeric code is registered
        /// twice at all.
        /// <para>
        /// The random delay <c>dran</c> and the generated 2 ms delay <c>d002</c> both carry code
        /// 10087, and the explicit <c>d125</c>/<c>d500</c> rows share their tokens with their
        /// generated twins. Those collisions are an artefact of the code space, not two names for
        /// one action: resolution there goes through the token and never the code, so calling
        /// <c>d002</c> an alias of <c>dran</c> would report a 2 ms delay as a random one. The
        /// whole table is excluded rather than the one pair, because the same hazard covers the
        /// shared tokens too.
        /// </para>
        /// <para>
        /// §3.12 rows are also flagged <see cref="KeyDefinitionFlags.HiddenFromSearch"/> and so
        /// never reach a catalog built by <see cref="Build"/>. The exclusion lives here rather
        /// than leaning on that, so <see cref="LinkAliases"/> is safe over any row list a caller
        /// hands it and the rule survives a change to the sentinel.
        /// </para>
        /// </summary>
        public static bool IsAliasable(KeyDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return definition.Table != KeyTable.MacroTiming;
        }

        /// <summary>
        /// Returns <paramref name="entries"/> with every row linked to the earliest row that
        /// resolves to the same action, so the picker can label the later ones "alias of …".
        /// <para>
        /// Two rows are the same action when the registry's first-match lookups cannot tell them
        /// apart (05 §7 — "duplicated codes and tokens are intentional"): they share a numeric
        /// <see cref="KeyDefinition.Code"/>, which <see cref="KeyRegistry.FindByCode"/> resolves
        /// to the earliest, or they share a file token, which
        /// <see cref="KeyRegistry.FindByToken(string?)"/> resolves to the earliest
        /// case-insensitively. Sharing is transitive, so a chain of duplicates all points at one
        /// canonical row. Rows <see cref="IsAliasable"/> rejects are always canonical and never
        /// draw others into a group.
        /// </para>
        /// <para>
        /// Input order is preserved and is the registration order §11.6's list is built in; the
        /// first row of a group is the canonical one.
        /// </para>
        /// </summary>
        public static IReadOnlyList<KeySearchEntry> LinkAliases(IReadOnlyList<KeySearchEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var linked = new KeySearchEntry[entries.Count];
            var canonicalByCode = new Dictionary<int, int>(entries.Count);
            var canonicalByToken = new Dictionary<string, int>(entries.Count, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];

                if (!IsAliasable(entry.Definition))
                {
                    linked[index] = entry with { AliasOf = null };
                    continue;
                }

                var canonicalIndex = FindCanonicalIndex(entry, canonicalByCode, canonicalByToken);

                if (canonicalIndex < 0)
                {
                    linked[index] = entry with { AliasOf = null };
                    canonicalIndex = index;
                }
                else
                {
                    linked[index] = entry with { AliasOf = linked[canonicalIndex] };
                }

                canonicalByCode.TryAdd(entry.Definition.Code, canonicalIndex);
                canonicalByToken.TryAdd(entry.FileToken, canonicalIndex);
            }

            return new ReadOnlyCollection<KeySearchEntry>(linked);
        }

        /// <summary>
        /// Composes the item text of §11.6 and classifies the row. The key table carries no
        /// <c>SearchText</c> column (05 §1.1 documents it on the legacy model only), so the
        /// entry's dialect-neutral caption plays the search name and the dialect caption plays the
        /// display text — the two differ exactly where a §3 table gives a per-dialect override.
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

            return new KeySearchEntry(definition, searchName, token, text.ToString())
            {
                Category = CategoryFor(definition.Table),
                Scope = ScopeFor(definition)
            };
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

        /// <summary>
        /// Splits §3.11 into the board's own hotkeys and the profile selectors. The table mixes
        /// them, so the token stem decides: <c>hk0</c>..<c>hk10</c> against
        /// <c>pro0</c>..<c>pro9</c>. Every dialect names these rows identically, so any non-empty
        /// token answers.
        /// </summary>
        private static KeySearchScope ProfileOrHotkeyScope(KeyDefinition definition)
        {
            if (HasTokenStem(definition, HotkeyTokenPrefix))
            {
                return KeySearchScope.DeviceHotkey;
            }

            return HasTokenStem(definition, ProfileTokenPrefix) ? KeySearchScope.Profile : KeySearchScope.None;
        }

        private static bool HasTokenStem(KeyDefinition definition, string stem)
        {
            return StartsWith(definition.LegacyToken, stem)
                   || StartsWith(definition.Gen1Token, stem)
                   || StartsWith(definition.Gen2Token, stem);
        }

        private static bool StartsWith(string token, string stem)
        {
            return token.Length > 0 && token.StartsWith(stem, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the index of the earliest already-seen row resolving to the same action, or -1
        /// when this row starts a group. Both lookup axes are consulted and the earlier wins, so a
        /// row bridging two groups collapses them onto one canonical.
        /// </summary>
        private static int FindCanonicalIndex(
            KeySearchEntry entry,
            Dictionary<int, int> canonicalByCode,
            Dictionary<string, int> canonicalByToken)
        {
            var canonicalIndex = -1;

            if (canonicalByCode.TryGetValue(entry.Definition.Code, out var byCode))
            {
                canonicalIndex = byCode;
            }

            if (canonicalByToken.TryGetValue(entry.FileToken, out var byToken)
                && (canonicalIndex < 0 || byToken < canonicalIndex))
            {
                canonicalIndex = byToken;
            }

            return canonicalIndex;
        }

        private static bool Equal(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
