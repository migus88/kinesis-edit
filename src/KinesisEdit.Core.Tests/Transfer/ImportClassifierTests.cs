using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Core.Tests.Transfer
{
    /// <summary>
    /// The import classification truth table of specs/07-lighting.md §1.4: on the per-key RGB
    /// boards a file is a led file when its first line starts with <c>[</c> and contains
    /// <c>&gt;</c> and it carries a known mode token or a color-style value; on the Advantage360
    /// the discriminator is simply whether the first line contains <c>[ind</c>. Everything else
    /// "is tried as a layout".
    /// </summary>
    public sealed class ImportClassifierTests
    {
        [Fact]
        public void MaxImportBytes_IsFiftyKilobytes()
        {
            Assert.Equal(51200, ImportClassifier.MaxImportBytes);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        public void Classify_WithALedFileCarryingAModeToken_ReturnsLighting(DeviceId deviceId)
        {
            var lines = new[] { "[spectrum]>[spd3]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(deviceId, lines));
        }

        [Fact]
        public void Classify_WithATkoEdgeModeToken_ReturnsLighting()
        {
            var lines = new[] { "[spectrum_edge]>[spd3]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.Tko, lines));
        }

        [Fact]
        public void Classify_WithAPerKeyLedFileCarryingOnlyColorStyles_ReturnsLighting()
        {
            var lines = new[] { "[F1]>[255][0][0]", "[F2]>[0][255][0]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithAModeTokenOnALaterLine_ReturnsLighting()
        {
            var lines = new[] { "[caps]>[a]", "[wave]>[spd5][dirleft]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithALayoutFile_ReturnsLayout()
        {
            var lines = new[] { "[caps]>[a]", "fn [F1]>[esc]" };

            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithALayoutFileWhoseFirstLineIsAMacro_ReturnsLayout()
        {
            var lines = new[] { "{q}>{s5}{x1}{a}{b}", "[caps]>[a]" };

            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithAFirstLineThatDoesNotStartWithABracket_ReturnsLayout()
        {
            // The mode token is there, but §1.4 gates on the first line's shape first.
            var lines = new[] { "fn [mono]>[10][20][30]", "[mono]>[10][20][30]" };

            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithAFirstLineWithoutAValueSeparator_ReturnsLayout()
        {
            var lines = new[] { "[wave]" };

            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithATapAndHoldLayoutFile_ReturnsLighting()
        {
            // Documented consequence of the §1.4 heuristic, not a defect here: a tap-and-hold
            // value (11 §11.1) carries three bracketed groups, which is the color-style test.
            var lines = new[] { "[caps]>[a][t&h250][lctrl]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithAnEmptyFile_ReturnsLayout()
        {
            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, []));
            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.Advantage360, []));
        }

        [Fact]
        public void Classify_WithBlankLeadingLines_ReadsTheFirstLineWithContent()
        {
            var lines = new[] { string.Empty, "   ", "  [mono]>[10][20][30]  " };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithUppercaseTokens_MatchesCaseInsensitively()
        {
            var lines = new[] { "[SPECTRUM]>[SPD3]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, lines));
        }

        [Fact]
        public void Classify_WithAnAdvantage360IndicatorFile_ReturnsLighting()
        {
            var lines = new[] { "[ind1]>[caps][255][0][0]" };

            Assert.Equal(ImportedFileKind.Lighting, ImportClassifier.Classify(DeviceId.Advantage360, lines));
        }

        [Fact]
        public void Classify_WithAnAdvantage360LayoutFile_ReturnsLayout()
        {
            // Colour styles and mode tokens are irrelevant on the Adv360: only "[ind" decides.
            var lines = new[] { "<base>", "[caps]>[a][t&h250][lctrl]", "[mono]>[10][20][30]" };

            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(DeviceId.Advantage360, lines));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Advantage2)]
        public void Classify_WithADeviceWithoutLedFiles_ReturnsLayout(DeviceId deviceId)
        {
            var lines = new[] { "[mono]>[10][20][30]" };

            Assert.Equal(ImportedFileKind.Layout, ImportClassifier.Classify(deviceId, lines));
        }

        [Fact]
        public void Classify_WithNullLines_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ImportClassifier.Classify(DeviceId.FreestyleEdgeRgb, null!));
        }
    }
}
