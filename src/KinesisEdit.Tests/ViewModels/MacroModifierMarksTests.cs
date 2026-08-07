using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The display spelling of a macro keystroke's modifiers. Plain <c>[Fact]</c>: the formatter is
    /// pure, knows nothing about a font and needs no toolkit — that the marks it emits can actually
    /// be drawn is <c>Design/KeySymbolFontTests</c>'s question.
    /// <para>
    /// <b>The rule this suite pins: left is unmarked, and only the right side is spelled.</b>
    /// <c>LS</c> draws a bare <c>⇧</c> and <c>RS</c> draws <c>R⇧</c>. Its price is that a
    /// <em>generic</em> modifier and a <em>left</em> one draw identically, which is asserted here
    /// deliberately rather than left as an accident someone tidies away — see
    /// <see cref="For_DrawsALeftModifierExactlyLikeAGenericOne_WhichIsTheCostOfLeavingLeftUnmarked"/>
    /// and the <c>Description</c> pair of tests that keep the two tellable apart.
    /// </para>
    /// </summary>
    public class MacroModifierMarksTests
    {
        /// <summary>
        /// The spelling rule, flag by flag: <b>left is unmarked and only right carries its side
        /// letter</b>. A left modifier draws the bare mark, exactly as a generic one does.
        /// </summary>
        [Theory]
        [InlineData(MacroModifiers.Shift, "", "⇧")]
        [InlineData(MacroModifiers.LeftShift, "", "⇧")]
        [InlineData(MacroModifiers.RightShift, "R", "⇧")]
        [InlineData(MacroModifiers.Control, "", "⌃")]
        [InlineData(MacroModifiers.LeftControl, "", "⌃")]
        [InlineData(MacroModifiers.RightControl, "R", "⌃")]
        [InlineData(MacroModifiers.Alt, "", "⌥")]
        [InlineData(MacroModifiers.LeftAlt, "", "⌥")]
        [InlineData(MacroModifiers.RightAlt, "R", "⌥")]
        [InlineData(MacroModifiers.Win, "", "⌘")]
        [InlineData(MacroModifiers.LeftWin, "", "⌘")]
        [InlineData(MacroModifiers.RightWin, "R", "⌘")]
        public void For_MapsEveryFlag_SpellingTheRightSideAndOnlyTheRightSide(
            MacroModifiers modifier,
            string side,
            string symbol)
        {
            var mark = MacroModifierMarks.For(modifier);

            Assert.Equal(modifier, mark.Modifier);
            Assert.Equal(side, mark.Side);
            Assert.Equal(symbol, mark.Symbol);
            Assert.Equal(side + symbol, mark.Text);
            Assert.Equal(side.Length > 0, mark.HasSide);
        }

        /// <summary>
        /// <b>The collision, asserted on purpose.</b> A generic modifier and a left one draw the
        /// same mark, because left is the unmarked side; the file still tells them apart (<c>"S "</c>
        /// against <c>"LS"</c>, 05 §5.1) and <see cref="MacroModifierMarks.Mark.Description"/> is
        /// where that distinction stays reachable — which is what makes the collision acceptable
        /// rather than a defect.
        /// <para>
        /// This test exists so the equality is read as <em>intended</em>: a future reader who
        /// restores an <c>L</c> prefix has to delete an assertion that says the two are meant to
        /// match, rather than "fix" a difference nobody wrote down.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData(MacroModifiers.Shift, MacroModifiers.LeftShift)]
        [InlineData(MacroModifiers.Control, MacroModifiers.LeftControl)]
        [InlineData(MacroModifiers.Alt, MacroModifiers.LeftAlt)]
        [InlineData(MacroModifiers.Win, MacroModifiers.LeftWin)]
        public void For_DrawsALeftModifierExactlyLikeAGenericOne_WhichIsTheCostOfLeavingLeftUnmarked(
            MacroModifiers generic,
            MacroModifiers left)
        {
            var genericMark = MacroModifierMarks.For(generic);
            var leftMark = MacroModifierMarks.For(left);

            Assert.Equal(genericMark.Text, leftMark.Text);
            Assert.Equal(genericMark.Side, leftMark.Side);
            Assert.Equal(genericMark.Symbol, leftMark.Symbol);
            Assert.False(genericMark.HasSide);
            Assert.False(leftMark.HasSide);

            // The file has never stopped distinguishing them, and neither has Core: this is a
            // display collision and only a display collision.
            Assert.NotEqual(MacroModifierCodes.GetCode(generic), MacroModifierCodes.GetCode(left));

            // And the escape hatch that makes it liveable — the two words a tooltip shows.
            Assert.NotEqual(genericMark.Description, leftMark.Description);
        }

        /// <summary>
        /// <see cref="MacroModifierMarks.Mark.Description"/> spells every one of the 12 flags, and
        /// spells all 12 differently — it is the only thing left that separates the flags the marks
        /// now draw alike.
        /// </summary>
        [Fact]
        public void Description_NamesEveryFlagInWords_AndNamesNoTwoOfThemTheSame()
        {
            var descriptions = new List<string>(MacroModifierCodes.All.Count);

            foreach (var modifier in MacroModifierCodes.All)
            {
                var description = MacroModifierMarks.For(modifier).Description;

                Assert.NotEqual(string.Empty, description);

                // Words, never the flag's own camel-case name: `LeftShift` is what Describe's
                // fallback arm produces, and a tooltip showing it means the switch lost an arm.
                for (var index = 1; index < description.Length; index++)
                {
                    Assert.False(
                        char.IsUpper(description[index]) && char.IsLower(description[index - 1]),
                        $"`{description}` reads as {modifier}'s camel-case name rather than as words.");
                }

                descriptions.Add(description);
            }

            Assert.Equal(12, descriptions.Count);
            Assert.Equal(descriptions.Count, descriptions.Distinct(StringComparer.Ordinal).Count());
        }

        [Theory]
        [InlineData(MacroModifiers.Shift, "Shift")]
        [InlineData(MacroModifiers.LeftShift, "Left Shift")]
        [InlineData(MacroModifiers.RightShift, "Right Shift")]
        [InlineData(MacroModifiers.Control, "Ctrl")]
        [InlineData(MacroModifiers.LeftControl, "Left Ctrl")]
        [InlineData(MacroModifiers.RightControl, "Right Ctrl")]
        [InlineData(MacroModifiers.Alt, "Alt")]
        [InlineData(MacroModifiers.LeftAlt, "Left Alt")]
        [InlineData(MacroModifiers.RightAlt, "Right Alt")]
        [InlineData(MacroModifiers.Win, "Win")]
        [InlineData(MacroModifiers.LeftWin, "Left Win")]
        [InlineData(MacroModifiers.RightWin, "Right Win")]
        public void Description_IsTheWordsATooltipShows(MacroModifiers modifier, string expected)
        {
            Assert.Equal(expected, MacroModifierMarks.For(modifier).Description);
        }

        [Fact]
        public void For_CoversEveryFlagTheFileFormatDefines()
        {
            // The table is authored by hand in two places — Core's codes and this app's marks — so
            // the guard is that neither can gain a flag the other lacks. A modifier with no mark
            // would throw at render time, in a row the user is looking at.
            foreach (var modifier in MacroModifierCodes.All)
            {
                var mark = MacroModifierMarks.For(modifier);

                Assert.NotEqual(string.Empty, mark.Symbol);
            }

            Assert.Equal(12, MacroModifierCodes.All.Count);
        }

        [Fact]
        public void For_WithNoneOrACombination_Throws()
        {
            // Mirrors MacroModifierCodes.GetCode: a single flag is the contract, and a caller with
            // a set uses Split.
            Assert.Throws<ArgumentOutOfRangeException>(() => MacroModifierMarks.For(MacroModifiers.None));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MacroModifierMarks.For(MacroModifiers.LeftShift | MacroModifiers.LeftControl));
        }

        [Fact]
        public void Split_WithNone_IsEmpty()
        {
            Assert.Empty(MacroModifierMarks.Split(MacroModifiers.None));
        }

        [Fact]
        public void Format_WithNone_IsEmpty()
        {
            Assert.Equal(string.Empty, MacroModifierMarks.Format(MacroModifiers.None));
        }

        [Fact]
        public void Split_KeepsTheSideAndTheMarkApart_SoAViewCanSetThemInDifferentFaces()
        {
            // The whole reason the parts are exposed: the key-symbol subset carries no Latin
            // letters, so `R` and `⌥` are two runs in two faces. A single joined property would
            // have made that impossible without re-parsing the string in the view. The left
            // modifier below is a single run — its side is empty — which is what a view's
            // `IsVisible="{Binding HasSide}"` is for.
            var parts = MacroModifierMarks.Split(MacroModifiers.LeftControl | MacroModifiers.RightAlt);

            Assert.Collection(
                parts,
                mark =>
                {
                    Assert.Equal(string.Empty, mark.Side);
                    Assert.Equal("⌃", mark.Symbol);
                },
                mark =>
                {
                    Assert.Equal("R", mark.Side);
                    Assert.Equal("⌥", mark.Symbol);
                });
        }

        [Fact]
        public void Split_IsInTheSameOrderTheFileFormatWritesIn()
        {
            // Ordering is delegated to MacroModifierCodes.Enumerate rather than re-declared, so
            // this asserts the delegation held: the marks and the file's codes must never disagree
            // about which modifier comes first.
            var modifiers = MacroModifiers.RightWin
                | MacroModifiers.Alt
                | MacroModifiers.LeftShift
                | MacroModifiers.Control;

            Assert.Equal(
                MacroModifierCodes.Enumerate(modifiers).ToList(),
                MacroModifierMarks.Split(modifiers).Select(mark => mark.Modifier).ToList());

            // The file's own spelling of that set, for the record: the codes and the marks are the
            // same sequence, differently drawn.
            Assert.Equal("LS,C ,A ,RW", MacroModifierCodes.Format(modifiers));
            Assert.Equal(
                string.Join(MacroModifierMarks.Separator, "⇧", "⌃", "⌥", "R⌘"),
                MacroModifierMarks.Format(modifiers));
        }

        [Fact]
        public void Format_JoinsWithAThinSpace_NotTheFileFormatsComma()
        {
            Assert.Equal(" ", MacroModifierMarks.Separator);

            var text = MacroModifierMarks.Format(MacroModifiers.LeftControl | MacroModifiers.LeftAlt);

            Assert.Equal("⌃ ⌥", text);
            Assert.DoesNotContain(MacroModifierCodes.Separator, text);

            // The same pair on the right, so the joiner is proved with the side letters present
            // too: a separator lost between `R⌃` and `R⌥` would not show up above.
            Assert.Equal(
                "R⌃ R⌥",
                MacroModifierMarks.Format(MacroModifiers.RightControl | MacroModifiers.RightAlt));
        }

        [Fact]
        public void Format_ForASingleModifier_IsJustTheMark()
        {
            Assert.Equal("⇧", MacroModifierMarks.Format(MacroModifiers.Shift));
            Assert.Equal("⌘", MacroModifierMarks.Format(MacroModifiers.LeftWin));
        }

        [Fact]
        public void Format_LeavesNoTwoLetterFileCodeInItsOutput()
        {
            // Acceptance criterion 5, stated as the property it is: over every combination of the
            // 12 flags there must be no `LC`/`LS`/`RA`/… left anywhere in the display string. The
            // `R` prefix survives — it is the one side the display still spells — but it is never
            // followed by a code letter.
            foreach (var modifiers in EveryCombination())
            {
                var text = MacroModifierMarks.Format(modifiers);

                foreach (var flag in MacroModifierCodes.All)
                {
                    var code = MacroModifierCodes.GetCode(flag).TrimEnd();

                    // A one-character generic code ("S", "C", "A", "W") is a letter, and the
                    // display string carries no letter but `R` — so both halves of the claim
                    // reduce to the same check: no code letter is in the output at all.
                    Assert.DoesNotContain(code, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void Format_OverEveryCombination_IsMadeOnlyOfMarksTheRightSideAndSeparators()
        {
            // The strongest form of "no file codes leak": the output alphabet is closed. Anything
            // outside {⇧ ⌃ ⌥ ⌘ R thin-space} would be a character nobody chose a face for —
            // and since left became the unmarked side there is no `L` in that set any more, which
            // makes this the assertion a re-prefixed left modifier trips first.
            var allowed = new HashSet<char>(
                MacroModifierMarks.Marks.Select(mark => mark[0])
                    .Concat([MacroModifierMarks.RightSide[0], MacroModifierMarks.Separator[0]]));

            Assert.DoesNotContain('L', allowed);

            foreach (var modifiers in EveryCombination())
            {
                foreach (var character in MacroModifierMarks.Format(modifiers))
                {
                    Assert.Contains(character, allowed);
                }
            }
        }

        [Fact]
        public void Split_DropsBitsTheFileFormatDoesNotDefine()
        {
            // Same contract as the codes: a bit outside MacroModifierCodes.Known names no
            // modifier, so it is not drawn rather than drawn as a blank.
            var stray = (MacroModifiers)(1 << 20);

            Assert.Empty(MacroModifierMarks.Split(stray));
            Assert.Equal("⇧", MacroModifierMarks.Format(MacroModifiers.Shift | stray));
        }

        [Fact]
        public void Split_DoesNotCanonicalize_SoTheRowShowsWhatTheFileSays()
        {
            // §5.1 maps both "W " and "LW" to the Left Win key, and Core's Canonicalize folds the
            // pair for keystroke *accounting*. Display must not: a step row is a view of the file's
            // own bytes, and collapsing the pair would show a macro that is not the one on the
            // drive. MacroModifierCodes.Format makes exactly the same choice.
            var both = MacroModifiers.Win | MacroModifiers.LeftWin;

            Assert.Equal("W ,LW", MacroModifierCodes.Format(both));
            Assert.Equal("⌘ ⌘", MacroModifierMarks.Format(both));
            Assert.Equal(2, MacroModifierMarks.Split(both).Count);

            // Twice the SAME mark, because left is unmarked: the row draws the two codes the
            // file carries as two identical glyphs rather than folding them into one. Two flags
            // are set, so two are drawn.
            Assert.Equal(
                MacroModifierMarks.WinMark,
                Assert.Single(MacroModifierMarks.Split(both).Select(mark => mark.Text).Distinct()));

            // And the canonical reading is still available to a caller that wants it — by
            // canonicalizing first, which is the seam Core already provides.
            Assert.Equal("⌘", MacroModifierMarks.Format(MacroModifierCodes.Canonicalize(both)));
        }

        [Fact]
        public void Marks_AreTheFourDistinctSymbols()
        {
            Assert.Equal(["⇧", "⌃", "⌥", "⌘"], MacroModifierMarks.Marks);

            // Every mark the table can emit is in that list, which is what a font gate walks.
            foreach (var modifier in MacroModifierCodes.All)
            {
                Assert.Contains(MacroModifierMarks.For(modifier).Symbol, MacroModifierMarks.Marks);
            }
        }

        [Fact]
        public void TheMarks_AreTheCodepointsTheSubsetWasCutFor()
        {
            // Pinned by codepoint, not by the glyph in this source file: an editor that
            // "helpfully" normalised ⌥ (U+2325) to a lookalike would leave every string comparison
            // in this suite green and the font unable to draw it.
            Assert.Equal("⇧", MacroModifierMarks.ShiftMark);
            Assert.Equal("⌃", MacroModifierMarks.ControlMark);
            Assert.Equal("⌥", MacroModifierMarks.AltMark);
            Assert.Equal("⌘", MacroModifierMarks.WinMark);
        }

        /// <summary>Every subset of the 12 known flags — 4 096 sets, which is cheap and exhaustive.</summary>
        private static IEnumerable<MacroModifiers> EveryCombination()
        {
            var flags = MacroModifierCodes.All;

            for (var mask = 0; mask < 1 << 12; mask++)
            {
                var modifiers = MacroModifiers.None;

                for (var bit = 0; bit < flags.Count; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        modifiers |= flags[bit];
                    }
                }

                yield return modifiers;
            }
        }
    }
}
