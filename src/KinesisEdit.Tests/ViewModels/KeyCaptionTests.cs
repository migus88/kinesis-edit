using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class KeyCaptionTests
    {
        [Fact]
        public void For_WithAGlyphKey_PrefersTheGlyph()
        {
            var playPause = TestLayouts.Gen1Key("play");

            Assert.Equal("⏯", KeyCaption.For(playPause, TokenDialect.Gen1, isMacOs: false));
            Assert.Equal("⏯", KeyCaption.For(playPause, TokenDialect.Gen1, isMacOs: true));
        }

        [Fact]
        public void For_OnMacOs_PrefersTheMacCaptionOverTheDialectCaption()
        {
            var enter = TestLayouts.Gen1Key("ent");

            Assert.Equal("Return", KeyCaption.For(enter, TokenDialect.Gen1, isMacOs: true));
            Assert.Equal("Enter", KeyCaption.For(enter, TokenDialect.Gen1, isMacOs: false));
        }

        [Fact]
        public void For_WithoutAGlyphOrMacCaption_UsesTheDialectCaption()
        {
            var one = TestLayouts.Gen1Key("1");

            Assert.Equal("1 !", KeyCaption.For(one, TokenDialect.Gen1, isMacOs: true));
            Assert.Equal("!\n1", KeyCaption.For(one, TokenDialect.Legacy, isMacOs: false));
        }

        [Fact]
        public void For_WithATwoLineCaption_KeepsTheLineBreak()
        {
            var backspace = TestLayouts.Gen1Key("bspc");

            Assert.Equal("Back\nSpace", KeyCaption.For(backspace, TokenDialect.Gen1, isMacOs: false));
            Assert.Equal("Fwd \nDelete", KeyCaption.For(TestLayouts.Gen1Key("del"), TokenDialect.Gen1, isMacOs: true));
        }

        [Theory]
        [InlineData("hk0")]
        [InlineData("hk1")]
        [InlineData("hk8")]
        public void For_WithABlankDisplayText_FallsBackToTheDialectToken(string token)
        {
            // 05 §3.11 registers hk0-hk8 with ' ' as their display text (the physical caps are
            // unlabelled), which would otherwise draw a column of indistinguishable empty caps.
            var hotkey = TestLayouts.Gen1Key(token);

            Assert.True(string.IsNullOrWhiteSpace(hotkey.GetDisplayText(TokenDialect.Gen1)));
            Assert.Equal(token, KeyCaption.For(hotkey, TokenDialect.Gen1, isMacOs: false));
        }

        [Fact]
        public void For_WithALabelledHotkey_KeepsItsDisplayTextRatherThanTheToken()
        {
            Assert.Equal("Fn\nToggle", KeyCaption.For(TestLayouts.Gen1Key("hk9"), TokenDialect.Gen1, isMacOs: false));
        }

        [Fact]
        public void ForKey_OnTheFreestyleEdgeRgbHotkeyColumn_NeverProducesABlankCaption()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var captions = layout.Layers[0].Keys
                .Select(key => KeyCaption.ForKey(key, layout.Dialect))
                .ToList();

            Assert.All(captions, caption => Assert.False(string.IsNullOrWhiteSpace(caption)));
        }

        [Fact]
        public void ForKey_WithARemappedKey_ReportsTheRemappedAction()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            Assert.Equal("1 !", KeyCaption.ForKey(key, layout.Dialect));

            key.Remap(TestLayouts.Gen1Key("z"));

            Assert.Equal("Z", KeyCaption.ForKey(key, layout.Dialect));
        }
    }
}
