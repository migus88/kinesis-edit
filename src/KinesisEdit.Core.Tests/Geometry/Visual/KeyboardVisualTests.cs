using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;

namespace KinesisEdit.Core.Tests.Geometry.Visual
{
    public class KeyboardVisualTests
    {
        [Fact]
        public void Bounds_OfAPopulatedBoard_AreTheMaximumRightAndBottomEdges()
        {
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 0.0, 0.0),
                    new KeyVisual(1, 1.0, 0.0, 2.5),
                    new KeyVisual(2, 0.0, 1.0, 1.0, 1.5)
                });

            Assert.Equal(3.5, visual.Width);
            Assert.Equal(2.5, visual.Height);
        }

        [Fact]
        public void Bounds_OfAnEmptyBoard_AreZero()
        {
            var visual = new KeyboardVisual(LayoutVariant.None, Array.Empty<KeyVisual>());

            Assert.Equal(0.0, visual.Width);
            Assert.Equal(0.0, visual.Height);
            Assert.Empty(visual.Keys);
        }

        [Fact]
        public void TryGetKey_WithAKnownIndex_ReturnsThePlacement()
        {
            var placement = new KeyVisual(42, 3.0, 2.0);
            var visual = new KeyboardVisual(LayoutVariant.Qwerty, new[] { placement });

            Assert.True(visual.TryGetKey(42, out var key));
            Assert.Same(placement, key);
        }

        [Fact]
        public void TryGetKey_WithAnUnknownIndex_ReturnsFalse()
        {
            var visual = new KeyboardVisual(LayoutVariant.Qwerty, new[] { new KeyVisual(0, 0.0, 0.0) });

            Assert.False(visual.TryGetKey(1, out var key));
            Assert.Null(key);
        }

        [Fact]
        public void Constructor_WithDuplicateIndices_Throws()
        {
            var keys = new[]
            {
                new KeyVisual(3, 0.0, 0.0),
                new KeyVisual(3, 1.0, 0.0)
            };

            Assert.Throws<ArgumentException>(() => new KeyboardVisual(LayoutVariant.Qwerty, keys));
        }

        [Fact]
        public void Constructor_WithNullKeyList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new KeyboardVisual(LayoutVariant.Qwerty, null!));
        }

        [Fact]
        public void Keys_AfterConstruction_AreDetachedFromTheSourceList()
        {
            var source = new List<KeyVisual> { new(0, 0.0, 0.0) };

            var visual = new KeyboardVisual(LayoutVariant.Qwerty, source);
            source.Add(new KeyVisual(1, 1.0, 0.0));

            Assert.Single(visual.Keys);
        }

        [Fact]
        public void Variant_AfterConstruction_IsPreserved()
        {
            var visual = new KeyboardVisual(LayoutVariant.Dvorak, Array.Empty<KeyVisual>());

            Assert.Equal(LayoutVariant.Dvorak, visual.Variant);
        }

        [Fact]
        public void Sections_OfABoardThatDeclaresNone_AreOneSectionHoldingEveryKey()
        {
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 0.0, 0.0),
                    new KeyVisual(1, 1.0, 0.0)
                });

            var section = Assert.Single(visual.Sections);

            Assert.Equal(0, section.Index);
            Assert.Equal(visual.Keys, section.Keys);
        }

        [Fact]
        public void Sections_OfAnEmptyBoard_AreEmpty()
        {
            Assert.Empty(new KeyboardVisual(LayoutVariant.None, Array.Empty<KeyVisual>()).Sections);
        }

        [Fact]
        public void Sections_PartitionTheKeySetExactly()
        {
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 0.0, 0.0, section: 0),
                    new KeyVisual(1, 4.0, 0.0, section: 1),
                    new KeyVisual(2, 1.0, 0.0, section: 0),
                    new KeyVisual(3, 5.0, 0.0, section: 1)
                });

            var union = visual.Sections.SelectMany(section => section.Keys).ToList();

            Assert.Equal(visual.Keys.Count, union.Count);
            Assert.Equal(visual.Keys.OrderBy(key => key.Index), union.OrderBy(key => key.Index));
            Assert.Equal(visual.Keys.Count, union.Distinct().Count());
        }

        [Fact]
        public void Sections_AreOrderedByIndexAndDense()
        {
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 8.0, 0.0, section: 2),
                    new KeyVisual(1, 0.0, 0.0, section: 0),
                    new KeyVisual(2, 4.0, 0.0, section: 1)
                });

            Assert.Equal(new[] { 0, 1, 2 }, visual.Sections.Select(section => section.Index));
        }

        [Fact]
        public void Sections_KeepTheBoardsAuthoringOrderInsideEachOne()
        {
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(7, 1.0, 0.0, section: 0),
                    new KeyVisual(3, 4.0, 0.0, section: 1),
                    new KeyVisual(5, 0.0, 0.0, section: 0)
                });

            Assert.Equal(new[] { 7, 5 }, visual.Sections[0].Keys.Select(key => key.Index));
        }

        [Fact]
        public void Sections_MeasureTheBoardAbsoluteBoundingBoxOfTheirKeys()
        {
            var visual = new KeyboardVisual(
                LayoutVariant.Qwerty,
                new[]
                {
                    new KeyVisual(0, 0.0, 0.0, 2.0, section: 0),
                    new KeyVisual(1, 0.0, 1.0, 1.0, 1.5, section: 0),
                    new KeyVisual(2, 4.0, 1.0, 2.5, section: 1),
                    new KeyVisual(3, 5.0, 2.0, section: 1)
                });

            var left = visual.Sections[0];
            var right = visual.Sections[1];

            Assert.Equal(0.0, left.X);
            Assert.Equal(0.0, left.Y);
            Assert.Equal(2.0, left.Width);
            Assert.Equal(2.5, left.Height);

            // The right panel keeps its board-absolute origin: nothing is re-based, because
            // KeyAdjacency navigates the one continuous coordinate space the keys were authored in.
            Assert.Equal(4.0, right.X);
            Assert.Equal(1.0, right.Y);
            Assert.Equal(2.5, right.Width);
            Assert.Equal(2.0, right.Height);
            Assert.Equal(6.5, right.Right);
            Assert.Equal(3.0, right.Bottom);
        }

        [Fact]
        public void Constructor_WithASectionIndexGap_Throws()
        {
            var keys = new[]
            {
                new KeyVisual(0, 0.0, 0.0, section: 0),
                new KeyVisual(1, 4.0, 0.0, section: 2)
            };

            Assert.Throws<ArgumentException>(() => new KeyboardVisual(LayoutVariant.Qwerty, keys));
        }

        [Fact]
        public void Constructor_WithSectionsThatDoNotStartAtZero_Throws()
        {
            var keys = new[] { new KeyVisual(0, 0.0, 0.0, section: 1) };

            Assert.Throws<ArgumentException>(() => new KeyboardVisual(LayoutVariant.Qwerty, keys));
        }
    }
}
