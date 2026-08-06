using System.Globalization;

namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// Every advisory sentence the app says, in one place. The wording is the design's, so it has
    /// to live once and be assertable on its own: a sentence built inline in a view model is a
    /// sentence that drifts from <c>docs/design/mockups.md</c> the first time somebody edits the
    /// surrounding code.
    /// <para>
    /// Two things about the copy are deliberate. Numbers are grouped with a <b>space</b>
    /// (<c>5 140 / 7 200</c>, mockup 1i) rather than with the invariant culture's comma, and the
    /// reassurance clauses — <see cref="OlderFirmwareReassurance"/>, <see cref="SavedAsIs"/>,
    /// <see cref="DuplicatesAreAllowed"/> — are fixed strings appended to the variable part, so
    /// "nothing is blocked" is said the same way every time it is said at all.
    /// </para>
    /// </summary>
    public static class AdvisoryText
    {
        /// <summary>Thousands separator of every count in an advisory — a space, per mockup 1i.</summary>
        public const string GroupSeparator = " ";

        /// <summary>Closes a layout advisory (mockup 1e, verbatim).</summary>
        public const string OlderFirmwareReassurance =
            "Files from older firmware can exceed today's limits; nothing is blocked.";

        /// <summary>Closes a budget advisory (mockup 1i, verbatim).</summary>
        public const string SavedAsIs = "Saved as-is.";

        /// <summary>Closes a duplicate-key advisory (mockup 1e, verbatim).</summary>
        public const string DuplicatesAreAllowed = "Duplicates are allowed.";

        /// <summary>Closes a section summary that is not the layout's own.</summary>
        public const string NothingIsBlocked = "Nothing is blocked.";

        /// <summary>Caption of the summary strip's action; the count follows it (mockup 1e: "Review 3").</summary>
        public const string ReviewPrefix = "Review";

        /// <summary>
        /// The invariant number format with the group separator swapped for a space. Built once and
        /// read-only: <see cref="NumberFormatInfo.ReadOnly"/> is what makes it safe to share.
        /// </summary>
        private static NumberFormatInfo GroupedFormat { get; } = CreateGroupedFormat();

        /// <summary>
        /// The strip's sentence on the Layout tab (mockup 1e, verbatim):
        /// <c>"3 keys carry advisory notes on this layer — tap-and-hold count is 11 of 10. Files
        /// from older firmware can exceed today's limits; nothing is blocked."</c>
        /// </summary>
        public static string LayerSummary(int keyCount, string detail)
        {
            ArgumentNullException.ThrowIfNull(detail);

            var subject = keyCount == 1
                ? "1 key carries advisory notes on this layer"
                : Number(keyCount) + " keys carry advisory notes on this layer";

            return $"{subject} — {detail}. {OlderFirmwareReassurance}";
        }

        /// <summary>
        /// The strip's sentence on every other tab. The mockups give no wording for these, so this
        /// is the app's own — built to the same shape as <see cref="LayerSummary"/> so the strip
        /// reads the same wherever it appears.
        /// </summary>
        public static string SectionSummary(int count, string detail)
        {
            ArgumentNullException.ThrowIfNull(detail);

            var subject = count == 1
                ? "1 advisory note in this section"
                : Number(count) + " advisory notes in this section";

            return $"{subject} — {detail}. {NothingIsBlocked}";
        }

        /// <summary>The tap-and-hold count as a mid-sentence fragment: "tap-and-hold count is 11 of 10".</summary>
        public static string TapAndHoldCountDetail(int actual, int limit)
        {
            return $"tap-and-hold count is {Number(actual)} of {Number(limit)}";
        }

        /// <summary>The tap-and-hold count as its own sentence, reassurance included.</summary>
        public static string TapAndHoldCount(int actual, int limit)
        {
            return $"Tap-and-hold count is {Number(actual)} of {Number(limit)}. {OlderFirmwareReassurance}";
        }

        /// <summary>The per-macro budget (mockup 1i, verbatim): "512 of 500 characters — over the device budget. Saved as-is."</summary>
        public static string MacroCharacters(int actual, int limit)
        {
            return $"{MacroCharactersDetail(actual, limit)}. {SavedAsIs}";
        }

        /// <summary>The same as a fragment: "512 of 500 characters — over the device budget".</summary>
        public static string MacroCharactersDetail(int actual, int limit)
        {
            return $"{Number(actual)} of {Number(limit)} characters — over the device budget";
        }

        /// <summary>The layout keystroke budget in mockup 1i's meter form, as a sentence.</summary>
        public static string LayoutKeystrokeBudget(int actual, int limit)
        {
            return $"{LayoutKeystrokeBudgetDetail(actual, limit)}. {SavedAsIs}";
        }

        /// <summary>The same as a fragment: "layout keystroke budget 5 140 / 7 200 — over the device budget".</summary>
        public static string LayoutKeystrokeBudgetDetail(int actual, int limit)
        {
            return $"layout keystroke budget {Number(actual)} / {Number(limit)} — over the device budget";
        }

        /// <summary>The co-trigger budget (mockup 1i, verbatim): "6 co-trigger modifiers — legacy budget is 4".</summary>
        public static string CoTriggers(int actual, int limit)
        {
            return $"{Number(actual)} co-trigger modifiers — legacy budget is {Number(limit)}";
        }

        /// <summary>
        /// A token the user's edits left on more than one position of a layer
        /// (<see cref="Core.Model.DuplicateKeyScan"/>). The token is bracketed the way the mockups
        /// spell a config value (<c>[esc]</c>) and the positions are named by index, which is what
        /// the app knows about a position — the mockups' "top-left position" is a phrase no data in
        /// this app can produce.
        /// </summary>
        public static string DuplicateKey(string token, IReadOnlyList<int> keyIndexes)
        {
            ArgumentNullException.ThrowIfNull(keyIndexes);

            return $"{DuplicateKeyDetail(token, keyIndexes)} of this layer — {JoinIndexes(keyIndexes)}. "
                + DuplicatesAreAllowed;
        }

        /// <summary>
        /// The same as a fragment: "[esc] is on 2 positions". It says <em>less</em> than the whole
        /// sentence on purpose — a summary already opens with "…on this layer", and repeating the
        /// phrase reads as a stutter on the strip.
        /// </summary>
        public static string DuplicateKeyDetail(string token, IReadOnlyList<int> keyIndexes)
        {
            ArgumentNullException.ThrowIfNull(token);
            ArgumentNullException.ThrowIfNull(keyIndexes);

            return $"[{token}] is on {Number(keyIndexes.Count)} positions";
        }

        /// <summary>The strip's action caption: "Review 3".</summary>
        public static string Review(int count)
        {
            return $"{ReviewPrefix} {Number(count)}";
        }

        /// <summary>
        /// What the post-save toast adds. A save with advisories still succeeds — that is the whole
        /// point of the law — so this reports what went to the drive, it does not warn about it.
        /// </summary>
        public static string SavedWithAdvisories(int count)
        {
            return count == 1
                ? "Saved with 1 advisory."
                : $"Saved with {Number(count)} advisories.";
        }

        /// <summary>Formats a count the way every advisory does: grouped with a space.</summary>
        public static string Number(int value)
        {
            return value.ToString("#,0", GroupedFormat);
        }

        private static NumberFormatInfo CreateGroupedFormat()
        {
            var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();

            format.NumberGroupSeparator = GroupSeparator;

            return NumberFormatInfo.ReadOnly(format);
        }

        private static string JoinIndexes(IReadOnlyList<int> keyIndexes)
        {
            var parts = new string[keyIndexes.Count];

            for (var index = 0; index < keyIndexes.Count; index++)
            {
                parts[index] = keyIndexes[index].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(", ", parts);
        }
    }
}
