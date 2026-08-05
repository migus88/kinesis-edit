using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the key-backlight led-file parsing rules of specs/07-lighting.md §2.4 against
    /// the exact sample files of §2.5: mode detection from the first (and second) line with
    /// Freestyle as the tolerant fallback, by-token base/effect assignment, the <c>fn </c>
    /// layer split, and the invalid-value fallbacks.
    /// </summary>
    public class RgbLedFileParserTests
    {
        private static readonly string[] Spec25FullTokenSet =
        [
            "hk0", "hk1", "hk2", "hk3", "hk4", "hk5", "hk6", "hk7", "hk8", "hk9", "hk10",
            "tilde", "hyph", "=", "bspc", "obrk", "cbrk", "\\", "colon", "apos", "com", "per",
            "lshft", "rshft", "lctrl", "rctrl", "lwin", "lalt", "ralt", "lspc", "rspc",
            "up", "lft", "dwn", "rght", "prnt", "pause", "del", "home", "end", "pup", "pdn",
            "caps", "ent", "tab"
        ];

        [Fact]
        public void ParseRgb_WithLed1BareWaveToken_UsesDefaultSpeedAndDirection()
        {
            var model = LedFileParser.ParseRgb(["[wave]"]);

            Assert.Equal(LightingMode.Wave, model.TopLayer.Mode);
            Assert.Equal(5, model.TopLayer.Speed);
            Assert.Equal(LightingDirection.Left, model.TopLayer.Direction);
            Assert.Equal(LightingMode.Disabled, model.FnLayer.Mode);
        }

        [Fact]
        public void ParseRgb_WithLed2EffectFirstRainFile_AssignsBaseAndEffectByToken()
        {
            var lines = new[]
            {
                "[rain]>[255][128][0][spd1]",
                "[mono]>[195][255][142]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Rain, model.TopLayer.Mode);
            Assert.Equal(new LedColor(255, 128, 0), model.TopLayer.EffectColor);
            Assert.Equal(1, model.TopLayer.Speed);
            Assert.Equal(new LedColor(195, 255, 142), model.TopLayer.BaseColor);
        }

        [Fact]
        public void ParseRgb_WithMonoFirstRainFile_YieldsTheSameModelAsEffectFirst()
        {
            var effectFirst = LedFileParser.ParseRgb(["[rain]>[255][128][0][spd1]", "[mono]>[195][255][142]"]);
            var monoFirst = LedFileParser.ParseRgb(["[mono]>[195][255][142]", "[rain]>[255][128][0][spd1]"]);

            Assert.True(effectFirst.IsEquivalentTo(monoFirst));
        }

        [Fact]
        public void ParseRgb_WithEmptyLed3File_LoadsAsDisabled()
        {
            var model = LedFileParser.ParseRgb([]);

            Assert.Equal(LightingMode.Disabled, model.TopLayer.Mode);
            Assert.Equal(LightingMode.Disabled, model.FnLayer.Mode);
            Assert.True(model.IsEquivalentTo(new LightingModel()));
        }

        [Fact]
        public void ParseRgb_WithWhitespaceOnlyLines_LoadsAsDisabled()
        {
            var model = LedFileParser.ParseRgb(["", "   "]);

            Assert.True(model.IsEquivalentTo(new LightingModel()));
        }

        [Fact]
        public void ParseRgb_WithLed4ReboundFile_ReadsColorSpeedAndDirection()
        {
            var model = LedFileParser.ParseRgb(["[rebound]>[0][255][0][spd8][dirleft]"]);

            Assert.Equal(LightingMode.Rebound, model.TopLayer.Mode);
            Assert.Equal(new LedColor(0, 255, 0), model.TopLayer.EffectColor);
            Assert.Equal(8, model.TopLayer.Speed);
            Assert.Equal(LightingDirection.Left, model.TopLayer.Direction);
            Assert.Equal(LedColor.Black, model.TopLayer.BaseColor);
        }

        [Fact]
        public void ParseRgb_WithLed5StarlightFile_ReadsTheStarToken()
        {
            var model = LedFileParser.ParseRgb(["[star]>[0][0][255][spd7]"]);

            Assert.Equal(LightingMode.Starlight, model.TopLayer.Mode);
            Assert.Equal(new LedColor(0, 0, 255), model.TopLayer.EffectColor);
            Assert.Equal(7, model.TopLayer.Speed);
        }

        [Fact]
        public void ParseRgb_WithLed8BreatheFile_ReadsSpeedAndPerKeyColor()
        {
            var lines = new[]
            {
                "[breathe]>[spd7]",
                "[s]>[0][255][0]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Breathe, model.TopLayer.Mode);
            Assert.Equal(7, model.TopLayer.Speed);
            var keyColor = Assert.Single(model.TopLayer.KeyColors);
            Assert.Equal(CodeOf("s"), keyColor.Key);
            Assert.Equal(new LedColor(0, 255, 0), keyColor.Value);
        }

        [Fact]
        public void ParseRgb_WithLed9BarePerKeyLines_FallsBackToFreestyle()
        {
            var lines = new[]
            {
                "[e]>[255][0][0]",
                "[t]>[255][0][0]",
                "[a]>[189][255][57]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Freestyle, model.TopLayer.Mode);
            Assert.Equal(3, model.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(255, 0, 0), model.TopLayer.KeyColors[CodeOf("e")]);
            Assert.Equal(new LedColor(255, 0, 0), model.TopLayer.KeyColors[CodeOf("t")]);
            Assert.Equal(new LedColor(189, 255, 57), model.TopLayer.KeyColors[CodeOf("a")]);
        }

        [Fact]
        public void ParseRgb_WithSpec25BreatheLayoutTokenSample_ResolvesEveryToken()
        {
            var lines = new[]
            {
                "[breathe]>[spd7]",
                "[esc]>[255][0][0]",
                "[F4]>[0][255][0]",
                "[cbrk]>[0][0][255]",
                "[hk6]>[255][255][0]",
                "[ent]>[0][255][255]",
                "[/]>[255][0][255]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Breathe, model.TopLayer.Mode);
            Assert.Equal(6, model.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(255, 0, 0), model.TopLayer.KeyColors[CodeOf("esc")]);
            Assert.Equal(new LedColor(0, 255, 0), model.TopLayer.KeyColors[CodeOf("F4")]);
            Assert.Equal(new LedColor(255, 0, 255), model.TopLayer.KeyColors[CodeOf("/")]);
        }

        [Fact]
        public void ParseRgb_WithSpec25FullTokenSetBreatheSample_ResolvesEveryToken()
        {
            var lines = new List<string> { "[breathe]>[spd9]" };

            foreach (var token in Spec25FullTokenSet)
            {
                lines.Add("[" + token + "]>[10][20][30]");
            }

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Breathe, model.TopLayer.Mode);
            Assert.Equal(9, model.TopLayer.Speed);
            Assert.Equal(Spec25FullTokenSet.Length, model.TopLayer.KeyColors.Count);

            foreach (var token in Spec25FullTokenSet)
            {
                Assert.Equal(new LedColor(10, 20, 30), model.TopLayer.KeyColors[CodeOf(token)]);
            }
        }

        [Fact]
        public void ParseRgb_WithReactiveAndNoMonoLine_LeavesBaseColorAtItsBlackDefault()
        {
            var model = LedFileParser.ParseRgb(["[reactive]>[0][0][255][spd5]"]);

            Assert.Equal(LightingMode.Reactive, model.TopLayer.Mode);
            Assert.Equal(new LedColor(0, 0, 255), model.TopLayer.EffectColor);
            Assert.Equal(LedColor.Black, model.TopLayer.BaseColor);
        }

        [Theory]
        [InlineData("[mono]>[1][2][3]", LightingMode.Monochrome)]
        [InlineData("[breathe]>[spd3]", LightingMode.Breathe)]
        [InlineData("[spectrum]>[spd3]", LightingMode.Spectrum)]
        [InlineData("[wave]>[spd3][dirup]", LightingMode.Wave)]
        [InlineData("[pulse]>[spd3]", LightingMode.Pulse)]
        [InlineData("[reactive]>[1][2][3][spd3]", LightingMode.Reactive)]
        [InlineData("[ripple]>[1][2][3][spd3]", LightingMode.Ripple)]
        [InlineData("[fireball]>[1][2][3][spd3][dirright]", LightingMode.Fireball)]
        [InlineData("[star]>[1][2][3][spd3]", LightingMode.Starlight)]
        [InlineData("[rebound]>[1][2][3][spd3][dirup]", LightingMode.Rebound)]
        [InlineData("[loop]>[1][2][3][spd3][dirdown]", LightingMode.Loop)]
        [InlineData("[rain]>[1][2][3][spd3]", LightingMode.Rain)]
        public void ParseRgb_WithEachModeToken_DetectsTheMode(string line, LightingMode expectedMode)
        {
            Assert.Equal(expectedMode, LedFileParser.ParseRgb([line]).TopLayer.Mode);
        }

        [Fact]
        public void ParseRgb_WithTwoLineEffectTokenOnLineTwo_DetectsTheMode()
        {
            var model = LedFileParser.ParseRgb(["[mono]>[9][9][9]", "[loop]>[1][2][3][spd4][dirup]"]);

            Assert.Equal(LightingMode.Loop, model.TopLayer.Mode);
            Assert.Equal(new LedColor(9, 9, 9), model.TopLayer.BaseColor);
            Assert.Equal(new LedColor(1, 2, 3), model.TopLayer.EffectColor);
            Assert.Equal(4, model.TopLayer.Speed);
            Assert.Equal(LightingDirection.Up, model.TopLayer.Direction);
        }

        [Fact]
        public void ParseRgb_WithReservedBlackToken_FallsBackToFreestyle()
        {
            // [black] is reserved and never read (§2.2); its line resolves to no key token.
            var model = LedFileParser.ParseRgb(["[black]"]);

            Assert.Equal(LightingMode.Freestyle, model.TopLayer.Mode);
            Assert.Empty(model.TopLayer.KeyColors);
        }

        [Fact]
        public void ParseRgb_WithMonoLineAmongPerKeyLines_IsFreestyleFillNotMonochrome()
        {
            // [mono] means Monochrome only with exactly one total line (§2.4 item 3).
            var lines = new[]
            {
                "[mono]>[255][0][0]",
                "[a]>[0][0][255]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Freestyle, model.TopLayer.Mode);
            Assert.Equal(95, model.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(0, 0, 255), model.TopLayer.KeyColors[CodeOf("a")]);
            Assert.Equal(new LedColor(255, 0, 0), model.TopLayer.KeyColors[CodeOf("q")]);
        }

        [Fact]
        public void ParseRgb_WithMonoFillInBreatheBody_SetsAllKeysThenPerKeyOverrides()
        {
            var lines = new[]
            {
                "[breathe]>[spd2]",
                "[mono]>[255][0][0]",
                "[a]>[0][0][255]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Breathe, model.TopLayer.Mode);
            Assert.Equal(95, model.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(0, 0, 255), model.TopLayer.KeyColors[CodeOf("a")]);
            Assert.Equal(new LedColor(255, 0, 0), model.TopLayer.KeyColors[CodeOf("tab")]);
        }

        [Fact]
        public void ParseRgb_WithBlackMonoFillInFreestyleBody_ClearsEarlierAssignments()
        {
            var lines = new[]
            {
                "[a]>[0][0][255]",
                "[mono]>[0][0][0]",
                "[b]>[255][0][0]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Freestyle, model.TopLayer.Mode);
            var keyColor = Assert.Single(model.TopLayer.KeyColors);
            Assert.Equal(CodeOf("b"), keyColor.Key);
        }

        [Theory]
        [InlineData("[breathe]>[spd12]")]
        [InlineData("[breathe]>[spd0]")]
        [InlineData("[breathe]>[spdx]")]
        [InlineData("[breathe]")]
        public void ParseRgb_WithMissingOrInvalidSpeed_FallsBackToFive(string line)
        {
            Assert.Equal(5, LedFileParser.ParseRgb([line]).TopLayer.Speed);
        }

        [Theory]
        [InlineData("[wave]>[spd5][dirsideways]", LightingDirection.Left)]
        [InlineData("[wave]>[spd5]", LightingDirection.Left)]
        [InlineData("[wave]>[spd5][dirdown]", LightingDirection.Down)]
        public void ParseRgb_WithWaveDirections_FallsBackToLeftWhenInvalid(string line, LightingDirection expected)
        {
            Assert.Equal(expected, LedFileParser.ParseRgb([line]).TopLayer.Direction);
        }

        [Theory]
        [InlineData("[fireball]>[1][2][3][spd5][dirup]", LightingDirection.Left)]
        [InlineData("[fireball]>[1][2][3][spd5][dirdown]", LightingDirection.Left)]
        [InlineData("[fireball]>[1][2][3][spd5][dirright]", LightingDirection.Right)]
        public void ParseRgb_WithFireballDirections_AcceptsOnlyLeftAndRight(string line, LightingDirection expected)
        {
            Assert.Equal(expected, LedFileParser.ParseRgb([line]).TopLayer.Direction);
        }

        [Theory]
        [InlineData("[rebound]>[1][2][3][spd5][dirright]", LightingDirection.Left)]
        [InlineData("[rebound]>[1][2][3][spd5][dirdown]", LightingDirection.Left)]
        [InlineData("[rebound]>[1][2][3][spd5][dirup]", LightingDirection.Up)]
        public void ParseRgb_WithReboundDirections_AcceptsOnlyLeftAndUp(string line, LightingDirection expected)
        {
            Assert.Equal(expected, LedFileParser.ParseRgb([line]).TopLayer.Direction);
        }

        [Fact]
        public void ParseRgb_WithUnparseablePerKeyColor_FallsBackToDefaultEffectColor()
        {
            var lines = new[]
            {
                "[e]>[255][0][0]",
                "[a]>[300][0][0]",
                "[b]>[nonsense]",
                "[c]>"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(new LedColor(255, 0, 0), model.TopLayer.KeyColors[CodeOf("e")]);
            Assert.Equal(LedColor.DefaultEffectColor, model.TopLayer.KeyColors[CodeOf("a")]);
            Assert.Equal(LedColor.DefaultEffectColor, model.TopLayer.KeyColors[CodeOf("b")]);
            Assert.Equal(LedColor.DefaultEffectColor, model.TopLayer.KeyColors[CodeOf("c")]);
        }

        [Fact]
        public void ParseRgb_WithUnresolvableKeyToken_IgnoresTheLine()
        {
            var lines = new[]
            {
                "[e]>[255][0][0]",
                "[nosuchkey]>[1][2][3]",
                "no brackets at all"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Freestyle, model.TopLayer.Mode);
            var keyColor = Assert.Single(model.TopLayer.KeyColors);
            Assert.Equal(CodeOf("e"), keyColor.Key);
        }

        [Fact]
        public void ParseRgb_WithOnlyUnscannableGarbageLines_FallsBackToFreestyleNotDisabled()
        {
            // Only an empty section loads as Disabled; any other content falls back to
            // Freestyle (§2.4 item 3), even when no line yields a bracket token.
            var model = LedFileParser.ParseRgb(["no brackets at all", "fn also garbage"]);

            Assert.Equal(LightingMode.Freestyle, model.TopLayer.Mode);
            Assert.Empty(model.TopLayer.KeyColors);
            Assert.Equal(LightingMode.Freestyle, model.FnLayer.Mode);
            Assert.Empty(model.FnLayer.KeyColors);
        }

        [Fact]
        public void ParseRgb_WithFnPrefixedLines_ParsesTheLayersIndependently()
        {
            var lines = new[]
            {
                "[wave]>[spd4][dirright]",
                "fn [breathe]>[spd2]",
                "fn [s]>[1][2][3]"
            };

            var model = LedFileParser.ParseRgb(lines);

            Assert.Equal(LightingMode.Wave, model.TopLayer.Mode);
            Assert.Empty(model.TopLayer.KeyColors);
            Assert.Equal(LightingMode.Breathe, model.FnLayer.Mode);
            Assert.Equal(2, model.FnLayer.Speed);
            Assert.Equal(new LedColor(1, 2, 3), model.FnLayer.KeyColors[CodeOf("s")]);
        }

        [Theory]
        [InlineData("F1", "mute")]
        [InlineData("F2", "vol-")]
        [InlineData("F3", "vol+")]
        [InlineData("F4", "play")]
        [InlineData("F5", "prev")]
        [InlineData("F6", "next")]
        [InlineData("pause", "ins")]
        [InlineData("del", "scrlk")]
        public void ParseRgb_WithFnLayerExceptionTokens_TranslatesToTheInMemoryKey(
            string fileToken,
            string expectedMemoryToken)
        {
            var model = LedFileParser.ParseRgb(["fn [" + fileToken + "]>[7][8][9]"]);

            var keyColor = Assert.Single(model.FnLayer.KeyColors);
            Assert.Equal(CodeOf(expectedMemoryToken), keyColor.Key);
        }

        [Fact]
        public void ParseRgb_WithTopLayerExceptionTokens_NeverTranslates()
        {
            var model = LedFileParser.ParseRgb(["[F1]>[7][8][9]", "[pause]>[7][8][9]"]);

            Assert.Equal(2, model.TopLayer.KeyColors.Count);
            Assert.True(model.TopLayer.KeyColors.ContainsKey(CodeOf("F1")));
            Assert.True(model.TopLayer.KeyColors.ContainsKey(CodeOf("pause")));
            Assert.False(model.TopLayer.KeyColors.ContainsKey(CodeOf("mute")));
        }

        [Fact]
        public void ParseRgb_WithMixedCasing_IsCaseInsensitive()
        {
            var model = LedFileParser.ParseRgb(["FN [WAVE]>[SPD3][DIRUP]"]);

            Assert.Equal(LightingMode.Wave, model.FnLayer.Mode);
            Assert.Equal(3, model.FnLayer.Speed);
            Assert.Equal(LightingDirection.Up, model.FnLayer.Direction);
        }

        private static int CodeOf(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)!.Code;
        }
    }
}
