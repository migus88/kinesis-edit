using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Pins the Advantage360 indicator-function catalog against the function table of
    /// specs/07-lighting.md §5: eight functions, twelve tokens (the Layer function writes
    /// five), and the colorless Battery/Disabled rows.
    /// </summary>
    public class IndicatorFunctionCatalogTests
    {
        [Fact]
        public void All_Always_HasEightFunctionsAndTwelveTokens()
        {
            Assert.Equal(8, IndicatorFunctionCatalog.All.Count);
            Assert.Equal(12, IndicatorFunctionCatalog.All.Sum(definition => definition.Tokens.Count));
        }

        [Theory]
        [InlineData(IndicatorFunction.NkroMode, "nkro", true)]
        [InlineData(IndicatorFunction.ScrollLock, "sclk", true)]
        [InlineData(IndicatorFunction.NumLock, "nmlk", true)]
        [InlineData(IndicatorFunction.CapsLock, "caps", true)]
        [InlineData(IndicatorFunction.Profile, "prof", true)]
        [InlineData(IndicatorFunction.Battery, "batt", false)]
        [InlineData(IndicatorFunction.Disabled, "null", false)]
        public void Find_WithSingleTokenFunction_CarriesTokenAndColorRule(
            IndicatorFunction function,
            string expectedToken,
            bool expectedHasColor)
        {
            var definition = IndicatorFunctionCatalog.Find(function);

            Assert.Equal(new[] { expectedToken }, definition.Tokens);
            Assert.Equal(expectedHasColor, definition.HasColor);
        }

        [Fact]
        public void Find_WithLayerFunction_CarriesFiveTokensInLayerOrder()
        {
            var definition = IndicatorFunctionCatalog.Find(IndicatorFunction.Layer);

            Assert.Equal(new[] { "layd", "layk", "lay1", "lay2", "lay3" }, definition.Tokens);
            Assert.True(definition.HasColor);
        }

        [Theory]
        [InlineData("nkro", IndicatorFunction.NkroMode, IndicatorLayer.Base)]
        [InlineData("BATT", IndicatorFunction.Battery, IndicatorLayer.Base)]
        [InlineData("layd", IndicatorFunction.Layer, IndicatorLayer.Base)]
        [InlineData("layk", IndicatorFunction.Layer, IndicatorLayer.Keypad)]
        [InlineData("lay1", IndicatorFunction.Layer, IndicatorLayer.Fn1)]
        [InlineData("lay2", IndicatorFunction.Layer, IndicatorLayer.Fn2)]
        [InlineData("lay3", IndicatorFunction.Layer, IndicatorLayer.Fn3)]
        public void FindByToken_WithKnownToken_ResolvesFunctionAndLayer(
            string token,
            IndicatorFunction expectedFunction,
            IndicatorLayer expectedLayer)
        {
            var match = IndicatorFunctionCatalog.FindByToken(token);

            Assert.NotNull(match);
            Assert.Equal(expectedFunction, match.Definition.Function);
            Assert.Equal(expectedLayer, match.Layer);
        }

        [Theory]
        [InlineData("mono")]
        [InlineData("lay4")]
        [InlineData("")]
        [InlineData(null)]
        public void FindByToken_WithUnknownToken_ReturnsNull(string? token)
        {
            Assert.Null(IndicatorFunctionCatalog.FindByToken(token));
        }
    }
}
