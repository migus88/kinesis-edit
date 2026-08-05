using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the Advantage360 indicator dialect of specs/07-lighting.md §5: <c>[INDn]&gt;</c>
    /// lines with a function token and color, the five-line Layer form, colorless Battery and
    /// Disabled, tolerant parsing, and the reset state.
    /// </summary>
    public class Advantage360LedFileTests
    {
        [Theory]
        [InlineData("nkro", IndicatorFunction.NkroMode)]
        [InlineData("sclk", IndicatorFunction.ScrollLock)]
        [InlineData("nmlk", IndicatorFunction.NumLock)]
        [InlineData("caps", IndicatorFunction.CapsLock)]
        [InlineData("prof", IndicatorFunction.Profile)]
        public void ParseAdvantage360_WithColorFunction_StoresFunctionAndBaseColor(
            string token,
            IndicatorFunction expectedFunction)
        {
            var model = LedFileParser.ParseAdvantage360(["[ind1]>[" + token + "][255][128][0]"]);

            Assert.Equal(expectedFunction, model.Indicators[0].Function);
            Assert.Equal(new LedColor(255, 128, 0), model.Indicators[0].GetColor(IndicatorLayer.Base));
        }

        [Fact]
        public void ParseAdvantage360_WithBatteryAndNull_TakesNoColor()
        {
            var lines = new[]
            {
                "[ind1]>[batt]",
                "[ind2]>[null]"
            };

            var model = LedFileParser.ParseAdvantage360(lines);

            Assert.Equal(IndicatorFunction.Battery, model.Indicators[0].Function);
            Assert.Equal(IndicatorFunction.Disabled, model.Indicators[1].Function);
            Assert.Equal(LedColor.Black, model.Indicators[0].GetColor(IndicatorLayer.Base));
        }

        [Fact]
        public void ParseAdvantage360_WithFiveLayerLines_StoresOneColorPerLayer()
        {
            var lines = new[]
            {
                "[ind3]>[layd][1][0][0]",
                "[ind3]>[layk][0][2][0]",
                "[ind3]>[lay1][0][0][3]",
                "[ind3]>[lay2][4][4][4]",
                "[ind3]>[lay3][5][5][5]"
            };

            var model = LedFileParser.ParseAdvantage360(lines);
            var indicator = model.Indicators[2];

            Assert.Equal(IndicatorFunction.Layer, indicator.Function);
            Assert.Equal(new LedColor(1, 0, 0), indicator.GetColor(IndicatorLayer.Base));
            Assert.Equal(new LedColor(0, 2, 0), indicator.GetColor(IndicatorLayer.Keypad));
            Assert.Equal(new LedColor(0, 0, 3), indicator.GetColor(IndicatorLayer.Fn1));
            Assert.Equal(new LedColor(4, 4, 4), indicator.GetColor(IndicatorLayer.Fn2));
            Assert.Equal(new LedColor(5, 5, 5), indicator.GetColor(IndicatorLayer.Fn3));
        }

        [Fact]
        public void ParseAdvantage360_WithMixedCasing_IsCaseInsensitive()
        {
            var model = LedFileParser.ParseAdvantage360(["[IND6]>[NKRO][0][255][0]"]);

            Assert.Equal(IndicatorFunction.NkroMode, model.Indicators[5].Function);
            Assert.Equal(new LedColor(0, 255, 0), model.Indicators[5].GetColor(IndicatorLayer.Base));
        }

        [Fact]
        public void ParseAdvantage360_WithUnknownIndicatorOrFunction_SkipsTheLine()
        {
            var lines = new[]
            {
                "[ind7]>[nkro][1][2][3]",
                "[ind0]>[nkro][1][2][3]",
                "[ind1]>[unknown][1][2][3]",
                "[ind2]>",
                "garbage"
            };

            var model = LedFileParser.ParseAdvantage360(lines);

            Assert.True(model.IsEquivalentTo(new Advantage360LightingModel()));
        }

        [Fact]
        public void ParseAdvantage360_WithEmptyFile_YieldsTheResetState()
        {
            var model = LedFileParser.ParseAdvantage360([]);

            Assert.True(model.IsEquivalentTo(new Advantage360LightingModel()));

            foreach (var indicator in model.Indicators)
            {
                Assert.Equal(IndicatorFunction.Disabled, indicator.Function);
            }
        }

        [Fact]
        public void SerializeAdvantage360_WithResetModel_WritesSixNullLines()
        {
            var lines = LedFileSerializer.SerializeAdvantage360(new Advantage360LightingModel());

            Assert.Equal(
                new[]
                {
                    "[IND1]>[null]",
                    "[IND2]>[null]",
                    "[IND3]>[null]",
                    "[IND4]>[null]",
                    "[IND5]>[null]",
                    "[IND6]>[null]"
                },
                lines);
        }

        [Fact]
        public void SerializeAdvantage360_WithEachFunctionKind_PinsTheCanonicalLines()
        {
            var model = new Advantage360LightingModel();

            model.Indicators[0].Function = IndicatorFunction.CapsLock;
            model.Indicators[0].SetColor(IndicatorLayer.Base, new LedColor(255, 0, 0));
            model.Indicators[1].Function = IndicatorFunction.Battery;
            model.Indicators[2].Function = IndicatorFunction.Layer;
            model.Indicators[2].SetColor(IndicatorLayer.Base, new LedColor(1, 0, 0));
            model.Indicators[2].SetColor(IndicatorLayer.Keypad, new LedColor(0, 2, 0));
            model.Indicators[2].SetColor(IndicatorLayer.Fn1, new LedColor(0, 0, 3));
            model.Indicators[2].SetColor(IndicatorLayer.Fn2, new LedColor(4, 4, 4));
            model.Indicators[2].SetColor(IndicatorLayer.Fn3, new LedColor(5, 5, 5));

            Assert.Equal(
                new[]
                {
                    "[IND1]>[caps][255][0][0]",
                    "[IND2]>[batt]",
                    "[IND3]>[layd][1][0][0]",
                    "[IND3]>[layk][0][2][0]",
                    "[IND3]>[lay1][0][0][3]",
                    "[IND3]>[lay2][4][4][4]",
                    "[IND3]>[lay3][5][5][5]",
                    "[IND4]>[null]",
                    "[IND5]>[null]",
                    "[IND6]>[null]"
                },
                LedFileSerializer.SerializeAdvantage360(model));
        }

        [Fact]
        public void SerializeAdvantage360_WithNoColorAssigned_WritesBlackAsNoColor()
        {
            var model = new Advantage360LightingModel();

            model.Indicators[0].Function = IndicatorFunction.Profile;

            var lines = LedFileSerializer.SerializeAdvantage360(model);

            Assert.Equal("[IND1]>[prof][0][0][0]", lines[0]);
        }

        [Fact]
        public void ParseAdvantage360_ThenSerializeAndReparse_YieldsAnEquivalentModel()
        {
            var lines = new[]
            {
                "[ind1]>[nkro][255][0][0]",
                "[ind2]>[batt]",
                "[ind3]>[layd][1][0][0]",
                "[ind3]>[layk][0][2][0]",
                "[ind3]>[lay1][0][0][3]",
                "[ind3]>[lay2][4][4][4]",
                "[ind3]>[lay3][5][5][5]",
                "[ind4]>[prof][0][128][255]",
                "[ind5]>[sclk][10][20][30]"
            };

            var firstParse = LedFileParser.ParseAdvantage360(lines);
            var serialized = LedFileSerializer.SerializeAdvantage360(firstParse);
            var secondParse = LedFileParser.ParseAdvantage360(serialized);

            Assert.True(firstParse.IsEquivalentTo(secondParse));
        }
    }
}
