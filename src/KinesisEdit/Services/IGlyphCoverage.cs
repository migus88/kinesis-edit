namespace KinesisEdit.Services
{
    /// <summary>
    /// Answers whether the app's own type can actually print a string, so a caption rule can
    /// prefer a glyph where it renders and fall back to words where it would draw tofu.
    /// <para>
    /// specs/05-key-model.md §3.7/§6 gate the glyph captions on <em>capability</em> — the legacy
    /// app used them "only on Windows 10 and later; otherwise plain text fallbacks", and swapped
    /// in <c>'Cambria Math'</c> where a face fell short. This app embeds its own fonts and forbids
    /// that swap, so the honest translation of that gate is font capability rather than OS
    /// version: can the families this app ships print it?
    /// </para>
    /// <para>
    /// Abstracted so the rule that consumes it stays a pure function of its arguments and can be
    /// tested in both directions without a UI toolkit — nothing outside an implementation of this
    /// interface knows that the answer comes from a font at all.
    /// </para>
    /// </summary>
    public interface IGlyphCoverage
    {
        /// <summary>
        /// Whether every non-whitespace character of <paramref name="text"/> resolves to a real
        /// glyph. Whitespace is skipped (a caption's <c>\n</c> is a line break, not a printed
        /// character), and an empty or null string is trivially printable.
        /// </summary>
        bool CanPrint(string? text);
    }
}
