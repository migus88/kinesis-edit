using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// <b>One press of Save writes every profile the user changed</b> (issue #133). Until this, the
    /// editor held exactly one session and Save wrote exactly that one; a profile edited and then
    /// switched away from was silently discarded, which is why the switch had to raise a modal.
    /// Every profile the user opens now stays alive, so Save's job grew and three policies came with
    /// it — each is a case here.
    /// <list type="number">
    /// <item>Only the <b>changed</b> ones are written — a clean profile is never rewritten, not even
    /// the one on screen, and a profile nobody opened has no session at all, so it is never read and
    /// never written. With nothing changed anywhere no file is written and the press says so.</item>
    /// <item>With several in the set, validation runs as a <b>pre-pass over all of them</b>: one bad
    /// profile means nothing is written, rather than the good ones landing and the bad one not. With
    /// one, the app adds no gate at all and Core's own is the only policy.</item>
    /// <item><b>One toast</b>, and the single-profile case keeps Core's wording byte for byte.</item>
    /// </list>
    /// </summary>
    public sealed class MultiProfileSaveTests : IDisposable
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

        // ===== What the write set is =========================================================

        [AvaloniaFact]
        public async Task Save_WritesEveryDirtyProfileAndSkipsTheCleanOne()
        {
            // Acceptance criterion 5, whole. Three profiles visited, two of them edited; the clean
            // one records zero Save calls even though the user walked through it.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var second = _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);
            var third = _profiles.Stage(3, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            third.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);
            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(0, second.SaveCallCount);
            Assert.Equal(1, third.SaveCallCount);
        }

        [AvaloniaFact]
        public async Task Save_NeverTouchesAProfileNobodyOpened()
        {
            // The other half of criterion 5, and the reason the cache is LAZY rather than eager: a
            // profile with no session cannot be written, and cannot even be read — which also means
            // a drive with gaps (specs/03 §5.3) never has a missing file opened behind the user's
            // back.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var ninth = _profiles.Stage(9, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            ninth.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(0, ninth.SaveCallCount);
            Assert.Equal([1], _profiles.LoadCalls.Select(call => call.ProfileNumber));
        }

        [AvaloniaFact]
        public async Task Save_PressedTwice_DoesNotRewriteTheProfilesItAlreadyWrote()
        {
            // The defect that made the baseline change necessary. `CollectSessionsToSave()` asks
            // the sessions directly, so with Core's baseline frozen at load every profile ever
            // written in this sitting would still report itself dirty and every later press of Save
            // would rewrite the lot. A successful `Save()` now moves each session's baseline to the
            // lines it wrote, so the second press finds them clean.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var third = _profiles.Stage(3, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            third.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(1, third.SaveCallCount);

            // ...and the amber goes out and stays out, rather than the next refresh flipping it
            // back on its own.
            Assert.False(editor.IsDirty);

            await editor.SaveCommand.ExecuteAsync(null);

            // Nothing at all. Not the profile the user left, and not the one on screen either.
            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(1, third.SaveCallCount);
            Assert.Empty(_files.WrittenPaths);

            Assert.Equal(
                KeyboardEditorViewModel.NothingToSaveMessage,
                _notifications.Toasts[^1].Message);
        }

        [AvaloniaFact]
        public async Task Save_WithNothingDirtyAtAll_WritesNoFileAndSaysSo()
        {
            // The user's own parenthesis — "if there were no changes - no need to override the
            // file" — and a v-Drive is flash, so an identical rewrite is a real cost. The press is
            // still not silent: it reports that there was nothing to do, which is why the button
            // stays live rather than being gated on IsDirty.
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsDirty);
            Assert.True(editor.SaveCommand.CanExecute(null));

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
            Assert.Empty(_files.WrittenPaths);

            var toast = Assert.Single(_notifications.Toasts);

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, toast.Title);
            Assert.Equal(KeyboardEditorViewModel.NothingToSaveMessage, toast.Message);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [AvaloniaFact]
        public async Task Save_AfterEditingAProfileAndLeavingIt_WritesTheProfileThatWasLeft()
        {
            // The defect the whole item exists for, stated end to end and through the model rather
            // than through a staged flag: edit profile 1, walk to profile 2, press Save — and the
            // remap made in profile 1 is what reaches the drive.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);

            _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));
            first.IsDirty = true;

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            Assert.Equal(2, editor.SelectedProfile!.Number);
            Assert.True(editor.IsDirty);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, first.SaveCallCount);
            Assert.True(first.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        // ===== The all-or-nothing validation policy ==========================================

        [AvaloniaFact]
        public async Task Save_WhenOneDirtyProfileIsOverBudget_WritesNothingAtAllAndNamesIt()
        {
            // Acceptance criterion 6. The rejected profile is the LAST one in file order, which is
            // the arrangement a write-until-failure loop passes and this policy does not: profile 1
            // would already be on the drive by the time profile 3 was refused.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var third = _profiles.Stage(3, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            third.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            // Over the device's macro budget, in the model — so the editor's own pre-pass sees it
            // through KeyboardLayout.Validate(), exactly as Core's save would have.
            TestLayouts.FillMacroSlots(third.Layout, MacroCountLimitOf(third.Layout) + 1);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(0, first.SaveCallCount);
            Assert.Equal(0, third.SaveCallCount);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, request.Title);
            Assert.Contains(KeyboardEditorViewModel.SaveRejectedProfilesMessage, request.Message, StringComparison.Ordinal);
            Assert.Contains(KeyboardEditorViewModel.BuildProfileCaption(3), request.Message, StringComparison.Ordinal);
            Assert.Empty(_notifications.Toasts);

            // Nothing landed, so nothing is clean.
            Assert.True(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task Save_WithOneProfileInTheSet_AddsNoAppSideGateOfItsOwn()
        {
            // The pre-pass is scoped to a set of SEVERAL profiles on purpose: with one file there is
            // nothing to be atomic about, Core's own gate already writes nothing, and a second gate
            // in the app would be a second policy over one question. So an over-budget single
            // profile still reaches the session, and what happens to it is Core's decision — which
            // is also what keeps "advisories never block" in the app's own hands.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            TestLayouts.FillMacroSlots(first.Layout, MacroCountLimitOf(first.Layout) + 1);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, first.SaveCallCount);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [AvaloniaFact]
        public async Task Save_WhenCoreRefusesTheOnlyProfile_KeepsTheSingleProfileWording()
        {
            // The common case must not change wording just because the mechanism grew: one profile
            // still says "The profile was not saved…" with no profile prefix on its violations.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
                ResultToReturn = new ProfileSaveResult
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
                }
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Contains(KeyboardEditorViewModel.SaveRejectedMessage, request.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(KeyboardEditorViewModel.SaveRejectedProfilesMessage, request.Message, StringComparison.Ordinal);
            Assert.Contains("the device allows 100", request.Message, StringComparison.Ordinal);
            Assert.Empty(_notifications.Toasts);
        }

        // ===== What the user is told =========================================================

        [AvaloniaFact]
        public async Task Save_ForOneProfile_ToastsCoresWordingVerbatim()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
                ResultToReturn = new ProfileSaveResult
                {
                    Success = true,
                    Violations = [],
                    PostSaveMessage = "To load Profile 1 to the keyboard, hold the SmartSet key and tap the 1 key."
                }
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(
                "To load Profile 1 to the keyboard, hold the SmartSet key and tap the 1 key.",
                Assert.Single(_notifications.Toasts).Message);
        }

        [AvaloniaFact]
        public async Task Save_ForSeveralProfiles_ToastsOnceAndNamesThemAll()
        {
            var first = StageWithMessage(1, "To load Profile 1 to the keyboard, hold the SmartSet key and tap the 1 key.");
            var third = StageWithMessage(3, "To load Profile 3 to the keyboard, hold the SmartSet key and tap the 3 key.");

            first.IsDirty = true;
            third.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            await editor.SaveCommand.ExecuteAsync(null);

            var toast = Assert.Single(_notifications.Toasts);

            // One toast, not one per profile — and it names them in file order.
            Assert.Contains("Saved profiles 1 and 3.", toast.Message, StringComparison.Ordinal);

            // ...and Core's per-profile refresh wording survives, because it is the only thing that
            // tells the user how to get each profile onto the board.
            Assert.Contains("tap the 1 key", toast.Message, StringComparison.Ordinal);
            Assert.Contains("tap the 3 key", toast.Message, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public async Task Save_ForSeveralProfilesSharingOneWording_SaysItOnce()
        {
            // The FS Edge/Pro family has a single post-save wording for every profile number (it has
            // no startup-profile concept at all), so repeating it per profile would be noise.
            var first = StageWithMessage(1, "Close the v-Drive to apply.");
            var second = StageWithMessage(2, "Close the v-Drive to apply.");

            first.IsDirty = true;
            second.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            await editor.SaveCommand.ExecuteAsync(null);

            var toast = Assert.Single(_notifications.Toasts);
            var occurrences = toast.Message.Split("Close the v-Drive to apply.").Length - 1;

            Assert.Equal(1, occurrences);
            Assert.Contains("Saved profiles 1 and 2.", toast.Message, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public async Task Save_ForThreeProfiles_ListsThemInFileOrder()
        {
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var second = _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);
            var third = _profiles.Stage(3, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            second.IsDirty = true;
            third.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);
            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal("Saved profiles 1, 2 and 3.", Assert.Single(_notifications.Toasts).Message);
        }

        // ===== Macro names, per profile ======================================================

        [AvaloniaFact]
        public async Task Save_WithRenamesInTwoProfiles_WritesOneMacroNameSetPerProfile()
        {
            // AppSettings.WithMacroNamesForProfile tombstones only its own profile's keys, so one
            // call per profile is safe — and necessary: a single call scoped to the open profile
            // would drop the rename made in the other one.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var second = _profiles.Stage(2, DeviceId.FreestyleEdgeRgb);

            var editor = await CreateLoadedEditorAsync();

            RenameTheFirstMacro(editor, "First profile macro");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[1]);

            RenameTheFirstMacro(editor, "Second profile macro");

            await editor.SaveCommand.ExecuteAsync(null);

            // Neither session reports itself dirty — a name is not in layout<n>.txt — so both are
            // in the write set only because of their rename marks. Without that term the set would
            // be empty, the press would report nothing to save, and both names would be lost when
            // the editor closed.
            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(1, second.SaveCallCount);
            Assert.Equal(new[] { 1, 2 }, _preferences.MacroNameWrites.Order());
            Assert.False(editor.HasUnsavedMacroNames);

            var stored = _preferences.Current.MacroNames;

            Assert.Contains(stored, pair => pair.Key.ProfileNumber == 1 && pair.Value == "First profile macro");
            Assert.Contains(stored, pair => pair.Key.ProfileNumber == 2 && pair.Value == "Second profile macro");
        }

        [AvaloniaFact]
        public async Task Save_WhenTheSetIsRejectedByThePrePass_WritesNoNamesForAnyProfile()
        {
            // The rule that already governed one profile, applied to the set: names must never reach
            // app_settings.txt naming macros the drive does not have — and the rename here is in the
            // profile that is FINE, so a per-profile write that ran before the set was cleared would
            // slip through.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var third = _profiles.Stage(3, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            third.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            RenameTheFirstMacro(editor, "Sign-off block");

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[2]);

            TestLayouts.FillMacroSlots(third.Layout, MacroCountLimitOf(third.Layout) + 1);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Empty(_preferences.MacroNameWrites);
            Assert.True(editor.HasUnsavedMacroNames);
        }

        // ===== Leaving the editor ============================================================

        [AvaloniaFact]
        public async Task ConfirmCloseAsync_WhenOnlyAProfileTheUserLeftIsDirty_StillAsks()
        {
            // Acceptance criterion 7. The guard asks about EVERY dirty profile now, and the profile
            // on screen is clean here — which is exactly the case a guard reading only the open
            // session would walk straight past.
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);

            _profiles.Stage(4, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[3]);

            Assert.Equal(4, editor.SelectedProfile!.Number);

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Cancel };

            Assert.False(await editor.ConfirmCloseAsync());

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(UnsavedChangesPrompt.Title, request.Title);
        }

        [AvaloniaFact]
        public async Task ConfirmCloseAsync_WhenTheUserSaves_WritesEveryDirtyProfileBeforeLeaving()
        {
            var first = _profiles.Stage(1, DeviceId.FreestyleEdgeRgb);
            var fourth = _profiles.Stage(4, DeviceId.FreestyleEdgeRgb);

            first.IsDirty = true;
            fourth.IsDirty = true;

            var editor = await CreateLoadedEditorAsync();

            await editor.SelectProfileCommand.ExecuteAsync(editor.Profiles[3]);

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            Assert.True(await editor.ConfirmCloseAsync());

            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(1, fourth.SaveCallCount);
        }

        // ===== Fixtures ======================================================================

        private FakeProfileSession StageWithMessage(int profileNumber, string postSaveMessage)
        {
            var session = _profiles.Stage(profileNumber, DeviceId.FreestyleEdgeRgb);

            session.ResultToReturn = new ProfileSaveResult
            {
                Success = true,
                Violations = [],
                PostSaveMessage = postSaveMessage
            };

            return session;
        }

        /// <summary>The device's own macro-count limit, so "over budget" is never a guessed number.</summary>
        private static int MacroCountLimitOf(KeyboardLayout layout)
        {
            return layout.Device.Macros.MaxMacroCount
                   ?? throw new InvalidOperationException("The Freestyle Edge RGB is expected to cap its macro count.");
        }

        /// <summary>
        /// Records a macro on the open profile's digit-1 position and renames it through the
        /// editor's one rename path.
        /// </summary>
        private void RenameTheFirstMacro(KeyboardEditorViewModel editor, string name)
        {
            var key = editor.SelectedLayer!.FindByIndex(TestLayouts.RgbDigitOneKeyIndex)!;

            editor.SelectKeyCommand.Execute(key);

            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            var panel = Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("a"));

            editor.Inspector.Deactivate();

            Assert.NotNull(editor.RenameMacro(editor.MacroLibrary!.Entries[0], name));
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync()
        {
            var device = TestDevices.CreateSnapshot(
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
