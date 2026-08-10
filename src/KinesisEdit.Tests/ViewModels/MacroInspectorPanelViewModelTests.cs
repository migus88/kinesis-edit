using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
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
    /// The key inspector's Macro panel: the slot chips, the Trigger strip, the composer, the
    /// footer's two macro actions, recording, the meters and the repeat stepper — and the three
    /// <see cref="Refresh"/> shapes every rail panel has to survive: a null key, the key it already
    /// had, and somebody else's mutation.
    /// <para>
    /// The <c>MacroName_*</c> set went with issue #146's removal of the inline name field. What
    /// replaces it is one case in the other direction — that the panel writes <c>Macro.Name</c>
    /// nowhere at all — plus <c>KeyboardEditorViewModelMacroNameTests</c>, which is where "a stored
    /// name survives a load and a save with no rename path" belongs.
    /// </para>
    /// </summary>
    public sealed class MacroInspectorPanelViewModelTests
    {
        private readonly FakeUrlLauncher _urlLauncher = new();

        [AvaloniaFact]
        public void Strings_MatchTheMockVerbatim()
        {
            Assert.Equal("Macro", MacroInspectorPanelViewModel.PanelTitle);

            // The two footer actions of issue #141, the first of them recaptioned to the designer's
            // mock by issue #148. It said `Copy macro to…` until then, so that the rail's own footer
            // six rows below — a `Copy to…` that copies the WHOLE position — could not be confused
            // with it. The mock draws the short caption and the mock won: the two are told apart by
            // where they are now, not by what they say. The cancel is unchanged, and is the rail
            // footer's own wording, because it ends that same pick.
            Assert.Equal("Copy to…", MacroInspectorPanelViewModel.CopyMacroCaption);
            Assert.Equal(KeyInspectorViewModel.CopyToCaption, MacroInspectorPanelViewModel.CopyMacroCaption);
            Assert.Equal(KeyInspectorViewModel.CancelCopyCaption, MacroInspectorPanelViewModel.CancelCopyCaption);
            Assert.Equal("Delete", MacroInspectorPanelViewModel.DeleteMacroCaption);

            // Two record buttons, two captions (issue #146). A bare `Record` on both said nothing
            // about which one starts a take and which one takes a single key.
            Assert.Equal("Record sequence", MacroInspectorPanelViewModel.RecordSequenceCaption);
            Assert.Equal("Record key", MacroInspectorPanelViewModel.RecordKeyCaption);
            Assert.Equal("Stop", MacroInspectorPanelViewModel.RecordingCaption);

            // A DELIBERATE deviation from mockup 2i, which ends the banner "Esc stops." (issue
            // #122, AC 2): Escape is a remappable position, so a macro has to be able to record one
            // — and a banner that offers it as the way out while the keystroke is being appended as
            // a step is exactly the lie this panel's capture rules exist to avoid.
            //
            // The step number went with issue #146, which took the numbers off the rows: a banner
            // counting up to a label nothing on screen carries would point at nothing.
            Assert.Equal(
                "Recording — your typing goes here, not into the app. "
                + "Click Stop, or anywhere else, to finish.",
                MacroInspectorPanelViewModel.RecordingBannerText);
            Assert.DoesNotContain(
                "Esc",
                MacroInspectorPanelViewModel.RecordingBannerText,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "{0}",
                MacroInspectorPanelViewModel.RecordingBannerText,
                StringComparison.Ordinal);

            // Issue #128's honest sentence about what recording CANNOT do, reworded for #139. It
            // names the user's own example, it does not blame the app — the chord is taken above the
            // application and never delivered — and it names the two steps that author one now: the
            // bare key is recordable, and the modifiers are ticked on the step afterwards.
            Assert.Contains("Ctrl+1", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("system", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("Record the bare key", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("tick the modifiers", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);

            // The single-shot banner is a different sentence from the run's, because it means
            // something different: one keystroke, onto the step the composer is pointed at. It names
            // that row as "the selected step" rather than by number, which is both what survives
            // #146 and the more honest phrasing — the row is the one wearing the selection ring.
            Assert.Equal(
                "Recording the selected step — the next key you press becomes it. "
                + "Click Stop, or anywhere else, to cancel.",
                MacroInspectorPanelViewModel.StepCaptureBannerText);
            Assert.DoesNotContain(
                "{0}",
                MacroInspectorPanelViewModel.StepCaptureBannerText,
                StringComparison.Ordinal);

            // `Playback speed` until #146: the mock draws `Speed ──●── 5 of 9` on one row of a rail
            // that also has to hold the slider.
            Assert.Equal("Speed", MacroInspectorPanelViewModel.SpeedMeterLabel);
            Assert.Equal("layout keystrokes", MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel);
            Assert.Equal("chars", MacroInspectorPanelViewModel.LayoutKeystrokeUnit);

            // TWO METERS, not four (issue #148). `this macro 128 / 500 · macros 24 / 100` was the
            // muted line #146 demoted the other pair onto, and the mock draws neither — so the
            // labels, the join and the two `MacroMeterViewModel`s went with the line. What that
            // costs is written down in docs/app/design-system.md: nothing in the app reads out 06
            // §6's profile-wide macro count or the per-macro character cap any more.

            // The two header strips of #137, one row and one label each since #146 drew them side
            // by side. `SLOTS` is plural: it labels the whole chip strip, not the slot under edit.
            Assert.Equal("SLOTS", MacroInspectorPanelViewModel.SlotSectionLabel);
            Assert.Equal("TRIGGER", MacroInspectorPanelViewModel.TriggerSectionLabel);
            Assert.Equal("+", MacroInspectorPanelViewModel.TriggerJoin);

            // spec 02's verbatim refusal, carried over from the old macro panel.
            Assert.Equal("You cannot assign a macro to a modifier key", MacroInspectorPanelViewModel.RestrictedKeyMessage);
        }

        [AvaloniaFact]
        public void Mode_IsTheMacroSlotAndItWantsTheWideRail()
        {
            var panel = Create();

            Assert.Equal(KeyInspectorMode.Macro, panel.Mode);

            // docs/design/handoff.md § Geometry: 268 on Layout, 300 on the macro-editing variant.
            Assert.True(panel.WantsWideRail);
        }

        [AvaloniaFact]
        public void Refresh_WithNoKey_RefusesPolitelyAndShowsNothing()
        {
            var panel = Create();

            panel.Refresh(null, null, null, EditorAdvisories.Empty);

            Assert.False(panel.IsAvailable);
            Assert.Equal(MacroInspectorPanelViewModel.NoSelectionMessage, panel.UnavailableReason);
            Assert.Empty(panel.Steps.Items);
            Assert.False(panel.IsRecording);
        }

        [AvaloniaFact]
        public void Refresh_OnAModifierPosition_CarriesTheSpecRefusal()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(scene.Panel.IsAvailable);
            Assert.Equal(MacroInspectorPanelViewModel.RestrictedKeyMessage, scene.Panel.UnavailableReason);
        }

        [AvaloniaFact]
        public void Refresh_WithTheSameKeyTwice_KeepsWhatWasBeingEdited()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Refresh();

            Assert.Single(scene.Panel.Steps.Items);
            Assert.Equal("a", scene.Panel.Steps.Items[0].TokenText);
        }

        [AvaloniaFact]
        public void Refresh_AfterAForeignMutation_ReReadsAndWritesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            var macroCount = scene.Layout.MacroCount;
            var assigned = 0;

            scene.Panel.Assigned += (_, _) => assigned++;

            // Somebody else — a reset, an import, a copy — empties the position.
            scene.Key.Key.ClearMacros();

            scene.Refresh();

            Assert.Empty(scene.Panel.Steps.Items);
            Assert.Equal(0, assigned);
            Assert.Equal(macroCount - 1, scene.Layout.MacroCount);
        }

        [AvaloniaFact]
        public void Refresh_MovingToAnotherKey_StopsRecording()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.RecordCommand.Execute(null);

            Assert.True(scene.Panel.IsRecording);

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.False(scene.Panel.IsRecording);
        }

        [AvaloniaFact]
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

            Assert.Equal("a", Assert.Single(scene.Panel.Steps.Items).TokenText);

            // Still armed: a take runs until it is stopped, so one press is not the end of a
            // recording — the sequence header's count is what moves as it grows.
            Assert.True(scene.Panel.IsRecording);
            Assert.Equal("1 step", scene.Panel.StepCountText);
        }

        /// <summary>
        /// One sentence per arm, and neither depends on anything but which arm is live (issue #146
        /// took the step numbers out of both).
        /// </summary>
        [AvaloniaFact]
        public void RecordingBanner_FollowsTheArmAndNamesNoStepNumber()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.RecordCommand.Execute(null);

            Assert.Equal(MacroInspectorPanelViewModel.RecordingBannerText, scene.Panel.RecordingBanner);

            scene.Panel.ReceiveKeystroke(Captured("a"));
            scene.Panel.ReceiveKeystroke(Captured("b"));

            // Three keystroke-lengths later it still reads the same: there is no number in it to
            // move, which is the whole of the change.
            Assert.Equal(MacroInspectorPanelViewModel.RecordingBannerText, scene.Panel.RecordingBanner);

            scene.Panel.Deactivate();
            scene.SelectStep(0);
            scene.Panel.RecordStepKeyCommand.Execute(null);

            Assert.Equal(MacroInspectorPanelViewModel.StepCaptureBannerText, scene.Panel.RecordingBanner);
        }

        /// <summary>
        /// The Sequence header's step count — <c>no steps</c> / <c>1 step</c> / <c>5 steps</c>. It is
        /// the step list's own <see cref="MacroInspectorStepsViewModel.CountText"/>, forwarded, and
        /// it has to be <b>announced</b>: nothing else on the panel raises it, and the header would
        /// otherwise keep reading the count the rail opened on.
        /// </summary>
        [AvaloniaFact]
        public void StepCountText_FollowsTheRowsAndIsAnnounced()
        {
            var scene = new Scene(this);
            var announced = 0;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MacroInspectorPanelViewModel.StepCountText))
                {
                    announced++;
                }
            };

            Assert.Equal("no steps", scene.Panel.StepCountText);

            scene.Record("a");

            Assert.Equal("1 step", scene.Panel.StepCountText);

            scene.Record("b", "c");

            Assert.Equal("3 steps", scene.Panel.StepCountText);
            Assert.Equal(scene.Panel.Steps.CountText, scene.Panel.StepCountText);
            Assert.True(announced > 0);

            // The ＋ placeholder is a row the user is looking at, so it counts — a header reading
            // `3 steps` over four visible rows would be counting something else.
            scene.Panel.InsertStepCommand.Execute(null);

            Assert.Equal("4 steps", scene.Panel.StepCountText);
        }

        [AvaloniaFact]
        public void ReceiveKeystroke_WhileNotRecording_WritesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.ReceiveKeystroke(Captured("a"));

            Assert.Empty(scene.Panel.Steps.Items);
        }

        [AvaloniaFact]
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
        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
        public void Meters_ReadTheDevicesOwnBudgets_WithTheSpaceGroupedNumbers()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            var capability = scene.Device.Device.Macros;

            Assert.Equal(capability.MaxTotalKeystrokes, scene.Panel.LayoutKeystrokeMeter.Limit);
            Assert.Equal(capability.Speed!.Maximum, scene.Panel.SpeedMeter.Limit);

            // AdvisoryText.Number's space separator, mockup 1i/2i — never the invariant comma. The
            // RGB's 7200 is the four-digit half of the reading, so the grouping is visible in it.
            Assert.Equal(
                MacroMeterViewModel.Build(scene.Layout.TotalKeystrokes, capability.MaxTotalKeystrokes),
                scene.Panel.LayoutKeystrokeMeter.Caption);
            Assert.Contains(" 200", scene.Panel.LayoutKeystrokeMeter.Caption, StringComparison.Ordinal);
            Assert.DoesNotContain(",", scene.Panel.LayoutKeystrokeMeter.Caption, StringComparison.Ordinal);
        }

        /// <summary>
        /// The speed meter reads <c>5 of 9</c> and the budget beside it reads <c>n / m</c> (issue
        /// #146's mock). It is one type with one separator argument rather than two types: the
        /// arithmetic and the over-budget rule are identical, and only the English differs —
        /// <c>5 / 9</c> would claim the macro has used five ninths of something.
        /// </summary>
        [AvaloniaFact]
        public void TheSpeedMeter_ReadsNOfM_WhileTheBudgetKeepsItsSlash()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Panel.Speed = scene.Panel.SpeedMinimum + 1;

            Assert.Equal(
                $"{scene.Panel.Speed} of {scene.Panel.SpeedMaximum}",
                scene.Panel.SpeedMeter.Caption);
            Assert.Contains(MacroMeterViewModel.OfSeparator, scene.Panel.SpeedMeter.Caption, StringComparison.Ordinal);

            var budget = scene.Panel.LayoutKeystrokeMeter;

            Assert.Contains(MacroMeterViewModel.CaptionSeparator, budget.Caption, StringComparison.Ordinal);
            Assert.DoesNotContain(MacroMeterViewModel.OfSeparator, budget.Caption, StringComparison.Ordinal);
        }

        /// <summary>
        /// The panel keeps <b>two</b> meters (issue #148), and the profile-wide macro count is not
        /// one of them — yet the <em>limit</em> it read is still enforced, which is the half that
        /// had to survive the readout's deletion. <c>MacroLimits.ResolveMaxMacroCount</c> is
        /// firmware-gated (09 §2) and still what refuses the macro that would exceed it.
        /// </summary>
        [AvaloniaFact]
        public void TheDeletedMacroCountMeter_TookNoDeviceLimitWithIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(
                [MacroInspectorPanelViewModel.SpeedMeterLabel, MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel],
                new[] { scene.Panel.SpeedMeter.Label, scene.Panel.LayoutKeystrokeMeter.Label });

            var limit = MacroLimits.ResolveMaxMacroCount(scene.Device)!.Value;

            TestLayouts.FillMacroSlots(scene.Layout, limit);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            Assert.Equal(MacroInspectorPanelViewModel.BuildMacroCountLimitMessage(limit), scene.Panel.Message);
            Assert.False(scene.Key.Key.IsMacro);
        }

        [AvaloniaFact]
        public void Meters_OverBudget_ReportAndNeverRefuse()
        {
            var meter = new MacroMeterViewModel(MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel);

            meter.Set(5140, 7200);
            Assert.False(meter.IsOverBudget);
            Assert.Equal("5 140 / 7 200", meter.Caption);

            meter.Set(7201, 7200);
            Assert.True(meter.IsOverBudget);

            // A null limit is "no limit", never zero — the Advantage2 states no macro count. That
            // holds whichever separator the meter was built with: there is nothing to separate.
            meter.Set(9000, null);
            Assert.False(meter.IsOverBudget);
            Assert.Equal("9 000", meter.Caption);

            var speed = new MacroMeterViewModel(
                MacroInspectorPanelViewModel.SpeedMeterLabel,
                MacroMeterViewModel.OfSeparator);

            speed.Set(9000, null);

            Assert.Equal("9 000", speed.Caption);
        }

        [AvaloniaFact]
        public void Speed_AssignedOnAKeyWithNoMacro_CreatesTheMacroAndWritesIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Speed = scene.Panel.SpeedMaximum;

            var macro = Assert.Single(scene.Key.Key.Macros.OfType<Macro>());

            Assert.Equal(scene.Panel.SpeedMaximum, macro.Speed);
            Assert.Equal(scene.Panel.SpeedMaximum, scene.Panel.SpeedMeter.Value);
        }

        /// <summary>
        /// Repeat is a <c>−</c> / value / <c>+</c> stepper since issue #146, and the two halves write
        /// through the very path the old slider's setter used — so a stepped value still creates the
        /// macro on an empty slot, still reaches <c>Macro.RepeatFrequency</c>, and still dirties the
        /// session through <c>Assigned</c>.
        /// </summary>
        [AvaloniaFact]
        public void TheRepeatStepper_WritesThroughTheSamePathAsTheValue()
        {
            var scene = new Scene(this);
            var assigned = 0;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Panel.Repeat = scene.Panel.RepeatMinimum;

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.Panel.IncreaseRepeatCommand.Execute(null);

            Assert.Equal(scene.Panel.RepeatMinimum + 1, scene.Panel.Repeat);
            Assert.Equal(scene.Panel.Repeat, scene.CurrentMacro!.RepeatFrequency);
            Assert.True(assigned > 0);

            scene.Panel.DecreaseRepeatCommand.Execute(null);

            Assert.Equal(scene.Panel.RepeatMinimum, scene.Panel.Repeat);
            Assert.Equal(scene.Panel.RepeatMinimum, scene.CurrentMacro.RepeatFrequency);
        }

        /// <summary>
        /// Each half goes <b>dead</b> at its own bound rather than clamping silently: a button that
        /// runs and changes nothing reads as broken rather than as at the end of its range. The
        /// bounds are the device's (06 §4), never a literal.
        /// </summary>
        [AvaloniaFact]
        public void TheRepeatStepper_IsDeadAtEachBound()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Panel.Repeat = scene.Panel.RepeatMinimum;

            Assert.False(scene.Panel.DecreaseRepeatCommand.CanExecute(null));
            Assert.True(scene.Panel.IncreaseRepeatCommand.CanExecute(null));

            scene.Panel.DecreaseRepeatCommand.Execute(null);

            Assert.Equal(scene.Panel.RepeatMinimum, scene.Panel.Repeat);

            scene.Panel.Repeat = scene.Panel.RepeatMaximum;

            Assert.False(scene.Panel.IncreaseRepeatCommand.CanExecute(null));
            Assert.True(scene.Panel.DecreaseRepeatCommand.CanExecute(null));

            scene.Panel.IncreaseRepeatCommand.Execute(null);

            Assert.Equal(scene.Panel.RepeatMaximum, scene.Panel.Repeat);
        }

        /// <summary>
        /// <see cref="MacroInspectorPanelViewModel.HasRepeat"/> gates the whole row, so on a board
        /// whose file keeps no repeat token (the Advantage2 — it models a range and writes no
        /// <c>{xN}</c>, 06 §3) the stepper cannot run at all.
        /// </summary>
        [AvaloniaFact]
        public void TheRepeatStepper_IsDeadWhereTheFileKeepsNoRepeat()
        {
            var scene = new Scene(this, DeviceId.Advantage2);

            scene.SelectFirstMacroKey();
            scene.Record("a");

            Assert.False(scene.Panel.HasRepeat);
            Assert.False(scene.Panel.IncreaseRepeatCommand.CanExecute(null));
            Assert.False(scene.Panel.DecreaseRepeatCommand.CanExecute(null));
        }

        // ===== The slot selector (issue #137) =================================================
        // A key holds up to five macros told apart by their co-triggers (06 §1). Until this strip
        // existed the rail could reach exactly one of them.

        /// <summary>
        /// The chips count the slots the <b>dialect writes</b>, never the five the model owns: 3 on
        /// the Advantage2 and Freestyle Edge/Pro, 5 on the RGB family. Read off
        /// <see cref="MacroCapability.PersistedSlotsPerKey"/>, so a device added to the catalog with
        /// a different figure is right by construction.
        /// </summary>
        [AvaloniaTheory]
        [InlineData(DeviceId.Advantage2, 3)]
        [InlineData(DeviceId.FreestyleEdge, 3)]
        [InlineData(DeviceId.FreestyleEdgeRgb, 5)]
        public void SlotOptions_CountThePersistedSlotsOfTheDevice(DeviceId deviceId, int expected)
        {
            var scene = new Scene(this, deviceId: deviceId);

            scene.SelectFirstMacroKey();

            Assert.Equal(expected, DeviceCatalog.GetById(deviceId).Macros.PersistedSlotsPerKey);
            Assert.True(scene.Panel.HasSlotSelector);
            Assert.Equal(expected, scene.Panel.SlotOptions.Count);
            Assert.Equal(
                Enumerable.Range(Macro.MinMacroIndex, expected),
                scene.Panel.SlotOptions.Select(option => option.Slot));

            // Nothing is recorded yet, so every chip reads `+` and the strip opens on slot 1.
            Assert.All(scene.Panel.SlotOptions, option => Assert.False(option.IsOccupied));
            Assert.All(
                scene.Panel.SlotOptions,
                option => Assert.Equal(MacroSlotOption.EmptyChipText, option.ChipText));
            Assert.Equal(Macro.MinMacroIndex, scene.Panel.SelectedSlot!.Slot);

            scene.Record("a");

            Assert.True(scene.Panel.SlotOptions[0].IsOccupied);
            Assert.All(scene.Panel.SlotOptions.Skip(1), option => Assert.False(option.IsOccupied));
        }

        /// <summary>
        /// A chip carries its slot's number when the slot holds a macro and a bare <c>+</c> when it
        /// does not (issue #146), the slot under edit is the one marked selected, and the tooltip
        /// wording survives the dots and the dropdown that used to own it — a numeral alone is not
        /// accessible text.
        /// </summary>
        [AvaloniaFact]
        public void SlotChips_CarryTheirNumberWhenOccupied_APlusWhenNot_AndKeepTheirCaption()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            var chips = scene.Panel.SlotOptions;

            Assert.Equal("1", chips[0].ChipText);
            Assert.Equal("+", chips[1].ChipText);
            Assert.Equal("Slot 1 — in use", chips[0].Caption);
            Assert.Equal("Slot 2 — empty", chips[1].Caption);

            // Exactly one chip is selected, and it is the slot the panel is editing.
            Assert.Same(chips[0], Assert.Single(chips, chip => chip.IsSelected));
            Assert.Equal(scene.Panel.SelectedSlotNumber, chips[0].Slot);

            // Nothing collides yet, so no chip is ringed.
            Assert.All(chips, chip => Assert.False(chip.IsColliding));

            scene.Panel.SelectSlotCommand.Execute(chips[2]);

            Assert.Equal(3, scene.Panel.SelectedSlotNumber);
            Assert.Same(
                scene.Panel.SlotOptions[2],
                Assert.Single(scene.Panel.SlotOptions, chip => chip.IsSelected));
        }

        /// <summary>
        /// <b>The single easiest defect here.</b> <c>ActiveMacroIndex</c> is an in-memory field that
        /// is never serialized (05 §1.3), so a slot pick cannot reach the editor's funnel — which is
        /// the only route to the dirty flag, and would raise an unsaved-changes prompt for a choice
        /// no save could persist. (The Macros tab's <c>Make active</c> already got this right; issue
        /// #140 deleted that surface, so this panel is the only one left that has to.)
        /// </summary>
        [AvaloniaFact]
        public void SelectingASlot_MovesTheActiveSlotAndReReads_WithoutReachingTheEditorsFunnel()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // A second macro, put straight into slot 2 the way an import or the tab would.
            scene.Key.Key.SetMacro(2, scene.Layout.CreateMacro());
            scene.Key.Key.GetMacro(2)!.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b")));

            scene.Refresh();

            var assigned = 0;

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];

            Assert.Equal(2, scene.Key.Key.ActiveMacroIndex);
            Assert.Equal(2, scene.Panel.SelectedSlot!.Slot);
            Assert.Equal(["b"], scene.Panel.Steps.Items.Select(step => step.TokenText));

            // `Assigned` is the panel's ONE hop to RefreshCounters(), and so to IsDirty.
            Assert.Equal(0, assigned);

            // ...and the chip runs the same path, so neither control can dirty the session while the
            // other does not.
            scene.Panel.SelectSlotCommand.Execute(scene.Panel.SlotOptions[0]);

            Assert.Equal(1, scene.Key.Key.ActiveMacroIndex);
            Assert.Equal(["a"], scene.Panel.Steps.Items.Select(step => step.TokenText));
            Assert.Equal(0, assigned);
        }

        /// <summary>
        /// Picking an empty slot yields an empty sequence with recording live — and creates
        /// <b>nothing</b> until a keystroke lands, because <c>EnsureMacro()</c> stays the only path
        /// that adds a macro. The one it then creates fills <b>the selected slot</b>, not the first
        /// free one.
        /// </summary>
        [AvaloniaFact]
        public void SelectingAnEmptySlot_RecordsIntoThatSlot_AndCreatesNothingBeforeTheFirstKeystroke()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            Assert.Equal(1, scene.Layout.MacroCount);

            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[2];

            // The panel is pointed at slot 3: empty sequence, nothing created, recording available.
            Assert.Equal(3, scene.Key.Key.ActiveMacroIndex);
            Assert.Empty(scene.Panel.Steps.Items);
            Assert.Equal(1, scene.Layout.MacroCount);
            Assert.Null(scene.Key.Key.GetMacro(3));
            Assert.True(scene.Panel.RecordCommand.CanExecute(null));

            scene.Record("b");

            Assert.Equal(2, scene.Layout.MacroCount);
            Assert.NotNull(scene.Key.Key.GetMacro(3));
            Assert.Null(scene.Key.Key.GetMacro(2));
            Assert.Equal(["[a]"], scene.Key.Key.GetMacro(1)!.Keystrokes.Select(k => "[" + k.Key.GetToken(TokenDialect.Gen1) + "]"));
            Assert.Equal(3, scene.Key.Key.GetMacro(3)!.MacroIndex);
        }

        /// <summary>
        /// The slot the panel is editing, as the <b>editor</b> reads it when it arms a macro copy —
        /// a bare number, because a caller outside this panel has no business unwrapping a dropdown
        /// row, and would otherwise have to spell the flat-list fallback a second time.
        /// </summary>
        [AvaloniaFact]
        public void SelectedSlotNumber_IsTheSlotUnderEdit_AndZeroWhereThereAreNoSlots()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            Assert.Equal(1, scene.Panel.SelectedSlotNumber);

            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[2];

            Assert.Equal(3, scene.Panel.SelectedSlotNumber);

            var flat = new Scene(this, deviceId: DeviceId.Advantage360);

            flat.SelectFirstMacroKey();

            Assert.False(flat.Panel.HasSlotSelector);
            Assert.Equal(MacroSites.FlatListSlot, flat.Panel.SelectedSlotNumber);
        }

        /// <summary>
        /// The choice belongs to the position it was made on: moving the rail must open the next key
        /// on whatever slot <em>it</em> carries, not on the number picked for the last one.
        /// </summary>
        [AvaloniaFact]
        public void TheSlotChoice_DoesNotFollowTheRailToAnotherPosition()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[2];

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Key.Key.SetMacro(2, scene.Layout.CreateMacro());

            scene.Refresh();

            // Slot 2 is the only populated one, so that is what the panel opens on.
            Assert.Equal(2, scene.Panel.SelectedSlot!.Slot);
        }

        /// <summary>
        /// Absent, never disabled (docs/design/README.md): a flat-list board keeps its macros in one
        /// per-layout list (06 §1) and has no slots at all, and a position that refuses macros
        /// (05 §5.3) draws the panel's refusal instead of a strip.
        /// </summary>
        [AvaloniaFact]
        public void NoSlotSelector_OnAFlatListBoardOrOnAPositionThatRefusesMacros()
        {
            var flat = new Scene(this, deviceId: DeviceId.Advantage360);

            flat.SelectFirstMacroKey();

            Assert.True(flat.Device.Device.Macros.UsesFlatMacroList);
            Assert.True(flat.Panel.IsAvailable);
            Assert.False(flat.Panel.HasSlotSelector);
            Assert.Empty(flat.Panel.SlotOptions);

            var restricted = new Scene(this);

            restricted.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(restricted.Panel.IsAvailable);
            Assert.False(restricted.Panel.HasSlotSelector);
            Assert.Empty(restricted.Panel.SlotOptions);
        }

        /// <summary>
        /// A macro is never put in a slot the dialect does not write (06 §1), which is why the panel
        /// resolves the slot itself instead of calling <see cref="KeyboardKey.AssignMacro"/> — that
        /// one takes the first free slot of the five the <em>model</em> owns. A key parked on slot 4
        /// by a tolerant load records into slot 1 on a Freestyle Edge, not into a slot the very next
        /// save would drop.
        /// </summary>
        [AvaloniaFact]
        public void ANewMacro_NeverLandsInASlotTheDialectDoesNotWrite()
        {
            var scene = new Scene(this, deviceId: DeviceId.FreestyleEdge);

            scene.SelectFirstMacroKey();

            scene.Key.Key.ActiveMacroIndex = 4;

            scene.Refresh();
            scene.Record("a");

            Assert.Equal(3, scene.Panel.SlotOptions.Count);
            Assert.NotNull(scene.Key.Key.GetMacro(1));
            Assert.Null(scene.Key.Key.GetMacro(4));
            Assert.Null(scene.Key.Key.GetMacro(5));
        }

        // ===== The Trigger strip (issue #137) =================================================

        [AvaloniaFact]
        public void TheStrip_OffersTheThreeLeftHandLatches()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(
                [MacroModifierMarks.ShiftMark, MacroModifierMarks.ControlMark, MacroModifierMarks.AltMark],
                scene.Panel.CoTriggers.Select(latch => latch.Symbol));
            Assert.All(scene.Panel.CoTriggers, latch => Assert.False(latch.HasSide));

            // No ⌘ latch: no co-trigger in specs 06 or 10 names one.
            Assert.DoesNotContain(scene.Panel.CoTriggers, latch => latch.Symbol == MacroModifierMarks.WinMark);
        }

        /// <summary>
        /// <b>The latches follow the selected slot</b> (issue #146). They are a fact about the macro
        /// in the slot under edit, not about the key, and since the two strips now share one row a
        /// latch left lit from the previous slot would claim a co-trigger the macro on screen does
        /// not carry. The coupling is <c>SelectSlot → ReadFromModel → RefreshTrigger</c>; it existed
        /// before this issue and had no test, which is exactly how it would have been refactored
        /// away.
        /// </summary>
        [AvaloniaFact]
        public void TheLatches_FollowTheSelectedSlot()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // Slot 1 is co-triggered with ⌃...
            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[1]);

            Assert.False(scene.Panel.CoTriggers[0].IsOn);
            Assert.True(scene.Panel.CoTriggers[1].IsOn);

            // ...and slot 2 with ⇧.
            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];
            scene.Record("b");
            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.True(scene.Panel.CoTriggers[0].IsOn);
            Assert.False(scene.Panel.CoTriggers[1].IsOn);

            // Switching back relights the other one, and switching forward again undoes it.
            scene.Panel.SelectSlotCommand.Execute(scene.Panel.SlotOptions[0]);

            Assert.False(scene.Panel.CoTriggers[0].IsOn);
            Assert.True(scene.Panel.CoTriggers[1].IsOn);

            scene.Panel.SelectSlotCommand.Execute(scene.Panel.SlotOptions[1]);

            Assert.True(scene.Panel.CoTriggers[0].IsOn);
            Assert.False(scene.Panel.CoTriggers[1].IsOn);
        }

        [AvaloniaFact]
        public void ALatch_WritesTheMacrosCoTriggers()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            var macro = Assert.Single(scene.Key.Key.Macros.OfType<Macro>());

            Assert.Equal(1, macro.CoTriggerCount);
            Assert.Equal(MacroModifierCodes.GetKeyCode(MacroModifiers.LeftShift), macro.CoTriggers[0].Code);
            Assert.True(scene.Panel.CoTriggers[0].IsOn);

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(0, macro.CoTriggerCount);
            Assert.False(scene.Panel.CoTriggers[0].IsOn);
        }

        /// <summary>
        /// The cap is the dialect's own <see cref="MacroCapability.PersistedCoTriggersPerMacro"/> —
        /// 1 on the old Freestyle, 3 on the Advantage2, 4 on the RGB family — and never a literal.
        /// <see cref="Macro.AddCoTrigger"/> deliberately neither de-duplicates nor refuses, so
        /// holding it is the panel's job.
        /// </summary>
        [AvaloniaTheory]
        [InlineData(DeviceId.FreestyleEdge, 1)]
        [InlineData(DeviceId.Advantage2, 3)]
        [InlineData(DeviceId.FreestyleEdgeRgb, 4)]
        public void TheCoTriggerCap_IsTheDialectsOwn(DeviceId deviceId, int expected)
        {
            var scene = new Scene(this, deviceId: deviceId);

            scene.SelectFirstMacroKey();
            scene.Record("a");

            Assert.Equal(expected, scene.Panel.MaxCoTriggers);

            var macro = scene.CurrentMacro!;
            var accepted = 0;

            foreach (var latch in scene.Panel.CoTriggers)
            {
                scene.Panel.ToggleCoTriggerCommand.Execute(latch);

                if (latch.IsOn)
                {
                    accepted++;
                }
            }

            // The strip offers three latches, so a cap of 4 is never reached from it; 1 and 3 are.
            Assert.Equal(Math.Min(expected, scene.Panel.CoTriggers.Count), accepted);
            Assert.Equal(accepted, macro.CoTriggerCount);

            if (accepted < scene.Panel.CoTriggers.Count)
            {
                Assert.Equal(MacroInspectorPanelViewModel.BuildCoTriggerLimitMessage(expected), scene.Panel.Message);
            }
        }

        /// <summary>
        /// <b>Preserve on load, normalize on edit.</b> Opening a key must not rewrite a co-trigger
        /// the user only looked at — that would dirty the profile for a read — so a file's
        /// right-hand or generic spelling survives untouched and lights the matching <em>left</em>
        /// latch. Touching any latch is the one moment the panel rewrites the set.
        /// </summary>
        [AvaloniaFact]
        public void AFilesRightHandCoTrigger_SurvivesLoad_LightsTheLeftLatch_AndNormalizesOnTheFirstTouch()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            var macro = scene.CurrentMacro!;
            var rightShift = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(MacroModifiers.RightShift))!;
            var genericAlt = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(MacroModifiers.Alt))!;

            macro.AddCoTrigger(rightShift);
            macro.AddCoTrigger(genericAlt);

            var assigned = 0;

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.Refresh();

            // Lit, so the user can see the co-trigger exists...
            Assert.True(scene.Panel.CoTriggers[0].IsOn);
            Assert.False(scene.Panel.CoTriggers[1].IsOn);
            Assert.True(scene.Panel.CoTriggers[2].IsOn);

            // ...and untouched, and not dirty: a load is a read.
            Assert.Equal([rightShift.Code, genericAlt.Code], macro.CoTriggers.Select(key => key.Code));
            Assert.Equal(0, assigned);

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[1]);

            // One touch rewrites the whole set to the left-hand spellings of the lit latches.
            Assert.Equal(
                [
                    MacroModifierCodes.GetKeyCode(MacroModifiers.LeftShift),
                    MacroModifierCodes.GetKeyCode(MacroModifiers.LeftControl),
                    MacroModifierCodes.GetKeyCode(MacroModifiers.LeftAlt)
                ],
                macro.CoTriggers.Select(key => key.Code));
            Assert.True(assigned > 0);
        }

        /// <summary>
        /// <b>The status is the advisory or nothing</b> (issue #146). It used to read
        /// <c>bare press · no collision</c> at rest — a line under every macro on every key saying
        /// that nothing was wrong — and the mock draws no line there at all.
        /// <see cref="MacroInspectorPanelViewModel.IsTriggerAdvisory"/> is now exactly "there is
        /// something to say", which is what the view binds <c>IsVisible</c> to.
        /// </summary>
        [AvaloniaFact]
        public void TriggerStatus_SaysNothingAtRest_WhateverTheCoTriggersAre()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            Assert.Equal(string.Empty, scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(string.Empty, scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);
        }

        /// <summary>
        /// 06 §5's duplicate-trigger rule, named on the slot it collides with — which is the whole
        /// reason the slot selector can drop the tab's <c>ACTIVE</c> badge: every populated slot is
        /// live, and what tells them apart is this. <b>Both sides of the clash are ringed</b>: two
        /// macros that cannot be told apart are equally at fault, and marking one of them would name
        /// a culprit where there is only a pair.
        /// </summary>
        [AvaloniaFact]
        public void ACollision_NamesTheSlotInTheAdvisory_AndRingsEverySlotInvolved()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // A second macro on the same key with the same (empty) co-trigger set — 06 §5 says two
            // macros with no co-triggers at all collide.
            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];
            scene.Record("b");

            Assert.Equal(MacroInspectorPanelViewModel.BuildCollisionStatus(1), scene.Panel.TriggerStatus);
            Assert.Equal("collides with slot 1", scene.Panel.TriggerStatus);
            Assert.True(scene.Panel.IsTriggerAdvisory);

            Assert.True(scene.Panel.SlotOptions[0].IsColliding);
            Assert.True(scene.Panel.SlotOptions[1].IsColliding);
            Assert.All(scene.Panel.SlotOptions.Skip(2), chip => Assert.False(chip.IsColliding));

            // The ring is a fact about the key, not about the selection: it stays on both chips from
            // whichever slot the panel is looking at them.
            scene.Panel.SelectSlotCommand.Execute(scene.Panel.SlotOptions[0]);

            Assert.Equal(MacroInspectorPanelViewModel.BuildCollisionStatus(2), scene.Panel.TriggerStatus);
            Assert.True(scene.Panel.SlotOptions[0].IsColliding);
            Assert.True(scene.Panel.SlotOptions[1].IsColliding);

            // Give one of them a co-trigger and the pair stops colliding — line and rings together.
            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(string.Empty, scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);
            Assert.All(scene.Panel.SlotOptions, chip => Assert.False(chip.IsColliding));
        }

        /// <summary>
        /// 06 §5's reserved triggers: <c>fn1s</c> and <c>keyt</c> need at least one co-trigger, so a
        /// bare macro on either can never fire. Gen2 only, exactly as <c>KeyboardLayoutValidator</c>
        /// gates it — the strip and the validator must not disagree about what the firmware refuses.
        /// </summary>
        [AvaloniaTheory]
        [InlineData(KeyboardKey.Fn1ShiftKeyCode)]
        [InlineData(KeyboardKey.KeypadToggleKeyCode)]
        public void AReservedTrigger_WithNoCoTrigger_IsAnAmberAdvisory(int triggerCode)
        {
            var scene = new Scene(this, deviceId: DeviceId.Advantage360);

            scene.SelectByTriggerCode(triggerCode);
            scene.Record("a");

            Assert.Equal(MacroInspectorPanelViewModel.ReservedTriggerStatus, scene.Panel.TriggerStatus);
            Assert.True(scene.Panel.IsTriggerAdvisory);

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(string.Empty, scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);
        }

        /// <summary>
        /// An ordinary trigger on the very same Gen2 board raises nothing, or the advisory above
        /// would pass for the wrong reason.
        /// </summary>
        [AvaloniaFact]
        public void AnOrdinaryGen2Trigger_RaisesNoReservedAdvisory()
        {
            var scene = new Scene(this, deviceId: DeviceId.Advantage360);

            scene.SelectFirstMacroKey();
            scene.Record("a");

            Assert.False(scene.Panel.IsTriggerAdvisory);
            Assert.Equal(string.Empty, scene.Panel.TriggerStatus);
        }

        /// <summary>
        /// <b>Neither advisory blocks anything</b> (docs/app/keyboard-editor.md, invariant 20): the
        /// layout carrying a collision still validates into a <em>report</em>, and every command on
        /// the panel is still runnable while the amber is on screen.
        /// </summary>
        [AvaloniaFact]
        public void NeitherAdvisory_RefusesAnything()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];
            scene.Record("b");

            Assert.True(scene.Panel.IsTriggerAdvisory);

            Assert.True(scene.Panel.RecordCommand.CanExecute(null));
            Assert.True(scene.Panel.InsertStepCommand.CanExecute(null));
            Assert.True(scene.Panel.ToggleCoTriggerCommand.CanExecute(scene.Panel.CoTriggers[0]));

            // Reported, never refused — the same rule every limit in this model follows.
            Assert.Contains(
                scene.Layout.Validate(),
                violation => violation.Kind == ModelViolationKind.MacroTriggerCollision);
        }

        // ===== The copy and the delete (issue #141, minus the name field with #146) ============
        // The dropdown over the profile's macro library went with the library (#141): there is no
        // shared macro on the drive, so a list of "the macros this profile has" was an identity the
        // hardware does not carry. The inline field that replaced it went with the designer's mock,
        // which draws none — so a macro is identified by the place it fires from, and the two
        // actions below are all that is left of the section.

        /// <summary>
        /// <b>Nothing on this panel names a macro</b> (issue #146), and it must therefore never
        /// write <c>Macro.Name</c> either. The harvest on save takes every non-empty name, so a
        /// panel that quietly stamped a derived one onto the model would put a
        /// <c>macro_name_*</c> line in <c>app_settings.txt</c> for a name nobody typed — which is
        /// exactly the trap the watermark existed to avoid, still live now that the field is gone.
        /// </summary>
        [AvaloniaFact]
        public void ThePanel_NeverWritesAMacroName()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            Assert.Equal(string.Empty, scene.CurrentMacro!.Name);
            Assert.Empty(MacroSites.EnumerateStoredNames(scene.Layout));

            // Every other control on the panel, over the same macro.
            scene.SelectStep(0);
            scene.TickChordModifier(MacroModifiers.LeftControl);
            scene.Panel.StepDelayMilliseconds = 80;
            scene.Panel.Speed = scene.Panel.SpeedMaximum;
            scene.Panel.IncreaseRepeatCommand.Execute(null);
            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);
            scene.Panel.SelectSlotCommand.Execute(scene.Panel.SlotOptions[1]);

            Assert.Equal(string.Empty, scene.Key.Key.GetMacro(1)!.Name);
            Assert.Empty(MacroSites.EnumerateStoredNames(scene.Layout));

            // ...and a name a load put there is left exactly as it was found.
            scene.Key.Key.GetMacro(1)!.Name = "Sign-off block";

            scene.Refresh();

            Assert.Equal("Sign-off block", scene.Key.Key.GetMacro(1)!.Name);
        }

        /// <summary>
        /// <c>Delete</c> empties <b>this slot</b>. The other slots of the key are untouched, and so
        /// is another key carrying a macro that merely looks the same — there is no shared macro to
        /// reach (06 §1).
        /// </summary>
        [AvaloniaFact]
        public void DeleteMacro_EmptiesThisSlotAlone()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];
            scene.Record("b");

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);
            scene.Record("a");

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];

            var writes = 0;

            scene.Panel.Assigned += (_, _) => writes++;

            Assert.True(scene.Panel.DeleteMacroCommand.CanExecute(null));

            scene.Panel.DeleteMacroCommand.Execute(null);

            Assert.Null(scene.Key.Key.GetMacro(2));
            Assert.NotNull(scene.Key.Key.GetMacro(1));
            Assert.Equal(2, scene.Layout.MacroCount);

            // ...and the panel is still on the slot it emptied rather than having jumped to the
            // key's other macro, which is what "open the first populated slot" would otherwise do
            // to a delete.
            Assert.Equal(2, scene.Panel.SelectedSlotNumber);
            Assert.Empty(scene.Panel.Steps.Items);
            Assert.False(scene.Panel.HasMacro);

            // A delete IS a layout write, unlike a rename: the counters, the cap and the dirty flag
            // all follow it.
            Assert.Equal(1, writes);

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.NotNull(scene.CurrentMacro);
        }

        /// <summary>
        /// On a flat-list board (06 §1) the same command takes the macro out of the per-layout list,
        /// which is the only store that board has.
        /// </summary>
        [AvaloniaFact]
        public void DeleteMacro_OnAFlatListBoard_TakesItOutOfTheLayoutsList()
        {
            var scene = new Scene(this, deviceId: DeviceId.Advantage360);

            scene.SelectFirstMacroKey();
            scene.Record("a");

            Assert.Equal(1, scene.Layout.MacroCount);

            scene.Panel.DeleteMacroCommand.Execute(null);

            Assert.Equal(0, scene.Layout.MacroCount);
            Assert.False(scene.Panel.HasMacro);
        }

        /// <summary>Nothing to delete on an empty slot, so the command is dead rather than silent.</summary>
        [AvaloniaFact]
        public void DeleteMacro_WithNoMacro_IsDisabled()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Panel.DeleteMacroCommand.CanExecute(null));

            scene.Record("a");

            Assert.True(scene.Panel.DeleteMacroCommand.CanExecute(null));
        }

        /// <summary>
        /// The two copy commands are the <b>editor's</b>, handed over rather than re-implemented —
        /// one armed state, one Escape route, one disarm set — and the armed flag is read back off
        /// the cancel command's own <c>CanExecute</c> so the panel cannot drift from it.
        /// </summary>
        [AvaloniaFact]
        public void TheCopyPair_IsTheEditorsOwn_AndIsCopyArmedFollowsIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Same(scene.CopyMacro, scene.Panel.CopyMacroCommand);
            Assert.Same(scene.CancelCopy, scene.Panel.CancelCopyCommand);
            Assert.False(scene.Panel.IsCopyArmed);

            var announced = 0;

            scene.Panel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MacroInspectorPanelViewModel.IsCopyArmed))
                {
                    announced++;
                }
            };

            scene.ArmCopy(true);

            Assert.True(scene.Panel.IsCopyArmed);
            Assert.Equal(1, announced);

            scene.ArmCopy(false);

            Assert.False(scene.Panel.IsCopyArmed);
            Assert.Equal(2, announced);
        }

        // ===== Revert (issue #122, AC 1) =====================================================
        // The rail's `Revert key` used to run the editor's ClearRemap(), which touches only the
        // remap — so on this panel it did nothing at all, and nothing anywhere kept a "before".

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
        public void TryRevert_WithNothingSelected_RefusesSoTheFooterFallsThroughToTheEditor()
        {
            var panel = Create();

            Assert.False(panel.TryRevert());

            panel.Refresh(null, null, null, EditorAdvisories.Empty);

            Assert.False(panel.TryRevert());
        }

        [AvaloniaFact]
        public void TryRevert_OnAPositionThatCannotCarryAMacro_Refuses()
        {
            // A modifier position (05 §5.3). There is no macro state to put back, so the footer must
            // fall through to the editor's own reset rather than claim the action.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(scene.Panel.IsAvailable);
            Assert.False(scene.Panel.TryRevert());
        }

        [AvaloniaFact]
        public void IsRecordingControl_NamesTheThreeButtonsThatArmCapture_AndNothingElse()
        {
            // What the editor's pointer stand-down asks before it ends a recording: the press that
            // lands on Record/Stop must not be the press that stops it. Since issue #139 the
            // composer's own Record is the third — an unclaimed one would be stood down by the very
            // click that armed it, and the composer would never capture anything.
            var panel = Create();

            Assert.True(panel.IsRecordingControl(panel.RecordCommand));
            Assert.True(panel.IsRecordingControl(panel.InsertStepCommand));
            Assert.True(panel.IsRecordingControl(panel.RecordStepKeyCommand));
            Assert.False(panel.IsRecordingControl(panel.ToggleCoTriggerCommand));
            Assert.False(panel.IsRecordingControl(panel.ToggleChordModifierCommand));
            Assert.False(panel.IsRecordingControl(null));
        }

        // ===== The composer (issue #139) ======================================================
        // One always-present block that edits THE SELECTED STEP: its key, its held modifiers, its
        // direction and the delay behind it. It replaced issue #128's append-a-chord strip and the
        // standalone per-row delay editor — three ways to change a macro, none of which could change
        // a step that already existed.

        [AvaloniaFact]
        public void TheComposer_WithNothingSelected_IsDeadExceptTheTwoWaysToMakeASelection()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // Recording appends; it does not select. So the panel is sitting on a macro with one
            // step and no selection — the state the whole composer is disabled in.
            Assert.Null(scene.Panel.Steps.SelectedStep);
            Assert.False(scene.Panel.IsComposerEnabled);
            Assert.False(scene.Panel.IsStepKeyEnabled);
            Assert.False(scene.Panel.AreStepModifiersEnabled);
            Assert.False(scene.Panel.IsStepDelayEnabled);
            Assert.False(scene.Panel.RecordStepKeyCommand.CanExecute(null));
            Assert.All(scene.Panel.ChordModifiers, latch => Assert.False(latch.IsEnabled));
            Assert.All(scene.Panel.StepDirections, segment => Assert.False(segment.IsEnabled));
            Assert.All(scene.Panel.StepDelayOptions, option => Assert.False(option.IsEnabled));

            // ...except these two, which are how a selection comes to exist at all.
            Assert.True(scene.Panel.RecordCommand.CanExecute(null));
            Assert.True(scene.Panel.InsertStepCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void TheComposer_OffersTheFourLeftHandModifiers_AndNoOthers()
        {
            // Authoring is left-only, exactly as the Trigger strip's three latches are (#137). A
            // file's right-hand or generic spelling is still read and still drawn by the step row;
            // what is refused is AUTHORING one that says less than capture already knows.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            Assert.Equal(
                [
                    MacroModifiers.LeftShift,
                    MacroModifiers.LeftControl,
                    MacroModifiers.LeftAlt,
                    MacroModifiers.LeftWin
                ],
                scene.Panel.ChordModifiers.Select(latch => latch.Modifier));

            // Left is the unmarked side, so no latch here draws an `R`.
            Assert.All(scene.Panel.ChordModifiers, latch => Assert.False(latch.HasSide));
        }

        [AvaloniaFact]
        public void SelectingAStep_SeedsTheComposerFromIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(1);

            Assert.True(scene.Panel.IsComposerEnabled);
            Assert.True(scene.Panel.HasStepKey);
            Assert.Equal("b", scene.Panel.StepTokenText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, scene.SelectedDirection().Caption);

            // A step with no delay lights NEITHER segment (issue #148) and shows an empty field.
            Assert.Null(scene.SelectedDelayMode());
            Assert.Equal(string.Empty, scene.Panel.StepDelayText);
        }

        [AvaloniaFact]
        public void AModifierLatch_WritesTheStepImmediately_AndDirtiesTheSession()
        {
            // There is no Apply here, deliberately: every control writes through the panel's own
            // OnMacroWritten hop, so the session goes dirty exactly as a recorded step does.
            var scene = new Scene(this);
            var assigned = 0;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.TickChordModifier(MacroModifiers.LeftControl);

            Assert.Equal(MacroModifiers.LeftControl, scene.CurrentMacro!.Keystrokes[0].Modifiers);
            Assert.Equal(MacroInspectorStepViewModel.HeldAction, scene.Panel.Steps.Items[0].ActionText);
            Assert.True(scene.FindChordModifier(MacroModifiers.LeftControl).IsOn);
            Assert.True(assigned > 0);

            scene.TickChordModifier(MacroModifiers.LeftControl);

            Assert.Equal(MacroModifiers.None, scene.CurrentMacro.Keystrokes[0].Modifiers);
        }

        [AvaloniaFact]
        public void TheChordGuard_ClearsTheDirectionBothInTheModelAndInTheControl()
        {
            // 05 §5.8: a modified keystroke's direction is discarded on the way to the file, so a
            // chord cannot also be a press. Ticking a modifier therefore WRITES UpDown = None and
            // both segments go dead — and unticking the last one must NOT bring the old direction
            // back, which is exactly what merely masking it would do.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.SetDirection(KeyDirection.Down);

            Assert.Equal(KeyDirection.Down, scene.CurrentMacro!.Keystrokes[0].UpDown);
            Assert.Equal(MacroInspectorStepViewModel.PressAction, scene.Panel.Steps.Items[0].ActionText);

            scene.TickChordModifier(MacroModifiers.LeftShift);

            Assert.Equal(KeyDirection.None, scene.CurrentMacro.Keystrokes[0].UpDown);
            Assert.False(scene.FindDirection(KeyDirection.Down).IsEnabled);
            Assert.False(scene.FindDirection(KeyDirection.Up).IsEnabled);

            // `tap` stays live: the step IS a tap now, and saying so is not a lie.
            Assert.True(scene.FindDirection(KeyDirection.None).IsEnabled);

            scene.TickChordModifier(MacroModifiers.LeftShift);

            Assert.Equal(KeyDirection.None, scene.CurrentMacro.Keystrokes[0].UpDown);
            Assert.True(scene.FindDirection(KeyDirection.Down).IsEnabled);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, scene.Panel.Steps.Items[0].ActionText);
        }

        [AvaloniaFact]
        public void OnAStepWhoseKeyIsAModifier_TheDirectionStaysLiveAndTheLatchesGoDead()
        {
            // The exception 05 §5.8 makes, and its consequence: Keystroke drops any modifier
            // assigned to a modifier key, so a live latch there would be a control that visibly
            // does nothing.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("lshft");
            scene.SelectStep(0);

            Assert.All(scene.Panel.ChordModifiers, latch => Assert.False(latch.IsEnabled));
            Assert.False(scene.Panel.AreStepModifiersEnabled);
            Assert.True(scene.FindDirection(KeyDirection.Down).IsEnabled);
            Assert.True(scene.FindDirection(KeyDirection.Up).IsEnabled);

            scene.SetDirection(KeyDirection.Up);

            Assert.Equal(KeyDirection.Up, scene.CurrentMacro!.Keystrokes[0].UpDown);
            Assert.Equal(MacroInspectorStepViewModel.ReleaseAction, scene.Panel.Steps.Items[0].ActionText);
        }

        [AvaloniaTheory]
        [InlineData(MacroModifiers.RightShift, MacroModifiers.LeftShift)]
        [InlineData(MacroModifiers.RightControl, MacroModifiers.LeftControl)]
        [InlineData(MacroModifiers.Shift, MacroModifiers.LeftShift)]
        [InlineData(MacroModifiers.Control, MacroModifiers.LeftControl)]
        public void SelectingAStep_NormalizesItsModifiersToTheLeftHandSpelling(
            MacroModifiers stored,
            MacroModifiers expected)
        {
            // A DELIBERATE write from a click, and the one difference from the Trigger strip beside
            // it (which is preserve-on-load / normalize-on-EDIT). The composer offers four left-hand
            // latches, so a right-hand or generic set cannot be shown honestly by them: lighting ⇧
            // for `RS` would make the next tick silently rewrite the other side.
            var scene = new Scene(this);
            var assigned = 0;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.CurrentMacro!.Keystrokes[0].Modifiers = stored;
            scene.Panel.Steps.RefreshFromModel();

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.SelectStep(0);

            Assert.Equal(expected, scene.CurrentMacro.Keystrokes[0].Modifiers);
            Assert.True(scene.FindChordModifier(expected).IsOn);

            // ...and it dirties the profile, which is the price of showing the truth.
            Assert.True(assigned > 0);
        }

        [AvaloniaFact]
        public void SelectingAnAlreadyLeftHandedStep_WritesNothing()
        {
            var scene = new Scene(this);
            var assigned = 0;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.CurrentMacro!.Keystrokes[0].Modifiers = MacroModifiers.LeftAlt;
            scene.Panel.Steps.RefreshFromModel();

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.SelectStep(0);

            Assert.Equal(0, assigned);
        }

        [AvaloniaFact]
        public void ADelayOnlyRow_EnablesTheDelaySectionAndNothingElse()
        {
            // 06 §2.2 lets a macro open with a delay; the row stays because dropping it would edit
            // the file behind the user's back. It has no key, so it has nothing else to edit.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.CurrentMacro!.ClearKeystrokes();
            scene.CurrentMacro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));
            scene.Panel.Steps.RefreshFromModel();

            scene.SelectStep(0);

            Assert.True(scene.Panel.Steps.Items[0].IsDelayOnly);
            Assert.True(scene.Panel.IsComposerEnabled);
            Assert.True(scene.Panel.IsStepDelayEnabled);
            Assert.Equal(MacroStepDelayMode.Random, scene.SelectedDelayMode());

            Assert.False(scene.Panel.IsStepKeyEnabled);
            Assert.False(scene.Panel.AreStepModifiersEnabled);
            Assert.False(scene.Panel.HasStepKey);
            Assert.All(scene.Panel.StepDirections, segment => Assert.False(segment.IsEnabled));
        }

        [AvaloniaFact]
        public void TheDelaySection_WritesImmediatelyAndHasNoApply()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.StepDelayMilliseconds = 120;

            Assert.Equal(["a", "d120"], scene.MacroTokens());
            Assert.Equal(MacroStepDelayMode.Fixed, scene.SelectedDelayMode());
            Assert.Equal(string.Empty, scene.Panel.StepDelayError);

            scene.SetDelayMode(MacroStepDelayMode.Random);

            Assert.Equal(["a", MacroDelayTokens.RandomToken], scene.MacroTokens());

            // ...and off again by EMPTYING THE FIELD, which is what the deleted `none` segment did
            // (issue #148). The step has to be carrying a fixed delay for the box to have something
            // to empty, so the fixture is walked back through one — clearing a field that was
            // already blank would prove nothing about the write.
            scene.TypeDelay("120");

            Assert.Equal(["a", "d120"], scene.MacroTokens());

            scene.TypeDelay(string.Empty);

            Assert.Equal(["a"], scene.MacroTokens());
            Assert.Null(scene.SelectedDelayMode());
            Assert.Equal(string.Empty, scene.Panel.StepDelayError);
        }

        /// <summary>
        /// The strip is the two segments the designer's mock draws, and <c>none</c> is not among
        /// them (issue #148) — while <see cref="MacroStepDelayMode.None"/> survives as a state of the
        /// step, which is what "lights neither segment" means.
        /// </summary>
        [AvaloniaFact]
        public void TheDelayStrip_IsFixedAndRandomOnly_AndNoneIsAStateRatherThanASegment()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            Assert.Equal(
                [MacroStepDelayMode.Fixed, MacroStepDelayMode.Random],
                MacroInspectorPanelViewModel.StepDelayModes);
            Assert.Equal(
                [MacroStepDelayMode.Fixed, MacroStepDelayMode.Random],
                scene.Panel.StepDelayOptions.Select(option => option.Mode));
            Assert.Equal(
                [MacroInspectorPanelViewModel.FixedDelayCaption, MacroInspectorStepViewModel.RandomDelayText],
                scene.Panel.StepDelayOptions.Select(option => option.Caption));

            // The enum member is still real, and a segment for it is refused outright rather than
            // given a caption nobody can press.
            Assert.Equal(0, (int)MacroStepDelayMode.None);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MacroStepDelayOption(MacroStepDelayMode.None, isOn: true, isEnabled: true));
        }

        /// <summary>
        /// Emptying the millisecond field is the write that took the <c>none</c> segment's place
        /// (issue #148) — including on a <b>delay-only</b> row (06 §2.2), where taking the delay off
        /// drops the row, because a row that was nothing but a delay has nothing left.
        /// </summary>
        [AvaloniaFact]
        public void ClearingTheField_OnADelayOnlyRow_DropsTheRow()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.CurrentMacro!.ClearKeystrokes();
            scene.CurrentMacro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));
            scene.Panel.Steps.RefreshFromModel();

            scene.SelectStep(0);

            Assert.True(scene.Panel.Steps.Items[0].IsDelayOnly);
            Assert.Equal("80", scene.Panel.StepDelayText);

            scene.TypeDelay(string.Empty);

            Assert.Empty(scene.CurrentMacro.Keystrokes);
            Assert.Empty(scene.Panel.Steps.Items);
        }

        /// <summary>
        /// The other half of the field's new job, and the one that must not be confused with it: a
        /// value that is <b>not a number at all</b> is a rejected input. It raises §11.3's message,
        /// writes nothing, and leaves the delay the step already had exactly where it was — an
        /// emptied field clears, a mistyped one does not.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("abc")]
        [InlineData("8o")]
        [InlineData("-")]
        public void GarbageInTheField_IsRejected_AndLeavesTheStepsDelayAlone(string typed)
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);
            scene.TypeDelay("80");

            Assert.Equal(["a", "d080"], scene.MacroTokens());

            scene.TypeDelay(typed);

            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
            Assert.True(scene.Panel.HasStepDelayError);
            Assert.Equal(["a", "d080"], scene.MacroTokens());
            Assert.Equal(MacroStepDelayMode.Fixed, scene.SelectedDelayMode());
        }

        /// <summary>
        /// A number outside §11.3's 1-999 is still refused, and still writes nothing — the field's
        /// clearing job did not turn every unusable value into "no delay". Driven from a step that
        /// <b>already carries</b> a delay, so a refusal that silently cleared one would fail here.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("1000")]
        [InlineData("-5")]
        public void ATypedDelayOutsideTheRange_IsRejected_AndLeavesTheStepsDelayAlone(string typed)
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);
            scene.TypeDelay("80");

            scene.TypeDelay(typed);

            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
            Assert.Equal(["a", "d080"], scene.MacroTokens());
        }

        /// <summary>
        /// The closed loop that made a fixed delay unauthorable. <c>fixed</c> used to be read back off
        /// the step alone, so on a step carrying no delay the field was 0, pressing <c>fixed</c> failed
        /// validation and wrote nothing, the step still reported "no delay", and the segment never
        /// latched — so the field it arms never came alive and no number could ever be entered.
        /// The press is an intent: it latches and arms, and the first usable number writes.
        /// </summary>
        [AvaloniaFact]
        public void PressingFixed_OnAStepWithNoDelay_LatchesAndArmsTheField_ThenTheFirstNumberWrites()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            Assert.Null(scene.SelectedDelayMode());

            scene.SetDelayMode(MacroStepDelayMode.Fixed);

            // Latched and armed on the press, with §11.3's message standing in for the empty field —
            // and nothing written, because 0 is not a delay.
            Assert.Equal(MacroStepDelayMode.Fixed, scene.SelectedDelayMode());
            Assert.True(scene.Panel.IsCustomStepDelay);
            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
            Assert.Equal(["a"], scene.MacroTokens());

            scene.Panel.StepDelayMilliseconds = 80;

            Assert.Equal(["a", "d080"], scene.MacroTokens());
            Assert.Equal(string.Empty, scene.Panel.StepDelayError);
            Assert.Equal(MacroStepDelayMode.Fixed, scene.SelectedDelayMode());
        }

        /// <summary>
        /// <b>Pressing the lit segment clears the delay (issue #152)</b> — the third route to "no
        /// delay", beside the emptied field and a typed <c>0</c>, and the one that closes the gap
        /// #148 left behind: a <c>random</c> delay puts no number in the field, so there was nothing
        /// to empty and clearing it took two gestures.
        /// <para>
        /// Driven from a step that is <b>really carrying</b> the delay in question, because pressing
        /// a lit segment on a step with nothing written is a different case with a different outcome
        /// (see <see cref="UntogglingFixed_WhileItIsLitButUnwritten_WritesNothingAndDirtiesNothing"/>)
        /// — and a fixture that never had a delay would pass either way.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData(MacroStepDelayMode.Fixed)]
        [InlineData(MacroStepDelayMode.Random)]
        public void PressingTheLitDelaySegment_ClearsTheDelay(MacroStepDelayMode mode)
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(1);

            // The delay is put on through the route that really writes it: a number for `fixed`,
            // the segment for `random` (which has no number to be given).
            if (mode == MacroStepDelayMode.Fixed)
            {
                scene.TypeDelay("80");

                Assert.Equal(["a", "b", "d080"], scene.MacroTokens());
            }
            else
            {
                scene.SetDelayMode(MacroStepDelayMode.Random);

                Assert.Equal(["a", "b", MacroDelayTokens.RandomToken], scene.MacroTokens());
            }

            Assert.Equal(mode, scene.SelectedDelayMode());

            scene.SetDelayMode(mode);

            // Neither segment lit, the field emptied, and `None` written — `ClearStepDelay` verbatim.
            Assert.Null(scene.SelectedDelayMode());
            Assert.Equal(string.Empty, scene.Panel.StepDelayText);
            Assert.Equal(0, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(string.Empty, scene.Panel.StepDelayError);
            Assert.False(scene.Panel.IsCustomStepDelay);
            Assert.Equal(["a", "b"], scene.MacroTokens());
            Assert.False(scene.Panel.Steps.Items[1].HasDelay);

            // ...and the OTHER segment is unaffected by the press: an unlit one still writes.
            scene.SetDelayMode(MacroStepDelayMode.Random);

            Assert.Equal(MacroStepDelayMode.Random, scene.SelectedDelayMode());
            Assert.Equal(["a", "b", MacroDelayTokens.RandomToken], scene.MacroTokens());
        }

        /// <summary>
        /// <b><c>fixed</c> can be lit with nothing written</b>, and un-arming it must stay free.
        /// Pressing it on a step with no delay latches the segment and arms the field without writing
        /// (that is what makes a fixed delay authorable at all), so <c>IsOn</c> does not imply the
        /// step carries anything. Pressing it again has to leave the step exactly as it was — and
        /// <b>not dirty the profile</b>, since nothing was ever written.
        /// <para>
        /// <c>Assigned</c> is the panel's one hop to the editor's funnel, and so to <c>IsDirty</c>;
        /// counting it is how a test says "the session is still clean".
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public void UntogglingFixed_WhileItIsLitButUnwritten_WritesNothingAndDirtiesNothing()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(1);

            var assigned = 0;

            scene.Panel.Assigned += (_, _) => assigned++;

            scene.SetDelayMode(MacroStepDelayMode.Fixed);

            // Armed, reported, and nothing written — the state this test exists for.
            Assert.Equal(MacroStepDelayMode.Fixed, scene.SelectedDelayMode());
            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
            Assert.Equal(["a", "b"], scene.MacroTokens());

            scene.SetDelayMode(MacroStepDelayMode.Fixed);

            Assert.Null(scene.SelectedDelayMode());
            Assert.False(scene.Panel.IsCustomStepDelay);
            Assert.Equal(string.Empty, scene.Panel.StepDelayError);
            Assert.Equal(string.Empty, scene.Panel.StepDelayText);
            Assert.Equal(["a", "b"], scene.MacroTokens());
            Assert.False(scene.Panel.Steps.Items[1].HasDelay);

            // NOTHING WAS WRITTEN, so nothing may have been announced: `TrySetSelectedDelay` refuses
            // a `None` against a step whose delay is already absent.
            Assert.Equal(0, assigned);
        }

        /// <summary>
        /// Untoggling is the same write as the other two routes, so it has the same consequence on a
        /// <b>delay-only</b> row (06 §2.2): the row goes, because a row that was nothing but a delay
        /// has nothing left once the delay does.
        /// <para>
        /// The fixture opens the macro with the delay and keeps a keystroke behind it, so "the row
        /// was dropped" and "the macro was emptied" cannot be confused for one another.
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public void UntogglingOnADelayOnlyRow_DropsTheRow()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            scene.CurrentMacro!.ClearKeystrokes();
            scene.CurrentMacro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));
            scene.CurrentMacro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            scene.Panel.Steps.RefreshFromModel();

            scene.SelectStep(0);

            Assert.True(scene.Panel.Steps.Items[0].IsDelayOnly);
            Assert.Equal(MacroStepDelayMode.Random, scene.SelectedDelayMode());

            scene.SetDelayMode(MacroStepDelayMode.Random);

            Assert.Equal(["a"], scene.MacroTokens());
            Assert.Single(scene.Panel.Steps.Items);
            Assert.False(scene.Panel.Steps.Items[0].IsDelayOnly);
        }

        /// <summary>
        /// The millisecond field is <b>empty</b> on a step with no delay, not <c>0</c>. Zero is the
        /// "nothing chosen" sentinel and is not itself a legal delay (§11.3's range is 1-999), so
        /// drawing it put a number in the box that the box would reject if it were typed. It only
        /// became worth fixing with #139: the old editor opened over a row on demand, and this
        /// section is on screen for every selected step.
        /// </summary>
        [AvaloniaFact]
        public void TheMillisecondField_IsEmptyWithNoDelay_AndNeverDrawsTheZeroSentinel()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            Assert.Equal(0, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(string.Empty, scene.Panel.StepDelayText);

            scene.Panel.StepDelayText = "80";

            Assert.Equal(["a", "d080"], scene.MacroTokens());
            Assert.Equal("80", scene.Panel.StepDelayText);

            // Clearing the box is "no delay", and since issue #148 it WRITES that rather than
            // reporting an unusable value: it is the control the `none` segment's job moved onto.
            scene.Panel.StepDelayText = string.Empty;

            Assert.Equal(0, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(string.Empty, scene.Panel.StepDelayText);
            Assert.Equal(string.Empty, scene.Panel.StepDelayError);
            Assert.Equal(["a"], scene.MacroTokens());
            Assert.False(scene.Panel.Steps.Items[0].HasDelay);
        }

        /// <summary>
        /// Typing a number is itself a choice of <c>fixed</c>, so the strip follows it — otherwise a
        /// delay could sit on the step while the segment above still read <c>none</c>.
        /// </summary>
        [AvaloniaFact]
        public void TypingANumber_LatchesFixed_WithoutEverPressingTheSegment()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);
            scene.SetDelayMode(MacroStepDelayMode.Random);

            Assert.Equal(MacroStepDelayMode.Random, scene.SelectedDelayMode());

            scene.Panel.StepDelayMilliseconds = 60;

            Assert.Equal(["a", "d060"], scene.MacroTokens());
            Assert.Equal(MacroStepDelayMode.Fixed, scene.SelectedDelayMode());
            Assert.True(scene.Panel.IsCustomStepDelay);
        }

        [AvaloniaTheory]
        [InlineData(1000)]
        [InlineData(-5)]
        public void ATypedDelayOutsideTheRange_ReportsSpecElevenPointThreesMessageAndWritesNothing(int delay)
        {
            // 0 IS NOT IN THIS THEORY ANY MORE (issue #148): it is the sentinel the millisecond field
            // draws as blank, and blank is now the write that clears a delay rather than an unusable
            // value that reports itself. See TheMillisecondField_… and ClearingTheField_… above.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.StepDelayMilliseconds = delay;

            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
            Assert.True(scene.Panel.HasStepDelayError);
            Assert.Equal(["a"], scene.MacroTokens());
        }

        /// <summary>
        /// The `+`/`-` arrows beside the millisecond field went with issue #146's compose bar, which
        /// draws a bare field. §11.3's range is unchanged and is still the *field's* — both bounds
        /// are readable off the panel, both ends of the range are writable, and everything outside
        /// it is refused by the validation above rather than silently clamped by an arrow.
        /// </summary>
        [AvaloniaFact]
        public void TheDelayField_TakesBothEndsOfTheSpecsOwnRange()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            Assert.Equal(1, scene.Panel.MinimumDelayMilliseconds);
            Assert.Equal(999, scene.Panel.MaximumDelayMilliseconds);

            scene.Panel.StepDelayMilliseconds = 1;

            Assert.Equal(1, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(["a", "d001"], scene.MacroTokens());

            scene.Panel.StepDelayMilliseconds = 999;

            Assert.Equal(999, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(["a", "d999"], scene.MacroTokens());
        }

        [AvaloniaFact]
        public void TheDelaySection_BelowTheFirmwareGate_RefusesInPlaceWithTheSpecMessage()
        {
            // 09 §2, answered in place exactly as the Tap & hold panel answers its gate — the
            // sanctioned exception to "absent features are not shown, not disabled", because the
            // feature is not absent, the firmware is old.
            var scene = new Scene(this, DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            scene.SelectFirstMacroKey();
            scene.Record("a");
            scene.SelectStep(0);

            Assert.False(scene.Panel.IsStepDelayEnabled);
            Assert.False(scene.Panel.Steps.AreDelaysAvailable);
            Assert.Equal(
                MacroInspectorStepsViewModel.FirmwareRefusalMessage,
                scene.Panel.Steps.DelayUnavailableReason);
            Assert.True(scene.Panel.Steps.CanUpdateFirmware);
            Assert.Equal(
                FirmwareFeatureGate.UpdateFirmwareButtonCaption,
                scene.Panel.Steps.UpdateFirmwareCaption);

            scene.Panel.StepDelayMilliseconds = 120;

            Assert.Equal(["a"], scene.MacroTokens());
        }

        [AvaloniaFact]
        public void ThePlaceholder_WritesNothingUntilAKeyLands()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(0);

            scene.Panel.InsertStepCommand.Execute(null);

            Assert.True(scene.Panel.Steps.HasPlaceholder);
            Assert.True(scene.Panel.Steps.Items[1].IsPlaceholder);
            Assert.Equal(["a", "b"], scene.MacroTokens());

            // Only the record button is live on it: a modifier with no key to qualify is meaningless.
            Assert.True(scene.Panel.RecordStepKeyCommand.CanExecute(null));
            Assert.False(scene.Panel.AreStepModifiersEnabled);
            Assert.False(scene.Panel.IsStepDelayEnabled);

            scene.RecordStepKey("z");

            Assert.Equal(["a", "z", "b"], scene.MacroTokens());
            Assert.False(scene.Panel.Steps.HasPlaceholder);
        }

        [AvaloniaFact]
        public void ThePlaceholder_WithNothingSelected_LandsAtTheEnd()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            scene.Panel.InsertStepCommand.Execute(null);
            scene.RecordStepKey("z");

            Assert.Equal(["a", "b", "z"], scene.MacroTokens());
        }

        [AvaloniaFact]
        public void ThePlaceholder_OnAKeyWithNoMacro_MakesTheMacroOnTheFirstKey()
        {
            // "The first keystroke IS the macro" holds for a composed step too — there is no Assign
            // button in this panel and none is wanted.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Null(scene.CurrentMacro);

            scene.Panel.InsertStepCommand.Execute(null);

            Assert.True(scene.Panel.Steps.HasPlaceholder);
            Assert.Null(scene.CurrentMacro);

            scene.RecordStepKey("z");

            Assert.NotNull(scene.CurrentMacro);
            Assert.Equal(["z"], scene.MacroTokens());
            Assert.Equal(1, scene.Layout.MacroCount);
        }

        [AvaloniaFact]
        public void ThePlaceholder_IsDiscardedOnDeselect_OnDeactivate_AndWhenTheRailMoves()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(0);

            scene.Panel.InsertStepCommand.Execute(null);
            scene.SelectStep(2);

            Assert.False(scene.Panel.Steps.HasPlaceholder);
            Assert.Equal(["a", "b"], scene.MacroTokens());

            scene.Panel.InsertStepCommand.Execute(null);
            scene.Panel.Deactivate();

            Assert.False(scene.Panel.Steps.HasPlaceholder);

            scene.Panel.InsertStepCommand.Execute(null);
            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.False(scene.Panel.Steps.HasPlaceholder);
        }

        [AvaloniaFact]
        public void TheComposersRecord_TakesExactlyOneKeystrokeAndDisarmsItself()
        {
            var scene = new Scene(this);
            var recordingChanges = 0;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(0);

            scene.Panel.RecordingChanged += (_, _) => recordingChanges++;
            scene.Panel.RecordStepKeyCommand.Execute(null);

            Assert.Equal(MacroCaptureMode.SingleStep, scene.Panel.CaptureMode);
            Assert.True(scene.Panel.IsRecording);
            Assert.True(((IKeystrokeSink)scene.Panel).WantsKeystrokes);
            Assert.Equal(MacroInspectorPanelViewModel.RecordingCaption, scene.Panel.RecordStepKeyCaption);

            // The header's own button must NOT read Stop: it would be offering to stop a take that
            // was never started.
            Assert.Equal(MacroInspectorPanelViewModel.RecordSequenceCaption, scene.Panel.RecordCommandCaption);

            scene.Panel.ReceiveKeystroke(Captured("z"));

            Assert.Equal(MacroCaptureMode.None, scene.Panel.CaptureMode);
            Assert.False(scene.Panel.IsRecording);
            Assert.False(((IKeystrokeSink)scene.Panel).WantsKeystrokes);
            Assert.Equal(["z", "b"], scene.MacroTokens());
            Assert.True(recordingChanges >= 2);

            // ...and a second keystroke after it goes nowhere: the arm is one-shot.
            scene.Panel.ReceiveKeystroke(Captured("q"));

            Assert.Equal(["z", "b"], scene.MacroTokens());
        }

        [AvaloniaFact]
        public void TheComposersRecord_ReplacesTheKeyAndKeepsTheStepsDelay()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.StepDelayMilliseconds = 80;
            scene.RecordStepKey("z");

            Assert.Equal(["z", "d080"], scene.MacroTokens());
            Assert.Equal(80, scene.Panel.Steps.Items[0].DelayMilliseconds);
        }

        [AvaloniaFact]
        public void TheTwoArms_AreMutuallyExclusive_BecauseTheyAreOneField()
        {
            // Invariant 5: one keystroke, one target. The panel is ONE IKeystrokeSink with one arm
            // field, so "both armed" is not a state that can be reached.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.RecordCommand.Execute(null);

            Assert.Equal(MacroCaptureMode.Run, scene.Panel.CaptureMode);

            scene.Panel.RecordStepKeyCommand.Execute(null);

            Assert.Equal(MacroCaptureMode.SingleStep, scene.Panel.CaptureMode);

            scene.Panel.RecordCommand.Execute(null);

            Assert.Equal(MacroCaptureMode.Run, scene.Panel.CaptureMode);

            // ...and pressing either one again stands it down rather than leaving a second arm up.
            scene.Panel.RecordCommand.Execute(null);

            Assert.Equal(MacroCaptureMode.None, scene.Panel.CaptureMode);
            Assert.False(scene.Panel.IsRecording);
        }

        [AvaloniaFact]
        public void TheRunArm_StillAppendsAtTheEndRegardlessOfTheSelection()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(0);

            scene.Record("c");

            Assert.Equal(["a", "b", "c"], scene.MacroTokens());
        }

        [AvaloniaFact]
        public void TheBanner_NamesTheSelectedStepRatherThanTheTake()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b", "c");
            scene.SelectStep(1);

            scene.Panel.RecordStepKeyCommand.Execute(null);

            // The two sentences are different because the two arms mean different things — the run
            // appends at the end, this one overwrites the row wearing the selection ring.
            Assert.Equal(MacroInspectorPanelViewModel.StepCaptureBannerText, scene.Panel.RecordingBanner);
            Assert.NotEqual(
                MacroInspectorPanelViewModel.RecordingBannerText,
                MacroInspectorPanelViewModel.StepCaptureBannerText);
        }

        [AvaloniaFact]
        public void Reverting_DropsTheSelectionAndAnyPlaceholder()
        {
            // A revert replaces the position's whole keystroke list; a placeholder held across it
            // would be an insertion index into a macro that no longer exists.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(0);

            scene.Panel.InsertStepCommand.Execute(null);

            Assert.True(scene.Panel.TryRevert());

            Assert.False(scene.Panel.Steps.HasPlaceholder);
            Assert.Null(scene.Panel.Steps.SelectedStep);
            Assert.False(scene.Panel.IsComposerEnabled);
            Assert.Null(scene.CurrentMacro);
        }

        [AvaloniaFact]
        public void AReorder_CarriesTheSelectionWithTheStep()
        {
            // MoveStep rebuilds every row, so a cached "selected index" goes stale — the selection
            // has to be re-resolved after the rebuild or the composer edits the wrong step next.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b", "c");
            scene.SelectStep(0);

            scene.Panel.Steps.MoveStepDownCommand.Execute(null);

            Assert.Equal(["b", "a", "c"], scene.MacroTokens());
            Assert.Equal("a", scene.Panel.StepTokenText);
            Assert.Same(scene.Panel.Steps.Items[1], scene.Panel.Steps.SelectedStep);
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

        /// <summary>One firmware version, for the 09 §2 gate cases.</summary>
        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }

        private MacroInspectorPanelViewModel Create()
        {
            return new MacroInspectorPanelViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _urlLauncher,
                new RelayCommand(() => { }),
                new RelayCommand(() => { }));
        }

        /// <summary>
        /// One panel over one real Freestyle Edge RGB layout, refreshed exactly as the rail
        /// refreshes it — pushed, never subscribed — and over a stand-in for the editor's copy pair.
        /// </summary>
        private sealed class Scene
        {
            public DeviceSnapshot Device { get; }

            public KeyboardLayout Layout { get; }

            /// <summary>
            /// The editor's <c>Copy macro to…</c>, as the panel sees it: a command it is handed and
            /// hands on. The arm itself is the editor's own state machine and is covered there;
            /// what this fixture has to be able to say is "it is armed now".
            /// </summary>
            public IRelayCommand CopyMacro { get; }

            /// <summary>Its cancel — the command the panel reads <c>IsCopyArmed</c> off.</summary>
            public IRelayCommand CancelCopy { get; }

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

            /// <summary>Whether the editor's copy pick is armed — what both commands are gated on.</summary>
            private bool _isCopyArmed;

            public Scene(
                MacroInspectorPanelViewModelTests owner,
                DeviceId deviceId = DeviceId.FreestyleEdgeRgb,
                FirmwareState? firmware = null)
            {
                Device = TestDevices.CreateSnapshot(deviceId);

                if (firmware is { } state)
                {
                    Device = Device with { Firmware = state };
                }

                Layout = KeyboardLayout.Create(deviceId);

                _layer = KeyboardLayerViewModel.BuildAll(Layout, ResolveVisual(deviceId, Layout), null)[0];

                // The editor's own shape: arming is what makes the cancel executable, which is the
                // one fact the panel reads back out of the pair.
                CopyMacro = new RelayCommand(() => ArmCopy(true), () => !_isCopyArmed);
                CancelCopy = new RelayCommand(() => ArmCopy(false), () => _isCopyArmed);

                Panel = new MacroInspectorPanelViewModel(Device, owner._urlLauncher, CopyMacro, CancelCopy);
            }

            /// <summary>Arms or disarms that pick, announcing it exactly as a real command does.</summary>
            public void ArmCopy(bool isArmed)
            {
                _isCopyArmed = isArmed;

                CopyMacro.NotifyCanExecuteChanged();
                CancelCopy.NotifyCanExecuteChanged();
            }

            public void Select(int keyIndex)
            {
                Key = _layer.FindByIndex(keyIndex)
                      ?? throw new InvalidOperationException($"The layer has no position {keyIndex}.");

                Refresh();
            }

            /// <summary>The first position of the layer that accepts a macro (05 §5.3).</summary>
            public void SelectFirstMacroKey()
            {
                foreach (var key in Layout.Layers[0].Keys)
                {
                    if (key.CanAssignMacro)
                    {
                        Select(key.Index);

                        return;
                    }
                }

                throw new InvalidOperationException("The device has no position that accepts a macro.");
            }

            /// <summary>
            /// The first position whose <b>trigger</b> key carries <paramref name="code"/> — how a
            /// test reaches 06 §5's reserved triggers, which are identified by their original
            /// action rather than by their position token (05 §1.3).
            /// </summary>
            public void SelectByTriggerCode(int code)
            {
                foreach (var key in Layout.Layers[0].Keys)
                {
                    if (key.TriggerKey.Code == code)
                    {
                        Select(key.Index);

                        return;
                    }
                }

                throw new InvalidOperationException($"The layer has no position triggering on {code}.");
            }

            public void Refresh()
            {
                Panel.Refresh(Key, _layer, Layout, EditorAdvisories.Empty);
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

            /// <summary>Points the composer at one row, through the command the pointer runs.</summary>
            public void SelectStep(int index)
            {
                Panel.Steps.SelectStepCommand.Execute(Panel.Steps.Items[index]);
            }

            /// <summary>
            /// Arms the composer's one-shot capture and delivers one keystroke to it, exactly as the
            /// editor's router would — the panel is the sink, so nothing is reached past.
            /// </summary>
            public void RecordStepKey(string token)
            {
                Panel.RecordStepKeyCommand.Execute(null);
                Panel.ReceiveKeystroke(Captured(token));
            }

            /// <summary>Ticks one modifier through the command the latch runs.</summary>
            public void TickChordModifier(MacroModifiers modifier)
            {
                Panel.ToggleChordModifierCommand.Execute(FindChordModifier(modifier));
            }

            /// <summary>The latch carrying <paramref name="modifier"/>, rebuilt on every change.</summary>
            public MacroChordModifier FindChordModifier(MacroModifiers modifier)
            {
                return Panel.ChordModifiers.First(latch => latch.Modifier == modifier);
            }

            /// <summary>Presses one direction segment through the command it runs.</summary>
            public void SetDirection(KeyDirection direction)
            {
                Panel.SetStepDirectionCommand.Execute(FindDirection(direction));
            }

            /// <summary>The segment for <paramref name="direction"/>, rebuilt on every change.</summary>
            public MacroStepDirection FindDirection(KeyDirection direction)
            {
                return Panel.StepDirections.First(segment => segment.Direction == direction);
            }

            /// <summary>Whichever direction segment is lit.</summary>
            public MacroStepDirection SelectedDirection()
            {
                return Panel.StepDirections.First(segment => segment.IsOn);
            }

            /// <summary>Presses one delay segment through the command it runs.</summary>
            public void SetDelayMode(MacroStepDelayMode mode)
            {
                Panel.SetStepDelayModeCommand.Execute(
                    Panel.StepDelayOptions.First(option => option.Mode == mode));
            }

            /// <summary>
            /// Types <paramref name="text"/> into the millisecond field, which is where the delay is
            /// cleared since issue #148 deleted the <c>none</c> segment.
            /// </summary>
            public void TypeDelay(string text)
            {
                Panel.StepDelayText = text;
            }

            /// <summary>
            /// Whichever delay segment is lit, or <c>null</c> where <b>neither</b> is — which is how
            /// the strip says "no delay" since issue #148 took the third segment away.
            /// </summary>
            public MacroStepDelayMode? SelectedDelayMode()
            {
                return Panel.StepDelayOptions.FirstOrDefault(option => option.IsOn)?.Mode;
            }

            /// <summary>
            /// The macro's keystrokes as the file spells them — read off the model rather than off
            /// the rows, because a composer that only moved the view models would pass a test
            /// written against them.
            /// </summary>
            public IReadOnlyList<string> MacroTokens()
            {
                return CurrentMacro is { } macro
                    ? macro.Keystrokes.Select(keystroke => keystroke.Key.GetToken(TokenDialect.Gen1)).ToList()
                    : [];
            }

            /// <summary>
            /// The device's own board picture where one is authored, and a throwaway one-per-key
            /// strip where it is not — only the Freestyle Edge RGB has a real visual today
            /// (issues #39-#42), and every device's <see cref="MacroCapability"/> is testable now.
            /// </summary>
            private static KeyboardVisual ResolveVisual(DeviceId deviceId, KeyboardLayout layout)
            {
                if (VisualCatalog.TryGet(deviceId, out var visual))
                {
                    return visual;
                }

                var keys = new List<KeyVisual>(layout.Layers[0].Keys.Count);

                foreach (var key in layout.Layers[0].Keys)
                {
                    keys.Add(new KeyVisual(key.Index, key.Index, 0));
                }

                return new KeyboardVisual(LayoutVariant.None, keys);
            }
        }
    }
}
