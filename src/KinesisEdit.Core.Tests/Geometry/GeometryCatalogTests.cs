using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    public class GeometryCatalogTests
    {
        public static IEnumerable<object[]> AllGeometryNames()
        {
            foreach (var name in CatalogByName.Keys)
            {
                yield return new object[] { name };
            }
        }

        private static IReadOnlyDictionary<string, DeviceGeometry> CatalogByName { get; } =
            new Dictionary<string, DeviceGeometry>
            {
                [nameof(GeometryCatalog.FreestyleEdge)] = GeometryCatalog.FreestyleEdge,
                [nameof(GeometryCatalog.FreestyleEdgeRgb)] = GeometryCatalog.FreestyleEdgeRgb,
                [nameof(GeometryCatalog.FreestylePro)] = GeometryCatalog.FreestylePro,
                [nameof(GeometryCatalog.Tko)] = GeometryCatalog.Tko,
                [nameof(GeometryCatalog.Advantage2Qwerty)] = GeometryCatalog.Advantage2Qwerty,
                [nameof(GeometryCatalog.Advantage2Dvorak)] = GeometryCatalog.Advantage2Dvorak,
                [nameof(GeometryCatalog.Advantage360)] = GeometryCatalog.Advantage360
            };

        [Theory]
        [InlineData(nameof(GeometryCatalog.FreestyleEdge), 0, "Qwerty-top", 95)]
        [InlineData(nameof(GeometryCatalog.FreestyleEdge), 1, "Qwerty-keypad", 95)]
        [InlineData(nameof(GeometryCatalog.FreestyleEdgeRgb), 0, "Qwerty-top", 95)]
        [InlineData(nameof(GeometryCatalog.FreestyleEdgeRgb), 1, "Qwerty-keypad", 95)]
        [InlineData(nameof(GeometryCatalog.FreestylePro), 0, "Qwerty-top", 95)]
        [InlineData(nameof(GeometryCatalog.FreestylePro), 1, "Qwerty-keypad", 95)]
        [InlineData(nameof(GeometryCatalog.Tko), 0, "Qwerty-top", 63)]
        [InlineData(nameof(GeometryCatalog.Tko), 1, "Qwerty-keypad", 63)]
        [InlineData(nameof(GeometryCatalog.Advantage2Qwerty), 0, "Qwerty-top", 89)]
        [InlineData(nameof(GeometryCatalog.Advantage2Qwerty), 1, "Qwerty-keypad", 89)]
        [InlineData(nameof(GeometryCatalog.Advantage2Dvorak), 0, "Dvorak-top", 89)]
        [InlineData(nameof(GeometryCatalog.Advantage2Dvorak), 1, "Dvorak-keypad", 89)]
        [InlineData(nameof(GeometryCatalog.Advantage360), 0, "Base", 77)]
        [InlineData(nameof(GeometryCatalog.Advantage360), 1, "Keypad", 77)]
        [InlineData(nameof(GeometryCatalog.Advantage360), 2, "Fn1", 77)]
        [InlineData(nameof(GeometryCatalog.Advantage360), 3, "Fn2", 77)]
        [InlineData(nameof(GeometryCatalog.Advantage360), 4, "Fn3", 77)]
        public void Layers_PerSpecSection4_HaveExpectedNameIndexAndPositionCount(
            string geometryName,
            int layerIndex,
            string expectedName,
            int expectedPositionCount)
        {
            var geometry = CatalogByName[geometryName];

            var layer = geometry.Layers[layerIndex];

            Assert.Equal(layerIndex, layer.Index);
            Assert.Equal(expectedName, layer.Name);
            Assert.Equal(expectedPositionCount, layer.Keys.Count);
        }

        [Theory]
        [InlineData(nameof(GeometryCatalog.FreestyleEdge), 2)]
        [InlineData(nameof(GeometryCatalog.FreestyleEdgeRgb), 2)]
        [InlineData(nameof(GeometryCatalog.FreestylePro), 2)]
        [InlineData(nameof(GeometryCatalog.Tko), 2)]
        [InlineData(nameof(GeometryCatalog.Advantage2Qwerty), 2)]
        [InlineData(nameof(GeometryCatalog.Advantage2Dvorak), 2)]
        [InlineData(nameof(GeometryCatalog.Advantage360), 5)]
        public void Layers_PerSpecSection15_HaveExpectedLayerCount(string geometryName, int expectedLayerCount)
        {
            var geometry = CatalogByName[geometryName];

            Assert.Equal(expectedLayerCount, geometry.Layers.Count);
        }

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void Layers_AcrossCatalog_HaveDenseUniqueIndicesInListOrder(string geometryName)
        {
            var geometry = CatalogByName[geometryName];

            foreach (var layer in geometry.Layers)
            {
                for (var i = 0; i < layer.Keys.Count; i++)
                {
                    Assert.Equal(i, layer.Keys[i].Index);
                }

                for (var i = 0; i < layer.EdgeZones.Count; i++)
                {
                    Assert.Equal(i, layer.EdgeZones[i].Index);
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void Layers_WithinOneDevice_ShareTheSamePositionCount(string geometryName)
        {
            var geometry = CatalogByName[geometryName];

            var firstLayer = geometry.Layers[0];

            foreach (var layer in geometry.Layers)
            {
                Assert.Equal(firstLayer.Keys.Count, layer.Keys.Count);
                Assert.Equal(firstLayer.EdgeZones.Count, layer.EdgeZones.Count);
            }
        }

        [Fact]
        public void Layers_OnFreestyleEdge_ShareHotkeyPositionsAtTheSameIndices()
        {
            var geometry = GeometryCatalog.FreestyleEdge;
            var hotkeyIndices = new[] { 17, 18, 34, 35, 51, 52, 67, 68, 83, 84 };

            foreach (var index in hotkeyIndices)
            {
                Assert.Equal(
                    geometry.Layers[0].Keys[index].DefaultToken,
                    geometry.Layers[1].Keys[index].DefaultToken);
            }
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, 2, LayoutVariant.Qwerty)]
        [InlineData(DeviceId.FreestyleEdgeRgb, 2, LayoutVariant.Qwerty)]
        [InlineData(DeviceId.FreestylePro, 2, LayoutVariant.Qwerty)]
        [InlineData(DeviceId.Tko, 2, LayoutVariant.Qwerty)]
        [InlineData(DeviceId.Advantage2, 2, LayoutVariant.Qwerty)]
        [InlineData(DeviceId.Advantage360, 5, LayoutVariant.None)]
        public void TryGet_WithProgrammableKeyboard_ReturnsDefaultGeometry(
            DeviceId deviceId,
            int expectedLayerCount,
            LayoutVariant expectedVariant)
        {
            var isFound = GeometryCatalog.TryGet(deviceId, out var geometry);

            Assert.True(isFound);
            Assert.NotNull(geometry);
            Assert.Equal(expectedLayerCount, geometry.Layers.Count);
            Assert.Equal(expectedVariant, geometry.Variant);
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void TryGet_WithDeviceWithoutKeyLayers_ReturnsFalse(DeviceId deviceId)
        {
            var isFound = GeometryCatalog.TryGet(deviceId, out var geometry);

            Assert.False(isFound);
            Assert.Null(geometry);
        }

        [Fact]
        public void TryGet_WithAdvantage2AndDvorakVariant_ReturnsDvorakGeometry()
        {
            var isFound = GeometryCatalog.TryGet(DeviceId.Advantage2, LayoutVariant.Dvorak, out var geometry);

            Assert.True(isFound);
            Assert.Same(GeometryCatalog.Advantage2Dvorak, geometry);
        }

        [Fact]
        public void TryGet_WithAdvantage2AndQwertyVariant_ReturnsQwertyGeometry()
        {
            var isFound = GeometryCatalog.TryGet(DeviceId.Advantage2, LayoutVariant.Qwerty, out var geometry);

            Assert.True(isFound);
            Assert.Same(GeometryCatalog.Advantage2Qwerty, geometry);
        }

        [Fact]
        public void TryGet_WithAdvantage2AndNoVariant_DefaultsToQwerty()
        {
            var isFound = GeometryCatalog.TryGet(DeviceId.Advantage2, out var geometry);

            Assert.True(isFound);
            Assert.Same(GeometryCatalog.Advantage2Qwerty, geometry);
        }

        [Fact]
        public void TryGet_WithDvorakVariantOnNonAdvantage2Device_ReturnsFalse()
        {
            var isFound = GeometryCatalog.TryGet(DeviceId.FreestyleEdge, LayoutVariant.Dvorak, out var geometry);

            Assert.False(isFound);
            Assert.Null(geometry);
        }
    }
}
