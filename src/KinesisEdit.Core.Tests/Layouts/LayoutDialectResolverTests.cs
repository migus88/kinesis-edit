using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// The device → layout-dialect mapping of specs/04-layout-file-format.md §4.1 and its token
    /// dialect (specs/05-key-model.md §3 legend). The SE2 pedal dialect is deliberately out of
    /// scope, as are the non-programmable devices.
    /// </summary>
    public class LayoutDialectResolverTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdge, LayoutDialect.Freestyle)]
        [InlineData(DeviceId.FreestylePro, LayoutDialect.Freestyle)]
        [InlineData(DeviceId.FreestyleEdgeRgb, LayoutDialect.Gen1Rgb)]
        [InlineData(DeviceId.Tko, LayoutDialect.Gen1Rgb)]
        [InlineData(DeviceId.Advantage360, LayoutDialect.Gen2)]
        [InlineData(DeviceId.Advantage2, LayoutDialect.Advantage2)]
        public void Resolve_ForAnInScopeDevice_ReturnsItsDialect(DeviceId deviceId, LayoutDialect expected)
        {
            Assert.Equal(expected, LayoutDialectResolver.Resolve(deviceId));
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void Resolve_ForAnOutOfScopeDevice_ReturnsNone(DeviceId deviceId)
        {
            Assert.Equal(LayoutDialect.None, LayoutDialectResolver.Resolve(deviceId));
        }

        [Theory]
        [InlineData(LayoutDialect.Freestyle, TokenDialect.Gen1)]
        [InlineData(LayoutDialect.Gen1Rgb, TokenDialect.Gen1)]
        [InlineData(LayoutDialect.Gen2, TokenDialect.Gen2)]
        [InlineData(LayoutDialect.Advantage2, TokenDialect.Legacy)]
        [InlineData(LayoutDialect.None, TokenDialect.None)]
        public void GetTokenDialect_ForEveryDialect_ReturnsTheTokenTable(LayoutDialect dialect, TokenDialect expected)
        {
            Assert.Equal(expected, LayoutDialectResolver.GetTokenDialect(dialect));
        }

        [Fact]
        public void Parser_ForThePedal_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LayoutFileParser(DeviceId.SavantElite2));
        }

        [Fact]
        public void Parser_ForAnInScopeDevice_ExposesTheDialect()
        {
            Assert.Equal(LayoutDialect.Gen1Rgb, new LayoutFileParser(DeviceId.Tko).Dialect);
        }
    }
}
