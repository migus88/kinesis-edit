using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The one rule for what a key cap says, kept here so it is testable on its own rather than
    /// buried in a view model. <see cref="KeyDefinition.GetDisplayText"/> resolves the per-dialect
    /// caption override only — it deliberately knows nothing about glyphs or macOS — so the two
    /// remaining columns of specs/05-key-model.md §3 are layered on top here, in this order:
    /// <list type="number">
    /// <item>
    /// <see cref="KeyDefinition.GlyphText"/> (§3.7/§3.9), <b>but only when the app's own type can
    /// print it</b>. §3.7 and §6 gate the glyph column on capability — the legacy app drew it
    /// "only on Windows 10 and later; otherwise plain text fallbacks" — and this app's capability
    /// is its embedded families, not an OS version. All 17 distinct glyph values are missing from
    /// every embedded IBM Plex face today, so in practice the whole column falls through; a glyph
    /// that <em>is</em> covered is still preferred, because this is a capability gate and not a
    /// ban.
    /// </item>
    /// <item><see cref="KeyDefinition.MacDisplayText"/> when running on macOS (§3.3/§3.5).</item>
    /// <item><see cref="KeyDefinition.GetDisplayText"/> for the device's dialect.</item>
    /// <item>
    /// <b>On the dropped-glyph path only:</b> the entry's caption in another dialect, when its own
    /// dialect has none. Exactly one entry needs it — code 10019 (<c>[null]</c>, glyph <c>⊗</c>)
    /// carries <c>' '</c> as its plain caption and <c>NUL</c> only as its Gen2 override, so a
    /// Legacy or Gen1 cap would otherwise fall past the blank check onto the literal token
    /// <c>null</c>, which reads as a bug. §3.9's own non-Gen2 fallback is the blank, which is
    /// spec-faithful and indistinguishable from an unassigned key, so the Gen2 word is borrowed
    /// for all three dialects instead.
    /// </item>
    /// <item>
    /// The dialect's file token, when every source above is blank. Nine of the eleven Freestyle
    /// hotkeys (<c>hk0</c>-<c>hk8</c>) carry <c>' '</c> as their display text in §3.11 — the
    /// physical caps are unlabelled — so rendering that verbatim would draw a column of
    /// indistinguishable empty caps. The token is what the user sees for that position in the
    /// layout file, which makes it the honest fallback rather than an invented label. Those nine
    /// carry no glyph, which is why step 4 is scoped to entries that do: it must not divert them.
    /// </item>
    /// </list>
    /// A <c>\n</c> in the caption is a two-line key cap (§1.1) and is passed through verbatim:
    /// splitting it is the view's job.
    /// </summary>
    public static class KeyCaption
    {
        /// <summary>The dialects step 4 may borrow a caption from, in registration order.</summary>
        private static readonly TokenDialect[] BorrowableDialects =
        [
            TokenDialect.Legacy,
            TokenDialect.Gen1,
            TokenDialect.Gen2
        ];

        /// <summary>
        /// Whether this process runs on macOS, resolved once. The BCL check is used rather than a
        /// service because the answer cannot change while the app runs; every rule that consumes it
        /// takes it as a parameter so it stays testable in both directions.
        /// </summary>
        public static bool IsMacOs { get; } = OperatingSystem.IsMacOS();

        /// <summary>
        /// The caption of <paramref name="key"/> in its current state: its remapped action when it
        /// carries one, otherwise its factory default
        /// (<see cref="KeyboardKey.ModifiedOrOriginalKey"/>).
        /// </summary>
        public static string ForKey(KeyboardKey key, TokenDialect dialect, IGlyphCoverage coverage)
        {
            ArgumentNullException.ThrowIfNull(key);

            return For(key.ModifiedOrOriginalKey, dialect, IsMacOs, coverage);
        }

        /// <summary>
        /// Applies the rule above to one key-table entry. <paramref name="coverage"/> is taken as a
        /// parameter for the same reason <paramref name="isMacOs"/> is: this stays a pure function
        /// of its arguments, so both directions of both capabilities are testable on one machine
        /// and with no UI toolkit running.
        /// </summary>
        public static string For(KeyDefinition key, TokenDialect dialect, bool isMacOs, IGlyphCoverage coverage)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(coverage);

            var hasGlyph = key.GlyphText.Length > 0;

            if (hasGlyph && coverage.CanPrint(key.GlyphText))
            {
                return key.GlyphText;
            }

            if (isMacOs && key.MacDisplayText.Length > 0)
            {
                return key.MacDisplayText;
            }

            var displayText = key.GetDisplayText(dialect);

            if (!string.IsNullOrWhiteSpace(displayText))
            {
                return displayText;
            }

            if (hasGlyph && TryBorrowDisplayText(key, dialect, out var borrowed))
            {
                return borrowed;
            }

            return key.GetToken(dialect);
        }

        /// <summary>
        /// The entry's caption in the first other dialect that spells one. Reached only when the
        /// glyph was dropped and the device's own dialect left the cap blank.
        /// </summary>
        private static bool TryBorrowDisplayText(KeyDefinition key, TokenDialect dialect, out string borrowed)
        {
            foreach (var candidate in BorrowableDialects)
            {
                if (candidate == dialect)
                {
                    continue;
                }

                var displayText = key.GetDisplayText(candidate);

                if (!string.IsNullOrWhiteSpace(displayText))
                {
                    borrowed = displayText;

                    return true;
                }
            }

            borrowed = string.Empty;

            return false;
        }
    }
}
