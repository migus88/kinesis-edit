using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    public class LayerGeometryTests
    {
        [Fact]
        public void Constructor_WithDenseKeys_StoresNameIndexAndKeys()
        {
            var keys = new[] { new KeyPosition(0, "esc"), new KeyPosition(1, "F1") };

            var layer = new LayerGeometry("Qwerty-top", 0, keys);

            Assert.Equal("Qwerty-top", layer.Name);
            Assert.Equal(0, layer.Index);
            Assert.Equal(2, layer.Keys.Count);
            Assert.Empty(layer.EdgeZones);
        }

        [Fact]
        public void Constructor_WithNonDenseKeyIndices_ThrowsArgumentException()
        {
            var keys = new[] { new KeyPosition(0, "esc"), new KeyPosition(2, "F1") };

            Assert.Throws<ArgumentException>(() => new LayerGeometry("Qwerty-top", 0, keys));
        }

        [Fact]
        public void Constructor_WithKeysOutOfIndexOrder_ThrowsArgumentException()
        {
            var keys = new[] { new KeyPosition(1, "F1"), new KeyPosition(0, "esc") };

            Assert.Throws<ArgumentException>(() => new LayerGeometry("Qwerty-top", 0, keys));
        }

        [Fact]
        public void Constructor_WithNonDenseEdgeZones_ThrowsArgumentException()
        {
            var keys = new[] { new KeyPosition(0, "esc") };
            var zones = new[] { new KeyPosition(5, "L1") };

            Assert.Throws<ArgumentException>(() => new LayerGeometry("Qwerty-top", 0, keys, zones));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithMissingName_Throws(string? name)
        {
            var keys = new[] { new KeyPosition(0, "esc") };

            Assert.ThrowsAny<ArgumentException>(() => new LayerGeometry(name!, 0, keys));
        }

        [Fact]
        public void Constructor_WithNegativeLayerIndex_ThrowsArgumentOutOfRangeException()
        {
            var keys = new[] { new KeyPosition(0, "esc") };

            Assert.Throws<ArgumentOutOfRangeException>(() => new LayerGeometry("Qwerty-top", -1, keys));
        }

        [Fact]
        public void Constructor_WithMutatedSourceListAfterwards_KeepsOriginalKeys()
        {
            var keys = new List<KeyPosition> { new(0, "esc"), new(1, "F1") };
            var layer = new LayerGeometry("Qwerty-top", 0, keys);

            keys[1] = new KeyPosition(1, "hacked");

            Assert.Equal("F1", layer.Keys[1].DefaultToken);
        }
    }
}
