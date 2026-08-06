using KinesisEdit.Core.Geometry.Visual;

namespace KinesisEdit.Core.Tests.Geometry.Visual
{
    public class KeyVisualTests
    {
        [Fact]
        public void Constructor_WithoutOptionalArguments_DefaultsToASingleUnitMainKey()
        {
            var key = new KeyVisual(7, 2.5, 3.0);

            Assert.Equal(7, key.Index);
            Assert.Equal(2.5, key.X);
            Assert.Equal(3.0, key.Y);
            Assert.Equal(1.0, key.Width);
            Assert.Equal(1.0, key.Height);
            Assert.Equal(KeyCluster.Main, key.Cluster);
        }

        [Fact]
        public void RightAndBottom_OfAKey_AreTheFarEdgesOfItsRectangle()
        {
            var key = new KeyVisual(0, 2.0, 4.0, 2.25, 1.5);

            Assert.Equal(4.25, key.Right);
            Assert.Equal(5.5, key.Bottom);
        }

        [Fact]
        public void Constructor_WithNegativeIndex_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyVisual(-1, 0.0, 0.0));
        }

        [Fact]
        public void Constructor_WithNegativeX_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyVisual(0, -0.5, 0.0));
        }

        [Fact]
        public void Constructor_WithNegativeY_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyVisual(0, 0.0, -0.5));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_WithNonPositiveWidth_Throws(double width)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyVisual(0, 0.0, 0.0, width));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_WithNonPositiveHeight_Throws(double height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyVisual(0, 0.0, 0.0, 1.0, height));
        }

        [Fact]
        public void Constructor_AtTheOrigin_IsAccepted()
        {
            var key = new KeyVisual(0, 0.0, 0.0, 1.0, 1.0, KeyCluster.Function);

            Assert.Equal(0.0, key.X);
            Assert.Equal(0.0, key.Y);
            Assert.Equal(KeyCluster.Function, key.Cluster);
        }

        [Fact]
        public void Constructor_WithoutSectionOrLegends_PutsTheKeyInSectionZeroWithNoPrint()
        {
            var key = new KeyVisual(3, 1.0, 1.0);

            Assert.Equal(0, key.Section);
            Assert.Null(key.Legend);
            Assert.Null(key.SecondaryLegend);
        }

        [Fact]
        public void Constructor_WithASectionAndLegends_KeepsThemVerbatim()
        {
            var key = new KeyVisual(3, 1.0, 1.0, 1.0, 1.0, KeyCluster.Main, section: 2, legend: "1", secondaryLegend: "!");

            Assert.Equal(2, key.Section);
            Assert.Equal("1", key.Legend);
            Assert.Equal("!", key.SecondaryLegend);
        }

        [Fact]
        public void Constructor_WithANegativeSection_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyVisual(0, 0.0, 0.0, section: -1));
        }
    }
}
