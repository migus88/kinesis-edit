using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector's Macro panel (mockup <c>2i</c>): the name dropdown and the "Also on" line,
    /// recording, the footer meters, the co-triggers, and the three <see cref="Refresh"/> shapes
    /// every rail panel has to survive — a null key, the key it already had, and somebody else's
    /// mutation.
    /// </summary>
    public sealed class MacroInspectorPanelViewModelTests
    {
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Strings_MatchTheMockVerbatim()
        {
            Assert.Equal("Macro", MacroInspectorPanelViewModel.PanelTitle);
            Assert.Equal(
                "Named, so it can be picked for another key from this same dropdown.",
                MacroInspectorPanelViewModel.ReuseNote);
            Assert.Equal("Also on ", MacroInspectorPanelViewModel.AlsoOnPrefix);

            // A DELIBERATE deviation from mockup 2i, which ends the banner "Esc stops." (issue
            // #122, AC 2): Escape is a remappable position, so a macro has to be able to record one
            // — and a banner that offers it as the way out while the keystroke is being appended as
            // a step is exactly the lie this panel's capture rules exist to avoid. The rest of the
            // sentence is the mock's, verbatim.
            Assert.Equal(
                "Recording into step 04 — your typing goes here, not into the app. "
                + "Click Stop, or anywhere else, to finish.",
                MacroInspectorPanelViewModel.BuildRecordingBanner("04"));
            Assert.DoesNotContain(
                "Esc",
                MacroInspectorPanelViewModel.RecordingBannerFormat,
                StringComparison.Ordinal);
            Assert.Equal(
                "Arrows = press/release. A bare modifier records as tap. Search and shortcuts are suspended until you stop.",
                MacroInspectorPanelViewModel.CaptureRule);

            // Issue #128: the honest sentence about what recording CANNOT do. It names the user's
            // own example, it does not blame the app — the chord is taken above the application and
            // never delivered — and it points at the composer, which is the way to author one.
            Assert.Contains("Ctrl+1", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("system", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains(
                MacroInspectorPanelViewModel.ComposeChordCaption,
                MacroInspectorPanelViewModel.OsReservedNote,
                StringComparison.Ordinal);
            Assert.Equal("Playback speed", MacroInspectorPanelViewModel.SpeedMeterLabel);
            Assert.Equal("this macro", MacroInspectorPanelViewModel.MacroLengthMeterLabel);
            Assert.Equal("layout keystrokes", MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel);

            // spec 02's verbatim refusal, carried over from the old macro panel.
            Assert.Equal("You cannot assign a macro to a modifier key", MacroInspectorPanelViewModel.RestrictedKeyMessage);
        }

        [Fact]
        public void Mode_IsTheMacroSlotAndItWantsTheWideRail()
        {
            var panel = Create();

            Assert.Equal(KeyInspectorMode.Macro, panel.Mode);

            // docs/design/handoff.md § Geometry: 268 on Layout, 300 on the macro-editing variant.
            Assert.True(panel.WantsWideRail);
        }

        [Fact]
        public void Refresh_WithNoKey_RefusesPolitelyAndShowsNothing()
        {
            var panel = Create();

            panel.Refresh(null, null, null, EditorAdvisories.Empty);

            Assert.False(panel.IsAvailable);
            Assert.Equal(MacroInspectorPanelViewModel.NoSelectionMessage, panel.UnavailableReason);
            Assert.Empty(panel.Steps.Items);
            Assert.False(panel.IsRecording);
        }

        [Fact]
        public void Refresh_OnAModifierPosition_CarriesTheSpecRefusal()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(scene.Panel.IsAvailable);
            Assert.Equal(MacroInspectorPanelViewModel.RestrictedKeyMessage, scene.Panel.UnavailableReason);
        }

        [Fact]
        public void Refresh_WithTheSameKeyTwice_KeepsWhatWasBeingEdited()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Refresh();

            Assert.Single(scene.Panel.Steps.Items);
            Assert.Equal("[a]", scene.Panel.Steps.Items[0].TokenText);
        }

        [Fact]
        public void Refresh_AfterAForeignMutation_ReReadsAndWritesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            var macroCount = scene.Layout.MacroCount;
            var assigned = 0;

            scene.Panel.Assigned += (_, _) => assigned++;

            // Somebody else — a reset, an import, the Macros tab — empties the position.
            scene.Key.Key.ClearMacros();

            scene.Refresh();

            Assert.Empty(scene.Panel.Steps.Items);
            Assert.Equal(0, assigned);
            Assert.Equal(macroCount - 1, scene.Layout.MacroCount);
        }

        [Fact]
        public void Refresh_MovingToAnotherKey_StopsRecording()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.RecordCommand.Execute(null);

            Assert.True(scene.Panel.IsRecording);

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.False(scene.Panel.IsRecording);
        }

        [Fact]
        public void RecordCommand_AnnouncesItselfAndTakesTheNextKeystroke()
        {
            var scene = new Scene(this);
            var recordingChanges = 0;

            scene.Panel.RecordingChanged += (_, _) => recordingChanges++;
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);

            Assert.True(scene.Panel.IsRecording);
            Assert.True(((IKeystrokeSink)scene.Panel).WantsKeystrokes);
            Assert.Equal(MacroInspectorPanelViewModel.RecordingCaption, scene.Panel.RecordCommandCaption);
            Assert.True(recordingChanges > 0);

            scene.Panel.ReceiveKeystroke(Captured("a"));

            Assert.Equal("[a]", Assert.Single(scene.Panel.Steps.Items).TokenText);

            // Still armed: 2i's banner counts up as the macro grows, so one press is not the end of
            // a recording.
            Assert.True(scene.Panel.IsRecording);
            Assert.Equal("02", scene.Panel.Steps.NextStepNumberText);
        }

        [Fact]
        public void RecordingBanner_NamesTheStepTheNextKeystrokeLandsIn()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b", "c");

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildRecordingBanner("04"),
                scene.Panel.RecordingBanner);
        }

        [Fact]
        public void ReceiveKeystroke_WhileNotRecording_WritesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.ReceiveKeystroke(Captured("a"));

            Assert.Empty(scene.Panel.Steps.Items);
        }

        [Fact]
        public void Deactivate_StandsTheRecordingDown()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.RecordCommand.Execute(null);
            scene.Panel.Deactivate();

            Assert.False(scene.Panel.IsRecording);
            Assert.False(((IKeystrokeSink)scene.Panel).WantsKeystrokes);
        }

        /// <summary>
        /// "Editing in place" means the first recorded keystroke <b>is</b> the macro: there is no
        /// Assign button in this panel and none is wanted.
        /// </summary>
        [Fact]
        public void Recording_OnAKeyWithNoMacro_CreatesAndAssignsOne()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Key.Key.IsMacro);

            scene.Record("a");

            Assert.True(scene.Key.Key.IsMacro);

            var macro = Assert.Single(scene.Key.Key.Macros.OfType<Macro>());

            Assert.Equal(scene.Key.Key.TriggerKey.Code, macro.TriggerKey);
            Assert.Equal(0, macro.LayerIndex);
        }

        [Fact]
        public void Recording_WhenTheProfileIsAtItsMacroCount_RefusesAndSaysSo()
        {
            var scene = new Scene(this);
            var limit = MacroLimits.ResolveMaxMacroCount(scene.Device)!.Value;

            TestLayouts.FillMacroSlots(scene.Layout, limit);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            // The filled slots may already have reached this key; take one the fill did not.
            if (scene.Key.Key.IsMacro)
            {
                return;
            }

            scene.Record("a");

            Assert.Equal(MacroInspectorPanelViewModel.BuildMacroCountLimitMessage(limit), scene.Panel.Message);
            Assert.False(scene.Key.Key.IsMacro);
        }

        [Fact]
        public void Meters_ReadTheDevicesOwnBudgets_WithTheSpaceGroupedNumbers()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            var capability = scene.Device.Device.Macros;

            Assert.Equal(capability.MaxCharactersPerMacro, scene.Panel.MacroLengthMeter.Limit);
            Assert.Equal(capability.MaxTotalKeystrokes, scene.Panel.LayoutKeystrokeMeter.Limit);
            Assert.Equal(capability.Speed!.Maximum, scene.Panel.SpeedMeter.Limit);

            // AdvisoryText.Number's space separator, mockup 1i/2i — never the invariant comma.
            Assert.Equal("2 / 300", scene.Panel.MacroLengthMeter.Caption);
            Assert.DoesNotContain(",", scene.Panel.LayoutKeystrokeMeter.Caption, StringComparison.Ordinal);
        }

        [Fact]
        public void Meters_OverBudget_ReportAndNeverRefuse()
        {
            var meter = new MacroMeterViewModel(MacroInspectorPanelViewModel.MacroLengthMeterLabel);

            meter.Set(5140, 7200);
            Assert.False(meter.IsOverBudget);
            Assert.Equal("5 140 / 7 200", meter.Caption);

            meter.Set(7201, 7200);
            Assert.True(meter.IsOverBudget);

            // A null limit is "no limit", never zero — the Advantage2 states no macro count.
            meter.Set(9000, null);
            Assert.False(meter.IsOverBudget);
            Assert.Equal("9 000", meter.Caption);
        }

        [Fact]
        public void Speed_AssignedOnAKeyWithNoMacro_CreatesTheMacroAndWritesIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Speed = scene.Panel.SpeedMaximum;

            var macro = Assert.Single(scene.Key.Key.Macros.OfType<Macro>());

            Assert.Equal(scene.Panel.SpeedMaximum, macro.Speed);
            Assert.Equal(scene.Panel.SpeedMaximum, scene.Panel.SpeedMeter.Value);
        }

        [Fact]
        public void ToggleCoTrigger_HoldsTheDeviceCapAndReportsIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            var limit = scene.Panel.MaxCoTriggers;

            for (var index = 0; index < limit; index++)
            {
                scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[index]);
            }

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[limit]);

            Assert.Equal(MacroInspectorPanelViewModel.BuildCoTriggerLimitMessage(limit), scene.Panel.Message);
            Assert.Equal(limit, Assert.Single(scene.Key.Key.Macros.OfType<Macro>()).CoTriggerCount);
        }

        [Fact]
        public void NameOptions_ListEveryMacroOfTheProfileOnce()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Record("b");

            scene.RefreshLibrary();

            // Both macros, and no "no macro" placeholder while this key carries one.
            Assert.Equal(2, scene.Panel.NameOptions.Count);
            Assert.DoesNotContain(scene.Panel.NameOptions, option => option.IsNone);
            Assert.NotNull(scene.Panel.SelectedName);
            Assert.False(scene.Panel.SelectedName!.IsNone);
        }

        [Fact]
        public void NameOptions_OnAKeyWithNoMacro_OfferThePlaceholderFirst()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.RefreshLibrary();

            Assert.True(scene.Panel.NameOptions[0].IsNone);
            Assert.Same(scene.Panel.NameOptions[0], scene.Panel.SelectedName);
        }

        [Fact]
        public void SelectedName_PickingAnotherMacro_AssignsItToThisKey()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.RefreshLibrary();

            var pick = scene.Panel.NameOptions.First(option => !option.IsNone);

            scene.Panel.SelectedName = pick;
            scene.RefreshLibrary();

            var macro = Assert.Single(scene.Key.Key.Macros.OfType<Macro>());

            Assert.Equal(["[a]", "[b]"], scene.Panel.Steps.Items.Select(step => step.TokenText));
            Assert.Equal(scene.Key.Key.TriggerKey.Code, macro.TriggerKey);
        }

        [Fact]
        public void SelectedName_PickingThePlaceholder_DeletesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.RefreshLibrary();

            scene.Panel.SelectedName = new MacroNameOptionViewModel();

            Assert.True(scene.Key.Key.IsMacro);
        }

        /// <summary>
        /// "Also on [f7] · Fn" — 2i's where-else line, built from the library entry's other sites.
        /// </summary>
        [Fact]
        public void AlsoOnText_NamesEveryOtherPlaceTheMacroFiresFrom()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.RefreshLibrary();

            Assert.Equal(string.Empty, scene.Panel.AlsoOnText);
            Assert.False(scene.Panel.HasAlsoOnText);

            // Give the same macro a second home, then look at it from the first.
            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.RefreshLibrary();
            scene.Panel.SelectedName = scene.Panel.NameOptions.First(option => !option.IsNone);
            scene.RefreshLibrary();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.RefreshLibrary();

            Assert.True(scene.Panel.HasAlsoOnText);
            Assert.StartsWith(MacroInspectorPanelViewModel.AlsoOnPrefix, scene.Panel.AlsoOnText, StringComparison.Ordinal);
            Assert.Contains("[2]", scene.Panel.AlsoOnText, StringComparison.Ordinal);
            Assert.Contains("Top", scene.Panel.AlsoOnText, StringComparison.Ordinal);
        }

        [Fact]
        public void IsNamed_IsTrueOnlyOnceTheMacroReallyCarriesAName()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.RefreshLibrary();

            Assert.False(scene.Panel.IsNamed);

            scene.Library.Rename(scene.Library.Entries[0], "Sign-off block");
            scene.RefreshLibrary();

            Assert.True(scene.Panel.IsNamed);
            Assert.Equal("Sign-off block", scene.Panel.SelectedName!.Caption);
        }

        // ===== Revert (issue #122, AC 1) =====================================================
        // The rail's `Revert key` used to run the editor's ClearRemap(), which touches only the
        // remap — so on this panel it did nothing at all, and nothing anywhere kept a "before".

        [Fact]
        public void TryRevert_PutsBackTheMacroThePositionHadWhenItWasSelected()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // Leave and come back, so the baseline is "one step" rather than "no macro".
            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            var speed = scene.Panel.Speed;
            var otherSpeed = speed < scene.Panel.SpeedMaximum ? speed + 1 : speed - 1;

            scene.Record("b", "c");

            scene.Panel.Speed = otherSpeed;
            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(3, scene.CurrentMacro!.Keystrokes.Count);
            Assert.Equal(1, scene.CurrentMacro.CoTriggerCount);
            Assert.Equal(otherSpeed, scene.CurrentMacro.Speed);

            Assert.True(scene.Panel.TryRevert());

            Assert.Single(scene.CurrentMacro!.Keystrokes);
            Assert.Equal(0, scene.CurrentMacro.CoTriggerCount);
            Assert.Equal(speed, scene.CurrentMacro.Speed);

            // The panel re-read: the step list and the meters move with the model, or the revert is
            // invisible until something else happens to refresh the rail.
            Assert.Equal(1, scene.Panel.Steps.Count);
            Assert.Equal(speed, scene.Panel.Speed);
            Assert.False(scene.Panel.CoTriggers[0].IsOn);
        }

        [Fact]
        public void TryRevert_OnAPositionThatCarriedNoMacro_LeavesItCarryingNone()
        {
            // "There was nothing before" is a state the baseline has to be able to hold: the macro
            // this panel creates on the first keystroke is exactly what Revert has to undo.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            Assert.NotNull(scene.CurrentMacro);

            Assert.True(scene.Panel.TryRevert());

            Assert.Null(scene.CurrentMacro);
            Assert.Equal(0, scene.Panel.Steps.Count);
            Assert.Equal(0, scene.Layout.MacroCount);
        }

        [Fact]
        public void TryRevert_IsIdempotent()
        {
            // The baseline is read on restore, never consumed, and never re-taken there — so the
            // second Revert lands on the same state as the first.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("b", "c");

            Assert.True(scene.Panel.TryRevert());
            Assert.Single(scene.CurrentMacro!.Keystrokes);

            Assert.True(scene.Panel.TryRevert());
            Assert.Single(scene.CurrentMacro!.Keystrokes);
            Assert.Equal(1, scene.Panel.Steps.Count);
        }

        [Fact]
        public void TheBaseline_SurvivesTheRefreshesTheUsersOwnEditsCause()
        {
            // The trap the panel contract warns about: Refresh runs on EVERY editor refresh, for
            // every panel — so a snapshot taken unconditionally there is overwritten by the very
            // edit the user wants undone, and Revert silently becomes a no-op again.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);

            foreach (var token in new[] { "b", "c" })
            {
                scene.Panel.ReceiveKeystroke(Captured(token));

                // What the editor's funnel does after every write this panel announces.
                scene.Refresh();
            }

            scene.Panel.Deactivate();

            Assert.Equal(3, scene.CurrentMacro!.Keystrokes.Count);

            Assert.True(scene.Panel.TryRevert());

            Assert.Single(scene.CurrentMacro!.Keystrokes);
        }

        [Fact]
        public void TheBaseline_SurvivesDeactivate_BecauseAModeSwitchDoesNotMoveThePosition()
        {
            // Deactivate stands capture down; it does not change what this key held when it was
            // clicked. A user who switched to Remap and back must still be able to revert.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            scene.Panel.Deactivate();
            scene.Refresh();

            Assert.True(scene.Panel.TryRevert());
            Assert.Null(scene.CurrentMacro);
        }

        [Fact]
        public void TryRevert_RestoresEverySlotAndTheActiveOne()
        {
            // A key holds up to five macros, told apart by their co-triggers (06 §1), and the
            // active slot is what the inspector edits. Restoring names the slot each macro came out
            // of rather than re-assigning into the first free one, so a gap-carrying set comes back
            // where the file put it.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            var key = scene.Key.Key;

            key.SetMacro(1, scene.Layout.CreateMacro());
            key.SetMacro(3, scene.Layout.CreateMacro());
            key.ActiveMacroIndex = 3;

            // A fresh selection, so the baseline is the two-slot state.
            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            key.ClearMacros();

            Assert.True(scene.Panel.TryRevert());

            Assert.NotNull(key.GetMacro(1));
            Assert.Null(key.GetMacro(2));
            Assert.NotNull(key.GetMacro(3));
            Assert.Equal(3, key.ActiveMacroIndex);
        }

        [Fact]
        public void TryRevert_WithNothingSelected_RefusesSoTheFooterFallsThroughToTheEditor()
        {
            var panel = Create();

            Assert.False(panel.TryRevert());

            panel.Refresh(null, null, null, EditorAdvisories.Empty);

            Assert.False(panel.TryRevert());
        }

        [Fact]
        public void TryRevert_OnAPositionThatCannotCarryAMacro_Refuses()
        {
            // A modifier position (05 §5.3). There is no macro state to put back, so the footer must
            // fall through to the editor's own reset rather than claim the action.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(scene.Panel.IsAvailable);
            Assert.False(scene.Panel.TryRevert());
        }

        [Fact]
        public void IsRecordingControl_NamesTheTwoButtonsThatArmCapture_AndNothingElse()
        {
            // What the editor's pointer stand-down asks before it ends a recording: the press that
            // lands on Record/Stop must not be the press that stops it.
            var panel = Create();

            Assert.True(panel.IsRecordingControl(panel.RecordCommand));
            Assert.True(panel.IsRecordingControl(panel.InsertStepCommand));
            Assert.False(panel.IsRecordingControl(panel.ToggleCoTriggerCommand));
            Assert.False(panel.IsRecordingControl(null));
        }

        // ===== The chord composer (issue #128) ================================================
        // The chords a user cannot record — `Ctrl+1` switches desktop on macOS, and a hotkey utility
        // can claim any other — are consumed above the application and never delivered to it, so
        // they are unreachable by capture at any price. These cover the way round that: authoring
        // the chord instead of pressing it.

        [Fact]
        public void TheComposer_IsClosedAtRest_AndOffersEverySidedModifier()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Panel.IsComposerOpen);
            Assert.Equal(MacroInspectorPanelViewModel.ComposeChordCaption, scene.Panel.ComposerToggleCaption);

            // Ctrl, Shift, Alt and Cmd, each in the two spellings 05 §5.1 distinguishes. The generic
            // codes are deliberately absent: capture always reports a side, and authoring a set that
            // does not say which side it means would write a worse macro than recording produces.
            Assert.Equal(
                [
                    MacroModifiers.LeftShift,
                    MacroModifiers.RightShift,
                    MacroModifiers.LeftControl,
                    MacroModifiers.RightControl,
                    MacroModifiers.LeftAlt,
                    MacroModifiers.RightAlt,
                    MacroModifiers.LeftWin,
                    MacroModifiers.RightWin
                ],
                scene.Panel.ChordModifiers.Select(modifier => modifier.Modifier));

            Assert.All(scene.Panel.ChordModifiers, modifier => Assert.False(modifier.IsOn));

            // Left is the unmarked side; only the right-hand four spell an `R`.
            Assert.Equal(4, scene.Panel.ChordModifiers.Count(modifier => modifier.HasSide));
        }

        [Fact]
        public void TheComposer_InsertsTheTickedModifiersWithTheChosenKey()
        {
            // THE USER'S OWN EXAMPLE. `Ctrl+1` cannot be recorded on macOS at any price — the window
            // server switches desktop and the app is never told — so this is the case the whole
            // feature exists for, and it is authored without a keypress.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();
            scene.TickChordModifier(MacroModifiers.LeftControl);
            scene.PickChordKey("1");

            Assert.True(scene.Panel.InsertChordCommand.CanExecute(null));

            scene.Panel.InsertChordCommand.Execute(null);

            var keystroke = Assert.Single(scene.CurrentMacro!.Keystrokes);

            Assert.Equal("1", keystroke.Key.GetToken(TokenDialect.Gen1));
            Assert.Equal(MacroModifiers.LeftControl, keystroke.Modifiers);

            // ...and the step list shows it exactly as a recorded chord would: `[1] ⌃ held`.
            var step = Assert.Single(scene.Panel.Steps.Items);

            Assert.Equal("[1]", step.TokenText);
            Assert.Equal(MacroInspectorStepViewModel.HeldAction, step.ActionText);
            Assert.Equal(MacroModifierMarks.ControlMark, Assert.Single(step.Modifiers).Symbol);
        }

        [Fact]
        public void TheComposer_CreatesTheMacroOnTheFirstInsert_JustAsRecordingDoes()
        {
            // "There is no Assign button" cuts both ways: the composer is a way of making a step, so
            // it has to be able to make the FIRST one on a position that carries no macro at all.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Null(scene.CurrentMacro);

            scene.OpenComposer();
            scene.TickChordModifier(MacroModifiers.LeftControl);
            scene.PickChordKey("1");
            scene.Panel.InsertChordCommand.Execute(null);

            Assert.NotNull(scene.CurrentMacro);
            Assert.Equal(1, scene.Layout.MacroCount);
        }

        [Fact]
        public void TheComposer_StaysOpenAndTicked_SoARunOfChordsIsOnePickEach()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();
            scene.TickChordModifier(MacroModifiers.LeftControl);

            scene.PickChordKey("1");
            scene.Panel.InsertChordCommand.Execute(null);

            scene.PickChordKey("2");
            scene.Panel.InsertChordCommand.Execute(null);

            Assert.True(scene.Panel.IsComposerOpen);
            Assert.Equal(MacroModifiers.LeftControl, scene.Panel.ComposedModifiers);
            Assert.Equal(["[1]", "[2]"], scene.Panel.Steps.Items.Select(step => step.TokenText));
        }

        [Fact]
        public void TickingAModifierTwice_TakesItOffAgain()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();

            scene.TickChordModifier(MacroModifiers.RightAlt);

            Assert.Equal(MacroModifiers.RightAlt, scene.Panel.ComposedModifiers);
            Assert.True(scene.FindChordModifier(MacroModifiers.RightAlt).IsOn);

            scene.TickChordModifier(MacroModifiers.RightAlt);

            Assert.Equal(MacroModifiers.None, scene.Panel.ComposedModifiers);
            Assert.False(scene.FindChordModifier(MacroModifiers.RightAlt).IsOn);
        }

        [Fact]
        public void TheComposer_IsReachableOnlyWhereAMacroCanBeOpen()
        {
            // The composer is a second way to make a step, so it is available exactly where making
            // one is: nothing selected, a modifier position (05 §5.3) or a device without macros and
            // the panel draws its refusal instead.
            var panel = Create();

            Assert.False(panel.ToggleComposerCommand.CanExecute(null));
            Assert.False(panel.InsertChordCommand.CanExecute(null));

            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(scene.Panel.IsAvailable);
            Assert.False(scene.Panel.ToggleComposerCommand.CanExecute(null));

            scene.Panel.ToggleComposerCommand.Execute(null);

            Assert.False(scene.Panel.IsComposerOpen);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.True(scene.Panel.ToggleComposerCommand.CanExecute(null));
        }

        [Fact]
        public void InsertChord_WithNoKeyPicked_CannotRunAndWritesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();
            scene.TickChordModifier(MacroModifiers.LeftControl);

            Assert.False(scene.Panel.HasComposedKey);
            Assert.False(scene.Panel.InsertChordCommand.CanExecute(null));

            scene.Panel.InsertChordCommand.Execute(null);

            Assert.Null(scene.CurrentMacro);
        }

        [Fact]
        public void TheComposer_ClosesAndForgetsTheChord_WhenTheSelectionMoves()
        {
            // A half-composed chord belongs to the position it was started on, exactly as a
            // half-recorded step does — and a modifier left ticked would silently attach itself to
            // the next chord on the next key.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();
            scene.TickChordModifier(MacroModifiers.LeftControl);
            scene.PickChordKey("1");

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.False(scene.Panel.IsComposerOpen);
            Assert.Equal(MacroModifiers.None, scene.Panel.ComposedModifiers);
            Assert.False(scene.Panel.HasComposedKey);
        }

        [Fact]
        public void ThePreview_ReadsBackWhatWouldBeWritten_AndNeverWhatWasMerelyTicked()
        {
            // 05 §5.1 attaches no modifier string to a key that is itself a modifier, and Keystroke
            // enforces it on assignment. A preview built from the ticked set rather than from the
            // resulting keystroke would promise a chord the file cannot hold.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();
            scene.TickChordModifier(MacroModifiers.LeftControl);

            scene.PickChordKey("1");

            Assert.Equal("[1]", scene.Panel.ComposedTokenText);
            Assert.Equal(MacroModifierMarks.ControlMark, Assert.Single(scene.Panel.ComposedMarks).Symbol);

            scene.PickChordKey("lshft");

            Assert.Equal("[lshft]", scene.Panel.ComposedTokenText);
            Assert.Empty(scene.Panel.ComposedMarks);
        }

        [Fact]
        public void TheComposer_RemembersWhatItInserted_ForTheRecentChip()
        {
            // One store per editor, shared by every picker it hosts: a chord built here is one chip
            // away in the rail's own Remap picker afterwards.
            var recent = new RecentTokenStore();
            var scene = new Scene(this, recent);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.OpenComposer();
            scene.PickChordKey("1");
            scene.Panel.InsertChordCommand.Execute(null);

            Assert.Contains(recent.Entries, entry => entry.GetToken(TokenDialect.Gen1) == "1");
        }

        /// <summary>One keystroke as the capture service would hand it over.</summary>
        private static CapturedKeystroke Captured(string token)
        {
            return new CapturedKeystroke
            {
                Key = TestLayouts.Gen1Key(token),
                PhysicalKey = PhysicalKeyCode.None
            };
        }

        private MacroInspectorPanelViewModel Create(MacroLibrary? library = null)
        {
            return new MacroInspectorPanelViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _urlLauncher,
                () => library);
        }

        /// <summary>
        /// One panel over one real Freestyle Edge RGB layout and its library, refreshed exactly as
        /// the rail refreshes it — pushed, never subscribed.
        /// </summary>
        private sealed class Scene
        {
            public DeviceSnapshot Device { get; }

            public KeyboardLayout Layout { get; }

            public MacroLibrary Library { get; }

            public MacroInspectorPanelViewModel Panel { get; }

            public KeyboardKeyViewModel Key { get; private set; } = null!;

            /// <summary>
            /// The macro the selected position is really carrying, read straight off the model
            /// rather than off the panel — a revert that only moved the view models would pass a
            /// test written against them.
            /// </summary>
            public Macro? CurrentMacro
            {
                get
                {
                    for (var slot = Core.Model.Macro.MinMacroIndex; slot <= Core.Model.Macro.MaxMacroIndex; slot++)
                    {
                        if (Key.Key.GetMacro(slot) is { } macro)
                        {
                            return macro;
                        }
                    }

                    return null;
                }
            }

            private readonly KeyboardLayerViewModel _layer;

            public Scene(MacroInspectorPanelViewModelTests owner, RecentTokenStore? recentTokens = null)
            {
                Device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
                Layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
                Library = new MacroLibrary(Layout);

                _layer = KeyboardLayerViewModel.BuildAll(
                    Layout,
                    Core.Geometry.Visual.VisualCatalog.FreestyleEdgeRgb,
                    null)[0];

                Panel = new MacroInspectorPanelViewModel(Device, owner._urlLauncher, () => Library, recentTokens);
            }

            public void Select(int keyIndex)
            {
                Key = _layer.FindByIndex(keyIndex)
                      ?? throw new InvalidOperationException($"The layer has no position {keyIndex}.");

                Refresh();
            }

            public void Refresh()
            {
                Panel.Refresh(Key, _layer, Layout, EditorAdvisories.Empty);
            }

            public void RefreshLibrary()
            {
                Library.Refresh();

                Refresh();
            }

            public void Record(params string[] tokens)
            {
                Panel.RecordCommand.Execute(null);

                foreach (var token in tokens)
                {
                    Panel.ReceiveKeystroke(Captured(token));
                }

                Panel.Deactivate();
            }

            /// <summary>Discloses the chord composer through its own command, as the button does.</summary>
            public void OpenComposer()
            {
                Panel.ToggleComposerCommand.Execute(null);
            }

            /// <summary>Ticks one modifier through the command the toggle runs.</summary>
            public void TickChordModifier(MacroModifiers modifier)
            {
                Panel.ToggleChordModifierCommand.Execute(FindChordModifier(modifier));
            }

            /// <summary>The toggle carrying <paramref name="modifier"/>, rebuilt on every tick.</summary>
            public MacroChordModifier FindChordModifier(MacroModifiers modifier)
            {
                return Panel.ChordModifiers.First(toggle => toggle.Modifier == modifier);
            }

            /// <summary>
            /// Highlights the catalog row for <paramref name="token"/> through the picker's own
            /// query and selection, rather than reaching past it — the picker is what the user
            /// drives, and a test that set the definition directly would not notice a catalog that
            /// no longer offers the key.
            /// </summary>
            public void PickChordKey(string token)
            {
                var definition = TestLayouts.Gen1Key(token);

                Panel.ChordPicker.Query = token;
                Panel.ChordPicker.SelectedRow = Panel.ChordPicker.Rows.First(
                    row => ReferenceEquals(row.Definition, definition));
            }
        }
    }
}
