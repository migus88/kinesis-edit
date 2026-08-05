using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class LayerCaptionsTests
    {
        [Fact]
        public void ForLayer_OnAGen1Device_MapsTheFileNamesToTopAndFn()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            // The geometry names are the spec's file-side names; the editor's layer switch is
            // described as "top ↔ Fn layer" (specs/10-apps-and-ui.md).
            Assert.Equal("Qwerty-top", layout.Layers[0].Name);
            Assert.Equal("Top", LayerCaptions.ForLayer(layout.Layers[0], layout.Dialect));
            Assert.Equal("Fn", LayerCaptions.ForLayer(layout.Layers[1], layout.Dialect));
        }

        [Fact]
        public void ForLayer_OnTheLegacyDialect_UsesTheSameTopAndFnMapping()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage2);

            Assert.Equal(TokenDialect.Legacy, layout.Dialect);
            Assert.Equal("Top", LayerCaptions.ForLayer(layout.Layers[0], layout.Dialect));
            Assert.Equal("Fn", LayerCaptions.ForLayer(layout.Layers[1], layout.Dialect));
        }

        [Fact]
        public void ForLayer_OnTheGen2Dialect_KeepsTheSpecNames()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            Assert.Equal(
                layout.Layers.Select(layer => layer.Name),
                layout.Layers.Select(layer => LayerCaptions.ForLayer(layer, layout.Dialect)));
        }
    }
}
