using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the color building blocks of specs/07-lighting.md §2.1 and the defaults of §6:
    /// black is the "no color" value and lime green RGB(0,255,0) the default effect color.
    /// </summary>
    public class LedColorTests
    {
        [Fact]
        public void Black_Always_HasAllZeroComponentsAndIsBlack()
        {
            Assert.Equal(0, LedColor.Black.Red);
            Assert.Equal(0, LedColor.Black.Green);
            Assert.Equal(0, LedColor.Black.Blue);
            Assert.True(LedColor.Black.IsBlack);
        }

        [Fact]
        public void DefaultEffectColor_Always_IsLimeGreenPerSpec06()
        {
            Assert.Equal(new LedColor(0, 255, 0), LedColor.DefaultEffectColor);
            Assert.False(LedColor.DefaultEffectColor.IsBlack);
        }

        [Fact]
        public void Constructor_WithComponents_StoresThem()
        {
            var color = new LedColor(255, 128, 0);

            Assert.Equal(255, color.Red);
            Assert.Equal(128, color.Green);
            Assert.Equal(0, color.Blue);
            Assert.False(color.IsBlack);
        }

        [Fact]
        public void Equality_WithSameComponents_IsValueBased()
        {
            Assert.Equal(new LedColor(1, 2, 3), new LedColor(1, 2, 3));
            Assert.NotEqual(new LedColor(1, 2, 3), new LedColor(3, 2, 1));
        }
    }
}
