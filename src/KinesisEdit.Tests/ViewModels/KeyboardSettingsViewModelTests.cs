using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The editor's Settings tab (specs/08-settings.md §5): the rows a device's capability
    /// designates, the values it round-trips, the Advantage2 4MB lock, and the demo-mode rule
    /// that nothing is ever written.
    /// </summary>
    public sealed class KeyboardSettingsViewModelTests
    {
        private readonly FakeSettingsService _settings = new();
        private readonly FakeNotificationService _notifications = new();

        [Fact]
        public void Rows_ForAPerKeyRgbBoard_AreTheKeysSpecEightSaysItWrites()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            Assert.Equal(
                new[]
                {
                    KeyboardSettingsRows.StartupProfileCaption,
                    KeyboardSettingsRows.MacroSpeedCaption,
                    KeyboardSettingsRows.StatusPlaySpeedCaption,
                    KeyboardSettingsRows.VDriveAutoMountCaption,
                    KeyboardSettingsRows.GameModeCaption
                },
                panel.Rows.Select(row => row.Caption));
        }

        [Fact]
        public void Rows_ForTheFreestylePro_HaveNoGameModeSwitch()
        {
            // Spec 08 §5.3 / spec 02: the Pro has no game mode, so its switch is not merely
            // disabled — it is absent.
            var panel = Create(DeviceId.FreestylePro);

            Assert.DoesNotContain(KeyboardSettingsRows.GameModeCaption, panel.Rows.Select(row => row.Caption));
            Assert.Contains(KeyboardSettingsRows.MacroSpeedCaption, panel.Rows.Select(row => row.Caption));
        }

        [Fact]
        public void Rows_ForTheAdvantage2_CarryTheTwoTonesAndTheOpenOnStartupSwitch()
        {
            var panel = Create(DeviceId.Advantage2, versionFile: CreateFourMegabyteVersionFile());

            Assert.Equal(
                new[]
                {
                    KeyboardSettingsRows.MacroSpeedCaption,
                    KeyboardSettingsRows.StatusPlaySpeedCaption,
                    KeyboardSettingsRows.VDriveOpenOnStartupCaption,
                    KeyboardSettingsRows.KeyClickToneCaption,
                    KeyboardSettingsRows.ToggleToneCaption
                },
                panel.Rows.Select(row => row.Caption));
        }

        [Fact]
        public void Rows_ForTheAdvantage360_CarryTheProfileStatusAndLockOnly()
        {
            var panel = Create(DeviceId.Advantage360);

            Assert.Equal(
                new[]
                {
                    KeyboardSettingsRows.StartupProfileCaption,
                    KeyboardSettingsRows.StatusPlaySpeedCaption,
                    KeyboardSettingsRows.ProgramLockCaption
                },
                panel.Rows.Select(row => row.Caption));
        }

        [Fact]
        public void Rows_ForTheFreestyleEdge_CarryTheLedModeChoice()
        {
            var panel = Create(DeviceId.FreestyleEdge);

            var ledMode = Assert.IsType<SettingsChoiceRowViewModel>(FindRow(panel, KeyboardSettingsRows.LedModeCaption));

            // The option set is Core's LedModeValues — the very domain the serializer throws
            // outside of — so the picker cannot offer a value a save would refuse.
            Assert.Equal(LedModeValues.All, ledMode.Options.Select(option => option.Value));
            Assert.Equal(KeyboardSettingsRows.PitchBlackCaption, ledMode.Options[10].Caption);
            Assert.Equal(KeyboardSettingsRows.BreatheCaption, ledMode.Options[11].Caption);
        }

        [Fact]
        public void Rows_ForAPerKeyRgbBoard_HaveNoSeparateLedModeRow()
        {
            // On an led-file device led_mode is the startup profile's paired file (spec 08 §5.1),
            // written by StartupProfileSettings — never by a control of its own.
            var panel = Create(DeviceId.Tko);

            Assert.DoesNotContain(KeyboardSettingsRows.LedModeCaption, panel.Rows.Select(row => row.Caption));
        }

        [Fact]
        public void Rows_ForADeviceWithoutASettingsFile_AreEmpty()
        {
            Assert.Empty(Create(DeviceId.SavantElite2).Rows);
            Assert.Empty(Create(DeviceId.Advantage360Professional, VDriveConnectionStatus.NotDetected).Rows);
        }

        [Fact]
        public async Task LoadAsync_FillsEveryRowFromTheFile()
        {
            _settings.KeyboardSettingsToReturn = new KeyboardSettings
            {
                StartupProfileNumber = 4,
                MacroSpeed = 6,
                StatusPlaySpeed = 3,
                IsVDriveAutoMountEnabled = true,
                IsGameModeEnabled = true
            };

            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            Assert.Equal(1, _settings.LoadKeyboardCallCount);
            Assert.Equal(4, Slider(panel, KeyboardSettingsRows.StartupProfileCaption).Value);
            Assert.Equal(6, Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).Value);
            Assert.Equal(3, Slider(panel, KeyboardSettingsRows.StatusPlaySpeedCaption).Value);
            Assert.True(Toggle(panel, KeyboardSettingsRows.VDriveAutoMountCaption).IsOn);
            Assert.True(Toggle(panel, KeyboardSettingsRows.GameModeCaption).IsOn);
            Assert.False(panel.IsLoading);
            Assert.False(panel.HasStatusMessage);
        }

        [Fact]
        public async Task LoadAsync_WithAZeroSpeed_ReadsItAsDisabled()
        {
            // 0 means "disabled" for both speed keys (spec 08 §2); on an RGB board the slider
            // floor is 1, so the value can only be expressed by the checkbox of §5.1.
            _settings.KeyboardSettingsToReturn = new KeyboardSettings { MacroSpeed = 0, StatusPlaySpeed = 0 };

            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            Assert.True(Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).IsDisabled);
            Assert.True(Slider(panel, KeyboardSettingsRows.StatusPlaySpeedCaption).IsDisabled);
            Assert.Equal(SettingsSliderRowViewModel.DisabledValueCaption, Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).ValueCaption);
        }

        [Fact]
        public async Task LoadAsync_WithAnAbsentKey_LeavesTheRowAtItsUnsetDefault()
        {
            // Spec 08 §2 notes: "missing keys leave zero/false/empty values"; the profile number
            // has no zero, so it rests at the slider floor.
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            Assert.Equal(SettingsValueRanges.MinimumProfileNumber, Slider(panel, KeyboardSettingsRows.StartupProfileCaption).Value);
            Assert.True(Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).IsDisabled);
            Assert.False(Toggle(panel, KeyboardSettingsRows.GameModeCaption).IsOn);
        }

        [Fact]
        public async Task LoadAsync_CalledTwice_ReadsTheFileOnce()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();
            await panel.LoadAsync();

            Assert.Equal(1, _settings.LoadKeyboardCallCount);
        }

        [Fact]
        public async Task LoadAsync_WhenTheFileCannotBeRead_ReportsItInlineAndRefusesToSave()
        {
            // The rows still hold their constructor defaults, which are this panel's invention and
            // not the device's settings — saving them would write v_drive=manual, macro_speed=1
            // and game_mode=OFF over a file nobody managed to read.
            _settings.LoadKeyboardExceptionToThrow = new IOException("the v-Drive went away");

            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            Assert.True(panel.HasStatusMessage);
            Assert.Contains("the v-Drive went away", panel.StatusMessage, StringComparison.Ordinal);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.False(panel.IsLoading);
            Assert.False(panel.HasLoadedSettings);
            Assert.False(panel.SaveCommand.CanExecute(null));
            Assert.All(panel.Rows, row => Assert.False(row.IsEnabled));

            await panel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(_settings.KeyboardSaves);
        }

        [Fact]
        public async Task LoadAsync_InDemoMode_StillReadsTheFile()
        {
            // Demo mode is entered for a drive that is merely not writable (03 §3.5); spec 08 §3
            // bans saving there, not loading, and the colour picker's custom slots already load
            // the same way. Inventing the values instead would show wrong settings for real,
            // connected hardware.
            _settings.KeyboardSettingsToReturn = new KeyboardSettings { MacroSpeed = 6, IsGameModeEnabled = true };

            var panel = Create(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess);

            await panel.LoadAsync();

            Assert.Equal(1, _settings.LoadKeyboardCallCount);
            Assert.True(panel.HasLoadedSettings);
            Assert.Equal(6, Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).Value);
            Assert.True(Toggle(panel, KeyboardSettingsRows.GameModeCaption).IsOn);
            Assert.Equal(KeyboardSettingsViewModel.DemoModeHint, panel.StatusMessage);
            Assert.False(panel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task LoadAsync_WithoutADrive_ReadsNothingAndSaysSoWithoutClaimingTheRowsAreTheDevices()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.NotDetected);

            await panel.LoadAsync();

            Assert.Equal(0, _settings.LoadKeyboardCallCount);
            Assert.False(panel.HasLoadedSettings);
            Assert.Equal(KeyboardSettingsViewModel.NoDriveHint, panel.StatusMessage);
            Assert.All(panel.Rows, row => Assert.False(row.IsEnabled));
            Assert.False(panel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task SaveCommand_InDemoMode_IsUnavailableAndWritesNothing()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess);

            await panel.LoadAsync();
            await panel.SaveCommand.ExecuteAsync(null);

            Assert.False(panel.SaveCommand.CanExecute(null));
            Assert.Empty(_settings.KeyboardSaves);
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task SaveCommand_WithoutADrive_IsUnavailable()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.NotDetected);

            await panel.LoadAsync();

            Assert.False(panel.SaveCommand.CanExecute(null));
            Assert.Equal(0, _settings.LoadKeyboardCallCount);
            Assert.Empty(_settings.KeyboardSaves);
        }

        [Fact]
        public async Task SaveCommand_WritesEveryRowAndShowsTheSettingsSavedNotice()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            Slider(panel, KeyboardSettingsRows.StartupProfileCaption).Value = 3;
            Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).IsDisabled = false;
            Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).Value = 8;
            Slider(panel, KeyboardSettingsRows.StatusPlaySpeedCaption).IsDisabled = true;
            Toggle(panel, KeyboardSettingsRows.GameModeCaption).IsOn = true;

            await panel.SaveCommand.ExecuteAsync(null);

            var save = Assert.Single(_settings.KeyboardSaves);

            Assert.Equal(3, save.Settings.StartupProfileNumber);
            // The paired led file of spec 08 §5.1 / 07 §1.2, applied by Core's helper.
            Assert.Equal("led3.txt", save.Settings.LedMode);
            Assert.Equal(8, save.Settings.MacroSpeed);
            Assert.Equal(0, save.Settings.StatusPlaySpeed);
            Assert.True(save.Settings.IsGameModeEnabled);

            var toast = Assert.Single(_notifications.Toasts);

            Assert.Equal(SettingsMessageCatalog.SettingsSavedTitle, toast.Title);
            Assert.Equal(SettingsMessageCatalog.SettingsSavedMessage, toast.Message);
            Assert.Null(_notifications.LoadingCaption);
            Assert.False(panel.IsBusy);
        }

        [Fact]
        public async Task SaveCommand_OnADeviceWithoutTheStartupKey_WritesNoProfileOrLedMode()
        {
            // StartupProfileSettings leaves FS Edge/Pro and Adv2 settings untouched: they have no
            // startup-profile key at all (spec 08 §2). led_mode stays null because the file
            // carried none — writing the picker's first option ("0") would switch the backlight
            // off on the first save.
            var panel = Create(DeviceId.FreestyleEdge);

            await panel.LoadAsync();
            await panel.SaveCommand.ExecuteAsync(null);

            var save = Assert.Single(_settings.KeyboardSaves);

            Assert.Null(save.Settings.StartupProfileNumber);
            Assert.Null(save.Settings.LedMode);
            Assert.True(LedMode(panel).IsUnset);
            Assert.Equal(SettingsChoiceRowViewModel.UnsetCaption, LedMode(panel).Placeholder);
        }

        [Fact]
        public async Task LedMode_ThatThisAppDoesNotKnow_IsKeptRatherThanOverwritten()
        {
            // A value newer firmware introduced must survive a save made by this app. Matching no
            // option leaves the row unset, and unset writes *null*: a null property emits no pair,
            // a save only passes managed pairs to UpdateSettingsFile, so the device's own
            // led_mode line survives verbatim (spec 08 §1). Writing the unknown text back instead
            // would make the serializer throw and the panel unsavable.
            _settings.KeyboardSettingsToReturn = new KeyboardSettings { LedMode = "X" };

            var panel = Create(DeviceId.FreestyleEdge);

            await panel.LoadAsync();

            Assert.True(LedMode(panel).IsUnset);
            Assert.Equal("X", LedMode(panel).UnrecognizedValue);
            Assert.Contains("X", LedMode(panel).Placeholder, StringComparison.Ordinal);

            await panel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(_settings.KeyboardSaves).Settings;

            Assert.Null(saved.LedMode);

            // The real serializer must accept the saved model — and emit no led_mode key.
            Assert.DoesNotContain(Serialize(DeviceId.FreestyleEdge, saved), line => line.StartsWith("led_mode=", StringComparison.Ordinal));
        }

        [Fact]
        public async Task SaveCommand_AfterAnUnknownLedModeWasRead_ReachesTheRealServiceWithoutThrowing()
        {
            // End to end through the real SettingsService seam: the unknown value must not reach
            // KeyboardSettingsSerializer, which throws on anything outside LedModeValues.
            var fileService = new FakeVDriveFileService();
            var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdge);

            fileService.SetFile(device.Location!.SettingsFilePath, "led_mode=X", "macro_speed=5");

            var panel = new KeyboardSettingsViewModel(
                device,
                TestDevices.CreateSettingsService(fileService),
                _notifications);

            await panel.LoadAsync();
            await panel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(_notifications.MessageBoxes);
            Assert.DoesNotContain(fileService.SettingsUpdates, pair => pair.Key == SettingsKeys.LedMode);
            Assert.Contains(fileService.SettingsUpdates, pair => pair.Key == SettingsKeys.MacroSpeed && pair.Value == "5");
        }

        [Fact]
        public async Task LedMode_ChosenAfterAnUnknownValueWasRead_ReplacesIt()
        {
            _settings.KeyboardSettingsToReturn = new KeyboardSettings { LedMode = "X" };

            var panel = Create(DeviceId.FreestyleEdge);

            await panel.LoadAsync();

            LedMode(panel).SelectedOption = LedMode(panel).Options.Single(option => option.Value == LedModeValues.Breathe);

            Assert.False(LedMode(panel).IsUnset);
            Assert.Null(LedMode(panel).UnrecognizedValue);

            await panel.SaveCommand.ExecuteAsync(null);

            Assert.Equal(LedModeValues.Breathe, Assert.Single(_settings.KeyboardSaves).Settings.LedMode);
        }

        [Fact]
        public async Task LedMode_ReadFromTheFile_SelectsTheMatchingOptionCaseInsensitively()
        {
            _settings.KeyboardSettingsToReturn = new KeyboardSettings { LedMode = "b" };

            var panel = Create(DeviceId.FreestyleEdge);

            await panel.LoadAsync();

            Assert.Equal(LedModeValues.Breathe, LedMode(panel).SelectedOption?.Value);
        }

        [Fact]
        public async Task SaveCommand_ClampsAValueTheSerializerWouldRefuse()
        {
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            var profile = Slider(panel, KeyboardSettingsRows.StartupProfileCaption);
            var macroSpeed = Slider(panel, KeyboardSettingsRows.MacroSpeedCaption);

            profile.Value = 99;
            macroSpeed.IsDisabled = false;
            macroSpeed.Value = -4;

            await panel.SaveCommand.ExecuteAsync(null);

            var save = Assert.Single(_settings.KeyboardSaves);

            Assert.Equal(SettingsValueRanges.MaximumProfileNumber, save.Settings.StartupProfileNumber);
            Assert.Equal(1, save.Settings.MacroSpeed);

            // The clamped model must actually survive the serializer that would have thrown.
            KeyboardSettingsSerializer.Serialize(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings, save.Settings);
        }

        [Fact]
        public async Task SaveCommand_WhenTheWriteThrows_ReportsItAsAMessageBox()
        {
            _settings.SaveKeyboardExceptionToThrow = new IOException("the v-Drive went away");

            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();
            await panel.SaveCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardSettingsViewModel.SaveTitle, request.Title);
            Assert.Contains("the v-Drive went away", request.Message, StringComparison.Ordinal);
            Assert.Empty(_notifications.Toasts);
            Assert.Null(_notifications.LoadingCaption);
            Assert.False(panel.IsBusy);
        }

        [Fact]
        public async Task SaveCommand_WhenTheMessageBoxCannotBeShown_DoesNotThrow()
        {
            _settings.SaveKeyboardExceptionToThrow = new IOException("the v-Drive went away");
            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no owner window");

            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();
            await panel.SaveCommand.ExecuteAsync(null);

            Assert.False(panel.IsBusy);
        }

        [Fact]
        public async Task SaveCommand_PassesTheSnapshotsVersionFileToTheGate()
        {
            var versionFile = CreateFourMegabyteVersionFile();
            var panel = Create(DeviceId.Advantage2, versionFile: versionFile);

            await panel.LoadAsync();
            await panel.SaveCommand.ExecuteAsync(null);

            Assert.Same(versionFile, Assert.Single(_settings.KeyboardSaves).VersionFile);
        }

        [Fact]
        public async Task Advantage2_WithoutTheFourMegabyteMarker_IsLockedWithCoresHint()
        {
            var panel = Create(DeviceId.Advantage2);

            await panel.LoadAsync();

            Assert.True(panel.IsLocked);
            Assert.False(panel.CanEditSettings);
            Assert.Equal(SettingsMessageCatalog.Advantage2SettingsDisabledHint, panel.LockHint);
            Assert.All(panel.Rows, row => Assert.False(row.IsEnabled));
            Assert.False(panel.SaveCommand.CanExecute(null));

            await panel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(_settings.KeyboardSaves);
        }

        [Fact]
        public async Task Advantage2_InDemoMode_IsNotLockedByTheAbsentMarker()
        {
            // Demo mode supplies VersionFileInfo.Empty — no 4MB marker, because no keyboard was
            // read. Every firmware gate passes in demo mode (FirmwareGateService, spec 09 §2), and
            // explaining a 2MB lock about a board that is not attached is a fabricated diagnosis.
            // Nothing can be written there anyway.
            var panel = Create(DeviceId.Advantage2, VDriveConnectionStatus.NotDetected);

            await panel.LoadAsync();

            Assert.True(panel.CanEditSettings);
            Assert.False(panel.IsLocked);
            Assert.Equal(string.Empty, panel.LockHint);
            Assert.False(panel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task Advantage2_WithTheFourMegabyteMarker_IsEditable()
        {
            var panel = Create(DeviceId.Advantage2, versionFile: CreateFourMegabyteVersionFile());

            await panel.LoadAsync();

            Assert.False(panel.IsLocked);
            Assert.Equal(string.Empty, panel.LockHint);
            Assert.All(panel.Rows, row => Assert.True(row.IsEnabled));
            Assert.True(panel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public void SaveCommand_BeforeTheFileHasBeenRead_IsUnavailable()
        {
            // Saving the panel's defaults over a file nobody has read yet would silently reset the
            // device's settings.
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            Assert.True(panel.IsLoading);
            Assert.False(panel.SaveCommand.CanExecute(null));
            Assert.All(panel.Rows, row => Assert.False(row.IsEnabled));
        }

        [Fact]
        public async Task EveryEdit_SerialisedAndReparsed_SurvivesTheRoundTrip()
        {
            // The acceptance criterion: what the panel writes into the model has to be expressible
            // in the device's settings file and read back as the same thing. SettingsService.Save
            // is exactly Serialize + UpdateSettingsFile, so this is the app-layer half of "a
            // settings edit survives Save → reload".
            var panel = Create(DeviceId.FreestyleEdgeRgb);

            await panel.LoadAsync();

            Slider(panel, KeyboardSettingsRows.StartupProfileCaption).Value = 3;
            Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).IsDisabled = false;
            Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).Value = 7;
            Slider(panel, KeyboardSettingsRows.StatusPlaySpeedCaption).IsDisabled = false;
            Slider(panel, KeyboardSettingsRows.StatusPlaySpeedCaption).Value = 2;
            Toggle(panel, KeyboardSettingsRows.VDriveAutoMountCaption).IsOn = false;
            Toggle(panel, KeyboardSettingsRows.GameModeCaption).IsOn = true;

            await panel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(_settings.KeyboardSaves).Settings;
            var lines = Serialize(DeviceId.FreestyleEdgeRgb, saved);

            // The asymmetric pair of spec 08 §2/§5.1: the active profile leaves as a *file name*
            // together with its paired led file, and comes back as a number through the shared
            // file-number logic.
            Assert.Contains("startup_file=layout3.txt", lines);
            Assert.Contains("led_mode=led3.txt", lines);
            Assert.Contains("v_drive=manual", lines);

            var reloaded = KeyboardSettingsParser.Parse(lines);

            Assert.Equal(3, reloaded.StartupProfileNumber);
            Assert.Equal("led3.txt", reloaded.LedMode);
            Assert.Equal(7, reloaded.MacroSpeed);
            Assert.Equal(2, reloaded.StatusPlaySpeed);
            Assert.False(reloaded.IsVDriveAutoMountEnabled);
            Assert.True(reloaded.IsGameModeEnabled);

            // And the reloaded model puts every row back where the user left it.
            _settings.KeyboardSettingsToReturn = reloaded;

            var reopened = Create(DeviceId.FreestyleEdgeRgb);

            await reopened.LoadAsync();

            Assert.Equal(3, Slider(reopened, KeyboardSettingsRows.StartupProfileCaption).Value);
            Assert.Equal(7, Slider(reopened, KeyboardSettingsRows.MacroSpeedCaption).Value);
            Assert.Equal(2, Slider(reopened, KeyboardSettingsRows.StatusPlaySpeedCaption).Value);
            Assert.False(Toggle(reopened, KeyboardSettingsRows.VDriveAutoMountCaption).IsOn);
            Assert.True(Toggle(reopened, KeyboardSettingsRows.GameModeCaption).IsOn);
        }

        [Fact]
        public async Task EveryEdit_OnTheModeStringDevice_SurvivesTheRoundTrip()
        {
            // The other led_mode form (spec 08 §2's dual typing): the Freestyle Edge writes the
            // brightness/mode string the picker offers, not a file name.
            var panel = Create(DeviceId.FreestyleEdge);

            await panel.LoadAsync();

            LedMode(panel).SelectedOption = LedMode(panel).Options.Single(option => option.Value == LedModeValues.PitchBlack);
            Slider(panel, KeyboardSettingsRows.MacroSpeedCaption).Value = 4;

            await panel.SaveCommand.ExecuteAsync(null);

            var lines = Serialize(DeviceId.FreestyleEdge, Assert.Single(_settings.KeyboardSaves).Settings);

            Assert.Contains("led_mode=P", lines);
            Assert.DoesNotContain(lines, line => line.StartsWith("startup_file=", StringComparison.Ordinal));

            var reloaded = KeyboardSettingsParser.Parse(lines);

            _settings.KeyboardSettingsToReturn = reloaded;

            var reopened = Create(DeviceId.FreestyleEdge);

            await reopened.LoadAsync();

            Assert.Equal(LedModeValues.PitchBlack, LedMode(reopened).SelectedOption?.Value);
            Assert.Equal(4, Slider(reopened, KeyboardSettingsRows.MacroSpeedCaption).Value);
        }

        [Fact]
        public void EveryOtherDevice_IsNeverLocked()
        {
            // The gate is Advantage2-only (specs/09-firmware.md §1.1).
            Assert.False(Create(DeviceId.FreestyleEdgeRgb).IsLocked);
            Assert.False(Create(DeviceId.Tko).IsLocked);
            Assert.False(Create(DeviceId.Advantage360).IsLocked);
        }

        [Fact]
        public void HasSettings_FollowsTheDevicesCapability()
        {
            Assert.True(KeyboardSettingsViewModel.HasSettings(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb)));
            Assert.True(KeyboardSettingsViewModel.HasSettings(DeviceCatalog.GetById(DeviceId.Advantage360)));
            Assert.False(KeyboardSettingsViewModel.HasSettings(DeviceCatalog.GetById(DeviceId.SavantElite2)));
            Assert.False(KeyboardSettingsViewModel.HasSettings(DeviceCatalog.GetById(DeviceId.CrossfireKeypad)));
        }

        [Fact]
        public void Constructor_WithoutACollaborator_Throws()
        {
            var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);

            Assert.Throws<ArgumentNullException>(() => new KeyboardSettingsViewModel(null!, _settings, _notifications));
            Assert.Throws<ArgumentNullException>(() => new KeyboardSettingsViewModel(device, null!, _notifications));
            Assert.Throws<ArgumentNullException>(() => new KeyboardSettingsViewModel(device, _settings, null!));
        }

        private static VersionFileInfo CreateFourMegabyteVersionFile()
        {
            return VersionFileParser.Parse(
                DeviceId.Advantage2,
                ["Model Name: Advantage2", "Firmware Version: 1.0.1709.us (4MB), 03/08/2019"]);
        }

        private static SettingsRowViewModel FindRow(KeyboardSettingsViewModel panel, string caption)
        {
            return Assert.Single(panel.Rows, row => row.Caption == caption);
        }

        private static SettingsSliderRowViewModel Slider(KeyboardSettingsViewModel panel, string caption)
        {
            return Assert.IsType<SettingsSliderRowViewModel>(FindRow(panel, caption));
        }

        private static SettingsToggleRowViewModel Toggle(KeyboardSettingsViewModel panel, string caption)
        {
            return Assert.IsType<SettingsToggleRowViewModel>(FindRow(panel, caption));
        }

        private static SettingsChoiceRowViewModel LedMode(KeyboardSettingsViewModel panel)
        {
            return Assert.IsType<SettingsChoiceRowViewModel>(FindRow(panel, KeyboardSettingsRows.LedModeCaption));
        }

        /// <summary>
        /// The file lines the real serializer produces for <paramref name="settings"/> — the same
        /// <c>key=value</c> form <c>UpdateSettingsFile</c> writes, so the parser can read them back.
        /// </summary>
        private static string[] Serialize(DeviceId deviceId, KeyboardSettings settings)
        {
            return KeyboardSettingsSerializer
                .Serialize(DeviceCatalog.GetById(deviceId).Settings, settings)
                .Select(pair => pair.Key + "=" + pair.Value)
                .ToArray();
        }

        private KeyboardSettingsViewModel Create(
            DeviceId deviceId,
            VDriveConnectionStatus status = VDriveConnectionStatus.Connected,
            VersionFileInfo? versionFile = null)
        {
            return new KeyboardSettingsViewModel(
                TestDevices.CreateSnapshot(deviceId, status, versionFile: versionFile),
                _settings,
                _notifications);
        }
    }
}
