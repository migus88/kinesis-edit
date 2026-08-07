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
            Assert.Equal(
                "Recording into step 04 — your typing goes here, not into the app. Esc stops.",
                MacroInspectorPanelViewModel.BuildRecordingBanner("04"));
            Assert.Equal(
                "Arrows = press/release. A bare modifier records as tap. Search and shortcuts are suspended until you stop.",
                MacroInspectorPanelViewModel.CaptureRule);
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

            private readonly KeyboardLayerViewModel _layer;

            public Scene(MacroInspectorPanelViewModelTests owner)
            {
                Device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
                Layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
                Library = new MacroLibrary(Layout);

                _layer = KeyboardLayerViewModel.BuildAll(
                    Layout,
                    Core.Geometry.Visual.VisualCatalog.FreestyleEdgeRgb,
                    null)[0];

                Panel = new MacroInspectorPanelViewModel(Device, owner._urlLauncher, () => Library);
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
        }
    }
}
