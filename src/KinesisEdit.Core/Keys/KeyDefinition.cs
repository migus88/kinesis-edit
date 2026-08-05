namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// One immutable entry of the master key-token table (specs/05-key-model.md §3): a numeric
    /// key code (Windows virtual-key code below 256 or legacy internal code ≥ 10000, §2), the
    /// per-dialect file tokens in canonical casing, and the display data the spec tables carry.
    /// Only the file tokens ever appear on disk.
    /// </summary>
    public sealed record KeyDefinition
    {
        /// <summary>Numeric key code (§2); duplicated codes across entries are intentional (§7).</summary>
        public required int Code { get; init; }

        /// <summary>The §3 sub-table this entry comes from.</summary>
        public required KeyTable Table { get; init; }

        /// <summary>Dialects the entry exists in (the spec's Family column, §3 legend).</summary>
        public required TokenDialects Dialects { get; init; }

        /// <summary>Legacy-dialect file token (Advantage2/Pedal); empty when absent.</summary>
        public string LegacyToken { get; init; } = string.Empty;

        /// <summary>Gen1-dialect file token (FS Edge/Pro, Edge RGB, TKO); empty when absent.</summary>
        public string Gen1Token { get; init; } = string.Empty;

        /// <summary>Gen2-dialect file token (Advantage360); empty when absent.</summary>
        public string Gen2Token { get; init; } = string.Empty;

        /// <summary>Default key-cap caption; <c>\n</c> produces a two-line caption (§1.1).</summary>
        public string DisplayText { get; init; } = string.Empty;

        /// <summary>Legacy-dialect caption override; empty means <see cref="DisplayText"/> applies.</summary>
        public string LegacyDisplayText { get; init; } = string.Empty;

        /// <summary>Gen1-dialect caption override; empty means <see cref="DisplayText"/> applies.</summary>
        public string Gen1DisplayText { get; init; } = string.Empty;

        /// <summary>Gen2-dialect caption override; empty means <see cref="DisplayText"/> applies.</summary>
        public string Gen2DisplayText { get; init; } = string.Empty;

        /// <summary>macOS caption override (§3.3/§3.5); empty means <see cref="DisplayText"/> applies.</summary>
        public string MacDisplayText { get; init; } = string.Empty;

        /// <summary>Character produced with Shift held (§1.1), e.g. <c>!</c> for <c>1</c>.</summary>
        public string ShiftedValue { get; init; } = string.Empty;

        /// <summary>
        /// Unicode glyph caption used on Windows 10+ (§3.7/§3.9); <see cref="DisplayText"/> holds
        /// the plain-text fallback. Empty when the entry has no glyph.
        /// </summary>
        public string GlyphText { get; init; } = string.Empty;

        /// <summary>Behavior flags the spec tables mark (§1.1, §3 notes).</summary>
        public KeyDefinitionFlags Flags { get; init; } = KeyDefinitionFlags.None;

        /// <summary>
        /// Returns the file token for <paramref name="dialect"/>, or an empty string when the
        /// entry has no token in that dialect.
        /// </summary>
        public string GetToken(TokenDialect dialect)
        {
            return dialect switch
            {
                TokenDialect.Legacy => LegacyToken,
                TokenDialect.Gen1 => Gen1Token,
                TokenDialect.Gen2 => Gen2Token,
                _ => string.Empty
            };
        }

        /// <summary>Returns whether the entry has a non-empty file token in <paramref name="dialect"/>.</summary>
        public bool HasToken(TokenDialect dialect)
        {
            return GetToken(dialect).Length > 0;
        }

        /// <summary>Returns whether the entry exists in <paramref name="dialect"/> (Family column).</summary>
        public bool IsAvailableIn(TokenDialect dialect)
        {
            var flag = dialect switch
            {
                TokenDialect.Legacy => TokenDialects.Legacy,
                TokenDialect.Gen1 => TokenDialects.Gen1,
                TokenDialect.Gen2 => TokenDialects.Gen2,
                _ => TokenDialects.None
            };

            return flag != TokenDialects.None && (Dialects & flag) == flag;
        }

        /// <summary>
        /// Returns the caption for <paramref name="dialect"/>: the dialect override when the
        /// spec table carries one, otherwise <see cref="DisplayText"/>.
        /// </summary>
        public string GetDisplayText(TokenDialect dialect)
        {
            var overrideText = dialect switch
            {
                TokenDialect.Legacy => LegacyDisplayText,
                TokenDialect.Gen1 => Gen1DisplayText,
                TokenDialect.Gen2 => Gen2DisplayText,
                _ => string.Empty
            };

            return overrideText.Length > 0 ? overrideText : DisplayText;
        }
    }
}
