using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The toolbar's profile picker (issue #128): <b>which numbered profile is being edited</b>.
    /// The editor could only ever open <c>LayoutScheme.FirstProfileNumber</c> before this, so the
    /// other eight <c>layout&lt;n&gt;.txt</c>/<c>led&lt;n&gt;.txt</c> pairs on the drive — the ones
    /// <c>SmartSet + &lt;n&gt;</c> switches on the board — were unreachable from the app.
    /// <para>
    /// This is <c>ProfileSession.ProfileNumber</c> and not <c>KeyboardSettings.StartupProfileNumber</c>:
    /// nothing here writes anything, and the power-on profile stays a slider on the Settings tab.
    /// The assertions below are mostly about what a switch must <b>not</b> do — half-swap the
    /// editor, lose unsaved work, or leave a listening key eating keystrokes for a board that no
    /// longer exists.
    /// </para>
    /// <para>
    /// <b>Issue #133 inverted the whole "unsaved work" section below.</b> A switch used to abandon
    /// the open session and therefore had to raise the unsaved-changes modal first; every profile
    /// the user opens is now kept alive in the editor, so the switch loses nothing, asks nothing,
    /// and finds the edits again on the way back. The cases that used to assert each answer of that
    /// modal now assert its absence and the survival it was protecting.
    /// </para>
    /// </summary>
    public sealed class ProfileSwitchingTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeSettingsService _settings = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeFolderPickerService _folderPicker = new();
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeVDriveFileService _files = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly List<KeyboardEditorViewModel> _editors = [];

        // ===== What the picker offers ========================================================

        [AvaloniaFact]
        public void Profiles_ForANumberedProfileDevice_AreItsWholeRange()
        {
            var editor = CreateEditor();

            Assert.Equal(Enumerable.Range(1, 9), editor.Profiles.Select(profile => profile.Number));
            Assert.Equal("Profile 1", editor.Profiles[0].Caption);
            Assert.Equal("Profile 9", editor.Profiles[8].Caption);
            Assert.All(editor.Profiles, profile => Assert.False(profile.IsReadOnly));
            Assert.True(editor.HasProfiles);
        }

        [AvaloniaFact]
        public void Profiles_AreBuiltBeforeAnythingIsRead()
        {
            // Which profiles a board has is a device fact, exactly like which tabs it renders — so
            // the picker is populated by the constructor and does not wait for the drive.
            var editor = CreateEditor();

            Assert.Equal(9, editor.Profiles.Count);
            Assert.Equal(0, _profiles.LoadCallCount);
            Assert.Null(editor.SelectedProfile);
        }

        [AvaloniaFact]
        public void Profiles_ForTheAdvantage360_StopAtTheNineFileBackedOnes()
        {
            // The factory profile 0 is deliberately NOT a row. It has no layout0.txt on the drive,
            // and Core refuses it three ways (docs/app/profiles.md, "Profile-0 guard"): the
            // read-only guard, the range check, and the missing file itself. A row that could only
            // ever produce an error dialog is the "absent, never disabled" law's own case.
            var editor = CreateEditor(TestDevices.CreateSnapshot(DeviceId.Advantage360));

            Assert.True(editor.Device.Device.LayoutScheme.HasReadOnlyFactoryProfile);
            Assert.Equal(Enumerable.Range(1, 9), editor.Profiles.Select(profile => profile.Number));
            Assert.DoesNotContain(editor.Profiles, profile => profile.Number == 0);
        }

        [AvaloniaFact]
        public void Profiles_ForADeviceWithNoNumberedProfiles_AreEmpty()
        {
            // The CROSSFIRE has no layout files at all, so its first and last profile numbers are
            // both 0 — a "Profile 0" row there would be an invention rather than a device fact.
            var editor = CreateEditor(TestDevices.CreateSnapshot(DeviceId.CrossfireKeypad));

            Assert.Empty(editor.Profiles);
            Assert.False(editor.HasProfiles);
        }

        [AvaloniaFact]
        public void HasProfiles_InDemoMode_IsFalse()
        {
            // Demo mode writes nowhere (03 §3.5) and reads one fixture profile; a picker there
            // would offer eight profiles that do not exist.
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            Assert.NotEmpty(editor.Profiles);
            Assert.False(editor.HasProfiles);
            Assert.False(editor.SelectProfileCommand.CanExecute(editor.Profiles[2]));
        }

        [AvaloniaFact]
        public void HasProfiles_WithoutADrive_IsFalse()
        {
            // A board with no location edits a factory-default model in memory: there is no file to
            // read another profile from.
            var editor = CreateEditor(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb) with { Location = null });

            Assert.False(editor.HasProfiles);
            Assert.False(editor.SelectProfileCommand.CanExecute(editor.Profiles[2]));
        }

        [AvaloniaFact]
        public void HasProfiles_OnASingleProfileDevice_IsFalse()
        {
            // A picker of one row is furniture. No catalog device is shaped like this today, which
            // is exactly why the rule is pinned here rather than left to be noticed later.
            var editor = CreateEditor(CreateSnapshotWithScheme(scheme => scheme with { LastProfileNumber = 1 }));

            Assert.Single(editor.Profiles);
            Assert.False(editor.HasProfiles);
        }

        // ===== The switch itself =============================================================

        [AvaloniaFact]
        public async Task SelectedProfile_AfterTheFirstLoad_IsTheProfileTheSessionOpened()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(1, editor.SelectedProfile!.Number);
            Assert.Same(editor.Profiles[0], editor.SelectedProfile);
            Assert.Equal("Profile 1", editor.ProfileCaption);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_ForAnotherProfile_ReplacesTheSessionLayoutLayersAndLighting()
        {
            var openLighting = new LightingModel();

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                Lighting = openLighting
            };

            var editor = await CreateLoadedEditorAsync();
            var openLayout = editor.Layout;
            var openLayers = editor.Layers;

            var arriving = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 3,
                Lighting = new LightingModel()
            };

            _profiles.SessionToReturn = arriving;

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            // It is a load, through the same seam and at the number the row names.
            Assert.Equal(2, _profiles.LoadCallCount);
            Assert.Equal(3, _profiles.LoadCalls[1].ProfileNumber);
            Assert.Same(editor.Device.Location, _profiles.LoadCalls[1].Location);

            // ...and everything the profile owns came with it.
            Assert.Same(arriving.Layout, editor.Layout);
            Assert.NotSame(openLayout, editor.Layout);
            Assert.NotSame(openLayers, editor.Layers);
            Assert.Same(editor.Layout!.Layers[0].Keys[0], editor.Layers[0].Keys[0].Key);
            Assert.Equal(3, editor.ProfileNumber);
            Assert.Equal(3, editor.SelectedProfile!.Number);
            Assert.Equal("Profile 3", editor.ProfileCaption);
            Assert.False(editor.IsLoading);

            // The lighting panel now edits the arriving profile's model and no longer the one that
            // was open — the trap Lighting.Attach exists for.
            editor.Lighting.SelectModeCommand.Execute(editor.Lighting.Modes.Single(mode => mode.Mode == LightingMode.Wave));

            Assert.Equal(LightingMode.Wave, ((LightingModel)arriving.Lighting!).TopLayer.Mode);
            Assert.NotEqual(LightingMode.Wave, openLighting.TopLayer.Mode);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WritesNothingAnywhere()
        {
            // A switch is a read. It must not touch the settings file, and above all not
            // `startup_file` — which profile the keyboard powers on into is a different question,
            // asked by a slider on the Settings tab.
            var editor = await CreateLoadedEditorAsync();
            var open = _profiles.SessionToReturn!;

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 5
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[4]);

            Assert.Equal(0, open.SaveCallCount);
            Assert.Empty(_settings.KeyboardSaves);
            Assert.Empty(_files.WrittenPaths);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_ForTheProfileAlreadyOpen_DoesNothing()
        {
            // Not a reload: it would drop every unsaved edit to arrive exactly where the user
            // already is.
            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[0]);

            Assert.Equal(1, _profiles.LoadCallCount);
            Assert.Same(editor.Profiles[0], editor.SelectedProfile);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_ReturningToAVisitedProfile_ReusesItsSessionWithoutReadingTheDrive()
        {
            // Issue #133's mechanism, stated on its own: every profile the user opened stays alive
            // in the editor, keyed by number. Coming back is a lookup, not a load — which is what
            // makes a return trip both instant and lossless.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var fourth = _profiles.Stage(4, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[3]);

            Assert.Same(fourth.Layout, editor.Layout);

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[0]);

            // Two reads, not three.
            Assert.Equal(2, _profiles.LoadCallCount);
            Assert.Equal(1, editor.SelectedProfile!.Number);
            Assert.Same(first.Layout, editor.Layout);
        }

        // ===== Unsaved work ==================================================================
        //
        // These eight cases INVERTED with issue #133. Every one of them used to be about the modal
        // a switch raised over unsaved work and what each of its three answers did. There is no
        // modal any more, because there is nothing to lose: the outgoing session is kept, its edits
        // come back when the user does, and the toolbar's one Save writes every profile that
        // changed. What is asserted now is that absence, and that the edits really do survive.

        [AvaloniaFact]
        public async Task SelectProfileCommand_WithUnsavedWork_AsksNothingAndWritesNothing()
        {
            var editor = await CreateDirtyEditorAsync();
            var open = _profiles.SessionToReturn!;

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 3
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            // No question, no save, no file — and the switch went through.
            Assert.Empty(_notifications.MessageBoxes);
            Assert.Equal(0, open.SaveCallCount);
            Assert.Empty(_settings.KeyboardSaves);
            Assert.Empty(_files.WrittenPaths);
            Assert.Equal(3, editor.SelectedProfile!.Number);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_AwayAndBack_StillHasTheEditsThatWereMade()
        {
            // The acceptance criterion in one case: an edit made in profile 1 is still there after a
            // trip to profile 2. It is a real remap through the model the editor is holding, and the
            // assertion is made on the editor's OWN caps after the return, not on the fake — a cache
            // that handed back the session but rebuilt the board off something else would pass a
            // weaker test.
            _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Assert.False(editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[0]);

            Assert.Equal(1, editor.SelectedProfile!.Number);
            Assert.True(editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.True(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhenTheLoadFails_ReassertsTheSelectionForTheBinding()
        {
            // The picker binds one way, so the control is already showing the row that was clicked
            // while the editor never left profile 1. Nothing moved, so SetProperty would announce
            // nothing and the ComboBox would sit there claiming a profile that is not open.
            // A failed load is what refuses a switch now that the unsaved-work modal is gone.
            var editor = await CreateLoadedEditorAsync();
            var announced = new List<string?>();

            editor.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            _profiles.ExceptionToThrow = new IOException("the v-Drive went away");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            Assert.Contains(nameof(KeyboardEditorViewModel.SelectedProfile), announced);
            Assert.Same(editor.Profiles[0], editor.SelectedProfile);
        }

        [AvaloniaFact]
        public async Task IsDirty_AfterSwitchingAwayFromADirtyProfile_StaysTrue()
        {
            // IsDirty is now an aggregate over every profile the editor holds, because Save is. A
            // switch that cleared it would leave the user's edits in memory with a grey Save over
            // them — unsaved work the chrome denies exists.
            var editor = await CreateDirtyEditorAsync();

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 7
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[6]);

            Assert.Equal(7, editor.SelectedProfile!.Number);
            Assert.True(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task IsDirty_AfterSwitchingAwayFromACleanProfile_IsFalse()
        {
            // The other half of the aggregate, so the case above cannot pass by the flag simply
            // never being cleared.
            _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            _profiles.Stage(5, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[4]);

            Assert.Equal(5, editor.SelectedProfile!.Number);
            Assert.False(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task MacroNames_AfterASwitchAwayAndBack_AreStillTheOnesTheUserTyped()
        {
            // THE TRAP the cache is most likely to be broken by. `Apply` stamps the stored names
            // from app_settings.txt onto a freshly parsed layout, and MacroLibrary.ApplyNames writes
            // every site UNCONDITIONALLY — a site with no stored name is set back to null. Re-running
            // it over a cached session would therefore wipe exactly the renames this switch exists
            // to preserve, and nothing else on screen would say so.
            _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor);

            var renamed = editor.RenameMacro(editor.MacroLibrary!.Entries[0], "Sign-off block");

            Assert.NotNull(renamed);
            Assert.True(editor.HasUnsavedMacroNames);

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);
            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[0]);

            Assert.Equal("Sign-off block", Assert.Single(editor.MacroLibrary!.Entries).Name);

            // ...and it is still unsaved work, in the profile it belongs to.
            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task IsDirty_AfterASwitchThatLeftARenamedMacroBehind_StaysTrue()
        {
            // The second term of IsDirty, and the inversion of the old claim. A rename used to be
            // dropped by the switch, because the profile it belonged to was dropped with it; now the
            // profile survives, so the rename does — and the flag has to keep reporting it or Save
            // would go grey over work that is still only in memory.
            var editor = await CreateLoadedEditorAsync();

            editor.MarkMacroNamesDirty();

            Assert.True(editor.IsDirty);
            Assert.True(editor.HasUnsavedMacroNames);

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 2
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Assert.Equal(2, editor.SelectedProfile!.Number);
            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);
        }

        // ===== When the switch is refused ====================================================

        [AvaloniaFact]
        public async Task SelectProfileCommand_BeforeTheFirstLoadHasFinished_IsRefused()
        {
            // IsLoading is true from construction until the first profile is on screen; a second
            // read racing that one would land whichever way the drive felt like.
            var editor = CreateEditor();

            Assert.True(editor.IsLoading);
            Assert.False(editor.SelectProfileCommand.CanExecute(editor.Profiles[2]));

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            Assert.Equal(0, _profiles.LoadCallCount);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhileASaveIsInFlight_IsRefused()
        {
            // Save serializes the model on a background thread; swapping the model underneath it
            // would race the serializer, exactly as every editing command's guard says.
            var editor = await CreateLoadedEditorAsync();
            var gate = new TaskCompletionSource();

            // Staged dirty, or there would be no save to be in flight: since issue #133 Save writes
            // only the profiles that changed.
            _profiles.SessionToReturn!.IsDirty = true;
            _profiles.SessionToReturn.DuringSave = () => gate.Task.Wait();

            var save = editor.SaveCommand.ExecuteAsync(null);

            Assert.True(editor.IsBusy);
            Assert.False(editor.SelectProfileCommand.CanExecute(editor.Profiles[2]));

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            Assert.Equal(1, _profiles.LoadCallCount);

            gate.SetResult();

            await save;

            // ...and it comes back the moment the write is done.
            Assert.True(editor.SelectProfileCommand.CanExecute(editor.Profiles[2]));
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhileAFeaturePanelIsOpen_IsRefused()
        {
            // An open panel owns the screen and, in two cases, the keyboard; rebuilding the board
            // under it would leave it pointing at a profile that is no longer there.
            var editor = await CreateLoadedEditorAsync();

            editor.ExportCommand.Execute(null);

            Assert.NotNull(editor.ActiveOverlay);
            Assert.False(editor.SelectProfileCommand.CanExecute(editor.Profiles[2]));

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            Assert.Equal(1, _profiles.LoadCallCount);
            Assert.Equal(1, editor.SelectedProfile!.Number);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_ForTheReadOnlyFactoryProfile_OffersItAndRefusesToOpenIt()
        {
            // A device whose range starts at 0 with a read-only factory profile: the row states the
            // rule, and the switch refuses because Core cannot read a profile that has no file.
            // No catalog device is shaped this way — the Advantage 360's range starts at 1 — so
            // this is the synthetic case that keeps IsReadOnly honest.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 1
            };

            var editor = await CreateLoadedEditorAsync(CreateSnapshotWithScheme(
                scheme => scheme with { FirstProfileNumber = 0, HasReadOnlyFactoryProfile = true }));

            var factory = editor.Profiles[0];

            Assert.Equal(0, factory.Number);
            Assert.True(factory.IsReadOnly);
            Assert.False(editor.SelectProfileCommand.CanExecute(factory));

            await editor.SelectProfileCommand.ExecuteAsync(factory);

            Assert.Equal(1, _profiles.LoadCallCount);
            Assert.Equal(1, editor.SelectedProfile!.Number);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_ToAProfileThatCannotBeWritten_LeavesSaveAndImportRefused()
        {
            // CanSave reads the session's own flag, so a switch to a profile Core reports read-only
            // has to disable the toolbar's Save — and Import with it, which is gated on the same
            // answer (docs/app/profiles.md).
            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.SaveCommand.CanExecute(null));

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 6,
                CanSave = false
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[5]);

            Assert.Equal(6, editor.SelectedProfile!.Number);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.ImportCommand.CanExecute(null));
        }

        // ===== A load that failed ============================================================

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhenTheLoadFails_LeavesThePreviousProfileSelectedAndUsable()
        {
            // THE TRAP: Apply mutates unconditionally, so the outcome is decided before it is
            // called. A half-swapped editor — a new caption over the old model, or an empty board
            // where a perfectly good profile was — is the one outcome that must be impossible.
            var editor = await CreateLoadedEditorAsync();
            var open = _profiles.SessionToReturn!;
            var openLayout = editor.Layout;
            var openLayers = editor.Layers;

            _profiles.ExceptionToThrow = new IOException("the v-Drive went away");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[4]);

            Assert.Equal(1, editor.SelectedProfile!.Number);
            Assert.Equal("Profile 1", editor.ProfileCaption);
            Assert.Same(openLayout, editor.Layout);
            Assert.Same(openLayers, editor.Layers);
            Assert.False(editor.IsLoading);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.LoadFailureTitle, request.Title);
            Assert.Contains("the v-Drive went away", request.Message, StringComparison.Ordinal);

            // ...and the session it kept is still the live one: it can still be saved.
            _profiles.ExceptionToThrow = null;
            open.IsDirty = true;

            Assert.True(editor.SaveCommand.CanExecute(null));

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, open.SaveCallCount);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhenTheLoadFails_CanBeTriedAgain()
        {
            var editor = await CreateLoadedEditorAsync();

            _profiles.ExceptionToThrow = new IOException("the v-Drive went away");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[4]);

            _profiles.ExceptionToThrow = null;
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 5
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[4]);

            Assert.Equal(5, editor.SelectedProfile!.Number);
        }

        // ===== The stand-downs ===============================================================

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhileAKeyIsListening_EndsTheListenAndStopsCapture()
        {
            // Capture is app-wide and never left running: the key that was listening belongs to a
            // board that is about to be replaced.
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = 2
            };

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
            Assert.False(_capture.IsCapturing);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhenTheLoadFails_HasStillStoodTheArmedCopyDown()
        {
            // The stand-downs run BEFORE the read, not as a side effect of the swap — this is the
            // case that can tell the difference, because a failed load never reaches Apply (whose
            // SelectLayer would have cancelled the pick anyway).
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);
            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);

            _profiles.ExceptionToThrow = new IOException("the v-Drive went away");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Assert.False(editor.IsCopyArmed);
            Assert.Equal(1, editor.SelectedProfile!.Number);
        }

        [AvaloniaFact]
        public async Task SelectProfileCommand_WhileAMacroIsBeingRecorded_StandsTheRailDown()
        {
            var editor = await CreateLoadedEditorAsync();

            ArmMacroRecording(editor);

            Assert.True(editor.Inspector.IsRecording);

            _profiles.ExceptionToThrow = new IOException("the v-Drive went away");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Assert.False(editor.Inspector.IsRecording);
            Assert.False(_capture.IsCapturing);
        }

        // ===== Fixtures ======================================================================

        /// <summary>
        /// Records a one-keystroke macro on the digit-1 position, through the app's own capture
        /// path — the only way a macro is ever created, so a test about macro <em>names</em> starts
        /// from a macro the editor really made.
        /// </summary>
        private void RecordAMacro(KeyboardEditorViewModel editor)
        {
            ArmMacroRecording(editor);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("a"));

            editor.Inspector.Deactivate();
        }

        /// <summary>Arms the key inspector's Macro panel — the app's one macro recorder.</summary>
        private static void ArmMacroRecording(KeyboardEditorViewModel editor)
        {
            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            var panel = Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);

            panel.RecordCommand.Execute(null);
        }

        /// <summary>
        /// A connected Freestyle Edge RGB whose layout scheme has been bent into a shape no catalog
        /// device has. Both callers need a device fact the catalog cannot give them — a single
        /// profile, and a range that starts at the read-only factory one.
        /// </summary>
        private static DeviceSnapshot CreateSnapshotWithScheme(Func<LayoutFileScheme, LayoutFileScheme> reshape)
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);

            return snapshot with
            {
                Device = snapshot.Device with { LayoutScheme = reshape(snapshot.Device.LayoutScheme) }
            };
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(DeviceSnapshot? snapshot = null)
        {
            var editor = CreateEditor(snapshot);

            await editor.LoadAsync();

            return editor;
        }

        /// <summary>A loaded editor whose session reports itself dirty — staged, not edited into.</summary>
        private async Task<KeyboardEditorViewModel> CreateDirtyEditorAsync()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.IsDirty);

            return editor;
        }

        private KeyboardEditorViewModel CreateEditor(DeviceSnapshot? snapshot = null)
        {
            var editor = new KeyboardEditorViewModel(
                snapshot ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

            _editors.Add(editor);

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
