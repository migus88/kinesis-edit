using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class KeyboardLayerViewModelTests
    {
        [Fact]
        public void BuildAll_ForTheFreestyleEdgeRgb_JoinsEveryKeyToItsPlacement()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            var layers = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null);

            Assert.Equal(2, layers.Count);
            Assert.All(layers, layer => Assert.Equal(95, layer.Keys.Count));
            Assert.All(
                layers,
                layer => Assert.Equal(
                    layer.Layer.Keys.Select(key => key.Index),
                    layer.Keys.Select(key => key.Index)));
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Width, layers[0].BoardWidth);
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Height, layers[0].BoardHeight);
        }

        [Fact]
        public void BuildAll_WithAKeyThatHasNoPlacement_SkipsThatKey()
        {
            var layout = TestLayouts.CreateLayout("esc", "F1", "F2");

            var layers = KeyboardLayerViewModel.BuildAll(layout, TestLayouts.CreateVisual(0, 2), lighting: null);

            Assert.Equal(new[] { 0, 2 }, Assert.Single(layers).Keys.Select(key => key.Index));
        }

        [Fact]
        public void BuildAll_WithAPlacementThatHasNoKey_ProducesNoCapForIt()
        {
            var layout = TestLayouts.CreateLayout("esc");

            var layers = KeyboardLayerViewModel.BuildAll(layout, TestLayouts.CreateVisual(0, 1, 2), lighting: null);

            Assert.Equal(0, Assert.Single(Assert.Single(layers).Keys).Index);
        }

        [Fact]
        public void FindByIndex_ReturnsTheCapOfThatPosition()
        {
            var layer = BuildRgbLayers(lighting: null)[0];

            Assert.Equal(TestLayouts.RgbDigitOneKeyIndex, layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex)!.Index);
            Assert.Null(layer.FindByIndex(4242));
        }

        [Fact]
        public void RefreshFromModel_AfterAModelEdit_UpdatesEveryCap()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            layer.RefreshFromModel();

            Assert.Equal("Z", layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Caption);
            Assert.True(layer.Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [Fact]
        public void BuildAll_WithPerKeyLighting_PutsTheColourOnTheKeyThatOwnsTheMemoryKeyCode()
        {
            var lighting = new LightingModel();

            // The map is keyed by memory key code, not by key index (specs/07-lighting.md §4).
            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));
            lighting.FnLayer.SetKeyColor(TestLayouts.Gen1Key("2").Code, new LedColor(0, 0, 255));

            var layers = BuildRgbLayers(lighting);

            Assert.Equal("#FF0000", layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ColorOverlayHex);
            Assert.True(layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].HasColorOverlay);
            Assert.Null(layers[0].Keys[TestLayouts.RgbDigitTwoKeyIndex].ColorOverlayHex);
            Assert.Equal("#0000FF", layers[1].Keys[TestLayouts.RgbDigitTwoKeyIndex].ColorOverlayHex);
            Assert.Null(layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ColorOverlayHex);
        }

        [Fact]
        public void BuildAll_WithABlackKeyColour_ProducesNoOverlay()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, LedColor.Black);

            var layers = BuildRgbLayers(lighting);

            Assert.All(layers[0].Keys, key => Assert.False(key.HasColorOverlay));
        }

        [Fact]
        public void BuildAll_WithoutALightingModel_ProducesNoOverlay()
        {
            var layers = BuildRgbLayers(lighting: null);

            Assert.All(layers[0].Keys, key => Assert.Null(key.ColorOverlayHex));
        }

        [Fact]
        public void Build_ForADeviceWithoutPerKeyRgb_ProducesNoOverlay()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));

            var layout = KeyboardLayout.Create(DeviceId.Advantage2);

            Assert.Empty(KeyColorOverlay.Build(layout.Device, lighting, layout.Layers[0]));
        }

        [Fact]
        public void Build_WithALightingModelOfAnotherShape_IsIgnored()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            Assert.Empty(KeyColorOverlay.Build(layout.Device, new TkoLightingModel(), layout.Layers[0]));
        }

        [Fact]
        public void Build_ForAKeyCodeTheLayerDoesNotCarry_SkipsIt()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("kp7").Code, new LedColor(1, 2, 3));

            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            Assert.Empty(KeyColorOverlay.Build(layout.Device, lighting, layout.Layers[0]));
        }

        [Fact]
        public void Caption_ForALayerViewModel_ComesFromTheDisplayMapping()
        {
            var layers = BuildRgbLayers(lighting: null);

            Assert.Equal("Top", layers[0].Caption);
            Assert.Equal("Fn", layers[1].Caption);
        }

        private static IReadOnlyList<KeyboardLayerViewModel> BuildRgbLayers(object? lighting)
        {
            return KeyboardLayerViewModel.BuildAll(
                KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb),
                VisualCatalog.FreestyleEdgeRgb,
                lighting);
        }
    }
}
