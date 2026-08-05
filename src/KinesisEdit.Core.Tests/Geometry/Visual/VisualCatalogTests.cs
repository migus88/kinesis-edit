using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;

namespace KinesisEdit.Core.Tests.Geometry.Visual
{
    public class VisualCatalogTests
    {
        public static IEnumerable<object[]> DevicesWithoutAuthoredVisual()
        {
            foreach (var deviceId in Enum.GetValues<DeviceId>())
            {
                if (deviceId == DeviceId.FreestyleEdgeRgb)
                {
                    continue;
                }

                yield return new object[] { deviceId };
            }
        }

        [Fact]
        public void TryGet_WithFreestyleEdgeRgb_ReturnsTheAuthoredVisual()
        {
            var isFound = VisualCatalog.TryGet(DeviceId.FreestyleEdgeRgb, out var visual);

            Assert.True(isFound);
            Assert.Same(VisualCatalog.FreestyleEdgeRgb, visual);
        }

        [Theory]
        [MemberData(nameof(DevicesWithoutAuthoredVisual))]
        public void TryGet_WithAnyOtherDevice_ReturnsFalse(DeviceId deviceId)
        {
            var isFound = VisualCatalog.TryGet(deviceId, out var visual);

            Assert.False(isFound);
            Assert.Null(visual);
        }

        [Fact]
        public void TryGet_WithMatchingVariant_ReturnsTheAuthoredVisual()
        {
            var isFound = VisualCatalog.TryGet(DeviceId.FreestyleEdgeRgb, LayoutVariant.Qwerty, out var visual);

            Assert.True(isFound);
            Assert.Same(VisualCatalog.FreestyleEdgeRgb, visual);
        }

        [Fact]
        public void TryGet_WithMismatchedVariant_ReturnsFalseWithoutFallingBack()
        {
            var isFound = VisualCatalog.TryGet(DeviceId.FreestyleEdgeRgb, LayoutVariant.Dvorak, out var visual);

            Assert.False(isFound);
            Assert.Null(visual);
        }

        [Fact]
        public void TryGet_WithNoVariant_ResolvesTheDefaultVisual()
        {
            var isFound = VisualCatalog.TryGet(DeviceId.FreestyleEdgeRgb, LayoutVariant.None, out var visual);

            Assert.True(isFound);
            Assert.Same(VisualCatalog.FreestyleEdgeRgb, visual);
        }

        [Fact]
        public void TryGet_WithAndWithoutTheVariantOverload_AgreeOnTheDefault()
        {
            VisualCatalog.TryGet(DeviceId.FreestyleEdgeRgb, out var fromDefaultOverload);
            VisualCatalog.TryGet(DeviceId.FreestyleEdgeRgb, LayoutVariant.None, out var fromVariantOverload);

            Assert.Same(fromDefaultOverload, fromVariantOverload);
        }

        [Fact]
        public void FreestyleEdgeRgb_AcrossRepeatedReads_IsASingleSharedInstance()
        {
            Assert.Same(VisualCatalog.FreestyleEdgeRgb, VisualCatalog.FreestyleEdgeRgb);
        }

        [Fact]
        public void TryGet_ForEveryDeviceWithAVisual_AgreesWithTheGeometryCatalog()
        {
            foreach (var deviceId in Enum.GetValues<DeviceId>())
            {
                if (!VisualCatalog.TryGet(deviceId, out var visual))
                {
                    continue;
                }

                Assert.True(GeometryCatalog.TryGet(deviceId, out var geometry));
                Assert.Equal(geometry!.Variant, visual.Variant);
                Assert.Equal(geometry.Layers[0].Keys.Count, visual.Keys.Count);
            }
        }
    }
}
