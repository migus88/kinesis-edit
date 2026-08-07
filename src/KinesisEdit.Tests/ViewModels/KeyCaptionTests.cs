using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The caption rule on its own. Deliberately a plain <c>[Fact]</c> suite: both capabilities it
    /// depends on — the platform and what the app's type can print — are parameters, so nothing
    /// here needs an Avalonia application and nothing here can be made to pass or fail by whether
    /// some other test booted one first.
    /// </summary>
    public class KeyCaptionTests
    {
        /// <summary>
        /// Every distinct <see cref="KeyDefinition.GlyphText"/> in the key table, the entry it is
        /// read from, and the caption the rule must produce once the glyph is dropped for want of
        /// a face that can print it. Written out rather than derived: the point of the table is to
        /// state what a user sees, and a computed expectation would agree with any rule at all.
        /// </summary>
        public static TheoryData<int, int, TokenDialect, string> GlyphFallbacks =>
            new()
            {
                // specs/05-key-model.md §3.7 — media and volume. The codes are the Windows
                // virtual-key values §2 registers them under; `VirtualKey` itself is internal to
                // Core, so they are spelled here the way the spec table spells them.
                { 0x1F568, 0xAD, TokenDialect.Gen1, "Mute" },
                { 0x1F569, 0xAE, TokenDialect.Gen1, "Vol-" },
                { 0x1F56A, 0xAF, TokenDialect.Gen1, "Vol+" },
                { 0x25FC, 0xB2, TokenDialect.Gen1, "Stop" },
                { 0x23EE, 0xB1, TokenDialect.Gen1, "Prev" },
                { 0x23ED, 0xB0, TokenDialect.Gen1, "Next" },
                { 0x23EF, 0xB3, TokenDialect.Gen1, "Play\nPause" },
                { 0x25B6, 11151, TokenDialect.Gen2, "Play" },
                { 0x23E9, 11147, TokenDialect.Gen1, "Forward" },
                { 0x23EA, 11148, TokenDialect.Gen1, "Rewind" },
                { 0x23F8, 11149, TokenDialect.Gen1, "Pause" },
                { 0x23CF, 11150, TokenDialect.Gen1, "Eject" },
                { 0x23FA, 11152, TokenDialect.Gen1, "Record" },
                // §3.9 — special actions.
                { 0x2600, 10022, TokenDialect.Gen1, "LED" },
                { 0x1F506, 11161, TokenDialect.Gen1, "Led+" },
                { 0x1F505, 11162, TokenDialect.Gen1, "Led-" },
                { 0x2297, 10019, TokenDialect.Gen1, "NUL" }
            };

        [Fact]
        public void For_WithACoveredGlyph_PrefersTheGlyph()
        {
            // The gate is a capability check, not a ban: a glyph the app's type can actually print
            // is still the caption specs/05-key-model.md §3.7 asks for.
            var playPause = TestLayouts.Gen1Key("play");
            var coverage = FakeGlyphCoverage.CoveringEverything;

            Assert.Equal("⏯", KeyCaption.For(playPause, TokenDialect.Gen1, isMacOs: false, coverage));
            Assert.Equal("⏯", KeyCaption.For(playPause, TokenDialect.Gen1, isMacOs: true, coverage));
        }

        [Fact]
        public void For_WithAGlyphTheTypeCannotPrint_FallsBackToThePlainCaption()
        {
            // §3.7/§6 gate the glyph column on capability — the legacy app drew it "only on
            // Windows 10 and later; otherwise plain text fallbacks". This app's capability is its
            // embedded families, and they carry none of the 17.
            var playPause = TestLayouts.Gen1Key("play");
            var coverage = FakeGlyphCoverage.CoveringNothing;

            Assert.Equal("Play\nPause", KeyCaption.For(playPause, TokenDialect.Gen1, isMacOs: false, coverage));
            Assert.Equal("Play\nPause", KeyCaption.For(playPause, TokenDialect.Gen1, isMacOs: true, coverage));
        }

        [Theory]
        [MemberData(nameof(GlyphFallbacks))]
        public void For_WithAnUnprintableGlyph_CaptionsTheEntryAsPlainText(
            int codepoint,
            int code,
            TokenDialect dialect,
            string expected)
        {
            var entry = KeyRegistry.FindByCode(code)
                ?? throw new InvalidOperationException($"No key registered for code {code}.");

            Assert.Equal(char.ConvertFromUtf32(codepoint), entry.GlyphText);

            // No glyph-carrying entry sets MacDisplayText, so both platforms read the same.
            Assert.Equal(expected, KeyCaption.For(entry, dialect, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal(expected, KeyCaption.For(entry, dialect, isMacOs: true, FakeGlyphCoverage.CoveringNothing));
        }

        [Fact]
        public void TheGlyphFallbackTable_CoversEveryGlyphTheKeyTableCarries()
        {
            // Anti-vacuity for the theory above: a table that drifted behind the registry would
            // still pass every row it kept. 23 entries carry a glyph; they spell 17 distinct ones,
            // because the media keys are registered twice for the Advantage2 keypad layer (§3.7).
            var glyphs = KeyRegistry.Entries
                .Select(entry => entry.GlyphText)
                .Where(glyph => glyph.Length > 0)
                .ToList();

            var tabled = GlyphFallbacks
                .Select(row => char.ConvertFromUtf32((int)row[0]!))
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal(23, glyphs.Count);
            Assert.Equal(17, glyphs.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(glyphs.Distinct(StringComparer.Ordinal).OrderBy(glyph => glyph, StringComparer.Ordinal), tabled.OrderBy(glyph => glyph, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData(TokenDialect.Legacy)]
        [InlineData(TokenDialect.Gen1)]
        [InlineData(TokenDialect.Gen2)]
        public void For_WithTheNullAction_BorrowsTheOneCaptionTheTableSpells(TokenDialect dialect)
        {
            // Code 10019 is the one entry that needs the borrow step. Its plain caption is ' '
            // (§3.9 records the blank as the non-Gen2 fallback for ⊗) and only its Gen2 override
            // spells a word, so without the step a Legacy or Gen1 cap would fall past the blank
            // check onto the literal file token `null` and read as a bug. A blank cap would be
            // spec-faithful and indistinguishable from an unassigned key, so the word is borrowed
            // for all three dialects.
            var nullAction = KeyRegistry.FindByCode(10019)
                ?? throw new InvalidOperationException("No key registered for code 10019.");

            Assert.Equal("null", nullAction.GetToken(dialect));
            Assert.True(string.IsNullOrWhiteSpace(nullAction.DisplayText));

            Assert.Equal("NUL", KeyCaption.For(nullAction, dialect, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal("NUL", KeyCaption.For(nullAction, dialect, isMacOs: true, FakeGlyphCoverage.CoveringNothing));
        }

        [Fact]
        public void For_OnMacOs_PrefersTheMacCaptionOverTheDialectCaption()
        {
            var enter = TestLayouts.Gen1Key("ent");

            Assert.Equal("Return", KeyCaption.For(enter, TokenDialect.Gen1, isMacOs: true, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal("Enter", KeyCaption.For(enter, TokenDialect.Gen1, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
        }

        [Fact]
        public void For_WithoutAGlyphOrMacCaption_UsesTheDialectCaption()
        {
            var one = TestLayouts.Gen1Key("1");

            Assert.Equal("1 !", KeyCaption.For(one, TokenDialect.Gen1, isMacOs: true, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal("!\n1", KeyCaption.For(one, TokenDialect.Legacy, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
        }

        [Fact]
        public void For_WithoutAGlyph_NeverAsksWhetherTheTypeCanPrintOne()
        {
            // The glyph gate is the first step, and an entry with no glyph has nothing to gate.
            var coverage = FakeGlyphCoverage.CoveringNothing;

            KeyCaption.For(TestLayouts.Gen1Key("1"), TokenDialect.Gen1, isMacOs: false, coverage);

            Assert.Empty(coverage.Queries);
        }

        [Fact]
        public void For_WithATwoLineCaption_KeepsTheLineBreak()
        {
            var backspace = TestLayouts.Gen1Key("bspc");

            Assert.Equal(
                "Back\nSpace",
                KeyCaption.For(backspace, TokenDialect.Gen1, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal(
                "Fwd \nDelete",
                KeyCaption.For(TestLayouts.Gen1Key("del"), TokenDialect.Gen1, isMacOs: true, FakeGlyphCoverage.CoveringNothing));
        }

        [Theory]
        [InlineData("hk0")]
        [InlineData("hk1")]
        [InlineData("hk2")]
        [InlineData("hk3")]
        [InlineData("hk4")]
        [InlineData("hk5")]
        [InlineData("hk6")]
        [InlineData("hk7")]
        [InlineData("hk8")]
        public void For_WithABlankDisplayText_FallsBackToTheDialectToken(string token)
        {
            // 05 §3.11 registers hk0-hk8 with ' ' as their display text (the physical caps are
            // unlabelled), which would otherwise draw a column of indistinguishable empty caps.
            // They carry no glyph, which is why the borrow step is scoped to entries that do: it
            // must not divert these nine off their tokens.
            var hotkey = TestLayouts.Gen1Key(token);

            Assert.True(string.IsNullOrWhiteSpace(hotkey.GetDisplayText(TokenDialect.Gen1)));
            Assert.Empty(hotkey.GlyphText);

            Assert.Equal(token, KeyCaption.For(hotkey, TokenDialect.Gen1, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal(token, KeyCaption.For(hotkey, TokenDialect.Gen1, isMacOs: true, FakeGlyphCoverage.CoveringEverything));
        }

        [Fact]
        public void For_WithALabelledHotkey_KeepsItsDisplayTextRatherThanTheToken()
        {
            Assert.Equal(
                "Fn\nToggle",
                KeyCaption.For(TestLayouts.Gen1Key("hk9"), TokenDialect.Gen1, isMacOs: false, FakeGlyphCoverage.CoveringNothing));
        }

        [Fact]
        public void ForKey_OnTheFreestyleEdgeRgbHotkeyColumn_NeverProducesABlankCaption()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var captions = layout.Layers[0].Keys
                .Select(key => KeyCaption.ForKey(key, layout.Dialect, FakeGlyphCoverage.CoveringNothing))
                .ToList();

            Assert.All(captions, caption => Assert.False(string.IsNullOrWhiteSpace(caption)));
        }

        [Fact]
        public void ForKey_WithARemappedKey_ReportsTheRemappedAction()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var coverage = FakeGlyphCoverage.CoveringNothing;

            Assert.Equal("1 !", KeyCaption.ForKey(key, layout.Dialect, coverage));

            key.Remap(TestLayouts.Gen1Key("z"));

            Assert.Equal("Z", KeyCaption.ForKey(key, layout.Dialect, coverage));
        }

        [Fact]
        public void ForKey_WithAKeyRemappedToAMediaAction_ReadsAsWordsRatherThanTofu()
        {
            // The regression this rule exists for: picking `next` off the token picker's Media chip
            // used to put U+23ED on the cap, and no embedded face carries it.
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            key.Remap(TestLayouts.Gen1Key("next"));

            Assert.Equal("Next", KeyCaption.ForKey(key, layout.Dialect, FakeGlyphCoverage.CoveringNothing));
            Assert.Equal("⏭", KeyCaption.ForKey(key, layout.Dialect, FakeGlyphCoverage.CoveringEverything));
        }
    }
}
