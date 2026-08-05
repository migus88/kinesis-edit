using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;

namespace KinesisEdit.Core.Tests.Geometry.Visual
{
    /// <summary>
    /// Pins the load-bearing invariant of the visual layer: the authored board must
    /// cover exactly the key indices of the logical geometry
    /// (<see cref="GeometryCatalog.FreestyleEdgeRgb"/>, spec 05 §4.2), on every layer,
    /// with non-overlapping caps.
    /// </summary>
    public class FreestyleEdgeRgbVisualTests
    {
        private const int ExpectedKeyCount = 95;

        public static IEnumerable<object[]> LayerIndices()
        {
            for (var layerIndex = 0; layerIndex < GeometryCatalog.FreestyleEdgeRgb.Layers.Count; layerIndex++)
            {
                yield return new object[] { layerIndex };
            }
        }

        private static KeyboardVisual Visual => VisualCatalog.FreestyleEdgeRgb;

        [Fact]
        public void Keys_OfTheAuthoredBoard_CountMatchesTheGeometryPositionCount()
        {
            Assert.Equal(ExpectedKeyCount, Visual.Keys.Count);
            Assert.Equal(ExpectedKeyCount, GeometryCatalog.FreestyleEdgeRgb.Layers[0].Keys.Count);
        }

        [Theory]
        [MemberData(nameof(LayerIndices))]
        public void Keys_ForEveryLayer_CoverExactlyTheGeometryIndices(int layerIndex)
        {
            var layer = GeometryCatalog.FreestyleEdgeRgb.Layers[layerIndex];

            var geometryIndices = layer.Keys.Select(key => key.Index).ToHashSet();
            var visualIndices = Visual.Keys.Select(key => key.Index).ToHashSet();

            Assert.Equal(geometryIndices, visualIndices);
        }

        [Theory]
        [MemberData(nameof(LayerIndices))]
        public void TryGetKey_ForEveryGeometryPosition_ResolvesAPlacement(int layerIndex)
        {
            var layer = GeometryCatalog.FreestyleEdgeRgb.Layers[layerIndex];

            foreach (var position in layer.Keys)
            {
                Assert.True(Visual.TryGetKey(position.Index, out var key));
                Assert.Equal(position.Index, key!.Index);
            }
        }

        [Fact]
        public void Keys_OfTheAuthoredBoard_HaveUniqueIndices()
        {
            var distinctIndices = Visual.Keys.Select(key => key.Index).Distinct().Count();

            Assert.Equal(Visual.Keys.Count, distinctIndices);
        }

        [Fact]
        public void Keys_OfTheAuthoredBoard_DoNotOverlap()
        {
            var keys = Visual.Keys;

            for (var i = 0; i < keys.Count; i++)
            {
                for (var j = i + 1; j < keys.Count; j++)
                {
                    Assert.False(
                        Overlaps(keys[i], keys[j]),
                        $"Keys {keys[i].Index} and {keys[j].Index} overlap.");
                }
            }
        }

        [Fact]
        public void Keys_OfTheAuthoredBoard_UseNonNegativeCoordinatesAndPositiveSizes()
        {
            foreach (var key in Visual.Keys)
            {
                Assert.True(key.X >= 0.0, $"Key {key.Index} has negative X.");
                Assert.True(key.Y >= 0.0, $"Key {key.Index} has negative Y.");
                Assert.True(key.Width > 0.0, $"Key {key.Index} has a non-positive width.");
                Assert.True(key.Height > 0.0, $"Key {key.Index} has a non-positive height.");
            }
        }

        [Fact]
        public void Bounds_OfTheAuthoredBoard_EqualTheMaximumRightAndBottomEdges()
        {
            Assert.Equal(Visual.Keys.Max(key => key.Right), Visual.Width);
            Assert.Equal(Visual.Keys.Max(key => key.Bottom), Visual.Height);
        }

        [Fact]
        public void Bounds_OfTheAuthoredBoard_SpanSixRowsAndBothHalves()
        {
            Assert.Equal(6.0, Visual.Height);
            Assert.Equal(19.75, Visual.Width);
        }

        [Fact]
        public void Variant_OfTheAuthoredBoard_MatchesTheLogicalGeometry()
        {
            Assert.Equal(GeometryCatalog.FreestyleEdgeRgb.Variant, Visual.Variant);
        }

        [Fact]
        public void Keys_OfTheHotkeyColumn_AreTheOnlyFunctionClusterKeys()
        {
            var functionIndices = Visual.Keys
                .Where(key => key.Cluster == KeyCluster.Function)
                .Select(key => key.Index)
                .OrderBy(index => index)
                .ToArray();

            Assert.Equal(new[] { 0, 17, 18, 34, 35, 51, 52, 67, 68, 83, 84 }, functionIndices);
        }

        [Fact]
        public void Keys_OutsideTheHotkeyColumn_BelongToTheMainCluster()
        {
            var clusters = Visual.Keys
                .Where(key => key.Cluster != KeyCluster.Function)
                .Select(key => key.Cluster)
                .Distinct()
                .ToArray();

            Assert.Equal(new[] { KeyCluster.Main }, clusters);
        }

        [Fact]
        public void Keys_OfTheTwoHalves_AreSeparatedByAHorizontalGap()
        {
            var leftHalfRight = Visual.Keys
                .Where(key => key.Cluster == KeyCluster.Main && key.X < 10.0)
                .Max(key => key.Right);

            var rightHalfLeft = Visual.Keys
                .Where(key => key.Cluster == KeyCluster.Main && key.X >= 10.0)
                .Min(key => key.X);

            Assert.True(rightHalfLeft - leftHalfRight >= 1.0);
        }

        private static bool Overlaps(KeyVisual first, KeyVisual second)
        {
            return first.X < second.Right
                && second.X < first.Right
                && first.Y < second.Bottom
                && second.Y < first.Bottom;
        }
    }
}
