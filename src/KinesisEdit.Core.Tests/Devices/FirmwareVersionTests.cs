using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    public class FirmwareVersionTests
    {
        [Theory]
        [InlineData("1.0.1709.us (4MB), 03/08/2019", 1, 0, 1709)]
        [InlineData("1.0.340.us (2MB), 09/26/2016", 1, 0, 340)]
        [InlineData("1.0.521", 1, 0, 521)]
        [InlineData("1.0.44", 1, 0, 44)]
        [InlineData("2.0.20", 2, 0, 20)]
        [InlineData("255.255", 255, 255, 0)]
        [InlineData("7", 7, 0, 0)]
        [InlineData("  1.0.480  ", 1, 0, 480)]
        public void TryParse_WithVersionStringsFromSpec_ReturnsParsedComponents(
            string text,
            int expectedMajor,
            int expectedMinor,
            int expectedRevision)
        {
            var isParsed = FirmwareVersion.TryParse(text, out var version);

            Assert.True(isParsed);
            Assert.Equal(expectedMajor, version.Major);
            Assert.Equal(expectedMinor, version.Minor);
            Assert.Equal(expectedRevision, version.Revision);
        }

        [Theory]
        [InlineData("1.x.5", 1, 0, 5)]
        [InlineData("1.0.beta", 1, 0, 0)]
        public void TryParse_WithNonNumericMinorOrRevisionToken_ParsesTokenAsZero(
            string text,
            int expectedMajor,
            int expectedMinor,
            int expectedRevision)
        {
            var isParsed = FirmwareVersion.TryParse(text, out var version);

            Assert.True(isParsed);
            Assert.Equal(new FirmwareVersion(expectedMajor, expectedMinor, expectedRevision), version);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("garbage")]
        [InlineData("us (4MB), 03/08/2019")]
        [InlineData("v1.0.340")]
        [InlineData("-1.0.0")]
        public void TryParse_WithGarbage_ReturnsFalse(string? text)
        {
            var isParsed = FirmwareVersion.TryParse(text, out var version);

            Assert.False(isParsed);
            Assert.Equal(default, version);
        }

        [Theory]
        [InlineData("1.0.340", "1.0.480")]
        [InlineData("1.0.44", "1.0.58")]
        [InlineData("1.0.999", "1.1.0")]
        [InlineData("1.9.999", "2.0.0")]
        public void CompareTo_WithSmallerVersionOnLeft_OrdersLexicographically(string smallerText, string biggerText)
        {
            FirmwareVersion.TryParse(smallerText, out var smaller);
            FirmwareVersion.TryParse(biggerText, out var bigger);

            Assert.True(smaller.CompareTo(bigger) < 0);
            Assert.True(bigger.CompareTo(smaller) > 0);
            Assert.True(smaller < bigger);
            Assert.True(bigger > smaller);
            Assert.True(smaller <= bigger);
            Assert.True(bigger >= smaller);
        }

        [Fact]
        public void CompareTo_WithEqualVersions_ReturnsZero()
        {
            var left = new FirmwareVersion(1, 0, 516);
            var right = new FirmwareVersion(1, 0, 516);

            Assert.Equal(0, left.CompareTo(right));
            Assert.True(left <= right);
            Assert.True(left >= right);
        }

        [Fact]
        public void Sort_WithUnorderedVersions_OrdersByMajorThenMinorThenRevision()
        {
            var versions = new List<FirmwareVersion>
            {
                new(1, 0, 1709),
                new(1, 0, 1),
                new(2, 0, 0),
                new(1, 0, 340),
                new(1, 1, 0),
            };

            versions.Sort();

            var expected = new List<FirmwareVersion>
            {
                new(1, 0, 1),
                new(1, 0, 340),
                new(1, 0, 1709),
                new(1, 1, 0),
                new(2, 0, 0),
            };

            Assert.Equal(expected, versions);
        }

        [Fact]
        public void Equals_WithSameComponents_ReturnsTrue()
        {
            var left = new FirmwareVersion(1, 0, 480);
            var right = new FirmwareVersion(1, 0, 480);

            Assert.True(left.Equals(right));
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Theory]
        [InlineData(2, 0, 480)]
        [InlineData(1, 1, 480)]
        [InlineData(1, 0, 481)]
        public void Equals_WithDifferentComponents_ReturnsFalse(int major, int minor, int revision)
        {
            var left = new FirmwareVersion(1, 0, 480);
            var right = new FirmwareVersion(major, minor, revision);

            Assert.False(left.Equals(right));
            Assert.False(left == right);
            Assert.True(left != right);
        }

        [Fact]
        public void ToString_WithParsedRealWorldString_ReturnsCanonicalForm()
        {
            FirmwareVersion.TryParse("1.0.1709.us (4MB), 03/08/2019", out var version);

            Assert.Equal("1.0.1709", version.ToString());
        }
    }
}
