using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// The master key-token registry — the single source of truth linking numeric key codes,
    /// per-dialect file tokens, and display captions (specs/05-key-model.md §3, §7).
    /// <see cref="Entries"/> reproduces the spec's registration order (tables 3.1 → 3.13, row
    /// order preserved), and lookups follow the legacy first-match semantics (§1.5): duplicated
    /// codes and tokens are intentional and always resolve to the earliest registration.
    /// </summary>
    public static partial class KeyRegistry
    {
        /// <summary>
        /// Every key-table entry in spec registration order: tables 3.1 → 3.13, row order
        /// preserved within each table.
        /// </summary>
        public static IReadOnlyList<KeyDefinition> Entries { get; }

        /// <summary>
        /// Pedal position tokens (specs/05-key-model.md §3.14). These are plain string tokens
        /// used by the pedal app, explicitly not entries of the key table.
        /// </summary>
        public static IReadOnlyList<string> PedalPositionTokens { get; }

        private static readonly Dictionary<int, KeyDefinition> _entriesByCode;
        private static readonly Dictionary<string, KeyDefinition> _entriesByToken;
        private static readonly Dictionary<TokenDialect, Dictionary<string, KeyDefinition>> _entriesByDialectToken;

        static KeyRegistry()
        {
            var entries = BuildEntries();

            Entries = new ReadOnlyCollection<KeyDefinition>(entries);
            PedalPositionTokens = new ReadOnlyCollection<string>(new[]
            {
                "lpedal",
                "mpedal",
                "rpedal",
                "jack1",
                "jack2",
                "jack3",
                "jack4"
            });

            _entriesByCode = BuildCodeIndex(entries);
            _entriesByToken = BuildTokenIndex(entries);
            _entriesByDialectToken = BuildDialectTokenIndexes(entries);
        }

        /// <summary>
        /// Returns the first entry (in spec registration order) with the given numeric code,
        /// or null when no entry has it. Duplicated codes are intentional (§7).
        /// </summary>
        public static KeyDefinition? FindByCode(int code)
        {
            return _entriesByCode.GetValueOrDefault(code);
        }

        /// <summary>
        /// Returns the first entry (in spec registration order) whose file token in any dialect
        /// matches <paramref name="token"/> case-insensitively, or null. Null, empty, and
        /// whitespace queries never match anything.
        /// </summary>
        public static KeyDefinition? FindByToken(string? token)
        {
            return FindByToken(token, TokenDialect.None);
        }

        /// <summary>
        /// Returns the first entry whose file token in <paramref name="dialect"/> matches
        /// <paramref name="token"/> case-insensitively, or null. Passing
        /// <see cref="TokenDialect.None"/> searches all dialects.
        /// </summary>
        public static KeyDefinition? FindByToken(string? token, TokenDialect dialect)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            if (dialect == TokenDialect.None)
            {
                return _entriesByToken.GetValueOrDefault(token);
            }

            return _entriesByDialectToken.TryGetValue(dialect, out var index)
                ? index.GetValueOrDefault(token)
                : null;
        }

        private static List<KeyDefinition> BuildEntries()
        {
            var entries = new List<KeyDefinition>(1300);

            AddLettersAndDigits(entries);
            AddPunctuationKeys(entries);
            AddNavigationKeys(entries);
            AddFunctionKeys(entries);
            AddModifierKeys(entries);
            AddKeypadKeys(entries);
            AddMediaKeys(entries);
            AddMouseKeys(entries);
            AddSpecialActionKeys(entries);
            AddLayerKeys(entries);
            AddProfileAndHotkeyKeys(entries);
            AddMacroTimingKeys(entries);
            AddEdgeZoneKeys(entries);

            return entries;
        }

        private static Dictionary<int, KeyDefinition> BuildCodeIndex(List<KeyDefinition> entries)
        {
            var index = new Dictionary<int, KeyDefinition>(entries.Count);

            foreach (var entry in entries)
            {
                index.TryAdd(entry.Code, entry);
            }

            return index;
        }

        private static Dictionary<string, KeyDefinition> BuildTokenIndex(List<KeyDefinition> entries)
        {
            var index = new Dictionary<string, KeyDefinition>(entries.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                TryIndexToken(index, entry.LegacyToken, entry);
                TryIndexToken(index, entry.Gen1Token, entry);
                TryIndexToken(index, entry.Gen2Token, entry);
            }

            return index;
        }

        private static Dictionary<TokenDialect, Dictionary<string, KeyDefinition>> BuildDialectTokenIndexes(
            List<KeyDefinition> entries)
        {
            var indexes = new Dictionary<TokenDialect, Dictionary<string, KeyDefinition>>
            {
                [TokenDialect.Legacy] = new(entries.Count, StringComparer.OrdinalIgnoreCase),
                [TokenDialect.Gen1] = new(entries.Count, StringComparer.OrdinalIgnoreCase),
                [TokenDialect.Gen2] = new(entries.Count, StringComparer.OrdinalIgnoreCase)
            };

            foreach (var entry in entries)
            {
                foreach (var (dialect, index) in indexes)
                {
                    TryIndexToken(index, entry.GetToken(dialect), entry);
                }
            }

            return indexes;
        }

        private static void TryIndexToken(Dictionary<string, KeyDefinition> index, string token, KeyDefinition entry)
        {
            if (token.Length == 0)
            {
                return;
            }

            index.TryAdd(token, entry);
        }

        private static void AddUniformKey(
            List<KeyDefinition> entries,
            KeyTable table,
            int code,
            string token,
            string displayText = "",
            TokenDialects dialects = TokenDialects.All,
            KeyDefinitionFlags flags = KeyDefinitionFlags.None)
        {
            entries.Add(new KeyDefinition
            {
                Code = code,
                Table = table,
                Dialects = dialects,
                LegacyToken = (dialects & TokenDialects.Legacy) != 0 ? token : string.Empty,
                Gen1Token = (dialects & TokenDialects.Gen1) != 0 ? token : string.Empty,
                Gen2Token = (dialects & TokenDialects.Gen2) != 0 ? token : string.Empty,
                DisplayText = displayText,
                Flags = flags
            });
        }

        private static void AddDialectKey(
            List<KeyDefinition> entries,
            KeyTable table,
            int code,
            string legacyToken,
            string gen1Token,
            string gen2Token,
            string displayText = "",
            TokenDialects dialects = TokenDialects.All,
            KeyDefinitionFlags flags = KeyDefinitionFlags.None)
        {
            entries.Add(new KeyDefinition
            {
                Code = code,
                Table = table,
                Dialects = dialects,
                LegacyToken = (dialects & TokenDialects.Legacy) != 0 ? legacyToken : string.Empty,
                Gen1Token = (dialects & TokenDialects.Gen1) != 0 ? gen1Token : string.Empty,
                Gen2Token = (dialects & TokenDialects.Gen2) != 0 ? gen2Token : string.Empty,
                DisplayText = displayText,
                Flags = flags
            });
        }

        private static void AddUniformGlyphKey(
            List<KeyDefinition> entries,
            KeyTable table,
            int code,
            string token,
            string glyphText,
            string displayText,
            TokenDialects dialects = TokenDialects.All)
        {
            entries.Add(new KeyDefinition
            {
                Code = code,
                Table = table,
                Dialects = dialects,
                LegacyToken = (dialects & TokenDialects.Legacy) != 0 ? token : string.Empty,
                Gen1Token = (dialects & TokenDialects.Gen1) != 0 ? token : string.Empty,
                Gen2Token = (dialects & TokenDialects.Gen2) != 0 ? token : string.Empty,
                GlyphText = glyphText,
                DisplayText = displayText
            });
        }
    }
}
