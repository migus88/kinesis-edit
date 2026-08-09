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
    /// The action row's <c>Discard changes</c> (issue #133) — <b>the open profile, and only the page
    /// it is open on</b>. The user's own scope, verbatim: *"Only the open profile and only the page
    /// it is open on. So for example I don't want it to discard lighting if I'm on the Layout page
    /// and vice versa."*
    /// <para>
    /// Two things are worth knowing about what these assert. The <b>scoping</b> is asserted from
    /// both sides every time — the half that was reverted <i>and</i> the half that was not — because
    /// a command that simply reverted everything would pass any test that only looked at the first.
    /// And the confirmation has <b>no suppression key</b>: the <c>*_msg</c> keys are spec 08's own,
    /// in the manufacturer's <c>app_settings.txt</c>, and this feature is not entitled to invent one
    /// or to borrow the reset's.
    /// </para>
    /// </summary>
    public sealed class DiscardChangesTests : IDisposable
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

        // ===== The tab decides the scope =====================================================

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheKeysTab_RevertsTheLayoutAndLeavesTheLightingAlone()
        {
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(1, session.RevertLayoutCallCount);
            Assert.Equal(0, session.RevertLightingCallCount);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheLightingTab_RevertsTheLightingAndLeavesTheLayoutAlone()
        {
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            session.LightingToRevertTo = new LightingModel();

            editor.SelectedTab = EditorTab.Lighting;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(1, session.RevertLightingCallCount);
            Assert.Equal(0, session.RevertLayoutCallCount);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheMacrosTab_RevertsTheLayout()
        {
            // A macro IS layout content — it lives in layout<n>.txt — so the Macros tab goes with
            // Keys rather than getting a scope of its own.
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            editor.SelectedTab = EditorTab.Macros;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(1, session.RevertLayoutCallCount);
            Assert.Equal(0, session.RevertLightingCallCount);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheSettingsTab_IsRefused()
        {
            // The settings file is outside the session's dirty comparison entirely, so there is
            // nothing on that tab a discard could revert — and reverting the layout from under a
            // user looking at settings would be a scope nobody asked for.
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            editor.SelectedTab = EditorTab.Settings;

            Assert.False(editor.DiscardChangesCommand.CanExecute(null));

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Empty(_notifications.MessageBoxes);
            Assert.Equal(0, session.RevertLayoutCallCount);
            Assert.Equal(0, session.RevertLightingCallCount);
        }

        // ===== The confirmation ==============================================================

        [AvaloniaFact]
        public async Task DiscardChanges_AsksFirstWithNoSuppressionKey()
        {
            var editor = await CreateLoadedEditorAsync();

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.DiscardChangesTitle, request.Title);
            Assert.Equal(KeyboardEditorViewModel.DiscardLayoutConfirmation, request.Message);
            Assert.Equal(MessageBoxButtons.YesNo, request.Buttons);
            Assert.Equal(KeyboardEditorViewModel.DiscardConfirmCaption, request.YesCaption);
            Assert.Equal(KeyboardEditorViewModel.DiscardDeclineCaption, request.NoCaption);
            Assert.Equal(MessageBoxResult.Yes, request.DestructiveResult);

            // The one that matters most: a discard destroys work and must never become unpromptable.
            Assert.Null(request.SuppressionKey);
            Assert.NotEqual(NotificationKeys.ResetLayer, request.SuppressionKey);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheLightingTab_AsksTheLightingQuestion()
        {
            var editor = await CreateLoadedEditorAsync();

            _profiles.SessionToReturn!.LightingToRevertTo = new LightingModel();

            editor.SelectedTab = EditorTab.Lighting;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            // Two sentences, one per scope: the prompt is the only thing on screen that says which
            // half is about to go.
            Assert.Equal(
                KeyboardEditorViewModel.DiscardLightingConfirmation,
                Assert.Single(_notifications.MessageBoxes).Message);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_WhenDeclined_ChangesNothing()
        {
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            var layout = editor.Layout;

            Answer(MessageBoxResult.No);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(0, session.RevertLayoutCallCount);
            Assert.Same(layout, editor.Layout);
            Assert.True(editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_WhenTheQuestionCannotBeShown_ChangesNothing()
        {
            // Same rule both resets follow: a confirmation that failed must not destroy anything,
            // and must not bring the app down either.
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no host");

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(0, session.RevertLayoutCallCount);
        }

        // ===== What the editor does with the reverted model ==================================

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheKeysTab_RebuildsTheBoardOffTheRestoredModel()
        {
            // RevertLayout replaces the KeyboardLayout, so every layer and cap view model over the
            // old one is stale. This is the assertion that fails if the discard reverts the session
            // and forgets to rebuild: the model would be clean and the board would still be drawing
            // the remap.
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            session.LayoutToRevertTo = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Same(session.LayoutToRevertTo, editor.Layout);
            Assert.False(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheLightingTab_ReparentsTheLightingPanelOntoTheRestoredModel()
        {
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;
            var restored = new LightingModel();

            session.LightingToRevertTo = restored;

            editor.SelectedTab = EditorTab.Lighting;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            // The panel edits the restored model now and not the one that was thrown away — the very
            // trap Lighting.Attach exists for on a profile switch.
            editor.Lighting.SelectModeCommand.Execute(
                editor.Lighting.Modes.Single(mode => mode.Mode == LightingMode.Wave));

            Assert.Equal(LightingMode.Wave, restored.TopLayer.Mode);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_StandsEveryInFlightInteractionDown()
        {
            // The same stand-downs a profile switch runs, and for the same reasons: the board they
            // belong to is about to be rebuilt.
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.False(editor.IsListening);
            Assert.False(_capture.IsCapturing);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheKeysTab_DropsTheProfilesUnsavedMacroNames()
        {
            // A rename names a macro this revert is about to remove, and it never reached
            // app_settings.txt — so it goes with the rest of the unsaved work, or the profile comes
            // back from the discard still amber with nothing to save.
            var editor = await CreateLoadedEditorAsync();

            editor.MarkMacroNamesDirty();

            Assert.True(editor.IsDirty);

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.False(editor.HasUnsavedMacroNames);
            Assert.False(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_OnTheLightingTab_LeavesTheMacroNamesAlone()
        {
            // The scoping again: a lighting discard has nothing to do with macro names.
            var editor = await CreateLoadedEditorAsync();

            _profiles.SessionToReturn!.LightingToRevertTo = new LightingModel();

            editor.MarkMacroNamesDirty();

            editor.SelectedTab = EditorTab.Lighting;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.True(editor.HasUnsavedMacroNames);
            Assert.True(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_WritesNothingAnywhere()
        {
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(0, session.SaveCallCount);
            Assert.Empty(_settings.KeyboardSaves);
            Assert.Empty(_files.WrittenPaths);
        }

        [AvaloniaFact]
        public async Task DiscardChanges_TouchesOnlyTheOpenProfile()
        {
            // "Only the open profile", the other half of the user's sentence. Every profile the
            // editor has visited is still alive, so a discard that walked the cache would throw away
            // work the user is not even looking at.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var second = _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Answer(MessageBoxResult.Yes);

            await editor.DiscardChangesCommand.ExecuteAsync(null);

            Assert.Equal(0, first.RevertLayoutCallCount);
            Assert.Equal(1, second.RevertLayoutCallCount);
        }

        // ===== It is not Reset Layout ========================================================

        [AvaloniaFact]
        public async Task ResetLayout_IsUntouchedAndStillMeansFactoryDefaults()
        {
            // Acceptance criterion 9. The two sit side by side in the action row and point in
            // opposite directions: a reset CLEARS the profile (an edit a save would then write), a
            // discard RESTORES what was loaded. Merging them is the mistake this pins shut.
            var editor = await CreateLoadedEditorAsync();
            var session = _profiles.SessionToReturn!;
            var layout = editor.Layout!;

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            Answer(MessageBoxResult.Yes);

            // Execute rather than ExecuteAsync: ResetLayoutCommand is declared IRelayCommand, and
            // the fake answers its confirmation synchronously, so the path runs to completion here
            // — the idiom every other reset case in the suite already uses.
            editor.ResetLayoutCommand.Execute(null);

            // The very same model, cleared in place — no revert, and no new instance.
            Assert.Same(layout, editor.Layout);
            Assert.Equal(0, session.RevertLayoutCallCount);
            Assert.False(layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);

            // ...and it is still the reset's own suppressible confirmation, not the discard's.
            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.ResetLayoutTitle, request.Title);
            Assert.Equal(NotificationKeys.ResetLayer, request.SuppressionKey);
        }

        // ===== When the command stands down ==================================================

        [AvaloniaFact]
        public void DiscardChanges_BeforeAnythingIsLoaded_IsRefused()
        {
            var editor = CreateEditor();

            Assert.True(editor.IsLoading);
            Assert.False(editor.DiscardChangesCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task DiscardChanges_WhileAFeaturePanelIsOpen_IsRefused()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.ExportCommand.Execute(null);

            Assert.NotNull(editor.ActiveOverlay);
            Assert.False(editor.DiscardChangesCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task DiscardChanges_InDemoMode_IsAvailable()
        {
            // Deliberately NOT gated on demo mode. A discard writes nowhere, and a demo session is a
            // real session over the fixture drive whose edits a user is as entitled to throw away as
            // anyone else's — it is the only one of the two buttons that still works there.
            var editor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            Assert.True(editor.IsDemoMode);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.True(editor.DiscardChangesCommand.CanExecute(null));
        }

        // ===== Fixtures ======================================================================

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(DeviceSnapshot? snapshot = null)
        {
            var editor = CreateEditor(snapshot);

            await editor.LoadAsync();

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

        /// <summary>Answers whatever box goes up next with <paramref name="result"/>.</summary>
        private void Answer(MessageBoxResult result)
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = result };
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
