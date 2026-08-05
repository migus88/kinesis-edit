using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Token → key-table resolution for layout files (specs/04-layout-file-format.md §1.4):
    /// case-insensitive, the parser's dialect first, then any dialect — the tolerant-read rule.
    /// The cross-dialect fallback is what accepts documented older spellings (the Advantage2
    /// <c>kp/</c>/<c>kp*</c>/<c>kp-</c>/<c>kp+</c> value spellings of specs/05-key-model.md
    /// §3.6 are the Gen1/Gen2 tokens of the same keys) and the generic modifier tokens that
    /// exist only in the Legacy table yet appear in Gen1 macro values (06 §7.3). Writing always
    /// goes through the canonical current-dialect token instead.
    /// </summary>
    internal static class LayoutTokenResolver
    {
        /// <summary>First generated <c>dNNN</c> delay code minus its millisecond count (05 §3.12: <c>d001</c> = 10086).</summary>
        private const int GeneratedDelayCodeBase = 10085;

        /// <summary>The two legacy delay tokens that shadow their generated twins (05 §3.12).</summary>
        private const string LegacyDelay125 = "d125";
        private const string LegacyDelay500 = "d500";

        /// <summary>Resolves a token in <paramref name="dialect"/> first, then in any dialect; null when unknown.</summary>
        public static KeyDefinition? Resolve(string token, TokenDialect dialect)
        {
            return KeyRegistry.FindByToken(token, dialect) ?? KeyRegistry.FindByToken(token);
        }

        /// <summary>
        /// Resolves a macro-value token (06 §2.2). On the RGB family <c>d125</c>/<c>d500</c>
        /// resolve to the generated <c>dNNN</c> keys instead of the distinct legacy delay keys —
        /// registry first-match always yields the legacy rows (they shadow their generated
        /// twins, 05 §3.12), so the generated codes are computed as 10085 + N here.
        /// </summary>
        public static KeyDefinition? ResolveMacroValueToken(string token, LayoutDialectRules rules)
        {
            if (rules.ResolvesLegacyDelaysAsGenerated && token is LegacyDelay125 or LegacyDelay500)
            {
                var milliseconds = int.Parse(token[1..], System.Globalization.CultureInfo.InvariantCulture);

                return KeyRegistry.FindByCode(GeneratedDelayCodeBase + milliseconds);
            }

            return Resolve(token, rules.Tokens);
        }

        /// <summary>
        /// The token written for a key in <paramref name="dialect"/>, falling back to the first
        /// dialect that names it (the renderer's rule: generic modifiers exist only in the
        /// Legacy table yet appear in Gen1 values, 06 §7.3). Empty when the key has no file
        /// token anywhere (05 §3.9, the Advantage2 Program button), in which case nothing is
        /// written.
        /// </summary>
        public static string GetWriteToken(KeyDefinition key, TokenDialect dialect)
        {
            var token = key.GetToken(dialect);

            if (token.Length > 0)
            {
                return token;
            }

            if (key.LegacyToken.Length > 0)
            {
                return key.LegacyToken;
            }

            return key.Gen1Token.Length > 0 ? key.Gen1Token : key.Gen2Token;
        }
    }
}
