using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the Advantage360 indicator model against specs/07-lighting.md §5: six
    /// indicators, five per-layer color slots, and the reset state (function Disabled, all
    /// colors black).
    /// </summary>
    public class IndicatorStateTests
    {
        [Fact]
        public void Constructor_Always_StartsDisabledWithBlackColors()
        {
            var state = new IndicatorState();

            Assert.Equal(IndicatorFunction.Disabled, state.Function);

            foreach (var layer in Enum.GetValues<IndicatorLayer>())
            {
                Assert.Equal(LedColor.Black, state.GetColor(layer));
            }
        }

        [Fact]
        public void SetColor_PerLayer_StoresIndependentSlots()
        {
            var state = new IndicatorState();

            state.SetColor(IndicatorLayer.Base, new LedColor(255, 0, 0));
            state.SetColor(IndicatorLayer.Fn3, new LedColor(0, 0, 255));

            Assert.Equal(new LedColor(255, 0, 0), state.GetColor(IndicatorLayer.Base));
            Assert.Equal(LedColor.Black, state.GetColor(IndicatorLayer.Keypad));
            Assert.Equal(new LedColor(0, 0, 255), state.GetColor(IndicatorLayer.Fn3));
        }

        [Fact]
        public void Reset_AfterChanges_RestoresDisabledAndBlack()
        {
            var state = new IndicatorState { Function = IndicatorFunction.CapsLock };

            state.SetColor(IndicatorLayer.Keypad, new LedColor(1, 2, 3));

            state.Reset();

            Assert.True(state.IsEquivalentTo(new IndicatorState()));
        }

        [Fact]
        public void Model_Always_HasSixIndicators()
        {
            Assert.Equal(6, Advantage360LightingModel.IndicatorCount);
            Assert.Equal(6, new Advantage360LightingModel().Indicators.Count);
        }

        [Fact]
        public void Model_Reset_ResetsEveryIndicator()
        {
            var model = new Advantage360LightingModel();

            model.Indicators[2].Function = IndicatorFunction.Battery;
            model.Indicators[5].SetColor(IndicatorLayer.Base, new LedColor(4, 5, 6));

            model.Reset();

            Assert.True(model.IsEquivalentTo(new Advantage360LightingModel()));
        }
    }
}
