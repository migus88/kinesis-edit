using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// The Advantage2 keypad-exception list and its explicit token→code map of
    /// specs/05-key-model.md §5.4 (codes 10043-10070, plus <c>kp-insert</c> → VK_INSERT $2D).
    /// </summary>
    public class Advantage2KeypadExceptionsTests
    {
        [Fact]
        public void TokenCodes_PinTheSpecList()
        {
            // 05 §5.4: menu play prev next calc kpshft mute vol- vol+ kp0..kp9 numlk kp= kpdiv
            // kpmult kpmin kpplus kpenter1 kpenter2 kp. kp-insert — 29 tokens.
            Assert.Equal(29, Advantage2KeypadExceptions.TokenCodes.Count);
        }

        [Theory]
        [InlineData("menu", 10043)]
        [InlineData("play", 10044)]
        [InlineData("prev", 10045)]
        [InlineData("next", 10046)]
        [InlineData("calc", 10047)]
        [InlineData("kpshft", 10048)]
        [InlineData("mute", 10049)]
        [InlineData("vol-", 10050)]
        [InlineData("vol+", 10051)]
        [InlineData("numlk", 10052)]
        [InlineData("kp=", 10053)]
        [InlineData("kpdiv", 10054)]
        [InlineData("kpmult", 10055)]
        [InlineData("kp0", 10056)]
        [InlineData("kp9", 10065)]
        [InlineData("kpmin", 10066)]
        [InlineData("kpplus", 10067)]
        [InlineData("kpenter1", 10068)]
        [InlineData("kpenter2", 10069)]
        [InlineData("kp.", 10070)]
        [InlineData("kp-insert", 0x2D)]
        public void TryGetCode_ForAnExceptionToken_ReturnsTheDedicatedCode(string token, int expected)
        {
            Assert.True(Advantage2KeypadExceptions.TryGetCode(token, out var code));
            Assert.Equal(expected, code);
        }

        [Fact]
        public void TryGetCode_IsCaseInsensitive()
        {
            Assert.True(Advantage2KeypadExceptions.TryGetCode("NUMLK", out var code));
            Assert.Equal(10052, code);
        }

        [Theory]
        [InlineData("insert")]
        [InlineData("kp-")]
        [InlineData("kpenter")]
        [InlineData("w")]
        public void IsExceptionToken_ForANonExceptionToken_ReturnsFalse(string token)
        {
            // "insert" alone is not in the list — only "kp-insert" is (05 §5.4) — and "kp-" is
            // the keypad-minus value spelling of 05 §3.6, not an exception token.
            Assert.False(Advantage2KeypadExceptions.IsExceptionToken(token));
        }
    }
}
