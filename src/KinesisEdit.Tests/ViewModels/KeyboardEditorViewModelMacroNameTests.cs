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
    /// The macro names that ride <c>app_settings.txt</c> beside a profile (issue #93, rewritten for
    /// issue #141): stamped onto a freshly parsed layout on load, harvested off its macro
    /// <see cref="MacroSites">sites</see> on save, folded into the dirty flag in between — and never
    /// written by a second reader/writer pair.
    /// <para>
    /// The <c>MacroLibrary</c> this suite used to be about is gone. There is no shared macro on disk
    /// (06 §1), so a name belongs to a <em>place</em>: two keys carrying the same name are two
    /// independent names, and the last case here is what pins that.
    /// </para>
    /// </summary>
    public sealed class KeyboardEditorViewModelMacroNameTests : IDisposable
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
        public async Task Load_StampsTheStoredNamesOntoTheFreshlyParsedLayout()
        {
            // A macro name is NOT in layoutN.txt: a parsed layout always arrives unnamed, and this
            // is the step that puts the stored name back.
            var siteKey = StageOneMacro(out _);

            _preferences.SetInitial(AppSettings.Empty.WithMacroName(siteKey, "Sign-off block"));

            var editor = await CreateLoadedEditorAsync();

            Assert.Equal("Sign-off block", FindStagedMacro(editor).Name);
            Assert.Equal(_profiles.SessionToReturn!.ProfileNumber, editor.ProfileNumber);
        }

        [Fact]
        public async Task Load_WithNoStoredName_LeavesTheMacroToDeriveOne()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();
            var macro = FindStagedMacro(editor);

            // Empty is the model's "unnamed", and it is what keeps a derived name out of
            // app_settings.txt: only a non-empty Macro.Name is ever harvested.
            Assert.Equal(string.Empty, macro.Name);
            Assert.NotEqual(string.Empty, MacroNaming.DeriveDisplayName(macro, editor.Layout!));
        }

        [Fact]
        public async Task Rename_MarksTheProfileDirtyAndWritesNothingYet()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsDirty);

            RenameTheStagedMacro(editor, "Sign-off block");

            // The exception the whole feature turns on: a name is session state, so Save writes it
            // and Discard drops it. Nothing has reached app_settings.txt yet.
            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);
            Assert.Empty(_preferences.MacroNameWrites);
            Assert.Equal(0, _preferences.UpdateCount);
        }

        [Fact]
        public async Task Save_HarvestsTheNamesThroughThePreferencesStoreScopedToTheProfile()
        {
            StageOneMacro(out var triggerCode);

            var editor = await CreateLoadedEditorAsync();

            RenameTheStagedMacro(editor, "Sign-off block");

            await editor.SaveCommand.ExecuteAsync(null);

            // Through the macro-name path, never through the preference/swatch one: SaveAppSettings
            // cannot reach a name, and one file holds nine profiles' names.
            Assert.Equal([editor.ProfileNumber], _preferences.MacroNameWrites);
            Assert.Equal(0, _preferences.UpdateCount);

            var stored = Assert.Single(_preferences.Current.MacroNames);

            Assert.Equal(editor.ProfileNumber, stored.Key.ProfileNumber);
            Assert.Equal("Sign-off block", stored.Value);

            // THE TRAP (issue #141): the key's trigger component is KeyboardKey.TriggerKey.Code and
            // never the position's own code. A site keyed by the position writes a settings key the
            // next load never looks up — the name would round-trip to nothing, silently.
            Assert.Equal(triggerCode, stored.Key.TriggerKeyCode);
            Assert.Equal(1, stored.Key.SlotNumber);

            Assert.False(editor.HasUnsavedMacroNames);
            Assert.False(editor.IsDirty);
        }

        [Fact]
        public async Task Save_WithTheNameCleared_WritesAnEmptySetSoTheOldKeyIsRemoved()
        {
            var siteKey = StageOneMacro(out _);

            _preferences.SetInitial(AppSettings.Empty.WithMacroName(siteKey, "Sign-off block"));

            var editor = await CreateLoadedEditorAsync();

            // Clearing the name is what makes the macro derive one again — and tombstones the key.
            RenameTheStagedMacro(editor, string.Empty);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal([editor.ProfileNumber], _preferences.MacroNameWrites);
            Assert.Null(_preferences.Current.GetMacroName(siteKey));
        }

        [Fact]
        public async Task Save_WhenTheProfileWasRejected_WritesNoNames()
        {
            StageOneMacro(out _);

            var editor = await CreateLoadedEditorAsync();

            RenameTheStagedMacro(editor, "Sign-off block");

            _profiles.SessionToReturn!.ResultToReturn = new ProfileSaveResult
            {
                Success = false,
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

            // Demo mode opens a real profile (issue #96), so a name genuinely scopes to one and the
            // macros are named normally — what demo mode refuses is the *write*, not the read:
            // spec 08 §3 bans saving app settings, never loading them.
            Assert.NotNull(editor.Layout);
            Assert.True(editor.ProfileNumber >= 1);

            editor.MarkMacroNamesDirty();

            await editor.SaveCommand.ExecuteAsync(null);

            // The names stay on Macro.Name for the session — the rail's field works exactly as on a
            // writable drive — but nothing reaches the fixture drive.
            Assert.Empty(_preferences.MacroNameWrites);
        }

        [Fact]
        public async Task ARename_ReachesTheEditorThroughTheRailsOwnEvent()
        {
            // The panel raises NameChanged rather than Assigned — a name moves no counter — and the
            // editor's handler is the only thing that turns it into unsaved work.
            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");

            var before = editor.MacroCount;
            var panel = OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex);

            panel.MacroName = "Sign-off block";

            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);

            // ...and nothing about it moved the macro count, which is what the second event exists
            // to keep separate.
            Assert.Equal(before, editor.MacroCount);
        }

        [Fact]
        public async Task ARename_OnOneKey_LeavesAnIdenticallyNamedMacroOnAnotherKeyAlone()
        {
            // The whole point of issue #141. The deleted MacroLibrary grouped sites by name and
            // propagated a rename across the group, which is an identity the hardware does not
            // have: every slot holds its own copy (06 §1).
            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");
            RecordAMacro(editor, TestLayouts.RgbDigitTwoKeyIndex, "a");

            OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex).MacroName = "Sign-off block";
            OpenMacroPanelFor(editor, TestLayouts.RgbDigitTwoKeyIndex).MacroName = "Sign-off block";

            OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex).MacroName = "Something else";

            Assert.Equal("Something else", FindMacro(editor, TestLayouts.RgbDigitOneKeyIndex).Name);
            Assert.Equal("Sign-off block", FindMacro(editor, TestLayouts.RgbDigitTwoKeyIndex).Name);

            // Two places, two settings keys — the harvest never merges them either.
            Assert.Equal(2, MacroSites.EnumerateStoredNames(editor.Layout!).Count);
        }

        [Fact]
        public async Task TheMacroMode_DoesNotStealTheSectionWhenItIsChosen()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex);

            // The bridge is gone: the rail has the panel and must not navigate away from the board.
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

        private static Macro FindStagedMacro(KeyboardEditorViewModel editor)
        {
            return FindMacro(editor, TestLayouts.RgbDigitOneKeyIndex);
        }

        private static Macro FindMacro(KeyboardEditorViewModel editor, int keyIndex)
        {
            return editor.Layout!.Layers[0].Keys[keyIndex].GetMacro(1)
                   ?? throw new InvalidOperationException($"Position {keyIndex} carries no macro in slot 1.");
        }

        /// <summary>
        /// Renames the staged macro <b>the way the user does</b> — the rail's inline name field —
        /// which is also the only path that exercises the panel's <c>NameChanged</c> hop into the
        /// editor.
        /// </summary>
        private static void RenameTheStagedMacro(KeyboardEditorViewModel editor, string name)
        {
            OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex).MacroName = name;
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

        private static MacroInspectorPanelViewModel OpenMacroPanelFor(KeyboardEditorViewModel editor, int keyIndex)
        {
            SelectKey(editor, keyIndex);

            return OpenMacroPanel(editor);
        }

        /// <summary>
        /// Puts the key inspector on its Macro mode and hands the panel back. The rail exposes only
        /// the showing panel, so this is also what proves the mode reaches it.
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
