using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The macro panel of specs/10-apps-and-ui.md ("a bottom Macro panel … 3 macro slots selected
    /// by radio buttons, six co-trigger buttons …, per-macro playback-speed slider,
    /// Backspace/Clear/Copy/Paste") over the model and limits of specs/06-macros.md §1, §4, §5, §6
    /// and the firmware gate of specs/09-firmware.md §2.
    /// </summary>
    public sealed class MacroPanelViewModelTests
    {
        [Fact]
        public void Constructor_ForTheFreestyleEdgeRgb_TakesEveryLimitFromTheDevice()
        {
            var panel = CreateRgbPanel(out _);
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros;

            // 06 §1, §4, §6 for the RGB family: 5 slots, speed and repeat 1-9, 300 per macro,
            // 7200 per layout, 100 macros.
            Assert.Equal(capability.SlotsPerKey, panel.Slots.Count);
            Assert.True(panel.UsesMacroSlots);
            Assert.False(panel.UsesFlatMacroList);
            Assert.Equal(capability.Speed!.Minimum, panel.SpeedMinimum);
            Assert.Equal(capability.Speed.Maximum, panel.SpeedMaximum);
            Assert.Equal(capability.Repeat!.Minimum, panel.RepeatMinimum);
            Assert.Equal(capability.Repeat.Maximum, panel.RepeatMaximum);
            Assert.Equal(capability.MaxCharactersPerMacro, panel.Budget.MaxMacroLength);
            Assert.Equal(capability.MaxTotalKeystrokes, panel.Budget.MaxTotalKeystrokes);
            Assert.Equal(capability.MaxMacroCount, panel.MaxMacroCount);
        }

        [Fact]
        public void SetTrigger_WithNoKeySelected_AsksForOne()
        {
            var panel = CreateRgbPanel(out _);

            Assert.Equal(MacroPanelViewModel.NoTriggerKeyMessage, panel.Message);
            Assert.False(panel.CanEditMacro);
            Assert.False(panel.RecordCommand.CanExecute(null));
        }

        [Fact]
        public void SetTrigger_OnAModifierPosition_RefusesWithTheSpecMessage()
        {
            var panel = CreateRgbPanel(out var layout);
            var leftShift = layout.Layers[0].Keys[TestLayouts.RgbLeftShiftKeyIndex];

            // specs/05-key-model.md §5.3 marks the modifier positions as unable to carry a macro,
            // and specs/02-devices.md quotes the refusal verbatim.
            Assert.False(leftShift.CanAssignMacro);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(leftShift), layout.Layers[0]);

            Assert.Equal("You cannot assign a macro to a modifier key", MacroPanelViewModel.RestrictedKeyMessage);
            Assert.Equal(MacroPanelViewModel.RestrictedKeyMessage, panel.Message);
            Assert.False(panel.CanEditMacro);
            Assert.Null(panel.EditedMacro);
            Assert.False(panel.RecordCommand.CanExecute(null));
            Assert.False(panel.AssignCommand.CanExecute(null));
        }

        [Fact]
        public void SetTrigger_OnAKeyThatTakesMacros_OpensSlotOneWithADraft()
        {
            var panel = CreateRgbPanel(out var layout, out _);

            Assert.Null(panel.Message);
            Assert.True(panel.CanEditMacro);
            Assert.NotNull(panel.EditedMacro);
            Assert.False(panel.IsEditedMacroAssigned);
            Assert.Equal(1, panel.SelectedSlot!.Slot);
            Assert.All(panel.Slots, slot => Assert.False(slot.IsOccupied));
            Assert.Equal(layout.Device.Macros.Speed!.Default, panel.Speed);
            Assert.Equal(layout.Device.Macros.Repeat!.Default, panel.Repeat);
        }

        [Fact]
        public void ReceiveKeystroke_WhileRecording_AppendsTheKeystrokesInOrder()
        {
            var panel = CreateRgbPanel(out _, out _);

            panel.RecordCommand.Execute(null);

            Assert.True(panel.IsRecording);
            Assert.True(panel.WantsKeystrokes);

            panel.ReceiveKeystroke(Captured("a"));
            panel.ReceiveKeystroke(Captured("b"));

            Assert.Equal(new[] { "A", "B" }, panel.Steps.Items.Select(step => step.KeyText));
            Assert.Equal(2, panel.EditedMacro!.Keystrokes.Count);
        }

        [Fact]
        public void ReceiveKeystroke_WhenNotRecording_ChangesNothing()
        {
            var panel = CreateRgbPanel(out _, out _);

            panel.ReceiveKeystroke(Captured("a"));

            Assert.False(panel.WantsKeystrokes);
            Assert.Empty(panel.Steps.Items);
        }

        [Fact]
        public void ReceiveKeystroke_WithHeldModifiers_FoldsThemIntoTheStep()
        {
            var panel = CreateRgbPanel(out _, out _);

            panel.RecordCommand.Execute(null);
            panel.ReceiveKeystroke(Captured("a", "lshft", "lctrl"));

            var step = Assert.Single(panel.Steps.Items);

            // specs/05-key-model.md §5.1: the held keys become the keystroke's modifier codes.
            Assert.Equal(MacroModifiers.LeftShift | MacroModifiers.LeftControl, step.Keystroke.Modifiers);
            Assert.Equal("LS,LC", step.ModifiersText);
            Assert.Equal("LS,LC + A", step.Caption);
        }

        [Fact]
        public void ReceiveKeystroke_OfAModifierKeyItself_CarriesNoModifiers()
        {
            var panel = CreateRgbPanel(out _, out _);

            panel.RecordCommand.Execute(null);
            panel.ReceiveKeystroke(Captured("lshft"));

            var step = Assert.Single(panel.Steps.Items);

            // §5.1 forbids attaching a modifier string to a modifier key; CapturedKeystroke and
            // Keystroke both enforce it, so the panel must not manufacture one either.
            Assert.Equal(MacroModifiers.None, step.Keystroke.Modifiers);
            Assert.False(step.HasModifiers);
        }

        [Fact]
        public void StopRecordingCommand_LeavesEverythingRecordedSoFar()
        {
            var panel = CreateRgbPanel(out _, out _);

            panel.RecordCommand.Execute(null);
            panel.ReceiveKeystroke(Captured("a"));
            panel.StopRecordingCommand.Execute(null);

            Assert.False(panel.IsRecording);
            Assert.Single(panel.Steps.Items);

            panel.ReceiveKeystroke(Captured("b"));

            Assert.Single(panel.Steps.Items);
        }

        [Fact]
        public void BackspaceCommand_DropsOnlyTheLastStep()
        {
            var panel = RecordAsync(out _, "a", "b", "c");

            panel.BackspaceCommand.Execute(null);

            Assert.Equal(new[] { "A", "B" }, panel.Steps.Items.Select(step => step.KeyText));
        }

        [Fact]
        public void ClearCommand_EmptiesTheMacroAndDisablesItself()
        {
            var panel = RecordAsync(out _, "a", "b");

            panel.ClearCommand.Execute(null);

            Assert.Empty(panel.Steps.Items);
            Assert.False(panel.ClearCommand.CanExecute(null));
            Assert.False(panel.BackspaceCommand.CanExecute(null));
        }

        [Fact]
        public void RemoveStepCommand_DropsOneArbitraryStep()
        {
            var panel = RecordAsync(out _, "a", "b", "c");

            panel.RemoveStepCommand.Execute(panel.Steps.Items[1]);

            Assert.Equal(new[] { "A", "C" }, panel.Steps.Items.Select(step => step.KeyText));
        }

        [Fact]
        public void InsertKeystroke_IsTheOverlayInsertionHookAndRefreshesTheBudget()
        {
            var panel = CreateRgbPanel(out _, out _);

            // What the Macro Timing Delays and Search Keys overlays of spec 11 call.
            Assert.True(panel.InsertKeystroke(TestLayouts.Gen1Key("d250")));

            var step = Assert.Single(panel.Steps.Items);

            Assert.Equal(TestLayouts.Gen1Key("d250"), step.Keystroke.Key);
            Assert.Equal(1, panel.Budget.MacroLength);
            Assert.Equal("1 / 300", panel.Budget.MacroCaption);
        }

        [Fact]
        public void ToggleCoTriggerCommand_BeyondTheDevicesCap_RefusesAndSaysWhy()
        {
            var panel = CreateRgbPanel(out var layout, out _);
            var limit = layout.Device.Macros.MaxCoTriggersPerMacro!.Value;

            foreach (var toggle in panel.CoTriggers.Take(limit))
            {
                panel.ToggleCoTriggerCommand.Execute(toggle);
            }

            Assert.Equal(limit, panel.EditedMacro!.CoTriggerCount);

            panel.ToggleCoTriggerCommand.Execute(panel.CoTriggers[limit]);

            // Macro.AddCoTrigger neither de-duplicates nor refuses (06 §5): the cap is the panel's.
            Assert.Equal(limit, panel.EditedMacro.CoTriggerCount);
            Assert.False(panel.CoTriggers[limit].IsOn);
            Assert.Equal(MacroPanelViewModel.BuildCoTriggerLimitMessage(limit), panel.Message);
        }

        [Fact]
        public void ToggleCoTriggerCommand_TurnedOffAgain_ClearsEverySlotHoldingThatKey()
        {
            var panel = CreateRgbPanel(out _, out _);
            var toggle = panel.CoTriggers[0];

            panel.ToggleCoTriggerCommand.Execute(toggle);

            // A tolerant load can carry the same co-trigger twice, and AddCoTrigger keeps both.
            panel.EditedMacro!.AddCoTrigger(toggle.Key);

            panel.ToggleCoTriggerCommand.Execute(toggle);

            Assert.False(toggle.IsOn);
            Assert.Equal(0, panel.EditedMacro.CoTriggerCount);
        }

        [Fact]
        public void Speed_OutsideTheDevicesRange_IsClampedAndWrittenToTheMacro()
        {
            var panel = CreateRgbPanel(out var layout, out _);
            var range = layout.Device.Macros.Speed!;

            panel.Speed = range.Maximum + 5;

            Assert.Equal(range.Maximum, panel.Speed);
            Assert.Equal(range.Maximum, panel.EditedMacro!.Speed);

            panel.Speed = range.Minimum - 5;

            Assert.Equal(range.Minimum, panel.Speed);
            Assert.Equal(range.Minimum, panel.EditedMacro.Speed);
        }

        [Fact]
        public void Repeat_OutsideTheDevicesRange_IsClampedAndWrittenToTheMacro()
        {
            var panel = CreateRgbPanel(out var layout, out _);
            var range = layout.Device.Macros.Repeat!;

            panel.Repeat = range.Maximum + 5;

            Assert.Equal(range.Maximum, panel.Repeat);
            Assert.Equal(range.Maximum, panel.EditedMacro!.RepeatFrequency);
        }

        [Fact]
        public void BuildLengthLimitMessage_ForTheRgbCap_IsTheSpecStringVerbatim()
        {
            var limit = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros.MaxCharactersPerMacro!.Value;

            // specs/06-macros.md §6 / specs/04-layout-file-format.md §5.3, verbatim. The number is
            // derived from the capability so the wording cannot drift away from the device.
            Assert.Equal(
                "Macros are limited to approximately 300 characters.",
                MacroBudgetViewModel.BuildLengthLimitMessage(limit));
        }

        [Fact]
        public void Budget_ForAMacroPastThePerMacroCap_ShowsTheOverflowMessage()
        {
            var panel = CreateRgbPanel(out var layout, out _);
            var limit = layout.Device.Macros.MaxCharactersPerMacro!.Value;

            panel.RecordCommand.Execute(null);

            for (var index = 0; index <= limit; index++)
            {
                panel.ReceiveKeystroke(Captured("a"));
            }

            Assert.Equal(limit + 1, panel.Budget.MacroLength);
            Assert.True(panel.Budget.IsMacroOverBudget);
            Assert.Equal(MacroBudgetViewModel.BuildLengthLimitMessage(limit), panel.Budget.OverflowMessage);
            Assert.Equal("301 / 300", panel.Budget.MacroCaption);
        }

        [Fact]
        public void Budget_CountsAModifierAsThreeAgainstBothReadouts()
        {
            var panel = CreateRgbPanel(out _, out _);

            panel.RecordCommand.Execute(null);
            panel.ReceiveKeystroke(Captured("a", "lshft"));

            // 04 §5.3: 1 per keystroke plus 2 per attached modifier.
            Assert.Equal(3, panel.Budget.MacroLength);

            // The profile total is what the profile holds; a draft is not in it until it is
            // assigned (KeyboardLayout.TotalKeystrokes walks the stores, not the editor).
            Assert.Equal(0, panel.Budget.TotalKeystrokes);

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            Assert.Equal(3, panel.Budget.TotalKeystrokes);
            Assert.Equal("3 / 7200", panel.Budget.TotalCaption);
            Assert.False(panel.Budget.IsTotalOverBudget);
        }

        [Fact]
        public void ResolveMaxMacroCount_OnAFreestyleEdgeBelowTheGate_IsTheBaseline()
        {
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdge,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdge, "1.0.339"));

            // specs/09-firmware.md §2: FS Edge/Pro reach 100 macros only from firmware 1.0.340.
            Assert.Equal(24, MacroPanelViewModel.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void ResolveMaxMacroCount_OnAFreestyleEdgeAtTheGate_IsRaised()
        {
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdge,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdge, "1.0.340"));

            Assert.Equal(100, MacroPanelViewModel.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void ResolveMaxMacroCount_InDemoMode_PassesTheGate()
        {
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdge,
                VDriveConnectionStatus.NotDetected,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdge, "1.0.339"));

            Assert.True(snapshot.IsDemoMode);
            Assert.Equal(100, MacroPanelViewModel.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void ResolveMaxMacroCount_OnAnUngatedDevice_IsTheCatalogBaseline()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);

            // The RGB has no ExpandedMacroCount gate and no raised value: 100 either way.
            Assert.Equal(100, MacroPanelViewModel.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void AssignCommand_PutsTheMacroInTheSelectedSlot()
        {
            var panel = RecordAsync(out var layout, "a");
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            Assert.Same(panel.EditedMacro, key.GetMacro(1));
            Assert.True(panel.IsEditedMacroAssigned);
            Assert.True(panel.Slots[0].IsOccupied);
            Assert.Equal(1, layout.MacroCount);
            Assert.Equal(MacroPanelViewModel.BuildAssignedMessage(panel.TriggerCaption), panel.Message);
            Assert.StartsWith("Macro assigned to ", panel.Message, StringComparison.Ordinal);
            Assert.False(panel.AssignCommand.CanExecute(null));
        }

        [Fact]
        public void AssignCommand_ToASecondSlot_LeavesTheFirstAlone()
        {
            var panel = RecordAsync(out var layout, "a");
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            panel.SelectSlotCommand.Execute(panel.Slots[1]);

            Assert.Equal(2, panel.SelectedSlot!.Slot);
            Assert.Empty(panel.Steps.Items);

            panel.InsertKeystroke(TestLayouts.Gen1Key("b"));
            panel.AssignCommand.Execute(null);

            Assert.NotNull(key.GetMacro(1));
            Assert.NotNull(key.GetMacro(2));
            Assert.Equal(2, layout.MacroCount);
        }

        [Fact]
        public void AssignCommand_WhenTheProfileIsFull_RefusesWithTheCountLimit()
        {
            var panel = CreateRgbPanel(out var layout, out _);

            TestLayouts.FillMacroSlots(layout, panel.MaxMacroCount!.Value);

            Assert.Equal(panel.MaxMacroCount, layout.MacroCount);

            panel.InsertKeystroke(TestLayouts.Gen1Key("a"));
            panel.AssignCommand.Execute(null);

            Assert.Equal(MacroPanelViewModel.BuildMacroCountLimitMessage(panel.MaxMacroCount!.Value), panel.Message);
            Assert.Equal(panel.MaxMacroCount, layout.MacroCount);
            Assert.False(panel.IsEditedMacroAssigned);
        }

        [Fact]
        public void DeleteCommand_TakesTheMacroOffTheKeyAndOpensAFreshDraft()
        {
            var panel = RecordAsync(out var layout, "a");
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            panel.StopRecording();
            panel.AssignCommand.Execute(null);
            panel.DeleteCommand.Execute(null);

            Assert.Null(key.GetMacro(1));
            Assert.Equal(0, layout.MacroCount);
            Assert.NotNull(panel.EditedMacro);
            Assert.False(panel.IsEditedMacroAssigned);
            Assert.Empty(panel.Steps.Items);
        }

        [Fact]
        public void CopyAndPaste_MoveAKeysMacrosOntoAnother()
        {
            var panel = RecordAsync(out var layout, "a");
            var source = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var target = layout.Layers[0].Keys[TestLayouts.RgbDigitTwoKeyIndex];

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            Assert.True(panel.CopyCommand.CanExecute(null));

            panel.CopyCommand.Execute(null);

            Assert.True(panel.HasClipboard);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(target), layout.Layers[0]);

            Assert.True(panel.PasteCommand.CanExecute(null));

            panel.PasteCommand.Execute(null);

            // KeyboardKey.CopyFrom(other, Macros) deep-copies every slot (05 §1.3).
            Assert.NotNull(target.GetMacro(1));
            Assert.NotSame(source.GetMacro(1), target.GetMacro(1));
            Assert.True(source.GetMacro(1)!.IsEquivalentTo(target.GetMacro(1)));
            Assert.Equal(2, layout.MacroCount);
        }

        [Fact]
        public void PasteCommand_WithNothingCopied_IsUnavailable()
        {
            var panel = CreateRgbPanel(out _, out _);

            Assert.False(panel.HasClipboard);
            Assert.False(panel.PasteCommand.CanExecute(null));
        }

        [Fact]
        public void CopyCommand_OnAKeyWithoutMacros_IsUnavailable()
        {
            var panel = CreateRgbPanel(out _, out _);

            Assert.False(panel.CopyCommand.CanExecute(null));
        }

        [Fact]
        public void Macros_RenderEveryMacroOfTheProfileAsTheFileWillCarryIt()
        {
            var panel = RecordAsync(out var layout, "a", "b");

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            var entry = Assert.Single(panel.Macros);

            // 06 §3: {trigger}>{sN}{xN}{tokens}, with the RGB defaults speed 5 / repeat 1.
            Assert.Equal("{1}>{s5}{x1}{a}{b}", entry.Text);
            Assert.StartsWith("Top · ", entry.TriggerCaption, StringComparison.Ordinal);
            Assert.EndsWith(" (1)", entry.TriggerCaption, StringComparison.Ordinal);
            Assert.True(entry.IsSelected);
            Assert.Same(entry, panel.SelectedMacro);
            Assert.Same(layout.Layers[0], entry.Layer);
        }

        [Fact]
        public void SelectMacroCommand_LoadsTheRowForEditing()
        {
            var panel = RecordAsync(out var layout, "a");

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            var assigned = panel.EditedMacro!;

            panel.SelectSlotCommand.Execute(panel.Slots[1]);

            Assert.NotSame(assigned, panel.EditedMacro);

            panel.SelectMacroCommand.Execute(panel.Macros[0]);

            Assert.Same(assigned, panel.EditedMacro);
            Assert.True(panel.IsEditedMacroAssigned);
            Assert.Single(panel.Steps.Items);
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void RefreshFromModel_AfterALayoutReset_DropsTheDeletedMacro()
        {
            var panel = RecordAsync(out var layout, "a");

            panel.StopRecording();
            panel.AssignCommand.Execute(null);

            layout.Reset();

            panel.RefreshFromModel();

            Assert.Empty(panel.Macros);
            Assert.Empty(panel.Steps.Items);
            Assert.False(panel.IsEditedMacroAssigned);
            Assert.All(panel.Slots, slot => Assert.False(slot.IsOccupied));
        }

        [Fact]
        public void AssignCommand_OnAFlatListDevice_AppendsToTheLayoutsMacroList()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var panel = new MacroPanelViewModel(TestDevices.CreateSnapshot(DeviceId.Advantage360), layout);
            var key = FindMacroKey(layout);

            Assert.True(panel.UsesFlatMacroList);
            Assert.False(panel.UsesMacroSlots);
            Assert.Empty(panel.Slots);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(key, layout.Dialect), layout.Layers[0]);
            panel.InsertKeystroke(key.OriginalKey);
            panel.AssignCommand.Execute(null);

            // 06 §1: Gen2 macros live in one flat per-layout list tagged with trigger and layer.
            var macro = Assert.Single(layout.Macros);

            Assert.Same(panel.EditedMacro, macro);
            Assert.Equal(key.TriggerKey.Code, macro.TriggerKey);
            Assert.Equal(0, macro.LayerIndex);
            Assert.True(panel.IsEditedMacroAssigned);

            panel.DeleteCommand.Execute(null);

            Assert.Empty(layout.Macros);
        }

        /// <summary>
        /// specs/06-macros.md §6 states a macros-per-layout figure for the Freestyle, RGB and
        /// Advantage360 families and <b>none</b> for the Advantage2, which the catalog carries as a
        /// null. Null is "no limit", never "limit 0".
        /// </summary>
        [Fact]
        public void ResolveMaxMacroCount_OnADeviceThatStatesNoCount_IsNoLimitAtAll()
        {
            var macros = DeviceCatalog.GetById(DeviceId.Advantage2).Macros;

            Assert.Null(macros.MaxMacroCount);
            Assert.Null(macros.GatedMaxMacroCount);
            Assert.Null(MacroPanelViewModel.ResolveMaxMacroCount(TestDevices.CreateSnapshot(DeviceId.Advantage2)));
        }

        /// <summary>
        /// The consumer side of the same rule (06 §6): with no stated count nothing may be refused
        /// and the budget must read the bare number, not "1 / 0".
        /// </summary>
        [Fact]
        public void AssignCommand_OnADeviceThatStatesNoMacroCount_IsNeverRefused()
        {
            var panel = CreatePanel(DeviceId.Advantage2, out var layout);
            var key = FindMacroKey(layout);

            Assert.Null(panel.MaxMacroCount);
            Assert.Null(panel.Budget.MaxMacroCount);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(key, layout.Dialect), layout.Layers[0]);
            panel.InsertKeystroke(key.OriginalKey);
            panel.AssignCommand.Execute(null);

            Assert.Equal(1, layout.MacroCount);
            Assert.True(panel.IsEditedMacroAssigned);
            Assert.StartsWith("Macro assigned to ", panel.Message!, StringComparison.Ordinal);
            Assert.Equal("1", panel.Budget.MacroCountCaption);
            Assert.False(panel.Budget.IsMacroCountOverBudget);
        }

        /// <summary>
        /// 06 §1: the Advantage2 and Freestyle Edge/Pro dialects persist macro slots 1-3 of the
        /// model's five, and <c>LayoutFileSerializer</c> writes exactly
        /// <see cref="MacroCapability.PersistedSlotsPerKey"/> of them — so a fourth slot on the
        /// panel would offer a macro the next save silently drops.
        /// </summary>
        [Fact]
        public void Slots_OnADialectThatPersistsThreeOfFiveSlots_OffersOnlyTheThree()
        {
            var panel = CreatePanel(DeviceId.FreestyleEdge, out var layout);
            var macros = layout.Device.Macros;

            Assert.Equal(5, macros.SlotsPerKey);
            Assert.Equal(3, macros.PersistedSlotsPerKey);
            Assert.True(panel.UsesMacroSlots);
            Assert.Equal(new[] { 1, 2, 3 }, panel.Slots.Select(slot => slot.Slot));
        }

        /// <summary>
        /// 06 §2.1, §3: the old Freestyle parser and serializer keep only the <b>first</b>
        /// co-trigger of the four the model holds, so the panel's cap is
        /// <see cref="MacroCapability.PersistedCoTriggersPerMacro"/> — anything beyond it would
        /// vanish on save with nothing said.
        /// </summary>
        [Fact]
        public void ToggleCoTriggerCommand_OnADialectThatPersistsOneCoTrigger_CapsAtOne()
        {
            var panel = CreatePanel(DeviceId.FreestyleEdge, out var layout);
            var macros = layout.Device.Macros;

            Assert.Equal(4, macros.MaxCoTriggersPerMacro);
            Assert.Equal(1, macros.PersistedCoTriggersPerMacro);
            Assert.Equal(1, panel.MaxCoTriggers);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(FindMacroKey(layout), layout.Dialect), layout.Layers[0]);

            panel.ToggleCoTriggerCommand.Execute(panel.CoTriggers[0]);
            panel.ToggleCoTriggerCommand.Execute(panel.CoTriggers[1]);

            Assert.Equal(1, panel.EditedMacro!.CoTriggerCount);
            Assert.True(panel.CoTriggers[0].IsOn);
            Assert.False(panel.CoTriggers[1].IsOn);
            Assert.Equal(MacroPanelViewModel.BuildCoTriggerLimitMessage(1), panel.Message);
        }

        /// <summary>
        /// 06 §3: the Advantage2 serializer writes no <c>{xN}</c> repeat token at all, so a repeat
        /// set on that board is discarded by the very next save — the control is hidden rather than
        /// offering a value that cannot survive.
        /// </summary>
        [Fact]
        public void HasRepeat_OnTheAdvantage2_IsFalseBecauseTheDialectPersistsNoRepeatToken()
        {
            var advantage2 = CreatePanel(DeviceId.Advantage2, out var legacyLayout);
            var rgb = CreateRgbPanel(out _);

            Assert.NotNull(legacyLayout.Device.Macros.Repeat);
            Assert.False(legacyLayout.Device.Macros.PersistsRepeat);
            Assert.False(advantage2.HasRepeat);

            // The speed slider stays: {speedN} is written (06 §3).
            Assert.True(advantage2.HasSpeed);
            Assert.True(rgb.HasRepeat);
        }

        /// <summary>
        /// 06 §6 measures the Advantage360's 500 as the <b>length of the serialized macro text</b>,
        /// not as a weighted keystroke count — the same metric <see cref="KeyboardLayout.Validate"/>
        /// applies, so the readout and the gate that stops the save can never disagree.
        /// </summary>
        [Fact]
        public void Budget_OnTheAdvantage360_MeasuresTheSerializedMacroTextLength()
        {
            var panel = CreatePanel(DeviceId.Advantage360, out var layout);
            var key = FindMacroKey(layout);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(key, layout.Dialect), layout.Layers[0]);
            panel.InsertKeystroke(KeyRegistry.FindByToken("a", TokenDialect.Gen2)!);

            var macro = panel.EditedMacro!;

            Assert.Equal(500, panel.Budget.MaxMacroLength);
            Assert.Equal(MacroLengthMetric.Measure(macro, layout), panel.Budget.MacroLength);
            Assert.Equal("{a}".Length, panel.Budget.MacroLength);
            Assert.NotEqual(macro.WeightedKeystrokeCount, panel.Budget.MacroLength);
        }

        /// <summary>
        /// 06 §3 step 1 and 04 §3.1: a bottom-layer macro line on the Gen1 family carries the
        /// <c>fn </c> prefix. The row claims to show the line the file will carry, so
        /// <c>LayoutFileSerializer</c> is the authority it has to match.
        /// </summary>
        [Fact]
        public void Macros_ForABottomLayerMacro_RenderTheFnPrefixTheFileWillCarry()
        {
            var panel = CreateRgbPanel(out var layout);
            var key = layout.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex];

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(key), layout.Layers[1]);
            panel.InsertKeystroke(TestLayouts.Gen1Key("a"));
            panel.AssignCommand.Execute(null);

            var entry = Assert.Single(panel.Macros);

            Assert.Equal("fn {1}>{s5}{x1}{a}", entry.Text);
            Assert.Contains(entry.Text, LayoutFileSerializer.Serialize(layout));
        }

        /// <summary>
        /// The panel is one editor: clicking a row of the profile's macro list opens that macro, so
        /// the trigger it names and the slot it highlights have to be the macro's own. Otherwise
        /// Record, Clear, Backspace, the co-triggers and the sliders all write to a key the header
        /// is not naming (specs/10-apps-and-ui.md macro panel; 06 §1).
        /// </summary>
        [Fact]
        public void SelectMacroCommand_ForAMacroOnAnotherKey_MovesTheTriggerAndSlotWithIt()
        {
            var panel = CreateRgbPanel(out var layout);
            var owner = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var other = layout.Layers[0].Keys[TestLayouts.RgbDigitTwoKeyIndex];

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(owner), layout.Layers[0]);
            panel.SelectSlotCommand.Execute(panel.Slots[1]);
            panel.InsertKeystroke(TestLayouts.Gen1Key("a"));
            panel.AssignCommand.Execute(null);

            var assigned = panel.EditedMacro!;
            var ownerCaption = panel.TriggerCaption;

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(other), layout.Layers[0]);

            Assert.NotEqual(ownerCaption, panel.TriggerCaption);

            panel.SelectMacroCommand.Execute(panel.Macros[0]);

            Assert.Same(assigned, panel.EditedMacro);
            Assert.Equal(ownerCaption, panel.TriggerCaption);
            Assert.Equal(2, panel.SelectedSlot!.Slot);
            Assert.True(panel.Slots[1].IsOccupied);

            // ...and the edits land on the key the header now names.
            panel.RecordCommand.Execute(null);
            panel.ReceiveKeystroke(Captured("b"));
            panel.StopRecording();

            Assert.Equal(2, owner.GetMacro(2)!.Keystrokes.Count);
            Assert.Equal(0, other.MacroCount);
            Assert.Equal(1, layout.MacroCount);
        }

        /// <summary>
        /// 05 §1.3's copy works on copies, never on shared instances — and the panel's clipboard is
        /// a snapshot for the same reason: between the Copy and the Paste the source key can be
        /// emptied by Reset Key / Reset Layer / Reset Layout, and a live reference would then paste
        /// nothing and wipe the target.
        /// </summary>
        [Fact]
        public void PasteCommand_AfterTheCopiedKeyWasCleared_StillPastesWhatWasCopied()
        {
            var panel = RecordAsync(out var layout, "a");
            var source = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var target = layout.Layers[0].Keys[TestLayouts.RgbDigitTwoKeyIndex];

            panel.StopRecording();
            panel.AssignCommand.Execute(null);
            panel.CopyCommand.Execute(null);

            layout.Reset();
            panel.RefreshFromModel();

            Assert.Equal(0, layout.MacroCount);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(target), layout.Layers[0]);
            panel.PasteCommand.Execute(null);

            var pasted = target.GetMacro(1);

            Assert.NotNull(pasted);
            Assert.Single(pasted.Keystrokes);
            Assert.Equal(1, layout.MacroCount);
            Assert.Null(source.GetMacro(1));
        }

        /// <summary>
        /// 06 §6's macro count is a limit on the profile, and Assign holds it — Paste, which writes
        /// up to five macros at once, must not be the way around it.
        /// </summary>
        [Fact]
        public void PasteCommand_WhenTheProfileHoldsItsMacroCount_RefusesWithTheCountLimit()
        {
            var panel = RecordAsync(out var layout, "a");

            panel.StopRecording();
            panel.AssignCommand.Execute(null);
            panel.CopyCommand.Execute(null);

            layout.Reset();
            panel.RefreshFromModel();

            TestLayouts.FillMacroSlots(layout, panel.MaxMacroCount!.Value);

            Assert.Equal(panel.MaxMacroCount, layout.MacroCount);

            var target = FindEmptyMacroKey(layout);

            panel.SetTrigger(TestLayouts.CreateKeyViewModel(target), layout.Layers[0]);
            panel.PasteCommand.Execute(null);

            Assert.Equal(MacroPanelViewModel.BuildMacroCountLimitMessage(panel.MaxMacroCount!.Value), panel.Message);
            Assert.Equal(panel.MaxMacroCount, layout.MacroCount);
            Assert.Equal(0, target.MacroCount);
        }

        private static MacroPanelViewModel RecordAsync(out KeyboardLayout layout, params string[] tokens)
        {
            var panel = CreateRgbPanel(out layout, out _);

            panel.RecordCommand.Execute(null);

            foreach (var token in tokens)
            {
                panel.ReceiveKeystroke(Captured(token));
            }

            return panel;
        }

        private static MacroPanelViewModel CreateRgbPanel(out KeyboardLayout layout)
        {
            layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            return new MacroPanelViewModel(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb), layout);
        }

        private static MacroPanelViewModel CreateRgbPanel(out KeyboardLayout layout, out KeyboardKeyViewModel trigger)
        {
            var panel = CreateRgbPanel(out layout);

            trigger = TestLayouts.CreateKeyViewModel(layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            panel.SetTrigger(trigger, layout.Layers[0]);

            return panel;
        }

        private static MacroPanelViewModel CreatePanel(DeviceId deviceId, out KeyboardLayout layout)
        {
            layout = KeyboardLayout.Create(deviceId);

            return new MacroPanelViewModel(TestDevices.CreateSnapshot(deviceId), layout);
        }

        private static KeyboardKey FindMacroKey(KeyboardLayout layout)
        {
            foreach (var key in layout.Layers[0].Keys)
            {
                if (key.CanAssignMacro)
                {
                    return key;
                }
            }

            throw new InvalidOperationException("The device has no position that accepts a macro.");
        }

        private static KeyboardKey FindEmptyMacroKey(KeyboardLayout layout)
        {
            foreach (var key in layout.Layers[0].Keys)
            {
                if (key.CanAssignMacro && !key.IsMacro)
                {
                    return key;
                }
            }

            throw new InvalidOperationException("Every position that accepts a macro already holds one.");
        }

        private static CapturedKeystroke Captured(string token, params string[] heldModifierTokens)
        {
            var held = new KeyDefinition[heldModifierTokens.Length];

            for (var index = 0; index < heldModifierTokens.Length; index++)
            {
                held[index] = TestLayouts.Gen1Key(heldModifierTokens[index]);
            }

            return new CapturedKeystroke
            {
                Key = TestLayouts.Gen1Key(token),
                PhysicalKey = PhysicalKeyCode.None,
                HeldModifiers = held
            };
        }
    }
}
