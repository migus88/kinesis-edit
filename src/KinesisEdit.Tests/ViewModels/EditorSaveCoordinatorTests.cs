using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// Everything the editor does with a file, on its own — <b>no editor, no board and no drive</b>:
    /// the three gates and the one place they disagree (demo mode refuses a save and an import and
    /// allows an export), what one press of Save actually writes, and the question asked before
    /// unsaved work would be abandoned.
    /// <para>
    /// The editor is a fake host that records every write and call the coordinator makes on it, in
    /// order, which is what the stand-down triple's assertions need. The sessions and the cache are
    /// real, because the write set is a property of them.
    /// </para>
    /// </summary>
    public class EditorSaveCoordinatorTests
    {
        // ===== Demo mode: the one place the three gates disagree (invariant 9) ===============

        /// <summary>
        /// <b>Save and Import are refused, Export is not.</b> An export writes to a folder the user
        /// picked rather than to the v-Drive (11 §11.5), so 03 §3.5 has nothing to say about it —
        /// and it is where a user with no hardware gets something out of the app.
        /// </summary>
        [AvaloniaFact]
        public void InDemoMode_SaveAndImportAreRefused_AndExportIsNot()
        {
            var scene = Scene.Demo();

            scene.Host.Session = scene.Stage(1, isDirty: true);

            Assert.False(scene.Saves.CanSave());
            Assert.False(scene.Saves.SaveCommand.CanExecute(null));

            Assert.False(scene.Saves.CanImport());
            Assert.False(scene.Saves.ImportCommand.CanExecute(null));

            Assert.True(scene.Saves.CanExport());
            Assert.True(scene.Saves.ExportCommand.CanExecute(null));
        }

        /// <summary>The refusal is real, not only a greyed button: a demo press writes nothing.</summary>
        [AvaloniaFact]
        public async Task InDemoMode_Save_WritesNothingAtAll()
        {
            var scene = Scene.Demo();
            var session = scene.Stage(1, isDirty: true);

            scene.Host.Session = session;

            Assert.False(await scene.Saves.TrySaveAsync());
            Assert.Equal(0, session.SaveCallCount);
            Assert.Empty(scene.Notifications.Toasts);
        }

        /// <summary>The same three over a real drive, so the demo asymmetry is the only difference.</summary>
        [AvaloniaFact]
        public void OnARealDrive_AllThreeAreAvailable()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);

            Assert.True(scene.Saves.CanSave());
            Assert.True(scene.Saves.CanImport());
            Assert.True(scene.Saves.CanExport());
        }

        // ===== The rest of the gates =========================================================

        [AvaloniaTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void EveryGate_IsRefusedWhileALoadOrASaveIsInFlight(bool isLoading, bool isBusy)
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);
            scene.Host.IsLoading = isLoading;
            scene.Host.IsBusy = isBusy;

            Assert.False(scene.Saves.CanSave());
            Assert.False(scene.Saves.CanImport());
            Assert.False(scene.Saves.CanExport());
        }

        /// <summary>
        /// An open feature panel owns the screen, so an import and an export are refused — but
        /// <b>Save is not</b>, and that difference is deliberate: the panel that would be open over
        /// it is the export panel.
        /// </summary>
        [AvaloniaFact]
        public void AnOpenPanel_RefusesImportAndExport_ButNotSave()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);
            scene.Host.HasActiveOverlay = true;

            Assert.True(scene.Saves.CanSave());
            Assert.False(scene.Saves.CanImport());
            Assert.False(scene.Saves.CanExport());
        }

        /// <summary>A read-only profile (the Advantage 360's factory one) refuses both writes.</summary>
        [AvaloniaFact]
        public void AReadOnlyProfile_RefusesSaveAndImport_ButStillExports()
        {
            var scene = new Scene();
            var session = scene.Stage(1);

            session.CanSave = false;
            scene.Host.Session = session;

            Assert.False(scene.Saves.CanSave());
            Assert.False(scene.Saves.CanImport());
            Assert.True(scene.Saves.CanExport());
        }

        /// <summary>With no session there is nothing to write and nothing to serialize.</summary>
        [AvaloniaFact]
        public void WithNoSession_NothingIsAvailable()
        {
            var scene = new Scene();

            Assert.False(scene.Saves.CanSave());
            Assert.False(scene.Saves.CanImport());
            Assert.False(scene.Saves.CanExport());
        }

        // ===== The write set (invariant 31) ==================================================

        /// <summary>
        /// One press writes <b>every opened profile that changed, in file order, and nothing else</b>
        /// — a clean profile is never rewritten, not even the one on screen.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_WritesEveryDirtyProfile_AndLeavesTheCleanOnesAlone()
        {
            var scene = new Scene();
            var first = scene.Stage(1, isDirty: true);
            var second = scene.Stage(2);
            var third = scene.Stage(3, isDirty: true);

            scene.Host.Session = second;

            Assert.True(await scene.Saves.TrySaveAsync());

            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(0, second.SaveCallCount);
            Assert.Equal(1, third.SaveCallCount);
        }

        /// <summary>
        /// With nothing changed anywhere no file is written at all — a v-Drive is flash — and the
        /// press says so rather than looking dead.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_WithNothingChangedAnywhere_WritesNoFileAndSaysSo()
        {
            var scene = new Scene();
            var session = scene.Stage(1);

            scene.Host.Session = session;

            Assert.False(await scene.Saves.TrySaveAsync());
            Assert.Equal(0, session.SaveCallCount);

            var toast = Assert.Single(scene.Notifications.Toasts);

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, toast.Title);
            Assert.Equal(KeyboardEditorViewModel.NothingToSaveMessage, toast.Message);

            // Nothing was stood down either: the press never got as far as writing.
            Assert.Empty(scene.Host.Calls);
        }

        /// <summary>
        /// A macro rename is unsaved work no session can see — the name rides
        /// <c>app_settings.txt</c>, so the profile re-serializes identically. Without the second term
        /// of the write set it would be unsaveable.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_WritesAProfileWhoseOnlyChangeIsAMacroRename()
        {
            var scene = new Scene();
            var session = scene.Stage(3);

            scene.Host.Session = session;
            scene.Host.RenamedProfiles = new HashSet<int> { 3 };

            Assert.True(await scene.Saves.TrySaveAsync());
            Assert.Equal(1, session.SaveCallCount);

            // And the names reach the drive AFTER the layout did, so a refused save cannot leave the
            // file naming macros the drive does not have.
            Assert.Equal(1, scene.Host.PersistMacroNamesCalls);
        }

        /// <summary>
        /// <b>All-or-nothing.</b> The rejected profile is the last in file order, which is the
        /// arrangement a write-until-failure loop passes and this policy does not: profile 1 would
        /// already be on the drive by the time profile 3 was refused.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_WithSeveralProfiles_WritesNothingUntilAllOfThemValidate()
        {
            var scene = new Scene();
            var first = scene.Stage(1, isDirty: true);
            var third = scene.Stage(3, isDirty: true);

            scene.Host.Session = first;

            TestLayouts.FillMacroSlots(third.Layout, MacroCountLimitOf(third.Layout) + 1);

            Assert.False(await scene.Saves.TrySaveAsync());

            Assert.Equal(0, first.SaveCallCount);
            Assert.Equal(0, third.SaveCallCount);

            var box = Assert.Single(scene.Notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, box.Title);
            Assert.Contains(KeyboardEditorViewModel.SaveRejectedProfilesMessage, box.Message, StringComparison.Ordinal);
            Assert.Contains(KeyboardEditorViewModel.BuildProfileCaption(3), box.Message, StringComparison.Ordinal);
            Assert.Empty(scene.Notifications.Toasts);
        }

        /// <summary>
        /// With <b>one</b> profile the pre-pass adds no gate of its own: there is nothing to be
        /// atomic about, Core's own gate already writes nothing, and a second policy over one
        /// question is exactly what this app refuses everywhere else.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_WithOneProfile_AddsNoAppSideGateOfItsOwn()
        {
            var scene = new Scene();
            var only = scene.Stage(1, isDirty: true);

            scene.Host.Session = only;

            TestLayouts.FillMacroSlots(only.Layout, MacroCountLimitOf(only.Layout) + 1);

            Assert.True(await scene.Saves.TrySaveAsync());

            Assert.Equal(1, only.SaveCallCount);
            Assert.Empty(scene.Notifications.MessageBoxes);
        }

        /// <summary>The stand-down triple, in its order, before a byte is written.</summary>
        [AvaloniaFact]
        public async Task Save_StandsEveryInFlightInteractionDown_InOrder()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1, isDirty: true);

            await scene.Saves.TrySaveAsync();

            Assert.Equal(["CancelRemap", "CancelCopyKey", "DeactivateInspector"], scene.Host.Calls.Take(3));
        }

        /// <summary>
        /// The busy flag goes up around the write and comes down in a <c>finally</c>, whatever
        /// happened: a stranded one disables Save and every editing command for as long as the
        /// editor is open.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_ThatThrows_ReportsIt_AndNeverStrandsTheBusyFlag()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            session.SaveExceptionToThrow = new IOException("the drive went away");
            scene.Host.Session = session;

            Assert.False(await scene.Saves.TrySaveAsync());

            Assert.Equal([true, false], scene.Host.BusyWrites);
            Assert.Equal([KeyboardEditorViewModel.SavingCaption, null], scene.Notifications.LoadingHistory);

            var box = Assert.Single(scene.Notifications.MessageBoxes);

            Assert.StartsWith(KeyboardEditorViewModel.SaveErrorMessagePrefix, box.Message, StringComparison.Ordinal);
        }

        /// <summary>A save Core refused after the pre-pass let it through is reported, not asserted away.</summary>
        [AvaloniaFact]
        public async Task Save_ThatCoreRefuses_ReportsTheViolations()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            session.ResultToReturn = new ProfileSaveResult
            {
                Success = false,
                Violations =
                [
                    new ModelViolation
                    {
                        Kind = ModelViolationKind.MacroCountExceeded,
                        Message = "The layout holds 120 macros; the device allows 100."
                    }
                ]
            };

            scene.Host.Session = session;

            Assert.False(await scene.Saves.TrySaveAsync());

            var box = Assert.Single(scene.Notifications.MessageBoxes);

            Assert.Contains(KeyboardEditorViewModel.SaveRejectedMessage, box.Message, StringComparison.Ordinal);
            Assert.Contains("the device allows 100", box.Message, StringComparison.Ordinal);

            // Nothing landed, so the names stay in memory and the flag is never cleared behind them.
            Assert.Equal(0, scene.Host.PersistMacroNamesCalls);
        }

        /// <summary>
        /// A save that landed toasts the device's own refresh wording — and arrives amber rather
        /// than green when the profile carries advisories, because an advisory is a remark that is
        /// stated in the same breath, never a failure that blocks.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_ThatLanded_ToastsAmberWhenTheProfileCarriesAdvisories()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            session.ResultToReturn = new ProfileSaveResult
            {
                Success = true,
                Violations = [],
                PostSaveMessage = "To load Profile 1 to the keyboard, hold the SmartSet key and tap the 1 key."
            };

            scene.Host.Session = session;
            scene.Host.Advisories = AdvisoriesWithADuplicate();

            Assert.True(await scene.Saves.TrySaveAsync());

            var toast = Assert.Single(scene.Notifications.Toasts);

            Assert.Equal(ToastSeverity.Advisory, toast.Severity);
            Assert.Contains("hold the SmartSet key", toast.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// The dirty flag is a <b>re-read</b> after a save, never an assertion: each written session
        /// moved its own baseline, and a press may have written some of the held profiles and not
        /// others.
        /// </summary>
        [AvaloniaFact]
        public async Task Save_RereadsTheDirtyFlagRatherThanAssertingIt()
        {
            var scene = new Scene();
            var first = scene.Stage(1, isDirty: true);
            var third = scene.Stage(3, isDirty: true);

            // The second one is refused by Core, so it stays dirty — and so must the editor.
            third.ResultToReturn = new ProfileSaveResult { Success = true, Violations = [] };
            first.ResultToReturn = new ProfileSaveResult { Success = true, Violations = [] };

            scene.Host.Session = first;

            await scene.Saves.TrySaveAsync();

            Assert.False(scene.Host.IsDirty);

            third.IsDirty = true;

            scene.Saves.RefreshDirtyState();

            Assert.True(scene.Host.IsDirty);
        }

        // ===== The dirty flag ================================================================

        /// <summary>
        /// It asks <b>every</b> profile the editor has open, not the one on screen: an edit made in
        /// profile 3 is still unsaved work while the user is looking at profile 7.
        /// </summary>
        [AvaloniaFact]
        public void RefreshDirtyState_ReadsEveryOpenProfile()
        {
            var scene = new Scene();
            var open = scene.Stage(7);
            var other = scene.Stage(3, isDirty: true);

            scene.Host.Session = open;

            scene.Saves.RefreshDirtyState();

            Assert.True(scene.Host.IsDirty);

            other.IsDirty = false;

            scene.Saves.RefreshDirtyState();

            Assert.False(scene.Host.IsDirty);
        }

        /// <summary>
        /// An unsaved macro rename is the one deliberate exception to "app_settings.txt sits outside
        /// the dirty model": no session's line comparison can see it move.
        /// </summary>
        [AvaloniaFact]
        public void RefreshDirtyState_CountsAnUnsavedMacroRename()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);
            scene.Host.HasUnsavedMacroNames = true;

            scene.Saves.RefreshDirtyState();

            Assert.True(scene.Host.IsDirty);
        }

        // ===== Leaving with unsaved work =====================================================

        [AvaloniaFact]
        public async Task ConfirmCloseAsync_WithNothingUnsaved_LetsTheNavigationThrough()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);

            Assert.True(await scene.Saves.ConfirmCloseAsync());
            Assert.Empty(scene.Notifications.MessageBoxes);
        }

        /// <summary>
        /// A save in flight refuses outright, with no question but with a toast: leaving would
        /// dispose the editor mid-write, and the write is short enough that the navigation works the
        /// moment it finishes.
        /// </summary>
        [AvaloniaFact]
        public async Task ConfirmCloseAsync_WhileASaveIsInFlight_RefusesWithAToast()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1, isDirty: true);
            scene.Host.IsBusy = true;
            scene.Host.IsDirty = true;

            Assert.False(await scene.Saves.ConfirmCloseAsync());

            var toast = Assert.Single(scene.Notifications.Toasts);

            Assert.Equal(UnsavedChangesPrompt.SaveInProgressTitle, toast.Title);
            Assert.Empty(scene.Notifications.MessageBoxes);
        }

        /// <summary>
        /// Demo mode leaves without a word, and the test is explicit rather than falling out of the
        /// dirty flag: a demo session is a real session over the fixture drive, so an edit genuinely
        /// marks it dirty — and the question would then offer a Save that can never run.
        /// </summary>
        [AvaloniaFact]
        public async Task ConfirmCloseAsync_InDemoMode_LeavesWithoutAWord()
        {
            var scene = Scene.Demo();

            scene.Host.Session = scene.Stage(1, isDirty: true);
            scene.Host.IsDirty = true;

            Assert.True(await scene.Saves.ConfirmCloseAsync());
            Assert.Empty(scene.Notifications.MessageBoxes);
        }

        /// <summary>
        /// Answering <c>Save</c> lets the navigation through <b>only if the save actually landed</b>
        /// — letting it through after a failed one would discard the very work the question was
        /// asked about.
        /// </summary>
        [AvaloniaFact]
        public async Task ConfirmCloseAsync_AnsweredSave_LetsThroughOnlyWhenTheWriteLanded()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            scene.Host.Session = session;
            scene.Host.IsDirty = true;
            scene.Notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            Assert.True(await scene.Saves.ConfirmCloseAsync());
            Assert.Equal(1, session.SaveCallCount);
        }

        [AvaloniaFact]
        public async Task ConfirmCloseAsync_AnsweredSave_KeepsTheEditorOpenWhenTheWriteFailed()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            session.SaveExceptionToThrow = new IOException("gone");

            scene.Host.Session = session;
            scene.Host.IsDirty = true;
            scene.Notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            Assert.False(await scene.Saves.ConfirmCloseAsync());
        }

        /// <summary><c>Discard</c> is the No button of the three-answer box; it lets the caller through.</summary>
        [AvaloniaFact]
        public async Task ConfirmCloseAsync_AnsweredDiscard_LetsTheNavigationThrough()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            scene.Host.Session = session;
            scene.Host.IsDirty = true;
            scene.Notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            Assert.True(await scene.Saves.ConfirmCloseAsync());
            Assert.Equal(0, session.SaveCallCount);
        }

        /// <summary>
        /// A box that could not be put on screen keeps the editor open: losing work because the
        /// question failed is the exact outcome this guard exists to prevent.
        /// </summary>
        [AvaloniaFact]
        public async Task ConfirmCloseAsync_WithABoxThatCouldNotBeShown_KeepsTheEditorOpen()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1, isDirty: true);
            scene.Host.IsDirty = true;
            scene.Notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no window");

            Assert.False(await scene.Saves.ConfirmCloseAsync());
        }

        /// <summary>
        /// A read-only profile can hold edits it can never write, so the question is the two-answer
        /// one and <c>Yes</c> means Discard rather than Save.
        /// </summary>
        [AvaloniaFact]
        public async Task ConfirmCloseAsync_ForAProfileThatCannotBeSaved_AsksTheDiscardQuestion()
        {
            var scene = new Scene();
            var session = scene.Stage(1, isDirty: true);

            session.CanSave = false;

            scene.Host.Session = session;
            scene.Host.IsDirty = true;
            scene.Notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            Assert.True(await scene.Saves.ConfirmCloseAsync());

            Assert.Equal(UnsavedChangesPrompt.CannotSaveTitle, Assert.Single(scene.Notifications.MessageBoxes).Title);
            Assert.Equal(0, session.SaveCallCount);
        }

        // ===== Import and export =============================================================

        /// <summary>
        /// A successful import rebuilds the editor through the very <c>Apply</c> a load uses — the
        /// imported file built a brand-new model, exactly as a load would have — and stands the
        /// in-flight interactions down first, in the same order a save does.
        /// </summary>
        [AvaloniaFact]
        public async Task Import_ThatApplied_RebuildsTheEditorFromTheSession()
        {
            var scene = new Scene();
            var session = scene.Stage(1);

            scene.Host.Session = session;
            scene.FilePicker.SetFile("layout1.txt", "[caps]>[a]");

            await scene.Saves.ImportCommand.ExecuteAsync(null);

            Assert.Equal(["CancelRemap", "CancelCopyKey", "DeactivateInspector"], scene.Host.Calls.Take(3));
            Assert.Same(session, Assert.Single(scene.Host.Reapplied));
            Assert.Equal(ProfileImporter.DialogTitle, Assert.Single(scene.Notifications.Toasts).Title);
        }

        /// <summary>An import that lands after the editor went away touches nothing.</summary>
        [AvaloniaFact]
        public async Task Import_ThatLandsAfterTheEditorWentAway_TouchesNothing()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);
            scene.FilePicker.SetFile("layout1.txt", "[caps]>[a]");
            scene.Host.IsDisposed = true;

            await scene.Saves.ImportCommand.ExecuteAsync(null);

            Assert.Empty(scene.Host.Reapplied);
            Assert.Empty(scene.Notifications.Toasts);
        }

        /// <summary>A cancelled pick applies nothing and says nothing.</summary>
        [AvaloniaFact]
        public async Task Import_ThatWasCancelled_AppliesNothing()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);

            await scene.Saves.ImportCommand.ExecuteAsync(null);

            Assert.Empty(scene.Host.Reapplied);
            Assert.Empty(scene.Notifications.Toasts);
            Assert.Empty(scene.Notifications.MessageBoxes);
        }

        /// <summary>Export opens the §11.5 panel over the editor, and nothing else.</summary>
        [AvaloniaFact]
        public void Export_OpensTheExportPanel()
        {
            var scene = new Scene();

            scene.Host.Session = scene.Stage(1);

            scene.Saves.ExportCommand.Execute(null);

            Assert.IsType<ExportOverlayViewModel>(Assert.Single(scene.Host.Overlays));
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCollaborator()
        {
            var host = new FakeEditorSaveHost();
            var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var cache = new ProfileSessionCache();
            var notifications = new FakeNotificationService();
            var folders = new FakeFolderPickerService();
            var files = new FakeVDriveFileService();
            var importer = new ProfileImporter(new FakeFilePickerService());

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(null!, device, cache, notifications, folders, files, importer));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(host, null!, cache, notifications, folders, files, importer));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(host, device, null!, notifications, folders, files, importer));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(host, device, cache, null!, folders, files, importer));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(host, device, cache, notifications, null!, files, importer));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(host, device, cache, notifications, folders, null!, importer));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSaveCoordinator(host, device, cache, notifications, folders, files, null!));
        }

        private static int MacroCountLimitOf(KeyboardLayout layout)
        {
            return layout.Device.Macros.MaxMacroCount
                   ?? throw new InvalidOperationException("The fixture device has no macro-count limit.");
        }

        /// <summary>An advisory set with something in it: one position remapped onto another's token.</summary>
        private static EditorAdvisories AdvisoriesWithADuplicate()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layer.Keys[0].OriginalKey);

            var advisories = EditorAdvisories.Build(layout);

            return advisories.Total > 0 ? advisories : throw new InvalidOperationException("No advisory was produced.");
        }

        /// <summary>
        /// A coordinator over a fake editor, real sessions and a real cache — the write set is a
        /// property of those, so faking them would be faking the thing under test.
        /// </summary>
        private sealed class Scene
        {
            public FakeEditorSaveHost Host { get; } = new();

            public ProfileSessionCache Cache { get; } = new();

            public FakeNotificationService Notifications { get; } = new();

            public FakeFilePickerService FilePicker { get; } = new();

            public EditorSaveCoordinator Saves { get; }

            public Scene(DeviceSnapshot? device = null)
            {
                Saves = new EditorSaveCoordinator(
                    Host,
                    device ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                    Cache,
                    Notifications,
                    new FakeFolderPickerService(),
                    new FakeVDriveFileService(),
                    new ProfileImporter(FilePicker));
            }

            /// <summary>A board opened with no connected, writable drive (03 §3.5).</summary>
            public static Scene Demo()
            {
                return new Scene(TestDevices.CreateSnapshot(
                    DeviceId.FreestyleEdgeRgb,
                    VDriveConnectionStatus.NotDetected));
            }

            /// <summary>Files a session under <paramref name="profileNumber"/>, as <c>Apply</c> would.</summary>
            public FakeProfileSession Stage(int profileNumber, bool isDirty = false)
            {
                var session = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
                {
                    ProfileNumber = profileNumber,
                    IsDirty = isDirty
                };

                Cache.Add(session);

                return session;
            }
        }

        /// <summary>
        /// The editor as <see cref="EditorSaveCoordinator"/> sees it: the session and the flags the
        /// gates read, and a tally — in order — of the calls a write path makes back.
        /// <para>
        /// The three stand-downs are recorded into one list on purpose: their <em>order</em> is the
        /// claim, and there is deliberately no helper on the coordinator that would make it a single
        /// call.
        /// </para>
        /// </summary>
        private sealed class FakeEditorSaveHost : IEditorSaveHost
        {
            /// <summary>Every call the coordinator made, in order.</summary>
            public List<string> Calls { get; } = [];

            /// <summary>Every value the busy flag was set to, in order.</summary>
            public List<bool> BusyWrites { get; } = [];

            public IProfileSession? Session { get; set; }

            public bool IsDisposed { get; set; }

            public bool IsLoading { get; set; }

            public bool IsBusy
            {
                get => _isBusy;
                set
                {
                    _isBusy = value;

                    BusyWrites.Add(value);
                }
            }

            public bool IsDirty { get; set; }

            public bool HasActiveOverlay { get; set; }

            public bool HasUnsavedMacroNames { get; set; }

            public IReadOnlySet<int> RenamedProfiles { get; set; } = new HashSet<int>();

            public EditorAdvisories Advisories { get; set; } = EditorAdvisories.Empty;

            /// <summary>Every panel the coordinator opened over the editor.</summary>
            public List<EditorOverlayViewModel> Overlays { get; } = [];

            /// <summary>Every session an import asked to be re-applied.</summary>
            public List<IProfileSession> Reapplied { get; } = [];

            public int PersistMacroNamesCalls { get; private set; }

            private bool _isBusy;

            public void ShowOverlay(EditorOverlayViewModel overlay)
            {
                Overlays.Add(overlay);

                Calls.Add(nameof(ShowOverlay));
            }

            public void CancelRemap()
            {
                Calls.Add(nameof(CancelRemap));
            }

            public void CancelCopyKey()
            {
                Calls.Add(nameof(CancelCopyKey));
            }

            public void DeactivateInspector()
            {
                Calls.Add(nameof(DeactivateInspector));
            }

            public void ReapplyProfile(IProfileSession session)
            {
                Reapplied.Add(session);

                Calls.Add(nameof(ReapplyProfile));
            }

            public void PersistMacroNames()
            {
                PersistMacroNamesCalls++;

                Calls.Add(nameof(PersistMacroNames));
            }
        }
    }
}
