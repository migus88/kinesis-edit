using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the per-layer lighting state against the defaults and reset rules of
    /// specs/07-lighting.md §6, and the "black is no color" per-key map semantics of §2.1/§2.2
    /// (keys with no color are omitted on save).
    /// </summary>
    public class LayerLightingStateTests
    {
        [Fact]
        public void Constructor_Always_StartsInTheResetDefaultsOfSpec06()
        {
            var state = new LayerLightingState();

            Assert.Equal(LightingMode.Disabled, state.Mode);
            Assert.Equal(LedColor.DefaultEffectColor, state.EffectColor);
            Assert.Equal(LedColor.Black, state.BaseColor);
            Assert.Equal(5, state.Speed);
            Assert.Equal(LightingDirection.Left, state.Direction);
            Assert.Empty(state.KeyColors);
        }

        [Fact]
        public void Reset_AfterChanges_RestoresEveryDefault()
        {
            var state = new LayerLightingState
            {
                Mode = LightingMode.Rain,
                EffectColor = new LedColor(255, 0, 0),
                BaseColor = new LedColor(1, 2, 3),
                Speed = 9,
                Direction = LightingDirection.Up
            };

            state.SetKeyColor(65, new LedColor(10, 20, 30));

            state.Reset();

            Assert.True(state.IsEquivalentTo(new LayerLightingState()));
        }

        [Fact]
        public void SetKeyColor_WithNonBlackColor_StoresIt()
        {
            var state = new LayerLightingState();

            state.SetKeyColor(65, new LedColor(0, 0, 255));

            Assert.Equal(new LedColor(0, 0, 255), state.KeyColors[65]);
        }

        [Fact]
        public void SetKeyColor_WithBlack_RemovesTheAssignment()
        {
            var state = new LayerLightingState();

            state.SetKeyColor(65, new LedColor(0, 0, 255));
            state.SetKeyColor(65, LedColor.Black);

            Assert.Empty(state.KeyColors);
        }

        [Fact]
        public void ClearKeyColors_WithAssignments_ErasesThemAll()
        {
            var state = new LayerLightingState();

            state.SetKeyColor(65, new LedColor(0, 0, 255));
            state.SetKeyColor(66, new LedColor(255, 0, 0));

            state.ClearKeyColors();

            Assert.Empty(state.KeyColors);
        }

        [Fact]
        public void IsEquivalentTo_WithSameValues_ReturnsTrue()
        {
            var first = new LayerLightingState { Mode = LightingMode.Breathe, Speed = 7 };
            var second = new LayerLightingState { Mode = LightingMode.Breathe, Speed = 7 };

            first.SetKeyColor(83, new LedColor(0, 255, 0));
            second.SetKeyColor(83, new LedColor(0, 255, 0));

            Assert.True(first.IsEquivalentTo(second));
        }

        [Fact]
        public void IsEquivalentTo_WithDifferentKeyColors_ReturnsFalse()
        {
            var first = new LayerLightingState();
            var second = new LayerLightingState();

            first.SetKeyColor(83, new LedColor(0, 255, 0));
            second.SetKeyColor(83, new LedColor(255, 0, 0));

            Assert.False(first.IsEquivalentTo(second));
            Assert.False(first.IsEquivalentTo(null));
        }
    }
}
