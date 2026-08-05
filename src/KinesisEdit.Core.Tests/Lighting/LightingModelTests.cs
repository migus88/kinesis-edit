using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the aggregate lighting models: independent layer/context state sets and the
    /// full-reset semantics of specs/07-lighting.md §1.5 and §6.
    /// </summary>
    public class LightingModelTests
    {
        [Fact]
        public void Reset_AfterChangesOnBothLayers_RestoresBoth()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Wave;
            model.FnLayer.Mode = LightingMode.Breathe;
            model.FnLayer.SetKeyColor(65, new LedColor(1, 2, 3));

            model.Reset();

            Assert.True(model.IsEquivalentTo(new LightingModel()));
        }

        [Fact]
        public void IsEquivalentTo_WithDifferentFnLayer_ReturnsFalse()
        {
            var first = new LightingModel();
            var second = new LightingModel();

            first.FnLayer.Mode = LightingMode.Pulse;

            Assert.False(first.IsEquivalentTo(second));
            Assert.False(first.IsEquivalentTo(null));
        }

        [Fact]
        public void TkoModel_Reset_RestoresBothConfigurationContexts()
        {
            var model = new TkoLightingModel();

            model.KeyBacklight.TopLayer.Mode = LightingMode.Spectrum;
            model.Edge.TopLayer.Mode = LightingMode.FrozenWave;
            model.Edge.TopLayer.SetKeyColor(11113, new LedColor(9, 9, 9));

            model.Reset();

            Assert.True(model.IsEquivalentTo(new TkoLightingModel()));
        }

        [Fact]
        public void TkoModel_KeyBacklightAndEdge_AreIndependentStateSets()
        {
            var model = new TkoLightingModel();

            model.KeyBacklight.TopLayer.Mode = LightingMode.Rain;

            Assert.Equal(LightingMode.Disabled, model.Edge.TopLayer.Mode);
            Assert.False(model.IsEquivalentTo(new TkoLightingModel()));
        }
    }
}
