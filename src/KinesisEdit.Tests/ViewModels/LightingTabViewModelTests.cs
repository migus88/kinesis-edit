using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Lighting tab of specs/07-lighting.md §3/§4. Everything asserted here is a rule the
    /// panel reads off Core — mode membership, the per-mode panel matrix, the firmware gates, the
    /// zone key sets, the black-clears-the-key contract — so a Core change that moved one of them
    /// shows up as a failure here rather than as a silently wrong editor.
    /// </summary>
    public sealed class LightingTabViewModelTests
    {
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeAppPreferencesStore _preferences = new();

        [Fact]
        public void IsSupported_ForTheFreestyleEdgeRgb_IsTrueAndForEveryOtherBoardFalse()
        {
            // Per-key RGB without an edge strip is exactly the board whose led file is the plain
            // two-layer key-backlight model this panel edits.
            Assert.True(IsSupported(DeviceId.FreestyleEdgeRgb));
            Assert.False(IsSupported(DeviceId.Tko));
            Assert.False(IsSupported(DeviceId.Advantage360));
            Assert.False(IsSupported(DeviceId.FreestylePro));
            Assert.False(IsSupported(DeviceId.Advantage2));
            Assert.False(IsSupported(DeviceId.FreestyleEdge));
        }

        [Fact]
        public void Modes_ForCurrentFirmware_AreTheThirteenEffectsThenDisable()
        {
            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.121", ledFirmware: "1.0.58"));

            Assert.Equal(14, tab.Modes.Count);
            Assert.Equal(LightingMode.Disabled, tab.Modes[^1].Mode);
            Assert.Equal("Disable", tab.Modes[^1].Caption);
            Assert.DoesNotContain(LightingMode.PitchBlack, tab.Modes.Select(mode => mode.Mode));
            Assert.DoesNotContain(LightingMode.FrozenWave, tab.Modes.Select(mode => mode.Mode));
            Assert.Contains(LightingMode.Ripple, tab.Modes.Select(mode => mode.Mode));
            Assert.Contains(LightingMode.Fireball, tab.Modes.Select(mode => mode.Mode));
        }

        [Fact]
        public void Modes_BelowTheRippleAndFireballGate_OmitThoseTwo()
        {
            // specs/07-lighting.md §3: KBD ≥ 1.0.121 and LED ≥ 1.0.58.
            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.120", ledFirmware: "1.0.58"));

            Assert.Equal(12, tab.Modes.Count);
            Assert.DoesNotContain(LightingMode.Ripple, tab.Modes.Select(mode => mode.Mode));
            Assert.DoesNotContain(LightingMode.Fireball, tab.Modes.Select(mode => mode.Mode));
        }

        [Fact]
        public void Modes_InDemoMode_AreAllOffered()
        {
            var tab = CreateAttachedTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            Assert.Contains(LightingMode.Ripple, tab.Modes.Select(mode => mode.Mode));
            Assert.Contains(LightingMode.Fireball, tab.Modes.Select(mode => mode.Mode));
        }

        [Theory]
        [InlineData(LightingMode.Disabled, false, false, false, false, false)]
        [InlineData(LightingMode.Freestyle, true, false, false, false, true)]
        [InlineData(LightingMode.Monochrome, true, false, false, false, false)]
        [InlineData(LightingMode.Breathe, true, false, true, false, true)]
        [InlineData(LightingMode.Spectrum, false, false, true, false, false)]
        [InlineData(LightingMode.Wave, false, false, true, true, false)]
        [InlineData(LightingMode.Reactive, true, true, true, false, false)]
        [InlineData(LightingMode.Ripple, true, true, true, false, false)]
        [InlineData(LightingMode.Fireball, true, true, true, false, false)]
        [InlineData(LightingMode.Starlight, true, true, true, false, false)]
        [InlineData(LightingMode.Rebound, true, true, true, true, false)]
        [InlineData(LightingMode.Loop, true, true, true, true, false)]
        [InlineData(LightingMode.Pulse, false, false, true, false, false)]
        [InlineData(LightingMode.Rain, true, true, true, false, false)]
        public void Panels_ForEachMode_MatchTheSpecTable(
            LightingMode mode,
            bool showsEffectColor,
            bool showsBaseColor,
            bool showsSpeed,
            bool showsDirection,
            bool showsPerKeyColors)
        {
            // The §3 "Which parameter panels each mode shows" table, with the Fireball row's
            // "no direction UI on RGB" already applied.
            var panels = LightingPanelVisibility.For(
                DeviceId.FreestyleEdgeRgb,
                mode,
                isLayerCustomizationAvailable: true);

            Assert.Equal(showsEffectColor, panels.ShowsEffectColor);
            Assert.Equal(showsBaseColor, panels.ShowsBaseColor);
            Assert.Equal(showsSpeed, panels.ShowsSpeed);
            Assert.Equal(showsDirection, panels.ShowsDirection);
            Assert.Equal(showsPerKeyColors, panels.ShowsPerKeyColors);
            Assert.Equal(showsPerKeyColors, panels.ShowsZones);
            Assert.Equal(showsPerKeyColors, panels.ShowsResetAll);
        }

        [Fact]
        public void Panels_BelowTheLayerCustomizationGate_HideTheBaseColour()
        {
            var gated = LightingPanelVisibility.For(
                DeviceId.FreestyleEdgeRgb,
                LightingMode.Reactive,
                isLayerCustomizationAvailable: false);

            Assert.True(gated.ShowsEffectColor);
            Assert.False(gated.ShowsBaseColor);
        }

        [Fact]
        public void Directions_ForRebound_AreRelabelledHorizontalAndVertical()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Rebound);

            Assert.Equal(
                new[] { LightingDirection.Left, LightingDirection.Up },
                tab.Directions.Select(direction => direction.Direction));
            Assert.Equal(
                new[] { LightingDirectionViewModel.HorizontalCaption, LightingDirectionViewModel.VerticalCaption },
                tab.Directions.Select(direction => direction.Caption));
        }

        [Fact]
        public void Directions_ForWave_AreTheFourArrows()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Wave);

            Assert.Equal(new[] { "Down", "Left", "Up", "Right" }, tab.Directions.Select(entry => entry.Caption));
        }

        [Fact]
        public void Directions_ForFireballOnTheRgb_AreEmpty()
        {
            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.121", ledFirmware: "1.0.58"));

            SelectMode(tab, LightingMode.Fireball);

            Assert.Empty(tab.Directions);
            Assert.False(tab.Panels.ShowsDirection);
        }

        [Fact]
        public void SelectDirectionCommand_WritesThroughToTheLayer()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Wave);
            tab.SelectDirectionCommand.Execute(tab.Directions.Single(entry => entry.Direction == LightingDirection.Right));

            Assert.Equal(LightingDirection.Right, lighting.TopLayer.Direction);
            Assert.True(tab.Directions.Single(entry => entry.Direction == LightingDirection.Right).IsSelected);
        }

        [Fact]
        public void SelectMode_WithADirectionTheModeRejects_NormalisesItToTheDefault()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.Direction = LightingDirection.Right;

            var tab = CreateAttachedTab(lighting: lighting);

            // Rebound accepts left/up only (§2.4 item 5); the file would be written as left, so
            // the control and the model agree on it up front.
            SelectMode(tab, LightingMode.Rebound);

            Assert.Equal(LightingDirection.Left, lighting.TopLayer.Direction);
        }

        [Fact]
        public void Speed_IsClampedAndWrittenThrough()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Breathe);

            tab.Speed = 7;

            Assert.Equal(7, lighting.TopLayer.Speed);

            tab.Speed = 42;

            Assert.Equal(LayerLightingState.MaximumSpeed, tab.Speed);
            Assert.Equal(LayerLightingState.MaximumSpeed, lighting.TopLayer.Speed);
        }

        [Fact]
        public void Picker_WhileTheEffectSwatchIsSelected_WritesTheEffectColour()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Monochrome);
            tab.Picker.Color = new LedColor(255, 128, 0);

            Assert.Equal(new LedColor(255, 128, 0), lighting.TopLayer.EffectColor);
            Assert.Equal("#FF8000", tab.EffectColor.ColorHex);
        }

        [Fact]
        public void SelectColorSlotCommand_ForTheBaseSwatch_PointsThePickerAtIt()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Reactive);

            Assert.True(tab.EffectColor.IsSelected);

            tab.SelectColorSlotCommand.Execute(tab.BaseColor);

            Assert.True(tab.BaseColor.IsSelected);
            Assert.False(tab.EffectColor.IsSelected);

            tab.Picker.Color = new LedColor(10, 20, 30);

            Assert.Equal(new LedColor(10, 20, 30), lighting.TopLayer.BaseColor);
            Assert.Equal(LedColor.DefaultEffectColor, lighting.TopLayer.EffectColor);
        }

        [Fact]
        public void SelectColorSlotCommand_ForAHiddenSwatch_IsRefused()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Monochrome);

            tab.SelectColorSlotCommand.Execute(tab.BaseColor);

            Assert.False(tab.BaseColor.IsSelected);
            Assert.True(tab.EffectColor.IsSelected);
        }

        [Fact]
        public void SelectLayerCommand_ForTheFnLayer_ReReadsEveryControlFromIt()
        {
            // The two layers are fully independent (specs/07-lighting.md §4).
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(CreateSnapshot(), lighting);

            SelectMode(tab, LightingMode.Wave);
            tab.Speed = 3;
            tab.SelectDirectionCommand.Execute(tab.Directions.Single(entry => entry.Direction == LightingDirection.Up));

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Same(tab.Layers[1], tab.SelectedLayer);
            Assert.Equal(LightingMode.Disabled, tab.SelectedMode);
            Assert.Equal(LayerLightingState.DefaultSpeed, tab.Speed);
            Assert.Empty(tab.Directions);

            SelectMode(tab, LightingMode.Monochrome);
            tab.Picker.Color = new LedColor(1, 2, 3);

            Assert.Equal(new LedColor(1, 2, 3), lighting.FnLayer.EffectColor);
            Assert.Equal(LightingMode.Wave, lighting.TopLayer.Mode);
            Assert.Equal(3, lighting.TopLayer.Speed);
            Assert.Equal(LightingDirection.Up, lighting.TopLayer.Direction);

            tab.SelectLayerCommand.Execute(tab.Layers[0]);

            Assert.Equal(LightingMode.Wave, tab.SelectedMode);
            Assert.Equal(3, tab.Speed);
        }

        [Fact]
        public void Layers_BelowTheLayerCustomizationGate_LeaveTheFnLayerUnreachable()
        {
            var tab = CreateAttachedTab(CreateSnapshot(ledFirmware: "1.0.43"));

            Assert.False(tab.IsLayerCustomizationAvailable);
            Assert.Equal(LightingTabViewModel.LayerCustomizationLockedHint, tab.LayerLockHint);
            Assert.True(tab.Layers[0].IsEnabled);
            Assert.False(tab.Layers[1].IsEnabled);

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Same(tab.Layers[0], tab.SelectedLayer);
        }

        [Fact]
        public void AssignKeyColorCommand_PaintsTheKeyAndItsStrip()
        {
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(0, 128, 255);
            tab.AssignKeyColorCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(
                new LedColor(0, 128, 255),
                lighting.TopLayer.KeyColors[TestLayouts.Gen1Key("1").Code]);
            Assert.Equal("#0080FF", boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ColorOverlayHex);
        }

        [Fact]
        public void AssignKeyColorCommand_WithBlack_ErasesTheKeyRatherThanStoringIt()
        {
            // SetKeyColor's contract (specs/07-lighting.md §2.1): black is "no colour".
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(255, 0, 0);
            tab.AssignKeyColorCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            tab.Picker.Color = LedColor.Black;
            tab.AssignKeyColorCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Empty(lighting.TopLayer.KeyColors);
            Assert.Null(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ColorOverlayHex);
        }

        [Fact]
        public void AssignKeyColorCommand_InAModeWithoutPerKeyColours_IsUnavailable()
        {
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Wave);

            Assert.False(tab.AssignKeyColorCommand.CanExecute(boards[0].Keys[0]));

            tab.AssignKeyColorCommand.Execute(boards[0].Keys[0]);

            Assert.Empty(lighting.TopLayer.KeyColors);
        }

        [Fact]
        public void Zones_ForTheFreestyleEdgeRgb_AreTheEightOfTheSpec()
        {
            var tab = CreateAttachedTab();

            Assert.Equal(
                new[] { "All", "Number", "Function", "WASD", "Game", "Arrow", "Left Module", "Right Module" },
                tab.Zones.Select(zone => zone.Caption));
        }

        [Fact]
        public void ApplyZoneCommand_PaintsEveryKeyCodeOfTheZone()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Breathe);
            tab.Picker.Color = new LedColor(12, 34, 56);

            var numbers = tab.Zones.Single(zone => zone.Caption == "Number");

            tab.ApplyZoneCommand.Execute(numbers);

            Assert.Equal(numbers.KeyCodes.Count, lighting.TopLayer.KeyColors.Count);
            Assert.All(
                numbers.KeyCodes,
                keyCode => Assert.Equal(new LedColor(12, 34, 56), lighting.TopLayer.KeyColors[keyCode]));
        }

        [Fact]
        public void ApplyZoneCommand_OnTheFnLayer_PaintsTheSamePhysicalPositions()
        {
            // Zone membership is authored against the top layer; on the Fn layer the same
            // positions carry different memory keys (specs/07-lighting.md §2.4 item 6).
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting, boards);
            tab.SelectLayerCommand.Execute(tab.Layers[1]);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(7, 7, 7);

            // The Function zone is the sharp case: positions 2-7 carry F1-F6 on the top layer and
            // the media keys on the Fn layer, so a zone applied verbatim would colour keys the Fn
            // layer does not have.
            var function = tab.Zones.Single(zone => zone.Caption == "Function");

            tab.ApplyZoneCommand.Execute(function);

            Assert.Equal(function.KeyCodes.Count, lighting.FnLayer.KeyColors.Count);
            Assert.Contains(TestLayouts.Gen1Key("mute").Code, lighting.FnLayer.KeyColors.Keys);
            Assert.Contains(TestLayouts.Gen1Key("next").Code, lighting.FnLayer.KeyColors.Keys);
            Assert.DoesNotContain(TestLayouts.Gen1Key("F1").Code, lighting.FnLayer.KeyColors.Keys);
            Assert.All(
                lighting.FnLayer.KeyColors.Keys,
                keyCode => Assert.NotNull(boards[1].Layer.FindByOriginalKeyCode(keyCode)));
            Assert.Empty(lighting.TopLayer.KeyColors);
        }

        [Fact]
        public async Task ResetAllCommand_WhenConfirmed_ErasesEveryKeyColour()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.ApplyZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));

            Assert.NotEmpty(lighting.TopLayer.KeyColors);

            await tab.ResetAllCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(LightingTabViewModel.ResetAllConfirmation, request.Message);
            Assert.Equal(MessageBoxButtons.YesNo, request.Buttons);
            Assert.Empty(lighting.TopLayer.KeyColors);
        }

        [Fact]
        public async Task ResetAllCommand_WhenDeclined_ChangesNothing()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.ApplyZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));

            await tab.ResetAllCommand.ExecuteAsync(null);

            Assert.Equal(4, lighting.TopLayer.KeyColors.Count);
        }

        [Fact]
        public async Task ResetAllCommand_InAModeWithoutPerKeyColours_IsUnavailable()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Spectrum);

            Assert.False(tab.ResetAllCommand.CanExecute(null));

            await tab.ResetAllCommand.ExecuteAsync(null);

            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public void Attach_WithoutALightingModel_FallsBackToAnInMemoryOne()
        {
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting: null, BuildBoards(new LightingModel()));

            Assert.Equal(2, tab.Layers.Count);

            SelectMode(tab, LightingMode.Pulse);

            Assert.Equal(LightingMode.Pulse, tab.SelectedMode);
            Assert.Equal(LightingMode.Pulse, tab.Layers[0].State.Mode);
        }

        [Fact]
        public void Attach_ForADeviceThePanelCannotEdit_DoesNothing()
        {
            var tab = new LightingTabViewModel(
                TestDevices.CreateSnapshot(DeviceId.Tko),
                _notifications,
                _preferences);

            tab.Attach(new TkoLightingModel(), []);

            Assert.False(tab.IsAvailable);
            Assert.Empty(tab.Layers);
            Assert.Null(tab.SelectedLayer);
        }

        [Fact]
        public void EveryEdit_SerialisedAndReparsed_SurvivesTheRoundTrip()
        {
            // The acceptance criterion: what the panel writes into the model has to be expressible
            // in led<n>.txt. ProfileSession.Save is exactly SerializeRgb + a file write, so this is
            // the app-layer half of "a lighting edit survives Save → reload".
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting, boards);

            // Top layer: a two-line effect with an effect colour, a base colour, a speed and a
            // direction.
            SelectMode(tab, LightingMode.Loop);
            tab.Picker.Color = new LedColor(255, 128, 0);
            tab.SelectColorSlotCommand.Execute(tab.BaseColor);
            tab.Picker.Color = new LedColor(0, 0, 64);
            tab.Speed = 8;
            tab.SelectDirectionCommand.Execute(tab.Directions.Single(entry => entry.Direction == LightingDirection.Right));

            // Fn layer: a per-key mode with a hand-painted key and a zone fill.
            tab.SelectLayerCommand.Execute(tab.Layers[1]);
            SelectMode(tab, LightingMode.Breathe);
            tab.Speed = 2;
            tab.Picker.Color = new LedColor(10, 200, 30);
            tab.AssignKeyColorCommand.Execute(boards[1].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            // The Function zone lands on the Fn layer's media keys, which are exactly the eight
            // save-token exceptions of specs/07-lighting.md §2.4 item 7 — the hardest thing in the
            // file to round-trip.
            tab.ApplyZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "Function"));

            var reloaded = LedFileParser.ParseRgb(LedFileSerializer.SerializeRgb(lighting));

            Assert.True(lighting.TopLayer.IsEquivalentTo(reloaded.TopLayer));
            Assert.Equal(LightingMode.Loop, reloaded.TopLayer.Mode);
            Assert.Equal(new LedColor(255, 128, 0), reloaded.TopLayer.EffectColor);
            Assert.Equal(new LedColor(0, 0, 64), reloaded.TopLayer.BaseColor);
            Assert.Equal(8, reloaded.TopLayer.Speed);
            Assert.Equal(LightingDirection.Right, reloaded.TopLayer.Direction);

            Assert.Equal(LightingMode.Breathe, reloaded.FnLayer.Mode);
            Assert.Equal(2, reloaded.FnLayer.Speed);
            Assert.Equal(13, reloaded.FnLayer.KeyColors.Count);
            Assert.Equal(lighting.FnLayer.KeyColors.Count, reloaded.FnLayer.KeyColors.Count);
            Assert.All(
                lighting.FnLayer.KeyColors,
                entry => Assert.Equal(entry.Value, reloaded.FnLayer.KeyColors[entry.Key]));

            // The media keys came back through the §2.4 item 7 exception table: mute was written
            // as [F1] and read back as mute.
            Assert.Contains(TestLayouts.Gen1Key("mute").Code, reloaded.FnLayer.KeyColors.Keys);

            // The one thing that is deliberately not file state: a per-key mode writes no effect
            // colour line (§2.2), so the swatch is back at its default after a reload.
            Assert.Equal(new LedColor(10, 200, 30), lighting.FnLayer.EffectColor);
            Assert.Equal(LedColor.DefaultEffectColor, reloaded.FnLayer.EffectColor);
        }

        private static bool IsSupported(DeviceId deviceId)
        {
            return LightingTabViewModel.IsSupported(DeviceCatalog.GetById(deviceId));
        }

        private static void SelectMode(LightingTabViewModel tab, LightingMode mode)
        {
            tab.SelectModeCommand.Execute(tab.Modes.Single(entry => entry.Mode == mode));
        }

        private static DeviceSnapshot CreateSnapshot(
            string keyboardFirmware = "1.0.121",
            string ledFirmware = "1.0.58")
        {
            return TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                versionFile: TestDevices.CreateVersionFile(
                    DeviceId.FreestyleEdgeRgb,
                    keyboardFirmware,
                    ledFirmware));
        }

        private static IReadOnlyList<KeyboardLayerViewModel> BuildBoards(LightingModel lighting)
        {
            return KeyboardLayerViewModel.BuildAll(
                KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb),
                VisualCatalog.FreestyleEdgeRgb,
                lighting);
        }

        private LightingTabViewModel CreateTab(DeviceSnapshot? snapshot = null)
        {
            return new LightingTabViewModel(
                snapshot ?? CreateSnapshot(),
                _notifications,
                _preferences);
        }

        private LightingTabViewModel CreateAttachedTab(
            DeviceSnapshot? snapshot = null,
            LightingModel? lighting = null)
        {
            var model = lighting ?? new LightingModel();
            var tab = CreateTab(snapshot);

            tab.Attach(model, BuildBoards(model));

            return tab;
        }
    }
}
