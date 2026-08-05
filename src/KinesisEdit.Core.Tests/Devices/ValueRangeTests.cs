using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    /// <summary>
    /// Asserts the inclusive-range value type that carries the macro speed/repeat settings of
    /// specs/06-macros.md §4 and the tap-and-hold delay bounds of specs/11-feature-dialogs.md §11.1.
    /// </summary>
    public class ValueRangeTests
    {
        [Theory]
        [InlineData(0, 9, 0, 0, true)]
        [InlineData(0, 9, 0, 9, true)]
        [InlineData(0, 9, 0, 5, true)]
        [InlineData(0, 9, 0, -1, false)]
        [InlineData(0, 9, 0, 10, false)]
        [InlineData(1, 9, 5, 0, false)]
        [InlineData(1, 9, 5, 1, true)]
        [InlineData(1, 999, 250, 999, true)]
        [InlineData(1, 999, 250, 1000, false)]
        public void Contains_WithValue_ReportsInclusiveMembership(
            int minimum,
            int maximum,
            int defaultValue,
            int value,
            bool expectedContains)
        {
            var range = new ValueRange(minimum, maximum, defaultValue);

            Assert.Equal(expectedContains, range.Contains(value));
        }

        [Fact]
        public void Constructor_WithBoundsAndDefault_ExposesThemAsProperties()
        {
            var range = new ValueRange(Minimum: 1, Maximum: 999, Default: 250);

            Assert.Equal(1, range.Minimum);
            Assert.Equal(999, range.Maximum);
            Assert.Equal(250, range.Default);
        }

        [Fact]
        public void Equals_WithSameBoundsAndDefault_ComparesByValue()
        {
            Assert.Equal(new ValueRange(1, 9, 5), new ValueRange(1, 9, 5));
            Assert.NotEqual(new ValueRange(1, 9, 5), new ValueRange(1, 9, 1));
            Assert.NotEqual(new ValueRange(0, 9, 0), new ValueRange(1, 9, 0));
        }
    }
}
