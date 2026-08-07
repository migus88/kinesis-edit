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

        [Fact]
        public void Sections_OfTheAuthoredBoard_AreTheTwoPanelsOfMockup1e()
        {
            Assert.Equal(2, Visual.Sections.Count);
            Assert.Equal(new[] { 0, 1 }, Visual.Sections.Select(section => section.Index));
        }

        [Fact]
        public void Sections_OfTheAuthoredBoard_PutTheHotkeyColumnWithTheLeftHalf()
        {
            // Mockup 1e draws the hotkey column and the left typing half inside one bordered box.
            var left = Visual.Sections[0].Keys.Select(key => key.Index).ToHashSet();

            foreach (var hotkeyIndex in new[] { 0, 17, 18, 34, 35, 51, 52, 67, 68, 83, 84 })
            {
                Assert.Contains(hotkeyIndex, left);
            }

            Assert.Contains(1, left);
            Assert.Contains(88, left);
            Assert.DoesNotContain(8, left);
            Assert.DoesNotContain(94, left);
        }

        [Fact]
        public void Sections_OfTheAuthoredBoard_PartitionEveryKeyExactlyOnce()
        {
            var union = Visual.Sections.SelectMany(section => section.Keys).ToList();

            Assert.Equal(Visual.Keys.Count, union.Count);
            Assert.Equal(
                Visual.Keys.Select(key => key.Index).OrderBy(index => index),
                union.Select(key => key.Index).OrderBy(index => index));
        }

        [Fact]
        public void Sections_OfTheAuthoredBoard_MeasureTheTwoHalvesInBoardAbsoluteUnits()
        {
            var left = Visual.Sections[0];
            var right = Visual.Sections[1];

            Assert.Equal(0.0, left.X);
            Assert.Equal(0.0, left.Y);
            Assert.Equal(9.25, left.Width);
            Assert.Equal(6.0, left.Height);

            Assert.Equal(10.25, right.X);
            Assert.Equal(0.0, right.Y);
            Assert.Equal(9.5, right.Width);
            Assert.Equal(6.0, right.Height);

            // The authored split gap survives sectioning: navigation crosses it, and a renderer
            // replaces it with the design's gutter rather than the board losing the space.
            Assert.Equal(1.0, right.X - left.Right);
        }

        [Fact]
        public void Keys_OfTheAuthoredBoard_KeepTheirCoordinatesUnchangedBySectioning()
        {
            // The two halves are drawn as separate panels but are authored in one continuous
            // space, because KeyAdjacency navigates across the gap.
            Assert.True(Visual.TryGetKey(88, out var leftSpace));
            Assert.True(Visual.TryGetKey(89, out var rightSpace));

            Assert.Equal(5.75, leftSpace!.X);
            Assert.Equal(10.75, rightSpace!.X);
        }

        [Fact]
        public void Legends_OfTheHotkeyColumn_AreTheIndicatorRowTheBoardPrints()
        {
            var column = new[] { 0, 17, 18, 34, 35, 51, 52, 67, 68, 83, 84 }
                .Select(index => Legend(index))
                .ToArray();

            // Mockup 1e reads "☾ ① ② ③ ④ ⑤ ⑥ ⑦ ⑧ Fn ☼"; the moon, the sun and the circled digits
            // are absent from both embedded IBM Plex families, so the covered substitutes are the
            // positions' own names (spec 05 §3.11: "hotkey 0..8", and "LED" as the sun's fallback).
            Assert.Equal(new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "Fn", "LED" }, column);
        }

        [Fact]
        public void Legends_OfTheNumberRows_SplitTheCaptionIntoItsTwoPrintedHalves()
        {
            // The Gen1 caption of a digit is "1 !" on one line; the board prints the two apart.
            Assert.Equal(("`", "~"), LegendPair(19));
            Assert.Equal(("1", "!"), LegendPair(20));
            Assert.Equal(("6", "^"), LegendPair(25));
            Assert.Equal(("7", "&"), LegendPair(26));
            Assert.Equal(("0", ")"), LegendPair(29));
            Assert.Equal(("-", "_"), LegendPair(30));
            Assert.Equal(("=", "+"), LegendPair(31));
        }

        [Fact]
        public void Legends_OfTheShiftedPunctuationKeys_AreSplitTheSameWayAsTheDigits()
        {
            // Mockup 1e: "real legends including secondary labels". A board that splits `1 !` but
            // leaves `[ {` as one caption reads as two different keyboards.
            Assert.Equal(("[", "{"), LegendPair(47));
            Assert.Equal(("]", "}"), LegendPair(48));
            Assert.Equal(("\\", "|"), LegendPair(49));
            Assert.Equal((";", ":"), LegendPair(63));
            Assert.Equal(("'", "\""), LegendPair(64));
            Assert.Equal((",", "<"), LegendPair(77));
            Assert.Equal((".", ">"), LegendPair(78));
            Assert.Equal(("/", "?"), LegendPair(79));
        }

        [Fact]
        public void Legends_OfTheFunctionRows_AreTheDeviceHotkeysPrintedUnderTheFKeys()
        {
            // F1..F12 print their own name, which is exactly what the caption already says, so the
            // primary legend stays null and only the secondary is authored.
            Assert.Equal((null, "mute"), LegendPair(2));
            Assert.Equal((null, "next"), LegendPair(7));
            Assert.Equal((null, "status"), LegendPair(8));
            Assert.Equal((null, "reset"), LegendPair(13));
        }

        [Fact]
        public void Legends_OfThePrintPauseAndDeleteKeys_AreTheirSilkscreen()
        {
            Assert.Equal(("Prt sc", null), LegendPair(14));
            Assert.Equal(("Pause", "insert"), LegendPair(15));
            Assert.Equal(("Del", "scr lk"), LegendPair(16));
        }

        [Fact]
        public void Legends_ArePresentOnlyWhereThePrintDiffersFromTheCaption()
        {
            // Esc, Backspace, Home and every alpha say the same thing twice if authored, and a
            // second source of truth for one string is what "null means use the caption" avoids.
            foreach (var index in new[] { 1, 32, 33, 36, 37, 53, 69, 85, 88, 94 })
            {
                Assert.Equal((null, null), LegendPair(index));
            }
        }

        private static string? Legend(int index)
        {
            Assert.True(Visual.TryGetKey(index, out var key));

            return key!.Legend;
        }

        private static (string? Legend, string? Secondary) LegendPair(int index)
        {
            Assert.True(Visual.TryGetKey(index, out var key));

            return (key!.Legend, key.SecondaryLegend);
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
