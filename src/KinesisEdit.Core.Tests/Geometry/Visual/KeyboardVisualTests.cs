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
    }
}
