using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the TKO edge-line classifier of specs/07-lighting.md §2.3: after stripping an
    /// optional <c>fn </c> prefix, a line is edge syntax when it starts with <c>[lN]</c>,
    /// <c>[rN]</c>, or <c>[bN]</c> (N tolerated up to 30, beyond the 33 real LEDs) or with any
    /// <c>_edge</c> mode token.
    /// </summary>
    public class TkoEdgeLineClassifierTests
    {
        [Theory]
        [InlineData("[l1]>[255][0][0]")]
        [InlineData("[L9]>[255][0][0]")]
        [InlineData("[r5]>[0][0][255]")]
        [InlineData("[b15]>[0][255][0]")]
        [InlineData("[b30]>[0][255][0]")]
        [InlineData("[l30]>[1][2][3]")]
        [InlineData("[r30]")]
        public void IsEdgeLine_WithEdgeAddressUpToThirty_ReturnsTrue(string line)
        {
            Assert.True(TkoEdgeLineClassifier.IsEdgeLine(line));
        }

        [Theory]
        [InlineData("[mono_edge]>[255][0][0]")]
        [InlineData("[breathe_edge]>[spd5]")]
        [InlineData("[spectrum_edge]>[spd5]")]
        [InlineData("[wave_edge]>[spd5][dirleft]")]
        [InlineData("[rebound_edge]>[0][255][0][spd5]")]
        [InlineData("[loop_edge]>[0][255][0][spd5][dirright]")]
        [InlineData("[pulse_edge]>[spd5]")]
        [InlineData("[frozenwave_edge]")]
        [InlineData("[FROZENWAVE_EDGE]")]
        public void IsEdgeLine_WithEdgeModeToken_ReturnsTrue(string line)
        {
            Assert.True(TkoEdgeLineClassifier.IsEdgeLine(line));
        }

        [Theory]
        [InlineData("fn [l1]>[255][0][0]")]
        [InlineData("FN [mono_edge]>[255][0][0]")]
        [InlineData("fn [wave_edge]>[spd5][dirleft]")]
        public void IsEdgeLine_WithFnPrefixedEdgeLine_ReturnsTrue(string line)
        {
            Assert.True(TkoEdgeLineClassifier.IsEdgeLine(line));
        }

        [Theory]
        [InlineData("[mono]>[255][0][0]")]
        [InlineData("[breathe]>[spd5]")]
        [InlineData("[black]")]
        [InlineData("[b]>[255][0][0]")]
        [InlineData("[l0]>[255][0][0]")]
        [InlineData("[l31]>[255][0][0]")]
        [InlineData("[b31]>[255][0][0]")]
        [InlineData("[a]>[255][0][0]")]
        [InlineData("fn [q]>[255][0][0]")]
        [InlineData("[wave]>[spd5][dirleft]")]
        [InlineData("wave_edge without brackets")]
        [InlineData("")]
        [InlineData(null)]
        public void IsEdgeLine_WithKeyBacklightOrInvalidLine_ReturnsFalse(string? line)
        {
            Assert.False(TkoEdgeLineClassifier.IsEdgeLine(line));
        }
    }
}
