using KinesisEdit.Core.Keys;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// The pedal-only token spellings of specs/12-savant-elite.md §4.4: the bare-space macro
    /// token, the <c>{125}</c>/<c>{500}</c> field delays of firmware 1.0.44, and <c>win</c> — the
    /// catalog's only Win/Cmd token, which the key table spells <c>lwin</c>/<c>rwin</c> (05 §3.5).
    /// </summary>
    public class PedalTokenMapTests
    {
        [Fact]
        public void Resolve_WithALegacyToken_GoesStraightThroughTheRegistry()
        {
            Assert.Equal(KeyRegistry.FindByToken("bspace", TokenDialect.Legacy), PedalTokenMap.Resolve("bspace"));
            Assert.Equal(KeyRegistry.FindByToken("intl-\\", TokenDialect.Legacy), PedalTokenMap.Resolve("INTL-\\"));
        }

        [Fact]
        public void Resolve_WithTheBareSpaceToken_ReturnsTheSpaceKey()
        {
            Assert.Equal(PedalTokenMap.SpaceKey, PedalTokenMap.Resolve(PedalTokenMap.SpaceToken));
            Assert.Null(KeyRegistry.FindByToken(PedalTokenMap.SpaceToken));
        }

        [Theory]
        [InlineData("125", "d125")]
        [InlineData("500", "d500")]
        public void Resolve_WithTheFieldDelaySpelling_ReturnsTheSameKeyAsTheAppSpelling(string field, string canonical)
        {
            Assert.Equal(KeyRegistry.FindByToken(canonical, TokenDialect.Legacy), PedalTokenMap.Resolve(field));
        }

        [Fact]
        public void Resolve_WithTheWinToken_ReturnsTheLeftWinKey()
        {
            Assert.Null(KeyRegistry.FindByToken(PedalTokenMap.WinToken));
            Assert.Equal(KeyRegistry.FindByToken("lwin", TokenDialect.Legacy), PedalTokenMap.Resolve(PedalTokenMap.WinToken));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nonsense")]
        public void Resolve_WithAnUnknownToken_ReturnsNull(string? token)
        {
            Assert.Null(PedalTokenMap.Resolve(token));
        }

        [Fact]
        public void GetSingleToken_ForTheSpaceKey_UsesTheNamedToken()
        {
            Assert.Equal("space", PedalTokenMap.GetSingleToken(PedalTokenMap.SpaceKey));
        }

        [Fact]
        public void GetMacroToken_ForTheSpaceKey_UsesTheBareSpace()
        {
            Assert.Equal(PedalTokenMap.SpaceToken, PedalTokenMap.GetMacroToken(PedalTokenMap.SpaceKey));
        }

        [Theory]
        [InlineData("lwin")]
        [InlineData("rwin")]
        public void GetSingleToken_ForEitherWinKey_WritesThePedalWinToken(string registryToken)
        {
            var key = KeyRegistry.FindByToken(registryToken, TokenDialect.Legacy)!;

            Assert.Equal(PedalTokenMap.WinToken, PedalTokenMap.GetSingleToken(key));
        }

        [Fact]
        public void GetSingleToken_ForAFunctionKey_KeepsTheCapitalisedCasing()
        {
            Assert.Equal("F12", PedalTokenMap.GetSingleToken(KeyRegistry.FindByToken("F12", TokenDialect.Legacy)!));
        }

        [Fact]
        public void ExtraTokenCodes_AreUnresolvableThroughTheRegistryAndMapToTheirIntendedKey()
        {
            foreach (var (token, code) in PedalTokenMap.ExtraTokenCodes)
            {
                // Each entry must earn its place: the key table cannot spell it in the pedal's own
                // Legacy dialect, nor in any other dialect Resolve falls back to.
                Assert.Null(KeyRegistry.FindByToken(token, TokenDialect.Legacy));
                Assert.Null(KeyRegistry.FindByToken(token));

                var expected = KeyRegistry.FindByCode(code);

                Assert.NotNull(expected);
                Assert.Same(expected, PedalTokenMap.Resolve(token));
            }
        }
    }
}
