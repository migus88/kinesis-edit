using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The naming rules of <see cref="MacroNaming"/>: what a stored name may look like (it rides a
    /// line-oriented settings file, so it is one trimmed, bounded line), when two names are the
    /// same name, and what an unnamed macro is called.
    /// </summary>
    public class MacroNamingTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("\t\r\n", "")]
        [InlineData("Sign-off", "Sign-off")]
        [InlineData("  Sign-off  ", "Sign-off")]
        public void Sanitize_ForBlankAndTrimmableInput_ReturnsTheTrimmedName(string? input, string expected)
        {
            Assert.Equal(expected, MacroNaming.Sanitize(input));
        }

        [Theory]
        [InlineData("first\nsecond", "first second")]
        [InlineData("first\r\nsecond", "first second")]
        [InlineData("first\tsecond", "first second")]
        [InlineData("first  second", "first second")]
        [InlineData("a\n\n\nb", "a b")]
        public void Sanitize_WithALineBreak_CollapsesToOneLine(string input, string expected)
        {
            // A newline here would split one `macro_name_...=value` line into two and the second
            // half would be read back as a stray key (spec 08 §1).
            var sanitized = MacroNaming.Sanitize(input);

            Assert.Equal(expected, sanitized);
            Assert.DoesNotContain('\n', sanitized);
            Assert.DoesNotContain('\r', sanitized);
        }

        [Fact]
        public void Sanitize_WithAnOverlongName_BoundsItToMaxNameLength()
        {
            var sanitized = MacroNaming.Sanitize(new string('x', MacroNaming.MaxNameLength + 40));

            Assert.Equal(MacroNaming.MaxNameLength, sanitized.Length);
        }

        [Fact]
        public void Sanitize_IsIdempotent()
        {
            var once = MacroNaming.Sanitize("  Sign-off\nblock  " + new string('y', 40));

            Assert.Equal(once, MacroNaming.Sanitize(once));
        }

        [Theory]
        [InlineData("Sign-off", "sign-off", true)]
        [InlineData("Sign-off", "  SIGN-OFF  ", true)]
        [InlineData("Sign-off", "Sign off", false)]
        [InlineData(null, "", true)]
        [InlineData(null, "  ", true)]
        [InlineData("", "Sign-off", false)]
        public void AreSameName_ComparesTrimmedAndCaseInsensitively(string? a, string? b, bool expected)
        {
            Assert.Equal(expected, MacroNaming.AreSameName(a, b));
        }

        [Theory]
        [InlineData("Sign-off", 1, "Sign-off")]
        [InlineData("Sign-off", 2, "Sign-off (2)")]
        [InlineData("Sign-off", 11, "Sign-off (11)")]
        public void Disambiguate_AppendsTheOrdinalFromTwoOnwards(string name, int ordinal, string expected)
        {
            Assert.Equal(expected, MacroNaming.Disambiguate(name, ordinal));
        }

        [Fact]
        public void Disambiguate_WithAMaximumLengthName_StaysWithinTheBound()
        {
            var name = new string('x', MacroNaming.MaxNameLength);

            var disambiguated = MacroNaming.Disambiguate(name, 2);

            Assert.True(disambiguated.Length <= MacroNaming.MaxNameLength);
            Assert.EndsWith(" (2)", disambiguated);
        }

        [Fact]
        public void Disambiguate_WithABlankName_FallsBackToTheUnnamedName()
        {
            Assert.Equal(MacroNaming.UnnamedMacroName, MacroNaming.Disambiguate("  ", 1));
        }

        [Fact]
        public void Disambiguate_WithOrdinalBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MacroNaming.Disambiguate("Sign-off", 0));
        }

        [Fact]
        public void DeriveDisplayName_ForAMacroThatTypesText_IsThatText()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = Typing(layout, "hello");

            Assert.Equal("hello", MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveDisplayName_ForAShiftedKeystroke_UsesTheShiftedCharacter()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("h"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("i")));

            Assert.Equal("Hi", MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveDisplayName_ForPunctuationAndSpace_SpellsThemOut()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("2"), MacroModifiers.Shift));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("per")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("spc")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("c")));

            Assert.Equal("a@b. c", MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveDisplayName_ForAShortcut_IgnoresTheModifiedKeystroke()
        {
            // Ctrl+C types nothing, so it contributes nothing to a name built from typed text.
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.TriggerKey = ModelTokens.Key("f3").Code;
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("c"), MacroModifiers.LeftControl));

            Assert.Equal("Macro on [F3]", MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveDisplayName_ForAMacroThatTypesNothingPrintable_NamesItsTrigger()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.TriggerKey = ModelTokens.Key("f3").Code;
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("ent")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("tab")));

            Assert.Equal("Macro on [F3]", MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveDisplayName_NamesTheTriggerInTheLayoutsOwnDialect()
        {
            var gen1 = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var legacy = KeyboardLayout.Create(DeviceId.Advantage2);
            var escape = KeyRegistry.FindByToken("esc", TokenDialect.Gen1)!;

            var gen1Macro = gen1.CreateMacro();
            gen1Macro.TriggerKey = escape.Code;

            var legacyMacro = legacy.CreateMacro();
            legacyMacro.TriggerKey = escape.Code;

            Assert.Equal("Macro on [esc]", MacroNaming.DeriveDisplayName(gen1Macro, gen1));
            Assert.Equal("Macro on [escape]", MacroNaming.DeriveDisplayName(legacyMacro, legacy));
        }

        [Fact]
        public void DeriveDisplayName_ForAnUnboundMacro_FallsBackToTheUnnamedName()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = layout.CreateMacro();

            Assert.Equal(Macro.UnassignedIndex, macro.TriggerKey);
            Assert.Equal(MacroNaming.UnnamedMacroName, MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveDisplayName_ForALongMacro_IsBoundedAndDeterministic()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = Typing(layout, new string('a', 80));

            var first = MacroNaming.DeriveDisplayName(macro, layout);
            var second = MacroNaming.DeriveDisplayName(macro, layout);

            Assert.Equal(MacroNaming.MaxNameLength, first.Length);
            Assert.Equal(first, second);
        }

        [Fact]
        public void DeriveDisplayName_IgnoresKeyUpsAndDelayPseudoKeys()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("h")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("i")) { UpDown = KeyDirection.Up });

            Assert.Equal("h", MacroNaming.DeriveDisplayName(macro, layout));
        }

        [Fact]
        public void DeriveTypedText_ForAMacroThatTypesNothing_IsEmpty()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("ent")));

            Assert.Equal(string.Empty, MacroNaming.DeriveTypedText(macro));
        }

        private static Macro Typing(KeyboardLayout layout, string text)
        {
            var macro = layout.CreateMacro();

            foreach (var character in text)
            {
                macro.AddKeystroke(new Keystroke(ModelTokens.Key(character.ToString())));
            }

            return macro;
        }
    }
}
