using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Remap parsing per specs/04-layout-file-format.md §2.1, the layer encodings of §3, and
    /// the worked examples of §6.1-§6.4: position addressing per layer, last-line-wins,
    /// remap-to-original clearing, case-insensitive tolerant reads (§1.2), and the Advantage2
    /// keypad-exception and value-spelling handling of specs/05-key-model.md §5.4/§3.6.
    /// </summary>
    public class LayoutFileParserRemapTests
    {
        [Fact]
        public void Parse_TheSection61Example_RemapsF1OnTheTopLayer()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "[F1]>[a]");

            var key = LayoutFixtures.Position(result.Layout, 0, "F1", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
            Assert.Equal(LayoutFixtures.Key("a").Code, key.ModifiedKey!.Code);
        }

        [Fact]
        public void Parse_WithMixedCase_IsCaseInsensitive()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[f1]>[A]");

            var key = LayoutFixtures.Position(result.Layout, 0, "F1", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Parse_TheSection62Example_TargetsTheBottomLayerViaTheFnPrefix()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "fn [4]>[LED]");

            var bottomKey = LayoutFixtures.Position(result.Layout, 1, "4", TokenDialect.Gen1);
            var topKey = LayoutFixtures.Position(result.Layout, 0, "4", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(bottomKey.IsModified);
            Assert.Equal(LayoutFixtures.Key("LED").Code, bottomKey.ModifiedKey!.Code);
            Assert.False(topKey.IsModified);
        }

        [Fact]
        public void Parse_DuplicateRemaps_LastLineWins()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "[F1]>[a]", "[F1]>[b]");

            var key = LayoutFixtures.Position(result.Layout, 0, "F1", TokenDialect.Gen1);

            Assert.Equal(LayoutFixtures.Key("b").Code, key.ModifiedKey!.Code);
        }

        [Fact]
        public void Parse_ARemapBackToTheOriginal_ClearsTheRemap()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "[F1]>[a]", "[F1]>[F1]");

            var key = LayoutFixtures.Position(result.Layout, 0, "F1", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.False(key.IsModified);
        }

        [Fact]
        public void Parse_TheSection63Example_RoutesTheKpPrefixToTheKeypadLayer()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "[kp-w]>[b]");

            var key = LayoutFixtures.Position(result.Layout, 1, "w", TokenDialect.Legacy);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
            Assert.Equal(LayoutFixtures.Key("b", TokenDialect.Legacy).Code, key.ModifiedKey!.Code);
        }

        [Theory]
        [InlineData("numlk")]
        [InlineData("kp0")]
        [InlineData("kpenter1")]
        [InlineData("kp.")]
        [InlineData("menu")]
        public void Parse_AKeypadExceptionToken_RoutesToTheKeypadLayerWithoutAPrefix(string token)
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, $"[{token}]>[z]");

            var key = LayoutFixtures.Position(result.Layout, 1, token, TokenDialect.Legacy);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Parse_KpInsert_RoutesToTheKeypadInsertPosition()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "[kp-insert]>[z]");

            var key = LayoutFixtures.Position(result.Layout, 1, "insert", TokenDialect.Legacy);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Parse_TheKpMinusValueSpelling_IsAKeypadPositionNotAnEmptyPrefixedToken()
        {
            // 05 §3.6: "kp-" is the read spelling of the key saved as "kpmin" — the exception/
            // alias handling must run before prefix stripping.
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "[kp-]>[z]");

            var key = LayoutFixtures.Position(result.Layout, 1, "kpmin", TokenDialect.Legacy);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Parse_AKeypadValueSpellingAsOutput_ResolvesViaTheTolerantAlias()
        {
            // 05 §3.6: value spellings kp/ kp* kp- kp+ are accepted on read; the serializer
            // writes the Legacy save spellings.
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "[a]>[kp-]");

            var key = LayoutFixtures.Position(result.Layout, 0, "a", TokenDialect.Legacy);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(LayoutFixtures.Key("kpmin", TokenDialect.Legacy).Code, key.ModifiedKey!.Code);
        }

        [Fact]
        public void Parse_ALegacySpellingInAGen1File_IsToleratedOnRead()
        {
            // Read tolerantly: "escape" is the Legacy spelling of the Gen1 "esc" token; the
            // position resolves to the same key code either way.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "[escape]>[a]");

            var key = LayoutFixtures.Position(result.Layout, 0, "esc", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Parse_Gen2Headers_RouteRulesToTheHeadersLayer()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.Advantage360,
                "[caps]>[del]",
                "<function1>",
                "[1]>[F13]");

            // Before the first header the layer is Base (04 §3.3).
            var baseCaps = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen2);

            // On the Fn1 layer the number-row position "1" hosts F2 — remap lines address the
            // position token (05 §4.7).
            var fn1Key = LayoutFixtures.Position(result.Layout, 2, "1", TokenDialect.Gen2);

            Assert.Empty(result.InvalidLines);
            Assert.True(baseCaps.IsModified);
            Assert.True(fn1Key.IsModified);
            Assert.Equal(LayoutFixtures.Key("F2", TokenDialect.Gen2).Code, fn1Key.OriginalKey.Code);
            Assert.Equal(LayoutFixtures.Key("F13", TokenDialect.Gen2).Code, fn1Key.ModifiedKey!.Code);
        }
    }
}
