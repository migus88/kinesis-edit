using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The delay tokens of specs/11-feature-dialogs.md §11.3 and specs/06-macros.md §2.2:
    /// <c>dran</c> for the random delay, <c>d001</c>..<c>d999</c> for a custom one with mandatory
    /// three-digit zero padding, and resolution through the token so the <c>dran</c>/<c>d002</c>
    /// code collision of specs/05-key-model.md §7 cannot swap one for the other.
    /// </summary>
    public sealed class MacroDelayTokensTests
    {
        [Theory]
        [InlineData(1, "d001")]
        [InlineData(2, "d002")]
        [InlineData(50, "d050")]
        [InlineData(125, "d125")]
        [InlineData(999, "d999")]
        public void BuildCustomToken_WithDelayInRange_ZeroPadsToThreeDigits(int milliseconds, string expected)
        {
            Assert.Equal(expected, MacroDelayTokens.BuildCustomToken(milliseconds));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1000)]
        public void BuildCustomToken_WithDelayOutOfRange_Throws(int milliseconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MacroDelayTokens.BuildCustomToken(milliseconds));
        }

        [Fact]
        public void MinAndMaxDelay_MatchTheDialogRange()
        {
            Assert.Equal(1, MacroDelayTokens.MinDelayMilliseconds);
            Assert.Equal(999, MacroDelayTokens.MaxDelayMilliseconds);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(999, true)]
        [InlineData(250, true)]
        [InlineData(0, false)]
        [InlineData(1000, false)]
        [InlineData(-5, false)]
        public void IsValidDelay_ForValue_AnswersAgainstTheOneToNineNinetyNineRange(int milliseconds, bool expected)
        {
            Assert.Equal(expected, MacroDelayTokens.IsValidDelay(milliseconds));
        }

        [Theory]
        [InlineData(TokenDialect.Legacy)]
        [InlineData(TokenDialect.Gen1)]
        [InlineData(TokenDialect.Gen2)]
        public void ResolveRandom_ForEveryDialect_ReturnsTheRandomDelayKey(TokenDialect dialect)
        {
            var key = MacroDelayTokens.ResolveRandom(dialect);

            Assert.NotNull(key);
            Assert.Equal("dran", key.GetToken(dialect));
            Assert.Equal(KeyTable.MacroTiming, key.Table);
        }

        [Fact]
        public void ResolveCustom_WithTwoMilliseconds_ReturnsGeneratedD002NotTheRandomDelay()
        {
            var custom = MacroDelayTokens.ResolveCustom(2, TokenDialect.Gen1);
            var random = MacroDelayTokens.ResolveRandom(TokenDialect.Gen1);

            Assert.NotNull(custom);
            Assert.NotNull(random);
            Assert.Equal("d002", custom.Gen1Token);
            Assert.Equal("dran", random.Gen1Token);

            // Both rows carry code 10087 (05 §7), so only a token lookup can tell them apart:
            // KeyRegistry.FindByCode(10087) answers "dran" by first match.
            Assert.Equal(10087, custom.Code);
            Assert.Equal(10087, random.Code);
            Assert.Equal("dran", KeyRegistry.FindByCode(10087)!.Gen1Token);
        }

        [Theory]
        [InlineData(1, 10086)]
        [InlineData(3, 10088)]
        [InlineData(999, 11084)]
        public void ResolveCustom_WithUncollidedDelay_ReturnsTheGeneratedCode(int milliseconds, int expectedCode)
        {
            var key = MacroDelayTokens.ResolveCustom(milliseconds, TokenDialect.Gen1);

            Assert.NotNull(key);
            Assert.Equal(expectedCode, key.Code);
        }

        [Theory]
        [InlineData(125, 10007)]
        [InlineData(500, 10008)]
        public void ResolveCustom_WithLegacyFixedDelay_ReturnsTheShadowingLegacyRow(int milliseconds, int expectedCode)
        {
            var key = MacroDelayTokens.ResolveCustom(milliseconds, TokenDialect.Gen1);

            Assert.NotNull(key);
            Assert.Equal(expectedCode, key.Code);
            Assert.Equal(MacroDelayTokens.BuildCustomToken(milliseconds), key.Gen1Token);
        }

        [Fact]
        public void ResolveCustom_WithDelayOutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MacroDelayTokens.ResolveCustom(0, TokenDialect.Gen1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MacroDelayTokens.ResolveCustom(1000, TokenDialect.Gen1));
        }

        [Fact]
        public void ResolveRandomAndCustom_WithNoDialect_ResolveAcrossEveryDialect()
        {
            Assert.NotNull(MacroDelayTokens.ResolveRandom(TokenDialect.None));
            Assert.NotNull(MacroDelayTokens.ResolveCustom(250, TokenDialect.None));
        }
    }
}
