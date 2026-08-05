using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The one rule for what a key cap says, kept here so it is testable on its own rather than
    /// buried in a view model. <see cref="KeyDefinition.GetDisplayText"/> resolves the per-dialect
    /// caption override only — it deliberately knows nothing about glyphs or macOS — so the two
    /// remaining columns of specs/05-key-model.md §3 are layered on top here, in this order:
    /// <list type="number">
    /// <item><see cref="KeyDefinition.GlyphText"/> when the entry has one (§3.7/§3.9).</item>
    /// <item><see cref="KeyDefinition.MacDisplayText"/> when running on macOS (§3.3/§3.5).</item>
    /// <item><see cref="KeyDefinition.GetDisplayText"/> for the device's dialect.</item>
    /// <item>
    /// The dialect's file token, when every source above is blank. Nine of the eleven Freestyle
    /// hotkeys (<c>hk0</c>-<c>hk8</c>) carry <c>' '</c> as their display text in §3.11 — the
    /// physical caps are unlabelled — so rendering that verbatim would draw a column of
    /// indistinguishable empty caps. The token is what the user sees for that position in the
    /// layout file, which makes it the honest fallback rather than an invented label.
    /// </item>
    /// </list>
    /// A <c>\n</c> in the caption is a two-line key cap (§1.1) and is passed through verbatim:
    /// splitting it is the view's job.
    /// </summary>
    public static class KeyCaption
    {
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
        public static string ForKey(KeyboardKey key, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(key);

            return For(key.ModifiedOrOriginalKey, dialect, IsMacOs);
        }

        /// <summary>Applies the three-step rule above to one key-table entry.</summary>
        public static string For(KeyDefinition key, TokenDialect dialect, bool isMacOs)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (key.GlyphText.Length > 0)
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

            return key.GetToken(dialect);
        }
    }
}
