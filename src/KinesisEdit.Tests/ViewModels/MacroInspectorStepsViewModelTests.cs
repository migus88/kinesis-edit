using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector's step editor — the rows, the selection, the reorder, the delete, the
    /// <c>＋</c> placeholder — and <b>every write to a macro's keystroke list</b>, including
    /// specs/11-feature-dialogs.md §11.3's delays.
    ///
    /// <para><b>§11.3's coverage moved with the surface, twice, and was never dropped.</b> It came
    /// here from <c>MacroDelayOverlayViewModelTests</c> when the modal was absorbed (issue #93); with
    /// issue #139 the <em>controls</em> moved again, into the composer on
    /// <c>MacroInspectorPanelViewModel</c>, and its cases went with them — the segment, the
    /// millisecond field, the validation message and the arrows are in
    /// <c>MacroInspectorPanelViewModelTests</c>. What stays here is what this class still owns: the
    /// tokens themselves (<c>dran</c>, <c>d001</c>..<c>d999</c>), the write that produces them, the
    /// firmware gate of 09 §2, and the round trips that pin what a delay does once it reaches a
    /// layout file (specs/06-macros.md §2.2).</para>
    /// </summary>
    public sealed class MacroInspectorStepsViewModelTests
    {
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Strings_MatchTheSpecVerbatim()
        {
            Assert.Equal("Random Delay (1-150ms)", MacroInspectorStepsViewModel.RandomDelayCaption);
            Assert.Equal("Custom Delay (1-999ms)", MacroInspectorStepsViewModel.CustomDelayCaption);
            Assert.Equal(
                "Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.",
                MacroInspectorStepsViewModel.InvalidDelayMessage);

            // Mockup 2i's own heading and reorder affordance.
            Assert.Equal("Steps", MacroInspectorStepsViewModel.SectionTitle);
            Assert.Equal("drag", MacroInspectorStepsViewModel.ReorderHintPrefix);
            Assert.Equal("insert step", MacroInspectorStepsViewModel.InsertStepCaption);
        }

        [Fact]
        public void ReorderShortcut_SpellsTheSameModifierOnBothPlatforms()
        {
            // ⌥ is Alt everywhere — only the spelling changes, exactly as the layer chips do it.
            // The macOS spelling carries NO ⌥: U+2325 is in neither embedded family, so the mark
            // is drawn beside this text (IconOption, gated by ShowsOptionMark) rather than typed —
            // the same resolution issue #109 made for the layer chips.
            Assert.Equal("· ↑↓", MacroInspectorStepsViewModel.BuildReorderShortcut(isMacOs: true));
            Assert.Equal("· Alt+↑↓", MacroInspectorStepsViewModel.BuildReorderShortcut(isMacOs: false));
            Assert.DoesNotContain('⌥', MacroInspectorStepsViewModel.BuildReorderShortcut(isMacOs: true));
            Assert.DoesNotContain('⌥', MacroInspectorStepsViewModel.ReorderHandleHint);
        }

        [Fact]
        public void Load_WithNoMacro_ShowsNothingAndNumbersTheFirstStepOne()
        {
            var steps = Create();

            steps.Load(null);

            Assert.Empty(steps.Items);
            Assert.False(steps.HasItems);
            Assert.Equal("01", steps.NextStepNumberText);
        }

        [Fact]
        public void Load_RendersEachStepAsTheMockDrawsIt()
        {
            var steps = Create();
            var macro = new Macro();

            // Mockup 2i writes the tokens as `[lshift]`/`[enter]` — the *legacy* spelling. The panel
            // spells them in the open device's own dialect (Gen1 here: `lshft`, `ent`), because mono
            // means "this is literally a value in THIS board's config file".
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("lshft")) { UpDown = KeyDirection.Down });
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("lshft")) { UpDown = KeyDirection.Up });
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("e")));

            steps.Load(macro);

            Assert.Equal(["01", "02", "03", "04"], steps.Items.Select(step => step.NumberText));
            Assert.Equal("[lshft]", steps.Items[0].TokenText);
            Assert.Equal(MacroInspectorStepViewModel.PressAction, steps.Items[0].ActionText);
            Assert.Equal(MacroInspectorStepViewModel.HeldAction, steps.Items[1].ActionText);
            Assert.Equal(MacroInspectorStepViewModel.ReleaseAction, steps.Items[2].ActionText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, steps.Items[3].ActionText);
            Assert.Equal("05", steps.NextStepNumberText);
        }

        /// <summary>
        /// The row draws mockup <c>2i</c>'s marks, not the file's codes (issue #122, AC 5).
        /// <b>Left is unmarked and only right is spelled</b>, so a left modifier and a generic one
        /// both come out as the bare mark; the two halves still come out separately because no one
        /// face in the app can set a Latin <c>R</c> and a <c>⇧</c> at once.
        /// </summary>
        [Fact]
        public void Load_DrawsHeldModifiersAsMarksAndNotAsTheFilesCodes()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("c"), MacroModifiers.Shift));
            macro.AddKeystroke(new Keystroke(
                TestLayouts.Gen1Key("d"),
                MacroModifiers.RightControl | MacroModifiers.LeftAlt));

            steps.Load(macro);

            var left = Assert.Single(steps.Items[0].Modifiers);

            Assert.False(left.HasSide);
            Assert.Equal(MacroModifierMarks.LeftSide, left.Side);
            Assert.Equal(MacroModifierMarks.ShiftMark, left.Symbol);

            // A generic modifier draws the bare mark: the file's own spelling pads it to `S `, and
            // that trailing space is a file fact rather than a display one. It therefore draws
            // EXACTLY like the left one above — the intended cost of leaving left unmarked — and
            // `Description` is what still separates them.
            var generic = Assert.Single(steps.Items[1].Modifiers);

            Assert.False(generic.HasSide);
            Assert.Equal(MacroModifierMarks.ShiftMark, generic.Symbol);
            Assert.Equal(left.Text, generic.Text);
            Assert.NotEqual(left.Description, generic.Description);

            // Two modifiers come out in 05 §5.1's own order — Control before Alt — because Split
            // walks the same enumeration the serializer writes in.
            Assert.Equal(
                [MacroModifierMarks.ControlMark, MacroModifierMarks.AltMark],
                steps.Items[2].Modifiers.Select(mark => mark.Symbol));

            Assert.Equal(
                [MacroModifierMarks.RightSide, MacroModifierMarks.LeftSide],
                steps.Items[2].Modifiers.Select(mark => mark.Side));

            Assert.All(steps.Items, step => Assert.True(step.HasModifiers));
        }

        /// <summary>
        /// The two rows that carry no modifier set at all: a step that <em>is</em> a modifier (05
        /// §5.1 attaches no modifier string to one) and a bare delay.
        /// </summary>
        [Fact]
        public void Load_DrawsNoMarksOnAModifierKeyOrABareDelay()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("lshft")) { UpDown = KeyDirection.Down });
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            steps.Load(macro);

            Assert.All(steps.Items, step =>
            {
                Assert.Empty(step.Modifiers);
                Assert.False(step.HasModifiers);
            });
        }

        /// <summary>
        /// <c>held</c> is read off the modifier <b>flags</b>, not off a rendered string — a bit the
        /// display drops is still a bit the step was struck with.
        /// </summary>
        [Fact]
        public void Load_CallsAStepHeldWheneverItCarriesModifiers()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b"), MacroModifiers.LeftWin));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("c")));

            steps.Load(macro);

            Assert.Equal(MacroInspectorStepViewModel.HeldAction, steps.Items[0].ActionText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, steps.Items[1].ActionText);
        }

        /// <summary>
        /// Mockup 2i draws <c>07 [enter] tap · 80 ms</c>: the delay is a keystroke of its own in the
        /// file, and a <b>qualifier of the step it follows</b> on screen.
        /// </summary>
        [Fact]
        public void Load_FoldsADelayIntoTheStepItFollows()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("ent")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));

            steps.Load(macro);

            var step = Assert.Single(steps.Items);

            Assert.Equal("[ent]", step.TokenText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, step.ActionText);
            Assert.Equal("80 ms", step.DelayText);
            Assert.Equal(80, step.DelayMilliseconds);
            Assert.False(step.IsRandomDelay);
        }

        [Fact]
        public void Load_WithALeadingDelay_GivesItARowOfItsOwn()
        {
            // The mock never draws this, but the file can hold it, and dropping it would be editing
            // the macro behind the user's back.
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));

            steps.Load(macro);

            Assert.Equal(2, steps.Items.Count);
            Assert.True(steps.Items[0].IsDelayOnly);
            Assert.Equal(MacroInspectorStepViewModel.DelayAction, steps.Items[0].ActionText);
            Assert.Equal("random", steps.Items[0].DelayText);
            Assert.Equal("[a]", steps.Items[1].TokenText);
        }

        [Fact]
        public void MoveStep_ReordersTheMacroAndReportsIt()
        {
            var steps = Create();
            var macro = MacroOf("a", "b", "c");
            var changed = 0;

            steps.Changed += (_, _) => changed++;
            steps.Load(macro);

            Assert.True(steps.MoveStep(0, 2));

            Assert.Equal(["b", "c", "a"], macro.Keystrokes.Select(TokenOf));
            Assert.Equal(1, changed);
        }

        /// <summary>
        /// The reason a row is a step <em>plus its delay</em>: moving one without the other would
        /// silently retime the macro.
        /// </summary>
        [Fact]
        public void MoveStep_CarriesTheStepsOwnDelayWithIt()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b")));

            steps.Load(macro);

            Assert.True(steps.MoveStep(0, 1));

            Assert.Equal(["b", "a", "d080"], macro.Keystrokes.Select(TokenOf));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, 3)]
        [InlineData(1, 1)]
        public void MoveStep_OutsideTheListOrOntoItself_DoesNothing(int from, int to)
        {
            var steps = Create();
            var macro = MacroOf("a", "b", "c");

            steps.Load(macro);

            Assert.False(steps.MoveStep(from, to));
            Assert.Equal(["a", "b", "c"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void MoveStepUpCommand_MovesTheSelectedStepAndFollowsIt()
        {
            var steps = Create();
            var macro = MacroOf("a", "b", "c");

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[2]);

            Assert.True(steps.MoveStepUpCommand.CanExecute(null));

            steps.MoveStepUpCommand.Execute(null);

            Assert.Equal(["a", "c", "b"], macro.Keystrokes.Select(TokenOf));

            // The selection follows the step, or a second ⌥↑ would move a different row.
            Assert.Equal(2, steps.SelectedStep!.Position);
            Assert.Equal("[c]", steps.SelectedStep.TokenText);
        }

        [Fact]
        public void MoveStepCommands_AtTheEndsOfTheList_CannotRun()
        {
            var steps = Create();

            steps.Load(MacroOf("a", "b"));

            steps.SelectStepCommand.Execute(steps.Items[0]);
            Assert.False(steps.MoveStepUpCommand.CanExecute(null));
            Assert.True(steps.MoveStepDownCommand.CanExecute(null));

            steps.SelectStepCommand.Execute(steps.Items[1]);
            Assert.True(steps.MoveStepUpCommand.CanExecute(null));
            Assert.False(steps.MoveStepDownCommand.CanExecute(null));
        }

        [Fact]
        public void MoveStepCommands_WithNothingSelected_CannotRun()
        {
            var steps = Create();

            steps.Load(MacroOf("a", "b"));

            Assert.False(steps.MoveStepUpCommand.CanExecute(null));
            Assert.False(steps.MoveStepDownCommand.CanExecute(null));
        }

        [Fact]
        public void RemoveStepCommand_DropsTheRowAndItsDelayTogether()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b")));

            steps.Load(macro);
            steps.RemoveStepCommand.Execute(steps.Items[0]);

            Assert.Equal(["b"], macro.Keystrokes.Select(TokenOf));
        }

        // ===== The composer's write path (issue #139) =========================================
        // MacroInspectorStepViewModel keeps no Keystroke reference, so the composer cannot write
        // through the row it is pointed at — every edit lands here, and every one of them rebuilds
        // the whole keystroke list from the rows rather than patching an index.

        [Fact]
        public void TrySetSelectedKey_ReplacesTheKeyAndKeepsTheStepsOwnDelay()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b")));

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            Assert.True(steps.TrySetSelectedKey(TestLayouts.Gen1Key("z")));

            // The delay is a keystroke of its own (06 §2.2) and it belongs to the step in front of
            // it — a write that patched one index would have left it behind or duplicated it.
            Assert.Equal(["z", "d080", "b"], macro.Keystrokes.Select(TokenOf));
            Assert.Equal(80, steps.Items[0].DelayMilliseconds);
        }

        [Fact]
        public void TrySetSelectedModifiers_ClearsAnExplicitDirection_AndDoesNotBringItBack()
        {
            // 05 §5.8: a modified keystroke's direction is discarded on the way to the file. The
            // write CLEARS the field rather than relying on that mask, so unticking the last
            // modifier cannot resurrect a `press` the user visibly lost.
            var macro = MacroOf("a");
            var steps = Create();

            macro.Keystrokes[0].UpDown = KeyDirection.Down;

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);
            steps.TrySetSelectedModifiers(MacroModifiers.LeftControl);

            Assert.Equal(KeyDirection.None, macro.Keystrokes[0].UpDown);

            steps.TrySetSelectedModifiers(MacroModifiers.None);

            Assert.Equal(KeyDirection.None, macro.Keystrokes[0].UpDown);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, steps.Items[0].ActionText);
        }

        [Fact]
        public void TrySetSelectedDirection_OnAChord_IsRefused()
        {
            var macro = MacroOf("a");
            var steps = Create();

            macro.Keystrokes[0].Modifiers = MacroModifiers.LeftControl;

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            Assert.False(steps.TrySetSelectedDirection(KeyDirection.Down));
            Assert.Equal(KeyDirection.None, macro.Keystrokes[0].UpDown);
        }

        [Fact]
        public void TrySetSelectedDirection_OnAModifierKeyStep_IsAllowed()
        {
            // The exception 05 §5.8 makes: a key that IS a modifier keeps press/release, and
            // Keystroke refuses to attach modifiers to it anyway.
            var macro = MacroOf("lshft");
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            Assert.True(steps.TrySetSelectedDirection(KeyDirection.Down));
            Assert.Equal(KeyDirection.Down, macro.Keystrokes[0].UpDown);
            Assert.Equal(MacroInspectorStepViewModel.PressAction, steps.Items[0].ActionText);
        }

        [Fact]
        public void InsertPlaceholder_DrawsARowAndWritesNothingToTheMacro()
        {
            var macro = MacroOf("a", "b");
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);
            steps.InsertPlaceholder();

            Assert.True(steps.HasPlaceholder);
            Assert.Equal(3, steps.Items.Count);
            Assert.True(steps.Items[1].IsPlaceholder);
            Assert.Equal(["a", "b"], macro.Keystrokes.Select(TokenOf));

            // The row is numbered where it sits, and everything after it moved down with it.
            Assert.Equal(["01", "02", "03"], steps.Items.Select(step => step.NumberText));
        }

        [Fact]
        public void InsertPlaceholder_WithNothingSelected_LandsAtTheEnd()
        {
            var macro = MacroOf("a", "b");
            var steps = Create();

            steps.Load(macro);
            steps.InsertPlaceholder();

            Assert.True(steps.Items[^1].IsPlaceholder);
            Assert.Same(steps.Items[^1], steps.SelectedStep);
        }

        [Fact]
        public void TrySetSelectedKey_OnThePlaceholder_InsertsTheStepWhereTheRowWasDrawn()
        {
            var macro = MacroOf("a", "b");
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);
            steps.InsertPlaceholder();

            Assert.True(steps.TrySetSelectedKey(TestLayouts.Gen1Key("z")));
            Assert.Equal(["a", "z", "b"], macro.Keystrokes.Select(TokenOf));
            Assert.False(steps.HasPlaceholder);
            Assert.Same(steps.Items[1], steps.SelectedStep);
        }

        [Fact]
        public void SelectingAnotherStep_DiscardsThePlaceholder_AndSelectsWhatWasClicked()
        {
            var macro = MacroOf("a", "b");
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);
            steps.InsertPlaceholder();

            // Row 2 is the placeholder, so row 3 is `b`. Clicking it abandons the placeholder — and
            // rebuilds the list, so the selection has to land on `b` at its NEW position.
            steps.SelectStepCommand.Execute(steps.Items[2]);

            Assert.False(steps.HasPlaceholder);
            Assert.Equal(2, steps.Items.Count);
            Assert.Same(steps.Items[1], steps.SelectedStep);
            Assert.Equal(["a", "b"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void DiscardPlaceholder_DropsTheRowAndTheSelectionWithIt()
        {
            var macro = MacroOf("a");
            var steps = Create();

            steps.Load(macro);
            steps.InsertPlaceholder();

            Assert.True(steps.DiscardPlaceholder());
            Assert.False(steps.HasPlaceholder);
            Assert.Null(steps.SelectedStep);
            Assert.False(steps.DiscardPlaceholder());
        }

        [Fact]
        public void Load_WithAnotherMacro_DropsAnOpenPlaceholder()
        {
            var steps = Create();

            steps.Load(MacroOf("a"));
            steps.InsertPlaceholder();
            steps.Load(MacroOf("b"));

            Assert.False(steps.HasPlaceholder);
            Assert.Null(steps.SelectedStep);
        }

        [Fact]
        public void Load_WithTheSameMacro_KeepsAnOpenPlaceholder()
        {
            // Every unrelated write ends in a counter refresh that re-loads the same macro. A
            // placeholder that did not survive that would be gone before the key could land on it.
            var macro = MacroOf("a");
            var steps = Create();

            steps.Load(macro);
            steps.InsertPlaceholder();
            steps.Load(macro);

            Assert.True(steps.HasPlaceholder);
        }

        [Fact]
        public void MoveStep_WithAnOpenPlaceholder_IsRefused()
        {
            var macro = MacroOf("a", "b");
            var steps = Create();

            steps.Load(macro);
            steps.InsertPlaceholder();

            Assert.False(steps.MoveStep(0, 1));
            Assert.False(steps.MoveStepUpCommand.CanExecute(null));
            Assert.False(steps.MoveStepDownCommand.CanExecute(null));
            Assert.Equal(["a", "b"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void ARemovedStep_LeavesTheSelectionOnTheRowThatTookItsPlace()
        {
            // The rows are rebuilt by every write, so a cached row reference is stale — the
            // selection has to be re-resolved by position afterwards.
            var macro = MacroOf("a", "b", "c");
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[1]);
            steps.RemoveStepCommand.Execute(steps.Items[1]);

            Assert.Equal(["a", "c"], macro.Keystrokes.Select(TokenOf));
            Assert.Same(steps.Items[1], steps.SelectedStep);
        }

        [Fact]
        public void ARemovedLastStep_LeavesTheSelectionOnWhatIsLeft()
        {
            var macro = MacroOf("a", "b");
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[1]);
            steps.RemoveStepCommand.Execute(steps.Items[1]);

            Assert.Same(Assert.Single(steps.Items), steps.SelectedStep);
        }

        [Fact]
        public void ARowFoldsInTheDelayBehindIt_SoTheComposerCanSeedItselfFromTheRow()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(120, TokenDialect.Gen1)!));

            steps.Load(macro);

            Assert.Equal(120, Assert.Single(steps.Items).DelayMilliseconds);
            Assert.False(steps.Items[0].IsRandomDelay);
        }

        [Fact]
        public void ARowCarryingTheRandomDelay_ReportsItAsRandomAndNotAsZeroMilliseconds()
        {
            // 11 §11.3's `dran` and a custom delay are two different answers, never one value with
            // a sentinel — which is why the row carries both a millisecond count AND a flag.
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            steps.Load(macro);

            Assert.True(steps.Items[0].IsRandomDelay);
            Assert.Equal(0, steps.Items[0].DelayMilliseconds);
        }

        [Fact]
        public void TrySetSelectedDelay_WithNothingSelected_WritesNothing()
        {
            var macro = MacroOf("a");
            var steps = Create();

            steps.Load(macro);

            Assert.False(steps.TrySetSelectedDelay(MacroInspectorDelay.Random));
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void TrySetSelectedDelay_OutsideTheRange_WritesNothing(int delay)
        {
            var steps = SelectFirstStep(out var macro);

            Assert.False(steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(delay)));
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void TrySetSelectedDelay_WithTheRandomDelay_WritesTheRandomDelayKey()
        {
            var steps = SelectFirstStep(out var macro);

            Assert.True(steps.TrySetSelectedDelay(MacroInspectorDelay.Random));
            Assert.Equal(["a", MacroDelayTokens.RandomToken], macro.Keystrokes.Select(TokenOf));
            Assert.Equal("random", steps.Items[0].DelayText);
        }

        [Theory]
        [InlineData(1, "d001")]
        [InlineData(50, "d050")]
        [InlineData(250, "d250")]
        [InlineData(999, "d999")]
        public void TrySetSelectedDelay_WithACustomDelay_WritesTheZeroPaddedDelayKey(int delay, string token)
        {
            var steps = SelectFirstStep(out var macro);

            Assert.True(steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(delay)));
            Assert.Equal(["a", token], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void TrySetSelectedDelay_Twice_ReplacesTheDelayRatherThanAddingASecond()
        {
            var steps = SelectFirstStep(out var macro);

            steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(50));
            steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(120));

            Assert.Equal(["a", "d120"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void TrySetSelectedDelay_FromCustomToRandom_ReplacesTheKeyRatherThanKeepingBoth()
        {
            var steps = SelectFirstStep(out var macro);

            steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(250));
            steps.TrySetSelectedDelay(MacroInspectorDelay.Random);

            Assert.Equal(["a", MacroDelayTokens.RandomToken], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void TrySetSelectedDelay_WithNone_TakesTheDelayOffTheStep()
        {
            var steps = SelectFirstStep(out var macro);

            steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(50));

            Assert.True(steps.TrySetSelectedDelay(MacroInspectorDelay.None));
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
            Assert.False(steps.Items[0].HasDelay);
        }

        [Fact]
        public void TrySetSelectedDelay_WithNoneOnADelayOnlyRow_DropsTheRow()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            Assert.True(steps.TrySetSelectedDelay(MacroInspectorDelay.None));
            Assert.Empty(macro.Keystrokes);
            Assert.Empty(steps.Items);
        }

        [Fact]
        public void TrySetSelectedDelay_OnADelayOnlyRow_ReplacesTheDelayItself()
        {
            // 06 §2.2 lets a macro open with a delay, and dropping such a row would edit the file
            // behind the user's back — so it stays, and its ONE editable thing is the delay.
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            Assert.True(steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(80)));
            Assert.Equal(["d080"], macro.Keystrokes.Select(TokenOf));
            Assert.True(steps.Items[0].IsDelayOnly);
        }

        /// <summary>
        /// §11.3's own contract: a delay the panel produced must survive being written to the layout
        /// file and read back (06 §2.2), both as text and as model content.
        /// </summary>
        [Fact]
        public void AppliedDelays_RoundTrippedThroughTheLayoutFile_SurviveUnchanged()
        {
            var parsed = ParseRgb("{q}>{a}");
            var macro = parsed.Layout.EnumerateMacros().Single();
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);
            steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(250));

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            var lines = LayoutFileSerializer.Serialize(parsed.Layout, parsed.InvalidLines);
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
            var roundTripped = reparsed.Layout.EnumerateMacros().Single();

            Assert.Equal(["{q}>{s5}{x1}{a}{d250}{dran}"], lines);
            Assert.True(macro.IsEquivalentTo(roundTripped));
            Assert.Equal(lines, LayoutFileSerializer.Serialize(reparsed.Layout, reparsed.InvalidLines));
        }

        /// <summary>
        /// The known 125 ms / 500 ms asymmetry of 05 §3.12 and 06 §2.2, pinned rather than hidden:
        /// <see cref="MacroDelayTokens.ResolveCustom"/> resolves those two by token and the legacy
        /// rows 10007/10008 shadow their generated twins, while the RGB/TKO parser reads the same
        /// text back as the generated codes 10085 + N. The <b>file text</b> is identical in both
        /// directions — which is the interoperability contract — but the key-table identity is not.
        /// </summary>
        [Theory]
        [InlineData(125)]
        [InlineData(500)]
        public void AppliedDelay_AtALegacyFixedValueOnTheRgb_KeepsItsTextButNotItsKeyIdentity(int delay)
        {
            var parsed = ParseRgb("{q}>{a}");
            var macro = parsed.Layout.EnumerateMacros().Single();
            var steps = Create();

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);
            steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(delay));

            var picked = macro.Keystrokes[^1].Key;
            var lines = LayoutFileSerializer.Serialize(parsed.Layout, parsed.InvalidLines);
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
            var roundTripped = reparsed.Layout.EnumerateMacros().Single();

            Assert.Equal([$"{{q}}>{{s5}}{{x1}}{{a}}{{d{delay}}}"], lines);
            Assert.Equal(lines, LayoutFileSerializer.Serialize(reparsed.Layout, reparsed.InvalidLines));
            Assert.NotEqual(picked.Code, roundTripped.Keystrokes[^1].Key.Code);
            Assert.Equal(10085 + delay, roundTripped.Keystrokes[^1].Key.Code);
        }

        [Fact]
        public void Delays_OnAnUngatedBoard_AreAvailable()
        {
            var steps = Create(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 0));

            Assert.True(steps.AreDelaysAvailable);
            Assert.Equal(string.Empty, steps.DelayUnavailableReason);
            Assert.False(steps.CanUpdateFirmware);
        }

        [Fact]
        public void Delays_OnAFreestyleAtTheGate_AreAvailable()
        {
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 340));

            Assert.True(steps.AreDelaysAvailable);
        }

        [Fact]
        public void Delays_InDemoMode_BypassTheGate()
        {
            var steps = Create(DeviceId.FreestyleEdge, new FirmwareState { IsDemoMode = true });

            Assert.True(steps.AreDelaysAvailable);
        }

        /// <summary>
        /// The gate is answered <b>in place</b> now, not as a message box: a modal refusal for a
        /// surface already on screen would interrupt nothing.
        /// </summary>
        [Fact]
        public void Delays_OnAFreestyleBelowTheGate_RefuseInPlaceWithTheSpecMessage()
        {
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            Assert.False(steps.AreDelaysAvailable);
            Assert.Equal(MacroInspectorStepsViewModel.FirmwareRefusalMessage, steps.DelayUnavailableReason);
            Assert.True(steps.CanUpdateFirmware);
            Assert.Equal(FirmwareFeatureGate.UpdateFirmwareButtonCaption, steps.UpdateFirmwareCaption);
        }

        [Fact]
        public void TrySetSelectedDelay_BelowTheFirmwareGate_WritesNothing()
        {
            var macro = MacroOf("a");
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            Assert.False(steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(250)));
            Assert.False(steps.TrySetSelectedDelay(MacroInspectorDelay.Random));
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void UpdateFirmwareCommand_WhenTheGateRefuses_OpensTheDevicesSupportPage()
        {
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            steps.UpdateFirmwareCommand.Execute(null);

            Assert.Equal(
                FirmwareSupportUrls.FindUrl(DeviceId.FreestyleEdge),
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        /// <summary>Drift guard against the gate row that stores the same refusal (09 §2).</summary>
        [Fact]
        public void FirmwareRefusalMessage_MatchesTheGateRowThatStoresIt()
        {
            Assert.Equal(
                MacroInspectorStepsViewModel.FirmwareRefusalMessage,
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.CustomMacroDelays)!.Message);
        }

        private MacroInspectorStepsViewModel Create()
        {
            return Create(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 100));
        }

        private MacroInspectorStepsViewModel Create(DeviceId deviceId, FirmwareState firmware)
        {
            return new MacroInspectorStepsViewModel(deviceId, firmware, _urlLauncher);
        }

        /// <summary>One step, selected — the state every composer write starts from.</summary>
        private MacroInspectorStepsViewModel SelectFirstStep(out Macro macro)
        {
            var steps = Create();

            macro = MacroOf("a");

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[0]);

            return steps;
        }

        private static Macro MacroOf(params string[] tokens)
        {
            var macro = new Macro();

            foreach (var token in tokens)
            {
                macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key(token)));
            }

            return macro;
        }

        private static string TokenOf(Keystroke keystroke)
        {
            return keystroke.Key.GetToken(TokenDialect.Gen1);
        }

        private static LayoutParseResult ParseRgb(params string[] lines)
        {
            return new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
        }

        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }
    }
}
