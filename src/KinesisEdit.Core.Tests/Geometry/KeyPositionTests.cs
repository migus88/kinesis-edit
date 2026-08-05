using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    public class KeyPositionTests
    {
        [Fact]
        public void Constructor_WithOnlyIndexAndToken_UsesUnrestrictedDefaults()
        {
            var position = new KeyPosition(4, "esc");

            Assert.Equal(4, position.Index);
            Assert.Equal("esc", position.DefaultToken);
            Assert.Null(position.PositionToken);
            Assert.True(position.CanEdit);
            Assert.True(position.CanAssignMacro);
            Assert.Null(position.MasterAppDefaultToken);
        }

        [Fact]
        public void Constructor_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeyPosition(-1, "esc"));
        }

        [Fact]
        public void Constructor_WithNullDefaultToken_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new KeyPosition(0, null!));
        }

        [Fact]
        public void Constructor_WithEmptyDefaultToken_IsAllowedForNonWritableButtons()
        {
            var position = new KeyPosition(16, string.Empty, canEdit: false, canAssignMacro: false);

            Assert.Equal(string.Empty, position.DefaultToken);
            Assert.False(position.CanEdit);
        }

        [Fact]
        public void Equals_WithIdenticalValues_ReturnsTrue()
        {
            var left = new KeyPosition(6, "keyt", "kp");
            var right = new KeyPosition(6, "keyt", "kp");

            Assert.Equal(left, right);
        }

        [Fact]
        public void Equals_WithDifferentPositionToken_ReturnsFalse()
        {
            var left = new KeyPosition(6, "keyt", "kp");
            var right = new KeyPosition(6, "keyt");

            Assert.NotEqual(left, right);
        }
    }
}
