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
        public async Task FeatureCommands_WhileASaveIsInFlight_AreUnavailable()
        {
            var observed = new List<bool>();
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            // Staged dirty, or there would be no save to be in flight (issue #133: Save writes only
            // the profiles that changed).
            _profiles.SessionToReturn!.IsDirty = true;
            _profiles.SessionToReturn.DuringSave = () =>
            {
                observed.Add(editor.InsertSpecialActionCommand.CanExecute(null));
                observed.Add(editor.ExportCommand.CanExecute(null));
                observed.Add(editor.ImportCommand.CanExecute(null));
            };

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(new[] { false, false, false }, observed);
            Assert.True(editor.ExportCommand.CanExecute(null));
            Assert.True(editor.ImportCommand.CanExecute(null));
        }

        /// <summary>
        /// §11.1 is a rail panel now, not a modal — so the firmware gate of 09 §2 is answered
        /// <em>in place</em>. The panel is still rendered, which is the sanctioned exception to
        /// "absent features are not shown": the user pointed at this tab.
        /// </summary>
        [Fact]
        public async Task TheTapAndHoldPanel_BelowTheFirmwareGate_RefusesInPlaceAndOpensNoDialog()
        {
            // No version file at all: the RGB's tap-and-hold gate cannot be met by an unknown
            // firmware version.
            var editor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            SelectTapAndHoldTarget(editor);

            var panel = OpenTapAndHoldPanel(editor);

            Assert.False(panel.IsAvailable);
            Assert.Equal(TapAndHoldPanelViewModel.FirmwareRefusalFor(DeviceId.FreestyleEdgeRgb), panel.UnavailableReason);
            Assert.True(panel.CanUpdateFirmware);

            // Nothing modal happened: the rail is not an overlay and the gate raises no message box
            // of its own any more.
            Assert.Null(editor.ActiveOverlay);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task TheTapAndHoldPanel_OnAnAlphanumericTopLayerKey_ShowsThePreCheckRefusalInPlace()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var panel = OpenTapAndHoldPanel(editor);

            Assert.False(panel.IsAvailable);
            Assert.Equal(
                "You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.",
                panel.UnavailableReason);

            // A pre-dialog refusal is not a firmware refusal, so there is nowhere to send the user.
            Assert.False(panel.CanUpdateFirmware);
        }

        [Fact]
        public async Task TheTapAndHoldPanel_WhenAssigned_WritesTheAssignmentAndRefreshesTheBoard()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectTapAndHoldTarget(editor);
            var panel = OpenTapAndHoldPanel(editor);

            panel.AssignAction(TapAndHoldField.Tap, Gen1("a"));
            panel.AssignAction(TapAndHoldField.Hold, Gen1("lctrl"));
            panel.AssignCommand.Execute(null);

            Assert.True(key.Key.IsTapAndHold);
            Assert.Equal(Gen1("a"), key.Key.TapAction);
            Assert.Equal(Gen1("lctrl"), key.Key.HoldAction);
            Assert.Equal(1, editor.Layout!.TapAndHoldCount);

            // The Assigned hook is what makes this true: Core announces nothing, so the cap and
            // every counter above it are re-read by hand.
            Assert.True(key.IsTapAndHold);
            Assert.Equal(editor.Layout.ModifiedKeyCount, editor.ModifiedKeyCount);

            // And the rail is still open on the same position afterwards — nothing about a write
            // dismisses it.
            Assert.True(editor.Inspector.IsOpen);
        }

        [Fact]
        public async Task TheTapAndHoldPanel_AnArmedField_TakesTheNextKeystrokeThroughTheEditorsRouter()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            var panel = OpenTapAndHoldPanel(editor);

            // Capture belongs to the editor: the panel only says it is waiting for a keypress.
            Assert.False(_capture.IsCapturing);

            panel.ArmTapActionCommand.Execute(null);

            Assert.True(_capture.IsCapturing);
            Assert.True(editor.IsCaptureActive);

            _capture.RaiseKeystroke(Gen1("a"));

            Assert.Equal(Gen1("a"), panel.TapAction);

            // The field took its action, so nothing is armed and the keyboard goes back to the app.
            Assert.False(_capture.IsCapturing);
            Assert.False(editor.IsCaptureActive);
        }

        /// <summary>
        /// §11.1's <c>Search</c> used to nest a second modal over the first — the only consumer
        /// <c>EditorOverlayHost.ShowNested</c> ever had. It is inline in the panel now, so nothing
        /// is opened over anything.
        /// </summary>
        [Fact]
        public async Task TheTapAndHoldPanel_Search_OpensThePickerInsideThePanelAndOpensNoOverlay()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectTapAndHoldTarget(editor);

            var panel = OpenTapAndHoldPanel(editor);

            panel.SearchHoldActionCommand.Execute(null);

            Assert.True(panel.IsPickerOpen);
            Assert.Equal(TapAndHoldPanelViewModel.HoldFieldLabel, panel.PickerFieldLabel);
            Assert.Null(editor.ActiveOverlay);
            Assert.False(editor.HasActiveOverlay);

            Pick(panel.Picker, Gen1("lctrl"));

            Assert.False(panel.IsPickerOpen);
            Assert.Equal(Gen1("lctrl"), panel.HoldAction);

            // Nothing is written until Assign: the pick fills a field, it does not touch the model.
            Assert.Equal(0, editor.Layout!.TapAndHoldCount);
        }

        /// <summary>
        /// The rail is not modal, so a panel that is merely <em>showing</em> must let a keystroke
        /// fall through to the cap the user is remapping beside it. That is the one behavioural
        /// difference from the overlay this replaced, and it is the difference this pins.
        /// </summary>
        [Fact]
        public async Task AnUnarmedInspectorPanel_LetsTheKeystrokeReachTheListeningKey()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectTapAndHoldTarget(editor);

            OpenTapAndHoldPanel(editor);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            _capture.RaiseKeystroke(Gen1("f13"));

            Assert.True(key.Key.IsModified);
            Assert.Equal(Gen1("f13").Code, key.Key.ModifiedKey!.Code);
            Assert.False(editor.IsListening);
        }

        [Fact]
        public async Task InsertSpecialActionCommand_FollowsWhetherAMacroIsBeingEdited()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            // The rail is on Remap and the position carries no macro: there is nothing to insert
            // into, and the picked token would land where the user cannot see it.
            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));

            OpenMacroEditor(editor);

            Assert.True(editor.InsertSpecialActionCommand.CanExecute(null));

            editor.SelectKeyCommand.Execute(null);

            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));
        }

        /// <summary>
        /// specs/11-feature-dialogs.md §11.6 inserts into "the active macro", which since issue #93
        /// is whatever the <b>key inspector's</b> Macro panel has open. With the rail on any other
        /// mode the command must stay dead: a token appended there lands in a macro the user is not
        /// looking at.
        /// <para>
        /// §11.3's insertion is no longer one of these: it is edited in place on the same panel, so
        /// there is no delay command left to gate.
        /// </para>
        /// </summary>
        [Fact]
        public async Task InsertSpecialActionCommand_WithTheRailOnAnotherMode_IsUnavailable()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            Assert.True(editor.InsertSpecialActionCommand.CanExecute(null));

            SelectMode(editor, KeyInspectorMode.Remap);

            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));

            SelectMode(editor, KeyInspectorMode.Macro);

            Assert.True(editor.InsertSpecialActionCommand.CanExecute(null));

            // The rail closed is the same answer: nothing is on screen to insert into.
            editor.Inspector.CloseCommand.Execute(null);

            Assert.False(editor.InsertSpecialActionCommand.CanExecute(null));
        }

        [Fact]
        public async Task InsertSpecialActionCommand_WhenAccepted_AppendsThePickedActionToTheMacro()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            editor.InsertSpecialActionCommand.Execute(null);

            var overlay = Assert.IsType<TokenPickerOverlayViewModel>(editor.ActiveOverlay);

            Assert.Equal(TokenPickerOverlayViewModel.MacroTitle, overlay.Title);

            Pick(overlay.Picker, Gen1("f13"));

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(Gen1("f13"), Assert.Single(editor.Layout!.EnumerateMacros()).Keystrokes[^1].Key);

            // One RecentTokenStore per editor: an action inserted into a macro here is offered by
            // the key inspector's own Recent chip afterwards. Since issue #93 the rail is left on
            // its MACRO panel by this flow — that is where a macro is edited now — so the Remap
            // panel, which is the one that owns a token picker, is asked for by name.
            SelectMode(editor, KeyInspectorMode.Remap);

            var remap = Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            Assert.Same(overlay.Picker.Recent, remap.Picker.Recent);
            Assert.True(remap.Picker.Recent.Contains(Gen1("f13")));
        }

        [Fact]
        public async Task ExportCommand_OpensTheExportPanel_InDemoModeToo()
        {
            // Export is the one transfer action demo mode keeps, and this reverses a recorded
            // deviation: §11.5 writes to a folder the user picked, never to the v-Drive, so it
            // breaks none of 03 §3.5's promise — and a user with no hardware is exactly who wants
            // a layout file out of the app. Mockup 1f puts it on the Demo Mode bar for that reason.
            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.ExportCommand.CanExecute(null));

            editor.ExportCommand.Execute(null);

            var overlay = Assert.IsType<ExportOverlayViewModel>(editor.ActiveOverlay);

            Assert.True(overlay.CanExport);

            var demoEditor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            Assert.True(demoEditor.ExportCommand.CanExecute(null));

            demoEditor.ExportCommand.Execute(null);

            var demoOverlay = Assert.IsType<ExportOverlayViewModel>(demoEditor.ActiveOverlay);

            Assert.True(demoOverlay.CanExport);
        }

        [Fact]
        public async Task ExportCommand_WithNoDriveAndNoDemoContent_IsStillRefused()
        {
            // The other side of the line above. Export needs something to serialize, and a board
            // the demo gate does not answer for opens no session — so the command is refused there
            // exactly as it always was, by the clause that was never about demo mode.
            var editor = await CreateLoadedEditorAsync(
                DeviceSnapshot.CreateDemo(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb), new FakeDemoDeviceProvider()));

            editor.ExportCommand.Execute(null);

            Assert.False(editor.ExportCommand.CanExecute(null));
            Assert.Null(editor.ActiveOverlay);
        }

        [Fact]
        public async Task ImportCommand_InDemoModeOrOnAReadOnlyProfile_IsUnavailable()
        {
            // Import is a *write* — it replaces the open profile, and the point of it is to save
            // the result — so unlike Export it stays refused in demo mode even though the session
            // is now real and the file picker would work. `!IsDemoMode` in CanImport is the whole
            // of that refusal now that "no session" no longer does it.
            var demoEditor = await CreateLoadedEditorAsync(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            Assert.NotNull(_profiles.SessionToReturn);

            await demoEditor.ImportCommand.ExecuteAsync(null);

            Assert.False(demoEditor.ImportCommand.CanExecute(null));
            Assert.Empty(_profiles.SessionToReturn.ImportCalls);
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

            // The picture, the macro library and the selection all come from the new model.
            Assert.Same(editor.Layout!.Layers[0].Keys[0], editor.Layers[0].Keys[0].Key);
            Assert.Same(editor.Layout, editor.MacroLibrary!.Layout);
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
            // still arms the insertion command.
            OpenMacroEditor(editor);

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
        public async Task Dispose_WithAMacroInsertionPanelOpen_ClosesItAndDropsEveryHook()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroEditor(editor);

            editor.InsertSpecialActionCommand.Execute(null);

            var overlay = Assert.IsType<TokenPickerOverlayViewModel>(editor.ActiveOverlay);
            var macro = Assert.Single(editor.Layout!.EnumerateMacros());
            var length = macro.Keystrokes.Count;

            editor.Dispose();

            Assert.True(overlay.IsClosed);
            Assert.Null(editor.ActiveOverlay);
            Assert.False(_capture.HasSubscribers);
            Assert.False(_capture.IsCapturing);

            // The closed panel still raises its own Selected event; the hook came off with it on
            // Closed, so nothing it says may reach a macro any more.
            Pick(overlay.Picker, Gen1("f13"));

            Assert.Equal(length, macro.Keystrokes.Count);
        }

        /// <summary>
        /// Puts the key inspector on its Tap &amp; hold mode and hands the panel back. The rail
        /// exposes only the showing panel, so this is also what proves the tab reaches it.
        /// </summary>
        private static TapAndHoldPanelViewModel OpenTapAndHoldPanel(KeyboardEditorViewModel editor)
        {
            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode != KeyInspectorMode.TapAndHold)
                {
                    continue;
                }

                editor.Inspector.SelectModeCommand.Execute(tab);

                return Assert.IsType<TapAndHoldPanelViewModel>(editor.Inspector.ActivePanel);
            }

            throw new InvalidOperationException("The key inspector carries no Tap and hold tab.");
        }

        /// <summary>Takes <paramref name="definition"/>'s row in <paramref name="picker"/>, as the pointer would.</summary>
        private static void Pick(TokenPickerViewModel picker, KeyDefinition definition)
        {
            foreach (var row in picker.Rows)
            {
                if (row.Definition.Code == definition.Code)
                {
                    picker.ChooseCommand.Execute(row);

                    return;
                }
            }

            throw new InvalidOperationException($"The picker lists no row for key code {definition.Code}.");
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
        /// <summary>
        /// Opens a macro where issue #93 put the editor: the board's position, on the key
        /// inspector's Macro panel, with one keystroke already recorded so there IS a macro.
        /// </summary>
        private KeyboardKeyViewModel OpenMacroEditor(KeyboardEditorViewModel editor)
        {
            var key = SelectDigitOne(editor);

            SelectMode(editor, KeyInspectorMode.Macro);

            var panel = Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(Gen1("a"));

            panel.Deactivate();

            return key;
        }

        /// <summary>Puts the key inspector on <paramref name="mode"/> through its own tab.</summary>
        private static void SelectMode(KeyboardEditorViewModel editor, KeyInspectorMode mode)
        {
            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == mode)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);

                    return;
                }
            }
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
