using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The 4-character multi-modifier combination codes of specs/05-key-model.md §5.7,
    /// specs/04-layout-file-format.md §2.3 and specs/11-feature-dialogs.md §11.2: exactly the 11
    /// combinations of two or more of Ctrl/Alt/Win/Shift, in the fixed positional order c-a-w-s
    /// with "x" for an omitted modifier, matched case-insensitively and stored lowercase.
    /// </summary>
    public class MultiModifierCodesTests
    {
        [Fact]
        public void All_ForTheSpecTable_HoldsTheElevenAcceptedCodes()
        {
            Assert.Equal(
                new[] { "caws", "cawx", "cxws", "caxs", "xaws", "caxx", "cxwx", "cxxs", "xawx", "xaxs", "xxws" },
                MultiModifierCodes.All);
        }

        [Fact]
        public void All_ForEveryCode_IsFourCharactersLong()
        {
            Assert.Equal(4, MultiModifierCodes.CodeLength);

            foreach (var code in MultiModifierCodes.All)
            {
                Assert.Equal(MultiModifierCodes.CodeLength, code.Length);
            }
        }

        [Fact]
        public void IsValid_ForEveryAcceptedCode_IsTrue()
        {
            foreach (var code in MultiModifierCodes.All)
            {
                Assert.True(MultiModifierCodes.IsValid(code));
                Assert.True(MultiModifierCodes.IsValid(code.ToUpperInvariant()));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("caw")]
        [InlineData("cawsx")]
        [InlineData("xxxx")]
        [InlineData("cxxx")]
        [InlineData("xaxx")]
        [InlineData("xxwx")]
        [InlineData("xxxs")]
        [InlineData("wacs")]
        [InlineData("lshft")]
        public void IsValid_ForAnythingElse_IsFalse(string? code)
        {
            // 04 §2.3: a single modifier is not a multi-modifier code, it is an ordinary remap;
            // 11 §11.2 requires at least two modifiers, so no code with three "x" is accepted.
            Assert.False(MultiModifierCodes.IsValid(code));
        }

        [Theory]
        [InlineData("CAWS", "caws")]
        [InlineData("cXxS", "cxxs")]
        [InlineData("xxws", "xxws")]
        public void TryNormalize_ForAnAcceptedCode_ReturnsTheCanonicalLowercase(string code, string expected)
        {
            Assert.True(MultiModifierCodes.TryNormalize(code, out var normalized));
            Assert.Equal(expected, normalized);
        }

        [Fact]
        public void TryNormalize_ForAnUnacceptedCode_ReturnsEmpty()
        {
            Assert.False(MultiModifierCodes.TryNormalize("xxxx", out var normalized));
            Assert.Equal(string.Empty, normalized);
        }
    }
}
