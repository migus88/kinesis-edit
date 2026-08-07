using System.Collections.Concurrent;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="IGlyphCoverage"/>: it asks the two embedded IBM Plex families whether
    /// they carry a glyph for every rune of the string, at every weight the type scale uses.
    /// <para>
    /// The probe is deliberately pessimistic — a character counts as printable only when
    /// <b>both</b> families answer yes at <b>every</b> weight. A caption can be set in either face
    /// (a keycap legend is sans, a mono token field is mono) and Avalonia substitutes a missing
    /// glyph silently, so a partial answer would still leave tofu on some surface.
    /// </para>
    /// <para>
    /// Two behaviours matter to callers. It <b>caches per codepoint</b>, because the fonts ship
    /// inside the assembly and the answer therefore cannot change while the process runs, while
    /// the caption rule is resolved across ~100 positions × 5 layers on every refresh. And it
    /// <b>never fails</b>: the families are resolved on first use rather than in a static
    /// initializer, and a probe that cannot reach them — no <see cref="Application"/>, or a call
    /// from off the UI thread, where reading an application's resources is not allowed — answers
    /// "not printable" and lets the caller fall back to plain text instead of throwing. That
    /// answer is <b>not</b> cached, so a later probe on the UI thread still reads the real fonts.
    /// </para>
    /// </summary>
    public sealed class EmbeddedFontGlyphCoverage : IGlyphCoverage
    {
        /// <summary>Resource key of the embedded sans family (<c>Themes/Typography.axaml</c>).</summary>
        private const string SansFamilyKey = "FontSans";

        /// <summary>Resource key of the embedded mono family (<c>Themes/Typography.axaml</c>).</summary>
        private const string MonoFamilyKey = "FontMono";

        private static readonly string[] FamilyKeys = [SansFamilyKey, MonoFamilyKey];

        /// <summary>
        /// The weights the type scale actually selects. <c>IBMPlexSans-Bold</c> is the seventh
        /// embedded face and is deliberately <b>not</b> probed: no role in
        /// <c>Themes/Typography.axaml</c> puts Bold on the sans family — the one Bold role
        /// (<c>.logoMark</c>) is mono, whose Bold face is not even shipped — so requiring a glyph
        /// there would tighten the gate against a face no caption is ever drawn in.
        /// </summary>
        private static readonly FontWeight[] ScaleWeights =
        [
            FontWeight.Normal,
            FontWeight.Medium,
            FontWeight.SemiBold
        ];

        /// <summary>
        /// The process-wide probe. A singleton because the answer is a property of the assembly's
        /// embedded fonts rather than of any session, and because the cache is worth sharing.
        /// Constructing it touches no toolkit; the first <see cref="CanPrint"/> does.
        /// </summary>
        public static EmbeddedFontGlyphCoverage Instance { get; } = new();

        private readonly ConcurrentDictionary<int, bool> _byCodepoint = new();

        /// <inheritdoc />
        public bool CanPrint(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            // Runes, never chars: five of the key table's glyphs are astral (🕨 U+1F568 and its
            // two volume siblings, 🔆 U+1F506, 🔅 U+1F505), so a char walk would hand each half
            // of a surrogate pair to the font and get a wrong answer twice.
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
        /// Whether one rune resolves in every probed face. Whitespace is printable by definition —
        /// a caption's <c>\n</c> is a line break the view splits on, not a character a font draws.
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
        /// The glyph typefaces to probe — both families at every scale weight — or null when there
        /// is no running application to resolve them through. The theme variant passed to the
        /// resource lookup is arbitrary: the families live in <c>Styles.Resources</c> rather than
        /// in a theme dictionary, because a typeface does not change when the OS flips.
        /// <para>
        /// The typefaces themselves are deliberately <b>not</b> held. They wrap native handles
        /// owned by the running application, and a cached one outlives the application that opened
        /// it — which in the test harness, where a session is started and torn down around the
        /// suite, takes the process down natively on the next read. <see cref="FontManager"/> keeps
        /// its own typeface cache, so re-asking is cheap, and the per-codepoint cache above means
        /// the hot path does not come here at all.
        /// </para>
        /// </summary>
        private static IReadOnlyList<IGlyphTypeface>? TryResolveFaces()
        {
            if (Application.Current is not { } application)
            {
                return null;
            }

            // Asking the application for a resource walks its Styles, and every AvaloniaObject on
            // that path verifies dispatcher access. `Application.Current` is a process-wide static
            // that outlives any one test, so a caption resolved on a worker thread would find an
            // application it is not allowed to read and throw — which is how this reached the
            // suite the first time. Off the UI thread there is no way to ask, so the answer is the
            // plain-text fallback, the same answer the whole glyph column gets today.
            if (!Dispatcher.UIThread.CheckAccess())
            {
                return null;
            }

            var faces = new List<IGlyphTypeface>(FamilyKeys.Length * ScaleWeights.Length);

            foreach (var familyKey in FamilyKeys)
            {
                if (!application.TryGetResource(familyKey, ThemeVariant.Dark, out var value) || value is not FontFamily family)
                {
                    return null;
                }

                foreach (var weight in ScaleWeights)
                {
                    var typeface = new Typeface(family, FontStyle.Normal, weight);

                    if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface))
                    {
                        return null;
                    }

                    faces.Add(glyphTypeface);
                }
            }

            return faces;
        }
    }
}
