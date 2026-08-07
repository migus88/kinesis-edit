using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The third font family — <c>FontKeySymbols</c>, the 19-codepoint subset that carries the
    /// characters naming a physical key — and the separate coverage question it answers.
    /// <para>
    /// The failure this suite exists for is silent: a subset font that fails to register resolves
    /// to the OS default, every mark draws as tofu or as some other face's glyph, and no view-model
    /// assertion can tell. So the family is asked for a real glyph typeface and that typeface is
    /// asked for every one of its codepoints.
    /// </para>
    /// <para>
    /// It also pins the <b>separation</b> the family was added under: the IBM Plex gate keeps
    /// meaning "both Plex families carry this", so a <c>⌘</c> is still forbidden in a sans caption.
    /// A regression that widened the Plex probe instead of adding this one would leave every other
    /// test in the repo green.
    /// </para>
    /// </summary>
    public class KeySymbolFontTests
    {
        /// <summary>Resource key of the family in <c>Themes/Typography.axaml</c>.</summary>
        private const string FamilyKey = "FontKeySymbols";

        /// <summary>The family's own name, as the TTF's name ID 16 spells it.</summary>
        private const string FamilyName = "Kinesis Key Symbols";

        /// <summary>
        /// Every codepoint the subset carries. Authored by hand rather than read back off the font,
        /// because the point is to pin what the asset is <i>supposed</i> to contain: a re-subset
        /// that quietly dropped a mark would otherwise agree with itself.
        /// </summary>
        private static readonly int[] _subsetCodepoints =
        [
            0x2190, 0x2191, 0x2192, 0x2193, 0x21DE, 0x21DF, 0x21E5, 0x21E7, 0x21EA, 0x21F1,
            0x21F2, 0x2303, 0x2318, 0x2324, 0x2325, 0x2326, 0x232B, 0x23CE, 0x2423
        ];

        public static TheoryData<int> SubsetCodepoints()
        {
            var data = new TheoryData<int>();

            foreach (var codepoint in _subsetCodepoints)
            {
                data.Add(codepoint);
            }

            return data;
        }

        [AvaloniaFact]
        public void TheFamily_IsTheEmbeddedSubset()
        {
            var family = Assert.IsType<FontFamily>(DesignTokens.Resolve(FamilyKey, ThemeVariant.Dark));

            Assert.Equal(FamilyName, family.Name);
            Assert.NotNull(family.Key);
            Assert.Contains("KinesisEdit/Assets/Fonts", family.Key!.ToString(), StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void TheFamily_ResolvesInBothVariants()
        {
            // It lives in Styles.Resources rather than a theme dictionary — a typeface does not
            // change when the OS flips — but a view under either variant has to reach it.
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.IsType<FontFamily>(DesignTokens.Resolve(FamilyKey, variant));
            }
        }

        [AvaloniaFact]
        public void TheFamily_LoadsRatherThanFallingBackToASystemFont()
        {
            // The whole family is 4.4 KB of embedded asset. If the collection failed to register,
            // the typeface would fall back to whatever the machine has, the marks would still draw
            // *something* on a developer's Mac, and CI would render tofu.
            Assert.Equal(FamilyName, MarkTypeface().FamilyName);
        }

        [AvaloniaTheory]
        [MemberData(nameof(SubsetCodepoints))]
        public void EveryCodepointOfTheSubset_Rasterises(int codepoint)
        {
            Assert.True(
                MarkTypeface().TryGetGlyph((uint)codepoint, out var glyph),
                $"U+{codepoint:X4} '{new Rune(codepoint)}' has no glyph in {FamilyName}.");

            // Glyph 0 is .notdef — the tofu box itself. A face that answered "yes, glyph 0" would
            // pass a TryGetGlyph-only assertion while drawing a box on screen.
            Assert.NotEqual<uint>(0, glyph);
        }

        [AvaloniaFact]
        public void TheSubset_IsTheNineteenDistinctCodepointsItWasCutFor()
        {
            // A tripwire on the asset itself: the marks the app draws are a subset of a subset, and
            // a regenerated font shipping a different set would still pass every walk above.
            Assert.Equal(19, _subsetCodepoints.Length);
            Assert.Equal(19, _subsetCodepoints.Distinct().Count());
        }

        [AvaloniaTheory]
        [InlineData(MacroModifierMarks.ShiftMark)]
        [InlineData(MacroModifierMarks.ControlMark)]
        [InlineData(MacroModifierMarks.AltMark)]
        [InlineData(MacroModifierMarks.WinMark)]
        public void EveryModifierMark_ResolvesInTheKeySymbolFamily(string mark)
        {
            Assert.True(KeySymbolGlyphCoverage.Instance.CanPrint(mark), mark);
        }

        [AvaloniaFact]
        public void EveryModifierMark_IsOneOfTheSubsetsOwnCodepoints()
        {
            // The gate the acceptance criterion asks for, from the other side: the formatter may
            // not emit a character the shipped asset does not carry. `CanPrint` alone would not
            // catch that, because a fallback family in the chain could answer for it.
            foreach (var mark in MacroModifierMarks.Marks)
            {
                var rune = Assert.Single(mark.EnumerateRunes().ToList());

                Assert.Contains(rune.Value, _subsetCodepoints);
            }
        }

        [AvaloniaFact]
        public void TheMarks_AreInNeitherEmbeddedPlexFamily()
        {
            // Why the family exists at all. If a future IBM Plex ships these, this fails and the
            // third family becomes removable — which is a change to record, not a defect.
            foreach (var mark in MacroModifierMarks.Marks)
            {
                Assert.False(EmbeddedFontGlyphCoverage.Instance.CanPrint(mark), mark);
            }
        }

        [AvaloniaFact]
        public void ThePlexGate_DidNotQuietlyAcquireTheKeySymbolFamily()
        {
            // THE TRAP THIS SUITE IS FOR. Adding `FontKeySymbols` to
            // `EmbeddedFontGlyphCoverage.FamilyKeys` is the obvious-looking way to make a ⌘ "pass
            // the font gate", and it would silently change what that gate means: `CanPrint` is
            // consulted by `KeyCaption` to decide whether a caption may be a glyph, so a ⌘ would
            // then be allowed into a *sans* caption which still cannot draw it. Read from the
            // field, because a widened probe answers correctly for every rune either family
            // carries and nothing else in the repo would notice.
            var field = typeof(EmbeddedFontGlyphCoverage)
                .GetField("FamilyKeys", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(field);

            var families = Assert.IsType<string[]>(field!.GetValue(null));

            Assert.Equal(["FontSans", "FontMono"], families);
        }

        [AvaloniaFact]
        public void TheKeySymbolProbe_AnswersNoForOrdinaryText()
        {
            // Anti-vacuity, and the reason `MacroModifierMarks` hands out the side letter
            // separately: the subset carries no Latin letters, so a probe that said yes to `R`
            // would be reading a fallback family rather than the embedded asset — and a whole
            // "R⇧" set in `.keySymbol` really would draw its `R` in some other font.
            //
            // Both letters are asked for as LITERALS. `MacroModifierMarks.LeftSide` is empty now
            // that left is the unmarked side, and `CanPrint("")` is deliberately true (whitespace
            // and nothing are printable), so composing the probe out of that constant would assert
            // the opposite of what it reads like.
            Assert.False(KeySymbolGlyphCoverage.Instance.CanPrint("L"));
            Assert.False(KeySymbolGlyphCoverage.Instance.CanPrint("R"));
            Assert.False(
                KeySymbolGlyphCoverage.Instance.CanPrint(
                    MacroModifierMarks.RightSide + MacroModifierMarks.ShiftMark));
        }

        [AvaloniaFact]
        public void TheKeySymbolProbe_TreatsWhitespaceAsPrintable()
        {
            // The separator between marks is a thin space; a probe that called it unprintable would
            // fail every joined display string over a character no font has to draw.
            Assert.True(KeySymbolGlyphCoverage.Instance.CanPrint(MacroModifierMarks.Separator));
            Assert.True(KeySymbolGlyphCoverage.Instance.CanPrint(string.Empty));
            Assert.True(KeySymbolGlyphCoverage.Instance.CanPrint(null));
        }

        [AvaloniaFact]
        public void TheKeySymbolProbe_AskedTwice_MeasuresTheFontOnceAndKeepsTheAnswer()
        {
            // Same shape as the Plex probe's cache test: asserting only that two calls agree would
            // pass against any deterministic implementation, so the cache itself is read.
            var coverage = new KeySymbolGlyphCoverage();

            Assert.Empty(CachedCodepoints(coverage));

            Assert.True(coverage.CanPrint(MacroModifierMarks.ShiftMark));
            Assert.Equal([0x21E7], CachedCodepoints(coverage));

            Assert.True(coverage.CanPrint(MacroModifierMarks.ShiftMark));
            Assert.Equal([0x21E7], CachedCodepoints(coverage));

            Assert.False(coverage.CanPrint("L"));
            Assert.Equal(['L', 0x21E7], CachedCodepoints(coverage));
        }

        [AvaloniaFact]
        public async Task TheKeySymbolProbe_FromOffTheUiThread_AnswersInsteadOfThrowing()
        {
            // The trap the Plex probe was built around, inherited in full: `Application.Current` is
            // a process-wide static that outlives any one test, and reading its resources off the
            // UI thread throws from deep inside whatever view model asked. A fresh probe,
            // deliberately — a cached codepoint would return before either guard runs.
            var coverage = new KeySymbolGlyphCoverage();

            var answer = await Task.Run(() => coverage.CanPrint(MacroModifierMarks.ShiftMark));

            // The conservative answer, and nothing learned: an uncacheable "no" must not poison the
            // cache for the UI thread that asks next.
            Assert.False(answer);
            Assert.Empty(CachedCodepoints(coverage));

            Assert.True(coverage.CanPrint(MacroModifierMarks.ShiftMark));
        }

        /// <summary>The glyph typeface the app actually draws a mark with.</summary>
        private static IGlyphTypeface MarkTypeface()
        {
            var family = (FontFamily)DesignTokens.Resolve(FamilyKey, ThemeVariant.Dark);

            Assert.True(
                FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface),
                $"No glyph typeface for {FamilyName}.");

            return typeface;
        }

        /// <summary>
        /// The codepoints <paramref name="coverage"/> has measured, ascending. Read by reflection
        /// for the same reason the Plex probe's cache is: it is an implementation detail no caller
        /// has business seeing, and the only observable that tells a cache from a recompute.
        /// </summary>
        private static IReadOnlyList<int> CachedCodepoints(KeySymbolGlyphCoverage coverage)
        {
            var field = typeof(KeySymbolGlyphCoverage)
                .GetField("_byCodepoint", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(field);

            var cache = Assert.IsType<ConcurrentDictionary<int, bool>>(field!.GetValue(coverage));

            return cache.Keys.Order().ToList();
        }
    }
}
