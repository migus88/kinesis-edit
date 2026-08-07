using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The editor's <b>one</b> <see cref="MacroLibrary"/> and the macro names that ride
    /// <c>app_settings.txt</c> with it (issue #93): applied on load, harvested on save, folded into
    /// the dirty flag in between — and never written by a second reader/writer pair.
    /// </summary>
    public sealed class KeyboardEditorViewModelMacroLibraryTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeSettingsService _settings = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeFolderPickerService _folderPicker = new();
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeVDriveFileService _files = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly FakeAppPreferencesStore _preferences = new();
        private readonly List<KeyboardEditorViewModel> _editors = [];

        [Fact]
        public async Task Load_BuildsOneLibraryOverTheOpenProfile()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.NotNull(editor.MacroLibrary);
            Assert.Same(editor.Layout, editor.MacroLibrary!.Layout);
            Assert.Equal(_profiles.SessionToReturn!.ProfileNumber, editor.ProfileNumber);
        }

        [Fact]
        public async Task Load_StampsTheStoredNamesOntoTheFreshlyParsedLayout()
        {
            // A macro name is NOT in layoutN.txt: a parsed layout always arrives unnamed, and this
            // is the step that puts the stored name back.
            var siteKey = StageOneMacro(out var triggerCode);

            _preferences.SetInitial(AppSettings.Empty.WithMacroName(siteKey, "Sign-off block"));

            var editor = await CreateLoadedEditorAsync();
            var entry = Assert.Single(editor.MacroLibrary!.Entries);

            Assert.Equal("Sign-off block", entry.Name);
            Assert.True(entry.IsExplicitlyNamed);
            Assert.Equal(triggerCode, entry.Sites[0].TriggerKeyCode);
        }

        [Fact]
        public async Task Load_WithNoStoredName_LeavesTheMacroToDeriveOne()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();
            var entry = Assert.Single(editor.MacroLibrary!.Entries);

            Assert.False(entry.IsExplicitlyNamed);
            Assert.NotEqual(string.Empty, entry.Name);
        }

        [Fact]
        public async Task RenameMacro_MarksTheSessionDirtyAndWritesNothingYet()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsDirty);

            var renamed = editor.RenameMacro(editor.MacroLibrary!.Entries[0], "Sign-off block");

            Assert.NotNull(renamed);
            Assert.Equal("Sign-off block", renamed!.Name);

            // The exception the whole feature turns on: a name is session state, so Save writes it
            // and Discard drops it. Nothing has reached app_settings.txt yet.
            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);
            Assert.Empty(_preferences.MacroNameWrites);
            Assert.Equal(0, _preferences.UpdateCount);
        }

        [Fact]
        public async Task RenameMacro_WithAStaleEntry_RefusesRatherThanRenamingWhateverIsThere()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();
            var stale = editor.MacroLibrary!.Entries[0];

            editor.RenameMacro(stale, "First");

            Assert.Null(editor.RenameMacro(stale, "Second"));
            Assert.Equal("First", editor.MacroLibrary.Entries[0].Name);
        }

        [Fact]
        public async Task Save_HarvestsTheNamesThroughThePreferencesStoreScopedToTheProfile()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();

            editor.RenameMacro(editor.MacroLibrary!.Entries[0], "Sign-off block");

            await editor.SaveCommand.ExecuteAsync(null);

            // Through the macro-name path, never through the preference/swatch one: SaveAppSettings
            // cannot reach a name, and one file holds nine profiles' names.
            Assert.Equal([editor.ProfileNumber], _preferences.MacroNameWrites);
            Assert.Equal(0, _preferences.UpdateCount);

            var stored = Assert.Single(_preferences.Current.MacroNames);

            Assert.Equal(editor.ProfileNumber, stored.Key.ProfileNumber);
            Assert.Equal("Sign-off block", stored.Value);

            Assert.False(editor.HasUnsavedMacroNames);
            Assert.False(editor.IsDirty);
        }

        [Fact]
        public async Task Save_WithNoExplicitName_WritesAnEmptySetSoTheOldKeyIsRemoved()
        {
            var siteKey = StageOneMacro(out _);

            _preferences.SetInitial(AppSettings.Empty.WithMacroName(siteKey, "Sign-off block"));

            var editor = await CreateLoadedEditorAsync();

            // Clearing the name is what makes the macro derive one again — and tombstones the key.
            editor.RenameMacro(editor.MacroLibrary!.Entries[0], string.Empty);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal([editor.ProfileNumber], _preferences.MacroNameWrites);
            Assert.Null(_preferences.Current.GetMacroName(siteKey));
        }

        [Fact]
        public async Task Save_WhenTheProfileWasRejected_WritesNoNames()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();

            editor.RenameMacro(editor.MacroLibrary!.Entries[0], "Sign-off block");

            _profiles.SessionToReturn!.ResultToReturn = new ProfileSaveResult
            {
                Success = false,
                Ejected = false,
                Violations =
                [
                    new ModelViolation
                    {
                        Kind = ModelViolationKind.MacroLengthExceeded,
                        Message = "The macro is too long."
                    }
                ]
            };

            await editor.SaveCommand.ExecuteAsync(null);

            // The names would otherwise name macros the drive does not have.
            Assert.Empty(_preferences.MacroNameWrites);
            Assert.True(editor.HasUnsavedMacroNames);
        }

        [Fact]
        public async Task Save_InDemoMode_KeepsTheNamesInMemoryAndThrowsNothing()
        {
            var editor = await CreateLoadedEditorAsync(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));

            // Demo mode reads no profile at all, so there is nothing to scope a name to — and the
            // library is still built over the factory-default layout.
            Assert.NotNull(editor.MacroLibrary);
            Assert.Equal(0, editor.ProfileNumber);

            editor.MarkMacroNamesDirty();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Empty(_preferences.MacroNameWrites);
        }

        [Fact]
        public async Task TheLibrary_FollowsEveryMacroEditThroughTheEditorsFunnel()
        {
            var editor = await CreateLoadedEditorAsync();
            var rebuilds = 0;

            editor.MacroLibraryChanged += (_, _) => rebuilds++;

            Assert.Empty(editor.MacroLibrary!.Entries);

            RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");

            Assert.Single(editor.MacroLibrary.Entries);
            Assert.True(rebuilds > 0);
        }

        [Fact]
        public async Task TheLibrary_IsTheSameInstanceTheRailsMacroPanelReads()
        {
            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");

            var panel = OpenMacroPanel(editor);

            // Two libraries over one layout would be two snapshots that disagree the moment either
            // is edited; the panel's dropdown is built from THIS one.
            Assert.Single(panel.NameOptions);
            Assert.Equal(editor.MacroLibrary!.Entries[0].Name, panel.NameOptions[0].Caption);
        }

        [Fact]
        public async Task TheMacroTab_NoLongerStealsTheSectionWhenTheRailsMacroModeIsChosen()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectKey(editor, TestLayouts.RgbDigitOneKeyIndex);

            OpenMacroPanel(editor);

            // The bridge is gone: the tab has a real panel and must not navigate away from the board.
            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
        }

        [Fact]
        public async Task ARailMacroEdit_ReachesTheCountersAndTheCap()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(0, editor.MacroCount);

            var key = RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");

            Assert.Equal(1, editor.MacroCount);
            Assert.True(key.IsMacro);
        }

        /// <summary>
        /// Stages a profile whose layout already carries one macro, and hands back the settings key
        /// its place is named by.
        /// </summary>
        private MacroNameKey StageOneMacro(out int triggerCode)
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.TriggerKey = key.TriggerKey.Code;
            macro.LayerIndex = 0;

            key.SetMacro(1, macro);

            triggerCode = key.TriggerKey.Code;

            _profiles.SessionToReturn = new FakeProfileSession(layout);

            return new MacroNameKey(_profiles.SessionToReturn.ProfileNumber, 0, triggerCode, 1);
        }

        private KeyboardKeyViewModel RecordAMacro(KeyboardEditorViewModel editor, int keyIndex, string token)
        {
            var key = SelectKey(editor, keyIndex);
            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key(token));

            panel.Deactivate();

            return key;
        }

        private static KeyboardKeyViewModel SelectKey(KeyboardEditorViewModel editor, int keyIndex)
        {
            var key = editor.SelectedLayer!.FindByIndex(keyIndex)
                      ?? throw new InvalidOperationException($"The layer has no position {keyIndex}.");

            editor.SelectKeyCommand.Execute(key);

            return key;
        }

        /// <summary>
        /// Puts the key inspector on its Macro mode and hands the panel back. The rail exposes only
        /// the showing panel, so this is also what proves the tab reaches it.
        /// </summary>
        private static MacroInspectorPanelViewModel OpenMacroPanel(KeyboardEditorViewModel editor)
        {
            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            return Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(DeviceSnapshot? snapshot = null)
        {
            var device = snapshot ?? TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdgeRgb));

            var editor = new KeyboardEditorViewModel(
                device,
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher,
                new StubSessionAccessor(new DeviceSession(device, _preferences)));

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

        private sealed class StubSessionAccessor : IDeviceSessionAccessor
        {
            public DeviceSession? Active { get; }

            public StubSessionAccessor(DeviceSession? active)
            {
                Active = active;
            }
        }
    }
}
