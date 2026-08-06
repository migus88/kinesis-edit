using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Transfer;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The editor's feature panels (spec 11) as the editor drives them: Tap and Hold with its
    /// firmware gate and four pre-dialog checks (§11.1), the two macro-insertion panels (§11.3,
    /// §11.6), Export (§11.5), and Import (specs/10-apps-and-ui.md, 07 §1.4) — plus the
    /// subscription bookkeeping around them, which is where this wiring can actually leak.
    /// </summary>
    public sealed class KeyboardEditorViewModelFeatureTests : IDisposable
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

        [Fact]
        public async Task TapAndHoldCommand_FollowsTheSelectionAndTheOpenPanel()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.TapAndHoldCommand.CanExecute(null));

            var key = SelectTapAndHoldTarget(editor);

            Assert.True(editor.TapAndHoldCommand.CanExecute(null));

            // A panel of its own owns the editor while it is open.
            editor.ShowOverlay(new StubOverlay());

            Assert.False(editor.TapAndHoldCommand.CanExecute(null));

            editor.CloseOverlayCommand.Execute(null);

            Assert.True(editor.TapAndHoldCommand.CanExecute(null));

            editor.SelectKeyCommand.Execute(null);

            Assert.Null(editor.SelectedKey);
            Assert.False(editor.TapAndHoldCommand.CanExecute(null));
            Assert.NotNull(key);
        }

        /// <summary>
        /// specs/11-feature-dialogs.md §11.1 is a feature a device either has or has not, and
        /// <c>TapAndHoldPrecheck</c>'s own contract says the caller asks that first. Without the
        /// guard the panel opens on a board that states no delay range at all — the assignment it
        /// writes is then reported as <c>TapAndHoldNotSupported</c> and blocks the entire save.
        /// </summary>
        [Fact]
        public async Task TapAndHoldCommand_OnADeviceWithoutTheFeature_IsUnavailable()
        {
            _profiles.SessionToReturn = new FakeProfileSession(TestLayouts.CreateLayoutWithoutTapAndHold());

            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.Layout!.Device.TapAndHold.IsSupported);

            SelectTapAndHoldTarget(editor);

            Assert.False(editor.TapAndHoldCommand.CanExecute(null));

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            Assert.Null(editor.ActiveOverlay);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task FeatureCommands_WhileASaveIsInFlight_AreUnavailable()
        {
            var observed = new List<bool>();
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            _profiles.SessionToReturn!.DuringSave = () =>
            {
                observed.Add(editor.TapAndHoldCommand.CanExecute(null));
                observed.Add(editor.InsertDelayCommand.CanExecute(null));
                observed.Add(editor.InsertSpecialActionCommand.CanExecute(null));
                observed.Add(editor.ExportCommand.CanExecute(null));
                observed.Add(editor.ImportCommand.CanExecute(null));
            };

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(new[] { false, false, false, false, false }, observed);
            Assert.True(editor.TapAndHoldCommand.CanExecute(null));
            Assert.True(editor.ExportCommand.CanExecute(null));
            Assert.True(editor.ImportCommand.CanExecute(null));
        }

        [Fact]
        public async Task TapAndHoldCommand_BelowTheFirmwareGate_RefusesWithTheGatesMessageAndOpensNothing()
        {
            // No version file at all: the RGB's tap-and-hold gate (09 §2) cannot be met by an
            // unknown firmware version.
            var editor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(TapAndHoldOverlayViewModel.OverlayTitle, request.Title);
            Assert.Equal(TapAndHoldOverlayViewModel.FirmwareRefusalMessage, request.Message);
            Assert.Equal(
                FirmwareFeatureGate.UpdateFirmwareButtonCaption,
                Assert.Single(request.CustomButtons).Caption);
            Assert.Null(editor.ActiveOverlay);
        }

        [Fact]
        public async Task TapAndHoldCommand_ForAnAlphanumericKeyOnTheTopLayer_ShowsThePreCheckRefusal()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            AssertRefused(
                editor,
                "You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.");
        }

        [Fact]
        public async Task TapAndHoldCommand_ForAKeyAlreadyAssignedOnTheOtherLayer_ShowsThePreCheckRefusal()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectTapAndHoldTarget(editor);

            AssignTapAndHold(editor.Layout!.Layers[1].FindByIndex(key.Index)!);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            AssertRefused(editor, "You cannot assign a Tap and Hold Action to the same key in both layers.");
        }

        [Fact]
        public async Task TapAndHoldCommand_WhenTheProfileHoldsTheMaximum_ShowsThePreCheckRefusal()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectTapAndHoldTarget(editor);

            FillTapAndHoldSlots(editor.Layout!, key.Index);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            AssertRefused(editor, "You have reached the maximum number of Tap and Hold actions for this Profile.");
        }

        [Fact]
        public async Task TapAndHoldCommand_ForAMacroTriggerKey_ShowsThePreCheckRefusal()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectTapAndHoldTarget(editor);

            Assert.NotEqual(0, key.Key.AssignMacro(editor.Layout!.CreateMacro()));

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            AssertRefused(editor, "You cannot assign a Tap and Hold Action to a macro trigger key.");
        }

        [Fact]
        public async Task TapAndHoldCommand_WhenAccepted_AppliesTheAssignmentAndRefreshesTheBoard()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<TapAndHoldOverlayViewModel>(editor.ActiveOverlay);

            Pick(overlay.SearchTapActionCommand, editor, Gen1("a"));
            Pick(overlay.SearchHoldActionCommand, editor, Gen1("lctrl"));

            overlay.AcceptCommand.Execute(null);

            Assert.Null(editor.ActiveOverlay);
            Assert.True(key.Key.IsTapAndHold);
            Assert.Equal(Gen1("a"), key.Key.TapAction);
            Assert.Equal(Gen1("lctrl"), key.Key.HoldAction);
            Assert.Equal(1, editor.Layout!.TapAndHoldCount);

            // The cap re-read the model even though nothing about its caption moved: Core
            // announces nothing, so the refresh has to be unconditional.
            Assert.Equal(editor.Layout.ModifiedKeyCount, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task TapAndHoldCommand_AnArmedField_CapturesTheNextKeystroke()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<TapAndHoldOverlayViewModel>(editor.ActiveOverlay);

            // §11.1's fields are filled by a real keypress, and the editor stopped capturing on
            // the way in — so arming is what has to turn the service back on.
            Assert.False(_capture.IsCapturing);

            overlay.ArmTapActionCommand.Execute(null);

            Assert.True(_capture.IsCapturing);

            _capture.RaiseKeystroke(Gen1("a"));

            Assert.Equal(Gen1("a"), overlay.TapAction);

            // The field took its action, so nothing is armed any more and the keyboard goes back
            // to the rest of the app.
            Assert.False(_capture.IsCapturing);

            overlay.CancelCommand.Execute(null);

            Assert.Null(editor.ActiveOverlay);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public async Task TapAndHoldCommand_TheSearchPicker_ClosesBackOntoTheTapAndHoldPanel()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<TapAndHoldOverlayViewModel>(editor.ActiveOverlay);

            overlay.SearchTapActionCommand.Execute(null);

            var picker = Assert.IsType<SearchKeysOverlayViewModel>(editor.ActiveOverlay);

            Assert.Equal(SearchKeysOverlayViewModel.TapActionTitle, picker.Title);

            // The picker is text entry, the panel under it is a keystroke sink: capture is
            // suspended for exactly as long as the picker is up.
            Assert.Equal(1, _capture.SuspendCount);
            Assert.True(_capture.IsSuspended);

            picker.SelectedEntry = FindEntry(picker, Gen1("a"));
            picker.AcceptCommand.Execute(null);

            Assert.Same(overlay, editor.ActiveOverlay);
            Assert.Equal(Gen1("a"), overlay.TapAction);
            Assert.Equal(1, _capture.ResumeCount);
            Assert.False(_capture.IsSuspended);
        }

        [Fact]
        public async Task TapAndHoldCommand_TheSearchPickerCancelled_ClosesBackOntoTheTapAndHoldPanelToo()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<TapAndHoldOverlayViewModel>(editor.ActiveOverlay);

            overlay.SearchHoldActionCommand.Execute(null);

            var picker = Assert.IsType<SearchKeysOverlayViewModel>(editor.ActiveOverlay);

            Assert.Equal(SearchKeysOverlayViewModel.HoldActionTitle, picker.Title);

            editor.CloseOverlayCommand.Execute(null);

            Assert.Same(overlay, editor.ActiveOverlay);
            Assert.Null(overlay.HoldAction);
            Assert.Equal(1, _capture.SuspendCount);
            Assert.Equal(1, _capture.ResumeCount);

            // ...and the restored panel is still the live one: a second picker nests just as well.
            overlay.SearchHoldActionCommand.Execute(null);

            Assert.IsType<SearchKeysOverlayViewModel>(editor.ActiveOverlay);
        }

        [Fact]
        public async Task ShowOverlay_WhileATapAndHoldPanelIsOpen_DetachesItsSearchHook()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<TapAndHoldOverlayViewModel>(editor.ActiveOverlay);
            var replacement = new StubOverlay();

            editor.ShowOverlay(replacement);

            // The replaced panel is not closed, so it can still raise its Search event — and must
            // reach nobody, or it would steal the editor back from whatever replaced it.
            overlay.SearchTapActionCommand.Execute(null);

            Assert.Same(replacement, editor.ActiveOverlay);
        }

        [Fact]
        public async Task InsertDelayCommand_FollowsWhetherAMacroIsBeingEdited()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectedTab = EditorTab.Macros;

            Assert.Null(editor.MacroPanel!.EditedMacro);
            Assert.False(editor.InsertDelayCommand.CanExecute(null));
            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));

            SelectDigitOne(editor);

            Assert.NotNull(editor.MacroPanel.EditedMacro);
            Assert.True(editor.InsertDelayCommand.CanExecute(null));
            Assert.True(editor.InsertSpecialActionCommand.CanExecute(null));

            editor.SelectKeyCommand.Execute(null);

            Assert.False(editor.InsertDelayCommand.CanExecute(null));
            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));
        }

        /// <summary>
        /// specs/11-feature-dialogs.md §11.3 and §11.6 insert into "the active macro". Selecting any
        /// macro-capable key opens an unassigned draft, so on the Keys tab — where the macro panel
        /// is not on screen at all — the two commands must stay dead: a token appended there lands
        /// in a macro the user cannot see and never assigns.
        /// </summary>
        [Fact]
        public async Task InsertDelayCommand_OnTheKeysTabWithAMacroCapableKeySelected_IsUnavailable()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.False(editor.IsMacroPanelVisible);
            Assert.NotNull(editor.MacroPanel!.EditedMacro);

            Assert.False(editor.InsertDelayCommand.CanExecute(null));
            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));

            editor.SelectedTab = EditorTab.Macros;

            Assert.True(editor.InsertDelayCommand.CanExecute(null));
            Assert.True(editor.InsertSpecialActionCommand.CanExecute(null));

            editor.SelectedTab = EditorTab.Keys;

            Assert.False(editor.InsertDelayCommand.CanExecute(null));
            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));
        }

        [Fact]
        public async Task InsertDelayCommand_WhenAccepted_AppendsTheDelayToTheMacro()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            await editor.InsertDelayCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<MacroDelayOverlayViewModel>(editor.ActiveOverlay);

            // The RGB is not gated for custom/random delays (09 §2 gates the Freestyle Edge/Pro
            // only), so the panel opens with nothing said.
            Assert.Empty(_notifications.MessageBoxes);

            overlay.CustomDelayMilliseconds = 250;
            overlay.AcceptCommand.Execute(null);

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(
                MacroDelayTokens.ResolveCustom(250, TokenDialect.Gen1),
                Assert.Single(editor.MacroPanel!.EditedMacro!.Keystrokes).Key);
        }

        [Fact]
        public async Task InsertDelayCommand_WhenCancelled_AppendsNothing()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            await editor.InsertDelayCommand.ExecuteAsync(null);

            editor.CloseOverlayCommand.Execute(null);

            Assert.Null(editor.ActiveOverlay);
            Assert.Empty(editor.MacroPanel!.Steps.Items);
            Assert.Equal(1, _capture.ResumeCount);
        }

        [Fact]
        public async Task InsertSpecialActionCommand_WhenAccepted_AppendsThePickedActionToTheMacro()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            editor.InsertSpecialActionCommand.Execute(null);

            var picker = Assert.IsType<SearchKeysOverlayViewModel>(editor.ActiveOverlay);

            Assert.Equal(SearchKeysOverlayViewModel.MacroTitle, picker.Title);

            picker.ChooseCommand.Execute(FindEntry(picker, Gen1("f13")));

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(Gen1("f13"), Assert.Single(editor.MacroPanel!.EditedMacro!.Keystrokes).Key);
        }

        [Fact]
        public async Task ExportCommand_OpensTheExportPanelAndIsRefusedInDemoMode()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.ExportCommand.CanExecute(null));

            editor.ExportCommand.Execute(null);

            var overlay = Assert.IsType<ExportOverlayViewModel>(editor.ActiveOverlay);

            Assert.True(overlay.CanExport);

            var demoEditor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            demoEditor.ExportCommand.Execute(null);

            Assert.False(demoEditor.ExportCommand.CanExecute(null));
            Assert.Null(demoEditor.ActiveOverlay);
        }

        [Fact]
        public async Task ImportCommand_InDemoModeOrOnAReadOnlyProfile_IsUnavailable()
        {
            var demoEditor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            await demoEditor.ImportCommand.ExecuteAsync(null);

            Assert.False(demoEditor.ImportCommand.CanExecute(null));
            Assert.Equal(0, _filePicker.PickCount);

            // specs/02-devices.md: the factory profile disables Import with Save.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                CanSave = false
            };

            var readOnlyEditor = await CreateLoadedEditorAsync();

            await readOnlyEditor.ImportCommand.ExecuteAsync(null);

            Assert.False(readOnlyEditor.ImportCommand.CanExecute(null));
            Assert.Equal(0, _filePicker.PickCount);
        }

        [Fact]
        public async Task ImportCommand_WhenThePickerIsCancelled_IsASilentNoOp()
        {
            var editor = await CreateLoadedEditorAsync();

            _filePicker.FileToReturn = null;

            await editor.ImportCommand.ExecuteAsync(null);

            Assert.Equal(1, _filePicker.PickCount);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.Empty(_notifications.Toasts);
            Assert.Empty(_profiles.SessionToReturn!.ImportCalls);
        }

        [Fact]
        public async Task ImportCommand_WithAFileOverTheMaximum_RefusesItAndChangesNothing()
        {
            var editor = await CreateLoadedEditorAsync();
            var layout = editor.Layout;

            _filePicker.FileToReturn = new PickedFile(
                "huge.txt",
                null,
                ImportClassifier.MaxImportBytes + 1,
                ["[F1]>[esc]"]);

            await editor.ImportCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(ProfileImporter.DialogTitle, request.Title);
            Assert.Equal(ProfileImporter.BuildTooLargeMessage("huge.txt", ImportClassifier.MaxImportBytes + 1), request.Message);
            Assert.Same(layout, editor.Layout);
            Assert.Empty(_profiles.SessionToReturn!.ImportCalls);
        }

        [Fact]
        public async Task ImportCommand_WithALayoutFile_ReplacesTheProfileAndRebuildsTheEditor()
        {
            var editor = await CreateLoadedEditorAsync();
            var originalLayout = editor.Layout;

            SelectDigitOne(editor);

            _filePicker.SetFile("layout3.txt", "[F1]>[esc]", "[ZZZ]>[a]");

            await editor.ImportCommand.ExecuteAsync(null);

            Assert.Equal(ImportedFileKind.Layout, Assert.Single(_profiles.SessionToReturn!.ImportCalls).Kind);
            Assert.NotSame(originalLayout, editor.Layout);
            Assert.Same(_profiles.SessionToReturn.Layout, editor.Layout);
            Assert.Equal(1, editor.ModifiedKeyCount);
            Assert.Equal("Remap (1)", editor.RemapCounterCaption);

            // The picture, the macro panel and the selection all come from the new model.
            Assert.Same(editor.Layout!.Layers[0].Keys[0], editor.Layers[0].Keys[0].Key);
            Assert.Same(editor.Layout, editor.MacroPanel!.Layout);
            Assert.Null(editor.SelectedKey);

            // ...and the line the parser could not apply is shown, not dropped (04 §5).
            Assert.True(editor.HasInvalidLines);
            Assert.Equal("Line 2: [ZZZ]>[a]", Assert.Single(editor.InvalidLineMessages));

            Assert.Equal(ProfileImporter.DialogTitle, Assert.Single(_notifications.Toasts).Title);
            Assert.Equal(
                "Imported 'layout3.txt' as this profile's layout.",
                _notifications.Toasts[0].Message);
            Assert.Empty(_notifications.MessageBoxes);

            // The rebuilt macro panel is wired up like the one it replaced: opening a macro on it
            // still arms the two insertion commands.
            OpenMacroEditor(editor);

            Assert.True(editor.InsertDelayCommand.CanExecute(null));
            Assert.True(editor.InsertSpecialActionCommand.CanExecute(null));
        }

        [Fact]
        public async Task ImportCommand_WithALedFile_ReplacesTheLightingAndKeepsTheLayout()
        {
            var editor = await CreateLoadedEditorAsync();
            var originalLayout = editor.Layout;

            _profiles.SessionToReturn!.LightingToImport = new Core.Lighting.LightingModel();

            _filePicker.SetFile("led3.txt", "[spectrum]>[spd3]");

            await editor.ImportCommand.ExecuteAsync(null);

            Assert.Equal(ImportedFileKind.Lighting, Assert.Single(_profiles.SessionToReturn.ImportCalls).Kind);
            Assert.Same(originalLayout, editor.Layout);
            Assert.Same(_profiles.SessionToReturn.LightingToImport, _profiles.SessionToReturn.Lighting);
            Assert.Equal(
                "Imported 'led3.txt' as this profile's lighting.",
                Assert.Single(_notifications.Toasts).Message);
        }

        [Fact]
        public async Task ImportCommand_WhenTheImportFails_ReportsItAndLeavesTheProfileAlone()
        {
            var editor = await CreateLoadedEditorAsync();
            var layout = editor.Layout;

            _profiles.SessionToReturn!.ImportExceptionToThrow = new IOException("the file went away");

            _filePicker.SetFile("layout3.txt", "[F1]>[esc]");

            await editor.ImportCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(ProfileImporter.DialogTitle, request.Title);
            Assert.Equal(MessageBoxIcon.Error, request.Icon);
            Assert.Contains("the file went away", request.Message, StringComparison.Ordinal);
            Assert.Same(layout, editor.Layout);
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task ImportCommand_WhileAKeyIsListening_CancelsTheListenFirst()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            _filePicker.SetFile("layout3.txt", "[F1]>[esc]");

            await editor.ImportCommand.ExecuteAsync(null);

            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
        }

        [Fact]
        public async Task Dispose_WithANestedSearchPickerOpen_RestoresNothing()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            await editor.TapAndHoldCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<TapAndHoldOverlayViewModel>(editor.ActiveOverlay);

            overlay.SearchTapActionCommand.Execute(null);

            var picker = Assert.IsType<SearchKeysOverlayViewModel>(editor.ActiveOverlay);

            editor.Dispose();

            Assert.Null(editor.ActiveOverlay);
            Assert.True(picker.IsClosed);
            Assert.False(_capture.HasSubscribers);
            Assert.False(_capture.IsSuspended);

            // The panel underneath was never restored, and closing it now must not resurrect it.
            overlay.Cancel();

            Assert.Null(editor.ActiveOverlay);
        }

        [Fact]
        public async Task Dispose_WithAMacroInsertionPanelOpen_ClosesItAndDropsEveryHook()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            await editor.InsertDelayCommand.ExecuteAsync(null);

            var overlay = Assert.IsType<MacroDelayOverlayViewModel>(editor.ActiveOverlay);
            var panel = editor.MacroPanel!;

            editor.Dispose();

            Assert.True(overlay.IsClosed);
            Assert.Null(editor.ActiveOverlay);
            Assert.Null(editor.MacroPanel);
            Assert.False(_capture.HasSubscribers);
            Assert.False(_capture.IsCapturing);

            // The detached panel still raises its events; none of them may reach the editor.
            panel.InsertKeystroke(Gen1("a"));

            Assert.Null(editor.MacroPanel);
            Assert.Equal(0, editor.MacroCount);
        }

        /// <summary>Asserts the editor showed §11.1's refusal for a pre-dialog check and opened nothing.</summary>
        private void AssertRefused(KeyboardEditorViewModel editor, string message)
        {
            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(TapAndHoldOverlayViewModel.OverlayTitle, request.Title);
            Assert.Equal(message, request.Message);
            Assert.Empty(request.CustomButtons);
            Assert.Null(editor.ActiveOverlay);
        }

        private static KeySearchEntry FindEntry(SearchKeysOverlayViewModel picker, KeyDefinition definition)
        {
            foreach (var entry in picker.Results)
            {
                if (entry.Definition.Code == definition.Code)
                {
                    return entry;
                }
            }

            throw new InvalidOperationException($"The picker lists no entry for key code {definition.Code}.");
        }

        private static void Pick(
            CommunityToolkit.Mvvm.Input.IRelayCommand searchCommand,
            KeyboardEditorViewModel editor,
            KeyDefinition definition)
        {
            searchCommand.Execute(null);

            var picker = Assert.IsType<SearchKeysOverlayViewModel>(editor.ActiveOverlay);

            picker.ChooseCommand.Execute(FindEntry(picker, definition));
        }

        private static KeyDefinition Gen1(string token)
        {
            return TestLayouts.Gen1Key(token);
        }

        private static void AssignTapAndHold(KeyboardKey key)
        {
            Assert.True(key.SetTapAndHold(Gen1("a"), Gen1("lctrl"), 250));
        }

        /// <summary>
        /// Fills the bottom layer with tap-and-hold assignments until the device's maximum is
        /// reached, skipping <paramref name="skippedKeyIndex"/> so the same-key check of §11.1
        /// (which is evaluated first) stays out of the way.
        /// </summary>
        private static void FillTapAndHoldSlots(KeyboardLayout layout, int skippedKeyIndex)
        {
            var maximum = layout.Device.TapAndHold.MaxPerLayout ?? 0;

            foreach (var key in layout.Layers[1].Keys)
            {
                if (layout.TapAndHoldCount >= maximum)
                {
                    return;
                }

                if (key.Index == skippedKeyIndex || !key.CanEdit || key.IsTapAndHold)
                {
                    continue;
                }

                AssignTapAndHold(key);
            }
        }

        /// <summary>
        /// Selects a top-layer position §11.1 lets a tap-and-hold onto: editable, macro-capable
        /// (so the macro-trigger check can be armed on purpose) and not one of the A-Z / 0-9 keys.
        /// </summary>
        private static KeyboardKeyViewModel SelectTapAndHoldTarget(KeyboardEditorViewModel editor)
        {
            foreach (var candidate in editor.SelectedLayer!.Keys)
            {
                if (!candidate.CanEdit
                    || !candidate.CanAssignMacro
                    || candidate.Key.OriginalKey.Table == KeyTable.LettersAndDigits)
                {
                    continue;
                }

                editor.SelectKeyCommand.Execute(candidate);

                return candidate;
            }

            throw new InvalidOperationException("The board has no position a tap-and-hold may be assigned to.");
        }

        private static KeyboardKeyViewModel SelectDigitOne(KeyboardEditorViewModel editor)
        {
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);

            return key;
        }

        /// <summary>
        /// Opens the Macros tab over a macro-capable key — what the two insertion panels of §11.3
        /// and §11.6 need, because they append to the macro the panel has <em>on screen</em>.
        /// </summary>
        private static KeyboardKeyViewModel OpenMacroEditor(KeyboardEditorViewModel editor)
        {
            editor.SelectedTab = EditorTab.Macros;

            return SelectDigitOne(editor);
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(DeviceSnapshot? snapshot = null)
        {
            var editor = new KeyboardEditorViewModel(
                snapshot ?? TestDevices.CreateSnapshot(
                    DeviceId.FreestyleEdgeRgb,
                    versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdgeRgb)),
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

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

        /// <summary>A feature panel the editor knows nothing about, used to occupy the host.</summary>
        private sealed class StubOverlay : EditorOverlayViewModel
        {
            public StubOverlay() : base("Stub")
            {
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }
    }
}
