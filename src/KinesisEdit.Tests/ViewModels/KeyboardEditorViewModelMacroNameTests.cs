using Avalonia.Headless.XUnit;
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
    /// <para>
    /// <b>Issue #146 removed the rail's inline name field, and with it every production caller of
    /// <c>MarkMacroNamesDirty</c>.</b> The rename cases below therefore drive that seam directly
    /// rather than through a control that no longer exists — the editor-side behaviour they cover is
    /// unchanged, only the way the mark is set. The case the removal is actually about is
    /// <see cref="ThePanelWithNoNameField_LeavesAStoredNameOnTheDrive"/>: with nothing marking a
    /// profile, <c>PersistMacroNames</c> returns on its first line and a stored
    /// <c>macro_name_*</c> line is never rewritten — which is exactly how it survives.
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
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

        [AvaloniaFact]
        public async Task ARename_ThroughTheSeam_MarksTheProfileWithoutMovingACounter()
        {
            // `MarkMacroNamesDirty` is the public seam that says "this profile's names are out of
            // date". The rail's inline field used to reach it through `NameChanged`; issue #146
            // removed the field, and the seam is what a future naming surface will call again.
            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");

            var before = editor.MacroCount;

            RenameTheStagedMacro(editor, "Sign-off block");

            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);

            // ...and nothing about it moved the macro count, which is why a name was never folded
            // into the layout's own funnel in the first place.
            Assert.Equal(before, editor.MacroCount);
        }

        [AvaloniaFact]
        public async Task ARename_OnOneKey_LeavesAnIdenticallyNamedMacroOnAnotherKeyAlone()
        {
            // The whole point of issue #141. The deleted MacroLibrary grouped sites by name and
            // propagated a rename across the group, which is an identity the hardware does not
            // have: every slot holds its own copy (06 §1).
            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "a");
            RecordAMacro(editor, TestLayouts.RgbDigitTwoKeyIndex, "a");

            RenameMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "Sign-off block");
            RenameMacro(editor, TestLayouts.RgbDigitTwoKeyIndex, "Sign-off block");

            RenameMacro(editor, TestLayouts.RgbDigitOneKeyIndex, "Something else");

            Assert.Equal("Something else", FindMacro(editor, TestLayouts.RgbDigitOneKeyIndex).Name);
            Assert.Equal("Sign-off block", FindMacro(editor, TestLayouts.RgbDigitTwoKeyIndex).Name);

            // Two places, two settings keys — the harvest never merges them either.
            Assert.Equal(2, MacroSites.EnumerateStoredNames(editor.Layout!).Count);
        }

        /// <summary>
        /// <b>Issue #146, AC 12.</b> Removing the rail's name field removed the app's only way to
        /// mark a profile's names dirty — so the question the change has to answer is what happens
        /// to a name that is already on the drive. The answer is that it is loaded, carried on
        /// <see cref="Macro.Name"/> for the session, and left exactly where it was:
        /// <c>PersistMacroNames</c> returns on its first line with no profile marked, so the
        /// <c>macro_name_*</c> line is never rewritten and therefore never tombstoned.
        /// <para>
        /// This is the case that fails if the harvest is ever made unconditional "for safety" — it
        /// would then run over macros the panel no longer names, which is the shape that drops them.
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public async Task ThePanelWithNoNameField_LeavesAStoredNameOnTheDrive()
        {
            var siteKey = StageOneMacro(out _);

            _preferences.SetInitial(AppSettings.Empty.WithMacroName(siteKey, "Sign-off block"));

            var editor = await CreateLoadedEditorAsync();

            // The load stamped it, exactly as it always did.
            Assert.Equal("Sign-off block", FindStagedMacro(editor).Name);

            // Edit the macro through the rail — a real layout write, so the profile is genuinely
            // dirty and the save genuinely runs.
            var panel = OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("b"));

            panel.Deactivate();

            // The fake session's dirty flag is staged rather than derived (a fake that computed it
            // would be asserting what Core's own suite proves), so the write set really contains
            // this profile and the save really runs.
            _profiles.SessionToReturn!.IsDirty = true;

            Assert.False(editor.HasUnsavedMacroNames);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, _profiles.SessionToReturn.SaveCallCount);

            // Nothing was written to the names — and nothing had to be, because nothing removed
            // them: the stored line is still there and the model still carries it.
            Assert.Empty(_preferences.MacroNameWrites);
            Assert.Equal("Sign-off block", _preferences.Current.GetMacroName(siteKey));
            Assert.Equal("Sign-off block", FindStagedMacro(editor).Name);
        }

        [AvaloniaFact]
        public async Task TheMacroMode_DoesNotStealTheSectionWhenItIsChosen()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex);

            // The bridge is gone: the rail has the panel and must not navigate away from the board.
            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
        }

        [AvaloniaFact]
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
        /// Renames the staged macro through the two things a naming surface has to do: write
        /// <see cref="Macro.Name"/>, and tell the editor its names moved
        /// (<c>MarkMacroNamesDirty</c>). The rail's inline field did exactly this via
        /// <c>NameChanged</c> until issue #146 removed it; the seam is unchanged, so everything
        /// below the seam — the dirty flag, the harvest, the tombstones, the per-profile set — is
        /// covered exactly as it was.
        /// </summary>
        private static void RenameTheStagedMacro(KeyboardEditorViewModel editor, string name)
        {
            RenameMacro(editor, TestLayouts.RgbDigitOneKeyIndex, name);
        }

        /// <summary>The same, on any position of the open layer.</summary>
        private static void RenameMacro(KeyboardEditorViewModel editor, int keyIndex, string name)
        {
            FindMacro(editor, keyIndex).Name = name;

            editor.MarkMacroNamesDirty();
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
