using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the Fn-layer save-token exceptions of specs/07-lighting.md §2.4 item 7 /
    /// specs/05-key-model.md §5.5: eight key-code pairs, translated in both directions, with
    /// unmapped codes passing through. The pairs are key codes, so the file tokens they
    /// produce come from the key registry (Gen1: <c>ins</c>, <c>scrlk</c>, not the spec's
    /// key names).
    /// </summary>
    public class FnLayerTokenTranslatorTests
    {
        [Fact]
        public void Pairs_Always_HasExactlyEightEntries()
        {
            Assert.Equal(8, FnLayerTokenTranslator.Pairs.Count);
            Assert.Equal(8, FnLayerTokenTranslator.Pairs.Select(pair => pair.MemoryKeyCode).Distinct().Count());
            Assert.Equal(8, FnLayerTokenTranslator.Pairs.Select(pair => pair.FileKeyCode).Distinct().Count());
        }

        [Theory]
        [InlineData("mute", "F1")]
        [InlineData("vol-", "F2")]
        [InlineData("vol+", "F3")]
        [InlineData("play", "F4")]
        [InlineData("prev", "F5")]
        [InlineData("next", "F6")]
        [InlineData("ins", "pause")]
        [InlineData("scrlk", "del")]
        public void TranslateForSave_WithMappedMemoryKey_YieldsTheTopLayerPositionToken(
            string memoryToken,
            string expectedFileToken)
        {
            var memoryCode = KeyRegistry.FindByToken(memoryToken, TokenDialect.Gen1)!.Code;

            var fileCode = FnLayerTokenTranslator.TranslateForSave(memoryCode);

            Assert.Equal(expectedFileToken, KeyRegistry.FindByCode(fileCode)!.Gen1Token);
        }

        [Theory]
        [InlineData("F1", "mute")]
        [InlineData("F2", "vol-")]
        [InlineData("F3", "vol+")]
        [InlineData("F4", "play")]
        [InlineData("F5", "prev")]
        [InlineData("F6", "next")]
        [InlineData("pause", "ins")]
        [InlineData("del", "scrlk")]
        public void TranslateForLoad_WithMappedFileToken_YieldsTheInMemoryKey(
            string fileToken,
            string expectedMemoryToken)
        {
            var fileCode = KeyRegistry.FindByToken(fileToken, TokenDialect.Gen1)!.Code;

            var memoryCode = FnLayerTokenTranslator.TranslateForLoad(fileCode);

            Assert.Equal(expectedMemoryToken, KeyRegistry.FindByCode(memoryCode)!.Gen1Token);
        }

        [Fact]
        public void Translate_WithUnmappedCodes_PassesThroughUnchanged()
        {
            var letterCode = KeyRegistry.FindByToken("a", TokenDialect.Gen1)!.Code;
            var functionKeyCode = KeyRegistry.FindByToken("F7", TokenDialect.Gen1)!.Code;

            Assert.Equal(letterCode, FnLayerTokenTranslator.TranslateForSave(letterCode));
            Assert.Equal(letterCode, FnLayerTokenTranslator.TranslateForLoad(letterCode));
            Assert.Equal(functionKeyCode, FnLayerTokenTranslator.TranslateForSave(functionKeyCode));
            Assert.Equal(functionKeyCode, FnLayerTokenTranslator.TranslateForLoad(functionKeyCode));
        }

        [Fact]
        public void Pairs_Always_RoundTripThroughBothDirections()
        {
            foreach (var pair in FnLayerTokenTranslator.Pairs)
            {
                Assert.Equal(pair.FileKeyCode, FnLayerTokenTranslator.TranslateForSave(pair.MemoryKeyCode));
                Assert.Equal(pair.MemoryKeyCode, FnLayerTokenTranslator.TranslateForLoad(pair.FileKeyCode));
            }
        }
    }
}
