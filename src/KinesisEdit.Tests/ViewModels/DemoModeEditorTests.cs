using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// Demo mode end to end, over the <b>real</b> profile session, settings service and parsers,
    /// pointed at the embedded fixture v-Drive: what the user gets with no hardware attached.
    /// <para>
    /// Nothing here is faked except the disk itself, and that one fake is a recorder rather than a
    /// stub — so "the demo v-Drive is never written, and the real one is never touched" is asserted
    /// rather than assumed. The editor under test is built exactly as the app builds it; the only
    /// difference between this and a connected board is which <c>IVDriveFileService</c> the
    /// composition root handed over, which is the whole design (docs/app/profiles.md, "The service
    /// seam").
    /// </para>
    /// </summary>
    public sealed class DemoModeEditorTests : IDisposable
    {
        private const string ExportFolder = "/tmp/kinesis-edit-demo-export";

        private readonly FakeVDriveFileService _disk = new();
        private readonly DemoVDriveFileService _files;
        private readonly ISettingsService _settings;
        private readonly DeviceSessionManager _sessions;
        private readonly ProfileSessionFactory _profiles;
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeFolderPickerService _folderPicker = new() { FolderToReturn = ExportFolder };
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly List<KeyboardEditorViewModel> _editors = [];

        public DemoModeEditorTests()
        {
            _files = new DemoVDriveFileService(_disk);
            _settings = TestDevices.CreateSettingsService(_files);
            _sessions = new DeviceSessionManager(_settings);
            _profiles = new ProfileSessionFactory(_files);
        }

        [Fact]
        public async Task LoadAsync_ForTheFreestyleEdgeRgb_OpensAPopulatedBoard()
        {
            // The whole point of the feature: demo mode used to open a factory-default layout with
            // nothing on it, which taught a user with no hardware nothing at all. Every value below
            // came off the fixture files through the production parsers.
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal("Profile 1", editor.ProfileCaption);
            Assert.Equal(new[] { "Top", "Fn" }, editor.Layers.Select(layer => layer.Caption));
            Assert.All(editor.Layers, layer => Assert.Equal(95, layer.Keys.Count));
            Assert.Empty(editor.InvalidLineMessages);

            // Remaps, macros and a tap-and-hold — the three states the board's badges are for.
            Assert.True(editor.ModifiedKeyCount > 0, $"expected remapped keys, got {editor.ModifiedKeyCount}");
            Assert.True(editor.MacroCount > 0, $"expected macros, got {editor.MacroCount}");
            Assert.Equal(1, editor.Layout!.TapAndHoldCount);

            // Which reach the board and the legend row, not only the model.
            Assert.Contains(editor.Layers[0].Keys, key => key.IsModified);
            Assert.True(editor.BoardLegend.RemappedCount > 0);
            Assert.NotEmpty(editor.MacroPanel!.Macros);
        }

        [Fact]
        public async Task LoadAsync_ForTheFreestyleEdgeRgb_OpensTheFixturesLightingAndSettings()
        {
            var editor = await CreateLoadedEditorAsync();

            // The lighting model is the session's, so the Lighting tab shows the fixture's own
            // mode and colours rather than the in-memory default demo mode used to fall back to.
            Assert.True(editor.Lighting.IsAvailable);
            Assert.Equal(LightingMode.Breathe, editor.Lighting.SelectedMode);
            Assert.Contains(editor.Layers[0].Keys, key => key.HasPaintColor);

            // And the Settings tab really read kbd_settings.txt: spec 08 §3 bans saving in demo
            // mode, not loading, and DemoModeHint is the note that says exactly that.
            Assert.True(editor.Settings.HasLoadedSettings);
            Assert.Equal(KeyboardSettingsViewModel.DemoModeHint, editor.Settings.StatusMessage);

            var macroSpeed = Assert.IsType<SettingsSliderRowViewModel>(
                editor.Settings.Rows.Single(row => row.Caption == KeyboardSettingsRows.MacroSpeedCaption));

            Assert.Equal(5, macroSpeed.Value);

            // Not one byte of any of it came off the machine.
            Assert.Equal(0, _disk.ReadCount);
        }

        [Fact]
        public async Task Editing_InDemoMode_WritesNothingAnywhere()
        {
            // Two independent guards, because either alone can pass while demo mode persists
            // everything: the recording fake underneath the demo file service catches a write that
            // escaped the fixture root, and the store the session chose catches the preferences
            // path, which never goes near a profile session at all.
            var editor = await CreateLoadedEditorAsync();

            Assert.IsType<ReadOnlyAppPreferencesStore>(_sessions.Active!.Preferences);

            // A key the fixture really remapped, so dropping its remap really moves the file.
            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys.First(key => key.IsModified));
            editor.ResetKeyCommand.Execute(null);
            editor.Lighting.SelectModeCommand.Execute(
                editor.Lighting.Modes.First(mode => mode.Mode == LightingMode.Spectrum));

            // The session really did move — which is the state that used to be impossible — and
            // still nothing may be written.
            Assert.True(editor.IsDirty);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.ImportCommand.CanExecute(null));
            Assert.False(editor.Settings.SaveCommand.CanExecute(null));
            Assert.False(editor.Lighting.Picker.CanStoreCustomColors);

            await editor.SaveCommand.ExecuteAsync(null);
            await editor.Settings.SaveCommand.ExecuteAsync(null);
            await editor.ImportCommand.ExecuteAsync(null);

            Assert.Empty(_disk.WrittenPaths);
            Assert.Empty(_disk.SettingsUpdates);
            Assert.Empty(_disk.SettingsRemovals);
        }

        [Fact]
        public async Task ExportCommand_InDemoMode_WritesARealLayoutFileIntoTheChosenFolder()
        {
            // The other side of that line, and the reason the demo file service refuses by path
            // rather than by mode: an export writes to a folder the user picked, so it is not a
            // write to the v-Drive at all and 03 §3.5 has nothing to say about it. Demo mode is
            // where it matters most — it is the only way a user with no hardware gets a file out.
            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.ExportCommand.CanExecute(null));

            editor.ExportCommand.Execute(null);

            var overlay = Assert.IsType<ExportOverlayViewModel>(editor.ActiveOverlay);

            overlay.IsLayoutOnlySelected = true;

            await overlay.ExportCommand.ExecuteAsync(null);

            var written = Assert.Single(_disk.WrittenPaths);

            Assert.Equal(Path.Combine(ExportFolder, "layout1.txt"), written);
            Assert.True(overlay.WasAccepted);

            // A real layout file, not a placeholder: the production parser reads it back and finds
            // the profile that was on screen.
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(_disk.ReadAllLines(written));

            Assert.Empty(reparsed.InvalidLines);
            Assert.Equal(editor.Layout!.ModifiedKeyCount, reparsed.Layout.ModifiedKeyCount);
            Assert.Equal(editor.Layout.MacroCount, reparsed.Layout.MacroCount);
        }

        [Fact]
        public async Task LoadAsync_ForABoardWithNoFixtures_OpensTheEmptyDemoEditorItAlwaysDid()
        {
            // Six of the seven boards, unchanged. The demo gate answers null for them, so there is
            // no drive, nothing is read and no session exists — which is also what keeps Export
            // refused there, since there is nothing to serialize.
            var snapshot = DeviceSnapshot.CreateDemo(DeviceCatalog.GetById(DeviceId.Advantage2));

            Assert.Null(snapshot.Location);

            var editor = await CreateLoadedEditorAsync(snapshot);

            Assert.NotNull(editor.Layout);
            Assert.Equal(0, editor.Layout.ModifiedKeyCount);
            Assert.Equal(string.Empty, editor.ProfileCaption);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.ExportCommand.CanExecute(null));
            Assert.IsType<NullAppPreferencesStore>(_sessions.Active!.Preferences);
            Assert.Equal(0, _disk.ReadCount);
            Assert.Empty(_disk.WrittenPaths);
        }

        [Fact]
        public async Task ConfirmCloseAsync_InDemoMode_LeavesWithoutAskingEvenWithRealUnsavedWork()
        {
            // #52's guard must stay silent here. The session is real and genuinely dirty, so the
            // question would be reachable — and it would offer a Save that can never run.
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys.First(key => key.IsModified));
            editor.ResetKeyCommand.Execute(null);

            Assert.True(editor.IsDirty);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);
        }

        private Task<KeyboardEditorViewModel> CreateLoadedEditorAsync()
        {
            return CreateLoadedEditorAsync(DeviceSnapshot.CreateDemo(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb)));
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(DeviceSnapshot snapshot)
        {
            // Built the way the shell builds it: the session is opened before the editor, so the
            // editor reads its preferences off the store the session already chose.
            _sessions.Begin(snapshot);

            var editor = new KeyboardEditorViewModel(
                snapshot,
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher,
                _sessions);

            _editors.Add(editor);

            await editor.LoadAsync();

            return editor;
        }

        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                editor.Dispose();
            }
        }
    }
}
