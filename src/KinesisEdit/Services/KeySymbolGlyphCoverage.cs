using System.Collections.Concurrent;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The coverage probe for the <b>key-symbol family</b> — <c>FontKeySymbols</c> in
    /// <c>Themes/Typography.axaml</c>, the 19-codepoint subset that carries the characters naming a
    /// physical key (<c>⇧ ⌃ ⌥ ⌘ ⌫ ⏎ ␣ …</c>).
    /// <para>
    /// <b>It is deliberately a second probe rather than a third family inside
    /// <see cref="EmbeddedFontGlyphCoverage"/>, and that separation is the whole point.</b>
    /// <see cref="EmbeddedFontGlyphCoverage.CanPrint"/> means "<em>both</em> IBM Plex families
    /// carry this rune at every scale weight", which is what stops a character reaching a caption
    /// that cannot draw it. Adding this family to that probe's list would make it answer <c>true</c>
    /// for <c>⌘</c> and let the character into a sans caption which still cannot draw it — the
    /// gate would keep its name and lose its meaning. The two answer different questions:
    /// <em>can the app's body type print this?</em> and <em>can the mark face print this?</em>
    /// </para>
    /// <para>
    /// The caching idiom below is copied from that class rather than shared with it, on purpose:
    /// the shared part is a dozen lines, the divergent part is the family set and the weight set,
    /// and the two must be free to disagree — this face ships one weight, that one is probed at
    /// three. Every constraint the older probe pays for is preserved here, because each was
    /// bought the hard way:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Cache the per-codepoint <c>bool</c>, never the
    /// <see cref="IGlyphTypeface"/>.</b> A typeface wraps a native handle owned by the running
    /// <see cref="Application"/>; a cached one outlives the session that opened it and takes the
    /// test host down natively on the next read, with no managed stack.</description></item>
    /// <item><description><b>Resolve the face lazily, never in a static initializer.</b> There is
    /// no application while the type is loading.</description></item>
    /// <item><description><b>Answer "not covered" rather than throwing</b> when there is no
    /// <see cref="Application.Current"/> or the call is off the UI thread — asking an application
    /// for a resource walks its <c>Styles</c>, and every <c>AvaloniaObject</c> on that path
    /// verifies dispatcher access — and <b>do not cache that answer</b>, so a later probe on the UI
    /// thread still reads the real font.</description></item>
    /// </list>
    /// </summary>
    public sealed class KeySymbolGlyphCoverage : IGlyphCoverage
    {
        /// <summary>Resource key of the embedded key-symbol family (<c>Themes/Typography.axaml</c>).</summary>
        private const string KeySymbolFamilyKey = "FontKeySymbols";

        /// <summary>
        /// The one weight the family is ever drawn at. It ships a single face, and
        /// <c>.keySymbol</c> in <c>Styles/Text.axaml</c> pins <c>FontWeight="Normal"</c> so nothing
        /// asks for a bolder one — which Avalonia would synthesise or substitute.
        /// </summary>
        private static readonly FontWeight[] MarkWeights = [FontWeight.Normal];

        /// <summary>
        /// The process-wide probe. A singleton for the same reason the Plex one is: the answer is a
        /// property of the assembly's embedded font rather than of any session, and the cache is
        /// worth sharing. Constructing it touches no toolkit; the first <see cref="CanPrint"/> does.
        /// </summary>
        public static KeySymbolGlyphCoverage Instance { get; } = new();

        private readonly ConcurrentDictionary<int, bool> _byCodepoint = new();

        /// <inheritdoc />
        /// <remarks>
        /// A Latin letter answers <b>false</b> here, and that is correct rather than a limitation:
        /// the subset carries the marks and nothing else, so <c>R⇧</c> asked as one string is not
        /// printable in this face. That is exactly why <c>MacroModifierMarks</c> hands out the side
        /// letter and the mark as separate parts.
        /// </remarks>
        public bool CanPrint(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            // Runes, never chars — the subset is all BMP today, but a char walk would hand each
            // half of a surrogate pair to the font and get a wrong answer twice the moment one is
            // not.
            foreach (var rune in text.EnumerateRunes())
            {
                if (!Covers(rune))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether one rune resolves in the key-symbol face. Whitespace is printable by definition:
        /// a separator between marks is spacing the layout applies, not a character the mark face
        /// has to draw.
        /// </summary>
        private bool Covers(Rune rune)
        {
            if (Rune.IsWhiteSpace(rune))
            {
                return true;
            }

            if (_byCodepoint.TryGetValue(rune.Value, out var cached))
            {
                return cached;
            }

            var faces = TryResolveFaces();

            if (faces is null)
            {
                return false;
            }

            var covered = true;

            foreach (var face in faces)
            {
                if (!face.TryGetGlyph((uint)rune.Value, out _))
                {
                    covered = false;

                    break;
                }
            }

            _byCodepoint[rune.Value] = covered;

            return covered;
        }

        /// <summary>
        /// The glyph typefaces to probe, or null when there is no running application to resolve
        /// them through. The theme variant handed to the lookup is arbitrary: the family lives in
        /// <c>Styles.Resources</c> rather than in a theme dictionary, because a typeface does not
        /// change when the OS flips. The typefaces themselves are never held — see the type's own
        /// remarks for what that costs when they are.
        /// </summary>
        private static IReadOnlyList<IGlyphTypeface>? TryResolveFaces()
        {
            if (Application.Current is not { } application)
            {
                return null;
            }

            if (!Dispatcher.UIThread.CheckAccess())
            {
                return null;
            }

            if (!application.TryGetResource(KeySymbolFamilyKey, ThemeVariant.Dark, out var value)
                || value is not FontFamily family)
            {
                return null;
            }

            var faces = new List<IGlyphTypeface>(MarkWeights.Length);

            foreach (var weight in MarkWeights)
            {
                var typeface = new Typeface(family, FontStyle.Normal, weight);

                if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface))
                {
                    return null;
                }

                faces.Add(glyphTypeface);
            }

            return faces;
        }
    }
}
