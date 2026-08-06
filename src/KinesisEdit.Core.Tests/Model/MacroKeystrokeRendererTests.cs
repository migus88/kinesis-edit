using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// Macro serialization per specs/06-macros.md §3 — layer prefix, co-triggers, trigger, "&gt;",
    /// {sN}, {xN}, then keystrokes whose modifier transitions come from diffing the previous
    /// held-modifier set against the current one — checked against the worked examples of §7 and
    /// against the Advantage360 500-character metric of §6.
    /// </summary>
    public class MacroKeystrokeRendererTests
    {
        [Fact]
        public void RenderKeystrokes_ForTheNadiaExample_MatchesTheSpecTokenStream()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("n"), MacroModifiers.LeftShift));
            macro.AddKeystrokes(
            [
                new Keystroke(ModelTokens.Key("a")),
                new Keystroke(ModelTokens.Key("d")),
                new Keystroke(ModelTokens.Key("i")),
                new Keystroke(ModelTokens.Key("a"))
            ]);

            Assert.Equal(
                "{-lshft}{n}{+lshft}{a}{d}{i}{a}",
                MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_WhenAModifierIsStillHeld_ClosesItAfterTheLastKeystroke()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b"), MacroModifiers.LeftShift));

            Assert.Equal("{a}{-lshft}{b}{+lshft}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_WhenTheModifierSetChanges_ReleasesBeforeItPresses()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b"), MacroModifiers.LeftControl));

            Assert.Equal(
                "{-lshft}{a}{+lshft}{-lctrl}{b}{+lctrl}",
                MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_WhenAModifierStaysHeld_WritesNoTransition()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b"), MacroModifiers.LeftShift));

            Assert.Equal("{-lshft}{a}{b}{+lshft}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_ForAGenericModifier_FallsBackToTheOnlyRegisteredToken()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("s"), MacroModifiers.Shift));

            Assert.Equal("{-shift}{s}{+shift}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_ForAnExplicitDirection_WritesTheDownAndUpPrefixes()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d", TokenDialect.Gen2)) { UpDown = KeyDirection.Down });
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d", TokenDialect.Gen2)) { UpDown = KeyDirection.Up });

            Assert.Equal("{-d}{a}{+d}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen2));
        }

        [Fact]
        public void RenderKeystrokes_ForABareModifierTap_WritesTheTokenWithoutAPrefix()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("lshft")));

            Assert.Equal("{lshft}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_ForAPedalDifferentPressReleaseKey_WrapsItInTheSpaceToken()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Legacy)) { DiffPressRelease = true });

            Assert.Equal("{-a}{ }{+a}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Legacy));
        }

        [Fact]
        public void Render_ForTheCoTriggeredExample_WritesPrefixTriggerSpeedAndRepeatInOrder()
        {
            var macro = new Macro
            {
                TriggerKey = ModelTokens.Key("esc").Code,
                Speed = 2,
                RepeatFrequency = 2
            };

            macro.AddCoTrigger(ModelTokens.Key("lshft"));
            macro.AddKeystrokes(
            [
                new Keystroke(ModelTokens.Key("a")),
                new Keystroke(ModelTokens.Key("s")),
                new Keystroke(ModelTokens.Key("d")),
                new Keystroke(ModelTokens.Key("f"))
            ]);

            Assert.Equal("{lshft}{esc}>{s2}{x2}{a}{s}{d}{f}", MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void Render_WithALayerPrefix_PutsItFirst()
        {
            var macro = new Macro
            {
                TriggerKey = ModelTokens.Key("esc").Code,
                Speed = 5,
                RepeatFrequency = 1
            };

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));

            Assert.Equal("fn {esc}>{s5}{x1}{a}", MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen1, "fn "));
        }

        [Theory]
        [InlineData(0, "{s0}")]
        [InlineData(9, "{s9}")]
        public void Render_ForAnInRangeSpeed_WritesTheSpeedToken(int speed, string expected)
        {
            var macro = new Macro
            {
                Speed = speed,
                RepeatFrequency = -1
            };

            Assert.Equal(">" + expected, MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen1));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        public void Render_ForAnOutOfRangeSpeedOrRepeat_WritesNoToken(int value)
        {
            var macro = new Macro
            {
                Speed = value,
                RepeatFrequency = value
            };

            Assert.Equal(">", MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void KeystrokeTextLength_ForALongMacro_ExceedsTheAdvantage360CapWhileTheKeystrokeBudgetDoesNot()
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage360).Macros;
            var macro = new Macro(capability)
            {
                TriggerKey = ModelTokens.Key("q", TokenDialect.Gen2).Code
            };

            for (var i = 0; i < 200; i++)
            {
                macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));
            }

            // 200 × "{a}" = 600 characters of keystroke text; the whole line adds "{q}>{s5}{x1}".
            Assert.Equal(600, MacroKeystrokeRenderer.KeystrokeTextLength(macro, TokenDialect.Gen2));
            Assert.Equal(612, MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen2).Length);
            Assert.Equal(500, capability.MaxCharactersPerMacro);
            Assert.Equal(200, macro.WeightedKeystrokeCount);
        }

        [Fact]
        public void KeystrokeTextLength_ForAShortMacro_StaysUnderTheAdvantage360Cap()
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage360).Macros;
            var macro = new Macro(capability)
            {
                TriggerKey = ModelTokens.Key("q", TokenDialect.Gen2).Code
            };

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));

            Assert.Equal("{q}>{s5}{x1}{a}", MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen2));
            Assert.Equal(3, MacroKeystrokeRenderer.KeystrokeTextLength(macro, TokenDialect.Gen2));
            Assert.True(MacroKeystrokeRenderer.KeystrokeTextLength(macro, TokenDialect.Gen2) < capability.MaxCharactersPerMacro);
        }

        [Fact]
        public void KeystrokeTextLength_ForAMacroWithATrigger_MeasuresOnlyTheValueSide()
        {
            // 06 §6 caps "the length of the serialized *macro* text" — the value side of the §3
            // line, so the trigger, the co-triggers and the {sN}/{xN} markers are not in it.
            var macro = new Macro
            {
                TriggerKey = ModelTokens.Key("q", TokenDialect.Gen2).Code,
                Speed = 5,
                RepeatFrequency = 1
            };

            macro.AddCoTrigger(ModelTokens.Key("lshf", TokenDialect.Gen2));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));

            Assert.Equal("{lshf}{q}>{s5}{x1}{a}", MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen2));
            Assert.Equal("{a}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen2));
            Assert.Equal(3, MacroKeystrokeRenderer.KeystrokeTextLength(macro, TokenDialect.Gen2));
        }

        [Fact]
        public void RenderKeystrokes_ForAKeyWithNoFileToken_WritesNeitherItNorItsModifiers()
        {
            // 05 §3.9: the Advantage2 Program button has no file token in any dialect, so it writes
            // no group — and must not drag the {-shift}/{+shift} transitions of a group that is
            // never written into the text either.
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.KeyByCode(10013), MacroModifiers.Shift));

            Assert.Equal(string.Empty, MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Legacy));
            Assert.Equal(">{s0}{x0}", MacroKeystrokeRenderer.Render(macro, TokenDialect.Legacy));
        }

        [Fact]
        public void RenderKeystrokes_AroundAKeyWithNoFileToken_KeepsTheSurroundingTransitionsCorrect()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Legacy), MacroModifiers.Shift));
            macro.AddKeystroke(new Keystroke(ModelTokens.KeyByCode(10013), MacroModifiers.LeftControl));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b", TokenDialect.Legacy), MacroModifiers.Shift));

            Assert.Equal(
                "{-shift}{a}{b}{+shift}",
                MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Legacy));
        }

        [Fact]
        public void RenderKeystrokes_ForGenericAndLeftWinTogether_PressesTheOneKeyOnce()
        {
            // 05 §5.1 maps both "W " and "LW" to the Left Win key and deduplicates the active
            // modifier set by key code, so "W ,LW" is one held key — one transition pair, and one
            // modifier in the keystroke accounting of 04 §5.3.
            var macro = new Macro();
            var keystroke = new Keystroke(
                ModelTokens.Key("a"),
                MacroModifiers.Win | MacroModifiers.LeftWin);

            macro.AddKeystroke(keystroke);

            Assert.Equal("{-lwin}{a}{+lwin}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
            Assert.Equal(1, keystroke.ModifierCount);
            Assert.Equal(3, keystroke.WeightedKeystrokeCount);
            Assert.Equal(3, macro.WeightedKeystrokeCount);
        }

        [Fact]
        public void RenderKeystrokes_ForGenericWinAlone_PressesTheLeftWinKey()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a"), MacroModifiers.Win));

            // §5.1: converting a modifier string back into keys maps "W " to the Left Win key.
            Assert.Equal("{-lwin}{a}{+lwin}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_WhenOnlyTheSpellingOfAHeldModifierChanges_WritesNoTransition()
        {
            var macro = new Macro();

            // Both spellings occur in field files. §5.1 deduplicates the active-modifier set by key
            // code, so Left Win never leaves the held set here — releasing and re-pressing it would
            // be a change in behaviour invented by the renderer.
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a"), MacroModifiers.Win));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b"), MacroModifiers.LeftWin));

            Assert.Equal("{-lwin}{a}{b}{+lwin}", MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        [Fact]
        public void RenderKeystrokes_WhenASpellingChangeAlsoAddsAKey_WritesOnlyTheAddedKey()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a"), MacroModifiers.Win));
            macro.AddKeystroke(new Keystroke(
                ModelTokens.Key("b"),
                MacroModifiers.LeftWin | MacroModifiers.LeftShift));

            // The closing transitions follow §5.1 table order, so Shift is released before Win.
            Assert.Equal(
                "{-lwin}{a}{-lshft}{b}{+lshft}{+lwin}",
                MacroKeystrokeRenderer.RenderKeystrokes(macro, TokenDialect.Gen1));
        }

        /// <summary>
        /// 06 §3 step 1 and 04 §3.1-§3.3: only the Gen1 family puts the layer into the line itself,
        /// as the <c>fn </c> prefix on the bottom layer. The Legacy dialect marks the keypad layer
        /// with <c>kp-</c> inside the first bracket and Gen2 with a header line, so neither
        /// contributes a prefix.
        /// </summary>
        [Theory]
        [InlineData(TokenDialect.Gen1, 0, "")]
        [InlineData(TokenDialect.Gen1, 1, "fn ")]
        [InlineData(TokenDialect.Legacy, 0, "")]
        [InlineData(TokenDialect.Legacy, 1, "")]
        [InlineData(TokenDialect.Gen2, 1, "")]
        [InlineData(TokenDialect.Gen2, 4, "")]
        public void LayerPrefixFor_PerDialectAndLayer_IsTheLinePrefixTheFileCarries(
            TokenDialect dialect,
            int layerIndex,
            string expected)
        {
            Assert.Equal(expected, MacroKeystrokeRenderer.LayerPrefixFor(dialect, layerIndex));
        }

        [Fact]
        public void Render_WithoutATrigger_WritesNoTriggerGroup()
        {
            var macro = new Macro
            {
                Speed = -1,
                RepeatFrequency = -1
            };

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));

            Assert.Equal(">{a}", MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen1));
        }
    }
}
