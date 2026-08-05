namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The 4-character multi-modifier combination codes of specs/05-key-model.md §5.7 and
    /// specs/04-layout-file-format.md §2.3: positional letters c=Ctrl, a=Alt, w=Win, s=Shift in
    /// that fixed order, with "x" marking an omitted modifier. Exactly these 11 codes — every
    /// combination of two or more modifiers — are accepted, case-insensitively.
    /// </summary>
    public static class MultiModifierCodes
    {
        /// <summary>Length of every multi-modifier code (§5.7).</summary>
        public const int CodeLength = 4;

        /// <summary>The 11 accepted codes in spec order, in the canonical lowercase casing (§5.7).</summary>
        public static IReadOnlyList<string> All { get; } =
        [
            "caws",
            "cawx",
            "cxws",
            "caxs",
            "xaws",
            "caxx",
            "cxwx",
            "cxxs",
            "xawx",
            "xaxs",
            "xxws"
        ];

        /// <summary>
        /// Whether <paramref name="code"/> is one of the 11 accepted codes, ignoring case (§5.7).
        /// This is the rule-type test of 04 §1.3: a single-key line is a multi-modifier line exactly
        /// when its value side is one of these codes.
        /// </summary>
        public static bool IsValid(string? code)
        {
            return TryNormalize(code, out _);
        }

        /// <summary>
        /// Normalises <paramref name="code"/> to the canonical lowercase spelling stored on a key,
        /// or fails for anything outside the 11 accepted codes (§5.7). A single modifier is not a
        /// multi-modifier code — it is an ordinary remap (04 §2.3).
        /// </summary>
        public static bool TryNormalize(string? code, out string normalized)
        {
            normalized = string.Empty;

            if (code is null || code.Length != CodeLength)
            {
                return false;
            }

            foreach (var accepted in All)
            {
                if (!string.Equals(accepted, code, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                normalized = accepted;
                return true;
            }

            return false;
        }
    }
}
