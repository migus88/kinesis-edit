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

            // Issue #128's honest sentence about what recording CANNOT do, reworded for #139. It
            // names the user's own example, it does not blame the app — the chord is taken above the
            // application and never delivered — and it names the two steps that author one now: the
            // bare key is recordable, and the modifiers are ticked on the step afterwards.
            Assert.Contains("Ctrl+1", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("system", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("Record the bare key", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);
            Assert.Contains("tick the modifiers", MacroInspectorPanelViewModel.OsReservedNote, StringComparison.Ordinal);

            // The single-shot banner is a different sentence from the run's, because it means
            // something different: one keystroke, onto the step the composer is pointed at.
            Assert.Equal(
                "Recording step 02 — the next key you press becomes this step. "
                + "Click Stop, or anywhere else, to cancel.",
                MacroInspectorPanelViewModel.BuildStepCaptureBanner("02"));
            Assert.Equal("Playback speed", MacroInspectorPanelViewModel.SpeedMeterLabel);
            Assert.Equal("this macro", MacroInspectorPanelViewModel.MacroLengthMeterLabel);
            Assert.Equal("layout keystrokes", MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel);

            // The fourth, since issue #140 moved 06 §6's profile-wide count here from the Macros
            // tab's footer. Its label is the tab's own wording, kept verbatim.
            Assert.Equal("macros", MacroInspectorPanelViewModel.MacroCountMeterLabel);

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

            // Somebody else — a reset, an import, a copy — empties the position.
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
        public void MacroCountMeter_ReadsTheProfilesCountAgainstTheDevicesOwnFirmwareGatedLimit()
        {
            // Issue #140's fourth meter, and the only readout of 06 §6's macro count left in the app
            // now that the Macros tab is gone. Its maximum is MacroLimits' — firmware-gated by
            // 09 §2 — and NEVER a literal, which is asserted by comparing against the resolver
            // rather than against a number.
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(MacroInspectorPanelViewModel.MacroCountMeterLabel, scene.Panel.MacroCountMeter.Label);
            Assert.Equal(MacroLimits.ResolveMaxMacroCount(scene.Device), scene.Panel.MacroCountMeter.Limit);
            Assert.NotNull(scene.Panel.MacroCountMeter.Limit);
            Assert.Equal(0, scene.Panel.MacroCountMeter.Value);

            scene.Record("a");

            // It counts the PROFILE's macros, not this position's, so recording one moves it.
            Assert.Equal(scene.Layout.MacroCount, scene.Panel.MacroCountMeter.Value);
            Assert.Equal(1, scene.Panel.MacroCountMeter.Value);
            Assert.False(scene.Panel.MacroCountMeter.IsOverBudget);
            Assert.Equal(
                $"1 / {MacroLimits.ResolveMaxMacroCount(scene.Device)}",
                scene.Panel.MacroCountMeter.Caption);
        }

        [Fact]
        public void MacroCountMeter_OnADeviceThatStatesNoCount_ReadsAsABareNumberThatIsNeverOverBudget()
        {
            // The Advantage2 states no macros-per-layout figure (06 §6), and null is "no limit"
            // rather than zero — a board whose count could never be met would read as permanently
            // over budget, which is the opposite of what the file says.
            var scene = new Scene(this, DeviceId.Advantage2);

            Assert.Null(MacroLimits.ResolveMaxMacroCount(scene.Device));

            scene.Select(scene.Layout.Layers[0].Keys.First(key => key.CanAssignMacro).Index);
            scene.Record("a");

            Assert.Null(scene.Panel.MacroCountMeter.Limit);
            Assert.False(scene.Panel.MacroCountMeter.IsOverBudget);
            Assert.Equal("1", scene.Panel.MacroCountMeter.Caption);
        }

        [Fact]
        public void MacroCountMeter_PastTheLimit_GoesOverBudgetAndRefusesNothing()
        {
            // Amber, never an error state, and never a refusal — the profile is reported as it
            // stands. The macros are put there behind the panel so the meter is measured against a
            // model the panel did not build, which is the half a record-driven case cannot show.
            var scene = new Scene(this);
            var limit = MacroLimits.ResolveMaxMacroCount(scene.Device)!.Value;

            TestLayouts.FillMacroSlots(scene.Layout, limit + 1);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(limit + 1, scene.Panel.MacroCountMeter.Value);
            Assert.Equal(limit, scene.Panel.MacroCountMeter.Limit);
            Assert.True(scene.Panel.MacroCountMeter.IsOverBudget);
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

        // ===== The slot selector (issue #137) =================================================
        // A key holds up to five macros told apart by their co-triggers (06 §1). Until this strip
        // existed the rail could reach exactly one of them.

        /// <summary>
        /// The dots and the dropdown count the slots the <b>dialect writes</b>, never the five the
        /// model owns: 3 on the Advantage2 and Freestyle Edge/Pro, 5 on the RGB family. Read off
        /// <see cref="MacroCapability.PersistedSlotsPerKey"/>, so a device added to the catalog with
        /// a different figure is right by construction.
        /// </summary>
        [Theory]
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

            // Nothing is recorded yet, so every dot is hollow and the dropdown opens on slot 1.
            Assert.All(scene.Panel.SlotOptions, option => Assert.False(option.IsOccupied));
            Assert.Equal(Macro.MinMacroIndex, scene.Panel.SelectedSlot!.Slot);

            scene.Record("a");

            Assert.True(scene.Panel.SlotOptions[0].IsOccupied);
            Assert.All(scene.Panel.SlotOptions.Skip(1), option => Assert.False(option.IsOccupied));
        }

        /// <summary>
        /// <b>The single easiest defect here.</b> <c>ActiveMacroIndex</c> is an in-memory field that
        /// is never serialized (05 §1.3), so a slot pick cannot reach the editor's funnel — which is
        /// the only route to the dirty flag, and would raise an unsaved-changes prompt for a choice
        /// no save could persist. (The Macros tab's <c>Make active</c> already got this right; issue
        /// #140 deleted that surface, so this panel is the only one left that has to.)
        /// </summary>
        [Fact]
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
            Assert.Equal(["[b]"], scene.Panel.Steps.Items.Select(step => step.TokenText));

            // `Assigned` is the panel's ONE hop to RefreshCounters(), and so to IsDirty.
            Assert.Equal(0, assigned);
        }

        /// <summary>
        /// Picking an empty slot yields an empty sequence with recording live — and creates
        /// <b>nothing</b> until a keystroke lands, because <c>EnsureMacro()</c> stays the only path
        /// that adds a macro. The one it then creates fills <b>the selected slot</b>, not the first
        /// free one.
        /// </summary>
        [Fact]
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
        /// The name dropdown's placeholder is <b>slot-scoped where slots exist</b>. Selecting an
        /// empty slot on a key that carries a macro in another one is a state this issue's slot
        /// selector created — before it, the panel only ever opened a populated slot, so
        /// <c>No macro on this key</c> could not be false. It would be false here, with the header's
        /// own dots showing slot 1 occupied two rows above the sentence denying it.
        /// </summary>
        [Fact]
        public void TheNamePlaceholder_SaysSlotNotKey_WhenAnEmptySlotOfAnOccupiedKeyIsSelected()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // The key really does carry a macro — the placeholder must not deny it.
            Assert.NotNull(scene.Key.Key.GetMacro(1));

            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[2];

            var placeholder = Assert.Single(scene.Panel.NameOptions, option => option.IsNone);

            Assert.Equal(MacroNameOptionViewModel.NoMacroInSlotCaption, placeholder.Caption);
            Assert.Same(placeholder, scene.Panel.SelectedName);
        }

        /// <summary>
        /// A flat-list board has no slots, so there the key-scoped wording is the true one and must
        /// survive — the fix above is scoped to the selector, not applied everywhere.
        /// </summary>
        [Fact]
        public void TheNamePlaceholder_StaysKeyScoped_OnABoardWithNoSlots()
        {
            var scene = new Scene(this, deviceId: DeviceId.Advantage360);

            scene.SelectFirstMacroKey();

            Assert.False(scene.Panel.HasSlotSelector);

            var placeholder = Assert.Single(scene.Panel.NameOptions, option => option.IsNone);

            Assert.Equal(MacroNameOptionViewModel.NoMacroCaption, placeholder.Caption);
        }

        /// <summary>
        /// The choice belongs to the position it was made on: moving the rail must open the next key
        /// on whatever slot <em>it</em> carries, not on the number picked for the last one.
        /// </summary>
        [Fact]
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
        [Fact]
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
        [Fact]
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

        [Fact]
        public void TheStrip_OffersTheThreeLeftHandLatchesAndTheTriggerToken()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(
                [MacroModifierMarks.ShiftMark, MacroModifierMarks.ControlMark, MacroModifierMarks.AltMark],
                scene.Panel.CoTriggers.Select(latch => latch.Symbol));
            Assert.All(scene.Panel.CoTriggers, latch => Assert.False(latch.HasSide));

            // No ⌘ latch: no co-trigger in specs 06 or 10 names one.
            Assert.DoesNotContain(scene.Panel.CoTriggers, latch => latch.Symbol == MacroModifierMarks.WinMark);

            Assert.Equal("[1]", scene.Panel.TriggerTokenText);
        }

        [Fact]
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
        [Theory]
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
        [Fact]
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

        [Fact]
        public void TriggerStatus_ReadsBarePressUntilACoTriggerIsSet()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildTriggerStatus(
                    MacroInspectorPanelViewModel.BarePressStatus,
                    MacroInspectorPanelViewModel.NoCollisionStatus),
                scene.Panel.TriggerStatus);
            Assert.Equal("bare press · no collision", scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildTriggerStatus(
                    MacroInspectorPanelViewModel.CoTriggeredStatus,
                    MacroInspectorPanelViewModel.NoCollisionStatus),
                scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);
        }

        /// <summary>
        /// 06 §5's duplicate-trigger rule, named on the slot it collides with — which is the whole
        /// reason the slot selector can drop the tab's <c>ACTIVE</c> badge: every populated slot is
        /// live, and what tells them apart is this.
        /// </summary>
        [Fact]
        public void TriggerStatus_NamesTheCollidingSlot_AndIsAnAmberAdvisory()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");

            // A second macro on the same key with the same (empty) co-trigger set — 06 §5 says two
            // macros with no co-triggers at all collide.
            scene.Panel.SelectedSlot = scene.Panel.SlotOptions[1];
            scene.Record("b");

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildTriggerStatus(
                    MacroInspectorPanelViewModel.BarePressStatus,
                    MacroInspectorPanelViewModel.BuildCollisionStatus(1)),
                scene.Panel.TriggerStatus);
            Assert.True(scene.Panel.IsTriggerAdvisory);

            // Give the second one a co-trigger and the pair stops colliding.
            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildTriggerStatus(
                    MacroInspectorPanelViewModel.CoTriggeredStatus,
                    MacroInspectorPanelViewModel.NoCollisionStatus),
                scene.Panel.TriggerStatus);
            Assert.False(scene.Panel.IsTriggerAdvisory);
        }

        /// <summary>
        /// 06 §5's reserved triggers: <c>fn1s</c> and <c>keyt</c> need at least one co-trigger, so a
        /// bare macro on either can never fire. Gen2 only, exactly as <c>KeyboardLayoutValidator</c>
        /// gates it — the strip and the validator must not disagree about what the firmware refuses.
        /// </summary>
        [Theory]
        [InlineData(KeyboardKey.Fn1ShiftKeyCode)]
        [InlineData(KeyboardKey.KeypadToggleKeyCode)]
        public void AReservedTrigger_WithNoCoTrigger_IsAnAmberAdvisory(int triggerCode)
        {
            var scene = new Scene(this, deviceId: DeviceId.Advantage360);

            scene.SelectByTriggerCode(triggerCode);
            scene.Record("a");

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildTriggerStatus(
                    MacroInspectorPanelViewModel.BarePressStatus,
                    MacroInspectorPanelViewModel.ReservedTriggerStatus),
                scene.Panel.TriggerStatus);
            Assert.True(scene.Panel.IsTriggerAdvisory);

            scene.Panel.ToggleCoTriggerCommand.Execute(scene.Panel.CoTriggers[0]);

            Assert.DoesNotContain(
                MacroInspectorPanelViewModel.ReservedTriggerStatus,
                scene.Panel.TriggerStatus,
                StringComparison.Ordinal);
            Assert.False(scene.Panel.IsTriggerAdvisory);
        }

        /// <summary>
        /// An ordinary trigger on the very same Gen2 board raises nothing, or the advisory above
        /// would pass for the wrong reason.
        /// </summary>
        [Fact]
        public void AnOrdinaryGen2Trigger_RaisesNoReservedAdvisory()
        {
            var scene = new Scene(this, deviceId: DeviceId.Advantage360);

            scene.SelectFirstMacroKey();
            scene.Record("a");

            Assert.False(scene.Panel.IsTriggerAdvisory);
            Assert.DoesNotContain(
                MacroInspectorPanelViewModel.ReservedTriggerStatus,
                scene.Panel.TriggerStatus,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// <b>Neither advisory blocks anything</b> (docs/app/keyboard-editor.md, invariant 20): the
        /// layout carrying a collision still validates into a <em>report</em>, and every command on
        /// the panel is still runnable while the amber is on screen.
        /// </summary>
        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void SelectingAStep_SeedsTheComposerFromIt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(1);

            Assert.True(scene.Panel.IsComposerEnabled);
            Assert.True(scene.Panel.HasStepKey);
            Assert.Equal("[b]", scene.Panel.StepTokenText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, scene.SelectedDirection().Caption);
            Assert.Equal(MacroStepDelayMode.None, scene.SelectedDelayMode());
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Theory]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

            scene.SetDelayMode(MacroStepDelayMode.None);

            Assert.Equal(["a"], scene.MacroTokens());
        }

        /// <summary>
        /// The closed loop that made a fixed delay unauthorable. <c>fixed</c> used to be read back off
        /// the step alone, so on a step carrying no delay the field was 0, pressing <c>fixed</c> failed
        /// validation and wrote nothing, the step still reported "no delay", and the segment never
        /// latched — so the field it arms never came alive and no number could ever be entered.
        /// The press is an intent: it latches and arms, and the first usable number writes.
        /// </summary>
        [Fact]
        public void PressingFixed_OnAStepWithNoDelay_LatchesAndArmsTheField_ThenTheFirstNumberWrites()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            Assert.Equal(MacroStepDelayMode.None, scene.SelectedDelayMode());

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
        /// The millisecond field is <b>empty</b> on a step with no delay, not <c>0</c>. Zero is the
        /// "nothing chosen" sentinel and is not itself a legal delay (§11.3's range is 1-999), so
        /// drawing it put a number in the box that the box would reject if it were typed. It only
        /// became worth fixing with #139: the old editor opened over a row on demand, and this
        /// section is on screen for every selected step.
        /// </summary>
        [Fact]
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

            // Clearing the box is "nothing chosen" again, and reports itself rather than writing.
            scene.Panel.StepDelayText = string.Empty;

            Assert.Equal(0, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
        }

        /// <summary>
        /// Typing a number is itself a choice of <c>fixed</c>, so the strip follows it — otherwise a
        /// delay could sit on the step while the segment above still read <c>none</c>.
        /// </summary>
        [Fact]
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

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void ATypedDelayOutsideTheRange_ReportsSpecElevenPointThreesMessageAndWritesNothing(int delay)
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.StepDelayMilliseconds = delay;

            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, scene.Panel.StepDelayError);
            Assert.True(scene.Panel.HasStepDelayError);
            Assert.Equal(["a"], scene.MacroTokens());
        }

        [Fact]
        public void TheDelayArrows_ClampToTheSpecsOwnRange()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a");
            scene.SelectStep(0);

            scene.Panel.DecreaseStepDelayCommand.Execute(null);

            Assert.Equal(1, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(1, scene.Panel.MinimumDelayMilliseconds);
            Assert.Equal(999, scene.Panel.MaximumDelayMilliseconds);

            scene.Panel.StepDelayMilliseconds = 999;
            scene.Panel.IncreaseStepDelayCommand.Execute(null);

            Assert.Equal(999, scene.Panel.StepDelayMilliseconds);
            Assert.Equal(["a", "d999"], scene.MacroTokens());
        }

        [Fact]
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

        [Fact]
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

        [Fact]
        public void ThePlaceholder_WithNothingSelected_LandsAtTheEnd()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");

            scene.Panel.InsertStepCommand.Execute(null);
            scene.RecordStepKey("z");

            Assert.Equal(["a", "b", "z"], scene.MacroTokens());
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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
            Assert.Equal(MacroInspectorPanelViewModel.RecordCaption, scene.Panel.RecordCommandCaption);

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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void TheRunArm_StillAppendsAtTheEndRegardlessOfTheSelection()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b");
            scene.SelectStep(0);

            scene.Record("c");

            Assert.Equal(["a", "b", "c"], scene.MacroTokens());
        }

        [Fact]
        public void TheBanner_NamesTheStepTheComposerIsPointedAt()
        {
            var scene = new Scene(this);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Record("a", "b", "c");
            scene.SelectStep(1);

            scene.Panel.RecordStepKeyCommand.Execute(null);

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildStepCaptureBanner("02"),
                scene.Panel.RecordingBanner);
        }

        [Fact]
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

        [Fact]
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
            Assert.Equal("[a]", scene.Panel.StepTokenText);
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
                Library = new MacroLibrary(Layout);

                _layer = KeyboardLayerViewModel.BuildAll(Layout, ResolveVisual(deviceId, Layout), null)[0];

                Panel = new MacroInspectorPanelViewModel(Device, owner._urlLauncher, () => Library);
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

            /// <summary>Whichever delay segment is lit.</summary>
            public MacroStepDelayMode SelectedDelayMode()
            {
                return Panel.StepDelayOptions.First(option => option.IsOn).Mode;
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
