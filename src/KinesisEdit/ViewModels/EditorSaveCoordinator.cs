using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Everything the editor does with a <b>file</b>: the amber Save and what one press of it
    /// writes (invariant 31), the import that replaces a profile's layout or lighting, the export
    /// panel, the dirty flag all three are read against, and the question asked before unsaved work
    /// would be abandoned.
    /// <para>
    /// It is a type of its own rather than a dozen more members on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is that all of it answers <b>one</b> question — <i>may the drive be written, and what
    /// exactly goes onto it?</i> — and that the three gates over that question have to be able to
    /// disagree: demo mode refuses a save and an import and <b>allows an export</b> (invariant 9),
    /// and the only thing keeping those three predicates honest is that they sit side by side.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes <see cref="SaveCommand"/>,
    /// <see cref="ImportCommand"/>, <see cref="ExportCommand"/> and <c>IsDirty</c> under its own
    /// names — the toolbar binds the first and the action row the other two — so a view never sees
    /// this type. Everything it reads about the editor arrives through
    /// <see cref="IEditorSaveHost"/> rather than a back-reference, and it <b>announces nothing</b>:
    /// <c>IsDirty</c> and <c>IsBusy</c> are assigned through the host, whose own setters raise the
    /// names and re-ask the commands.
    /// </para>
    /// <para>
    /// <b>The write set is <see cref="ProfileSessionCache"/>'s, and this only orchestrates it.</b>
    /// Which profiles a press of Save writes, whether the set validates all-or-nothing and how the
    /// outcome is worded are all decided there (invariant 31); the cache is handed to this class for
    /// the reason <see cref="MacroNameStore"/> is handed it — it is plain state with no editor of
    /// its own. The half the cache cannot know, which profiles carry a macro rename no session can
    /// see, arrives through the host.
    /// </para>
    /// <para>
    /// <b>Nothing here is disposable and nothing here subscribes.</b> The editor's <c>Dispose</c>
    /// therefore has nothing to say to it.
    /// </para>
    /// </summary>
    public sealed class EditorSaveCoordinator
    {
        /// <summary>Writes the profile back to the v-Drive; never available in demo mode (03 §3.5).</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// Imports an external <c>.txt</c> over this profile's layout or lighting
        /// (specs/10-apps-and-ui.md, 07 §1.4); never available in demo mode (03 §3.5).
        /// </summary>
        public IAsyncRelayCommand ImportCommand { get; }

        /// <summary>
        /// Opens the Export files panel (11 §11.5). <b>Deliberately not gated on demo mode</b>, and
        /// it is the one of the three that is not: an export writes to a folder the user picked
        /// rather than to the drive, so 03 §3.5 has nothing to say about it — and it is where a user
        /// with no hardware gets something out of the app, which is why mockup 1f puts it on the
        /// Demo Mode bar.
        /// </summary>
        public IRelayCommand ExportCommand { get; }

        private readonly IEditorSaveHost _host;
        private readonly DeviceSnapshot _device;
        private readonly ProfileSessionCache _sessionCache;
        private readonly INotificationService _notifications;
        private readonly IFolderPickerService _folderPicker;
        private readonly IVDriveFileService _files;
        private readonly ProfileImporter _importer;

        /// <summary>
        /// Builds the coordinator over the editor it writes for, the device whose demo flag decides
        /// two of its three gates, the cache that answers what a save writes, and the four services
        /// a write path needs: notifications, the folder picker and file service the export panel is
        /// built from, and the importer.
        /// <para>
        /// <paramref name="device"/> carries <c>IsDemoMode</c>, which is a device-level fact fixed
        /// for as long as the editor is open — the same reason <see cref="EditorSelection"/> is
        /// handed its device rather than asking the editor per gate.
        /// </para>
        /// </summary>
        public EditorSaveCoordinator(
            IEditorSaveHost host,
            DeviceSnapshot device,
            ProfileSessionCache sessionCache,
            INotificationService notifications,
            IFolderPickerService folderPicker,
            IVDriveFileService files,
            ProfileImporter importer)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _sessionCache = sessionCache ?? throw new ArgumentNullException(nameof(sessionCache));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave());
            ExportCommand = new RelayCommand(OpenExport, () => CanExport());
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => CanImport());
        }

        /// <summary>
        /// Whether the open profile may be written <em>right now</em>. Demo mode never writes
        /// (03 §3.5, invariant 9) and the Advantage 360's factory profile is read-only
        /// (<c>CanSave</c> on the session).
        /// <para>
        /// It is deliberately <b>not</b> gated on dirtiness: the amber already carries that signal,
        /// and a Save greyed out beside work that looks unsaved is worse than one that says there
        /// was nothing to do.
        /// </para>
        /// </summary>
        public bool CanSave()
        {
            // Demo mode never writes (03 §3.5), and the Advantage 360's factory profile is
            // read-only (CanSave on the session).
            return _host.Session is not null
                   && _host.Session.CanSave
                   && !_device.IsDemoMode
                   && !_host.IsLoading
                   && !_host.IsBusy;
        }

        /// <summary>Whether a file may be imported over the open profile right now.</summary>
        public bool CanImport()
        {
            // Demo mode holds no session to import into (03 §3.5), and the Advantage 360's
            // factory profile disables Import with Save (specs/02-devices.md, "Profiles 0-9") —
            // which is exactly what CanSave answers.
            return _host.Session is { CanSave: true }
                   && !_device.IsDemoMode
                   && !_host.IsLoading
                   && !_host.IsBusy
                   && !_host.HasActiveOverlay;
        }

        /// <summary>Whether the export panel may be opened right now.</summary>
        public bool CanExport()
        {
            // Deliberately not gated on demo mode. Export writes to a folder the user picked, never
            // to the v-Drive (specs/11-feature-dialogs.md §11.5), so it breaks none of 03 §3.5's
            // promise — and demo mode is where it is most useful, which is why mockup 1f puts it on
            // the Demo Mode bar. The write is scoped away from the fixture drive by path, in
            // DemoVDriveFileService, not by a mode flag here.
            return _host.Session is not null && !_host.IsLoading && !_host.IsBusy && !_host.HasActiveOverlay;
        }

        /// <summary>
        /// Runs the save sequence off the UI thread between the loading indicator's show and hide,
        /// then reports its outcome: the violations that stopped it (04 §5.3), or the device's
        /// post-save refresh wording as a toast.
        /// <para>
        /// <b>It writes every profile the user changed, not only the one on screen</b> (issue #133).
        /// The write set is <c>CollectSessionsToSave()</c> — every opened profile that is dirty, in
        /// file order, and <b>nothing else</b>: with nothing changed anywhere, no file is written at
        /// all and the press says so in a toast. With <b>more than one</b> in the set,
        /// validation runs as a pre-pass over all of them, so a rejected profile 5 cannot leave
        /// profiles 1 and 3 already on the drive; with one, the sequence is exactly what it always
        /// was and Core's own gate is the only one (see <c>BuildPreflightViolationMessage</c>). A
        /// profile nobody opened is never read and never written, because it has no session at all.
        /// </para>
        /// <para>
        /// <b>True means every profile in the set is on the drive</b>, and nothing else does. There
        /// are three ways not to get there — a session that cannot be written right now, a throw,
        /// and validation stopping the write — and the unsaved-changes guard has to tell all three
        /// apart from success, because letting a navigation through after any of them would
        /// discard the work the user asked to keep.
        /// </para>
        /// </summary>
        public async Task<bool> TrySaveAsync()
        {
            if (_host.Session is null || !CanSave())
            {
                return false;
            }

            // INVARIANT 31's two halves, joined here because they live in the two types that own
            // them: every opened profile that is dirty, plus every one carrying a macro rename no
            // session can see (a name rides app_settings.txt, so the profile re-serializes
            // identically). Empty is a legitimate answer, handled just below.
            var sessions = _sessionCache.CollectSessionsToSave(_host.RenamedProfiles);

            if (sessions.Count == 0)
            {
                // Nothing changed, so nothing is written — not even the profile on screen. Said out
                // loud rather than left as a dead press: the button is deliberately still live (the
                // amber is the dirty signal, and a Save greyed out beside work that looks unsaved is
                // worse than one that tells you there was nothing to do).
                _notifications.ShowToast(new ToastRequest
                {
                    Title = KeyboardEditorViewModel.SaveTitle,
                    Message = KeyboardEditorViewModel.NothingToSaveMessage
                });

                return false;
            }

            _host.CancelRemap();
            _host.CancelCopyKey();
            _host.DeactivateInspector();

            // Before a single byte is written, and over the whole set — see
            // BuildPreflightViolationMessage.
            if (ProfileSessionCache.BuildPreflightViolationMessage(sessions) is { } rejection)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = KeyboardEditorViewModel.SaveTitle,
                    Message = rejection,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            List<ProfileSaveOutcome>? results = null;
            Exception? error = null;

            _host.IsBusy = true;

            try
            {
                // Shown inside the try, and hidden after IsBusy is cleared below: both calls fan
                // out to the overlay, and a failure in either must not leave the flag stuck — it
                // disables Save and every editing command for as long as the editor is open.
                _notifications.ShowLoading(KeyboardEditorViewModel.SavingCaption);

                results = await Task.Run(() => ProfileSessionCache.SaveAll(sessions)).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                _host.IsBusy = false;

                _notifications.HideLoading();
            }

            if (error is not null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = KeyboardEditorViewModel.SaveTitle,
                    Message = KeyboardEditorViewModel.SaveErrorMessagePrefix + error.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            if (ProfileSessionCache.BuildRejectedSaveMessage(results!) is { } refused)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = KeyboardEditorViewModel.SaveTitle,
                    Message = refused,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            // The macro names go to app_settings.txt now, and only now: a rename is part of a
            // session's dirty model and reaches the drive when the profile does. It is written
            // AFTER the layouts landed, so a save that Core rejected cannot leave the file naming
            // macros the drive does not have (KeyboardEditorViewModel.MacroNames.cs).
            _host.PersistMacroNames();

            // Every profile in the set is on the drive, and each of them moved its own baseline to
            // the lines it wrote — so this is a re-read rather than an assertion, and it is the
            // only honest one now that a press of Save may have written some of the held profiles
            // and not others. It runs AFTER PersistMacroNames, whose cleared marks it also reads.
            RefreshDirtyState();

            var message = AdvisoryStripViewModel.BuildPostSaveMessage(
                ProfileSessionCache.BuildSavedProfilesMessage(results!),
                _host.Advisories.Total);

            if (message is not null)
            {
                // A message that counts advisories must not arrive on the success face: the amber
                // variant of mockup 1k exists for exactly this toast, and BuildPostSaveMessage has
                // already folded "saved with N advisories" into the text above. Everything was
                // still written — an advisory is a remark, never a failure — so this stays a toast
                // rather than becoming a message box (docs/design/README.md: advisories never block).
                _notifications.ShowToast(new ToastRequest
                {
                    Title = KeyboardEditorViewModel.SaveTitle,
                    Message = message,
                    Severity = _host.Advisories.Total > 0 ? ToastSeverity.Advisory : ToastSeverity.Success
                });
            }

            return true;
        }

        /// <summary>
        /// The unsaved-changes guard of docs/design/handoff.md §2 ("leaving via Home asks once
        /// (modal) unless opted out"), asked by the shell before Home or another device replaces
        /// the editor. The question itself is <see cref="UnsavedChangesPrompt"/>'s, so this board
        /// and the pedal ask it in the same words, and asking it is
        /// <see cref="ConfirmDiscardingUnsavedWorkAsync"/>'s.
        /// <para>
        /// A save in flight refuses outright, without a question — but with a toast, for the same
        /// reason the pedal editor does it (docs/app/savant-elite.md, decision 5): leaving would
        /// dispose the editor while <c>ProfileSession.Save</c> is still writing, and the write is
        /// short enough that the navigation works the moment it finishes.
        /// </para>
        /// <para>
        /// <b>What it cannot see:</b> the Settings tab. Settings are outside the session's dirty
        /// comparison by design (docs/app/keyboard-editor.md, "Settings are outside the dirty
        /// model"), so an unsaved settings row is invisible here — the tab has its own Save, and
        /// giving this guard a second, differently-shaped question would be worse than the gap.
        /// </para>
        /// </summary>
        public async Task<bool> ConfirmCloseAsync()
        {
            if (_host.IsBusy)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Title = UnsavedChangesPrompt.SaveInProgressTitle,
                    Message = UnsavedChangesPrompt.SaveInProgressMessage
                });

                return false;
            }

            return await ConfirmDiscardingUnsavedWorkAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Re-asks the sessions whether they still serialize to what was loaded. Split from the
        /// editor's <c>RefreshCounters</c> because the lighting tab moves the session without moving
        /// a counter, and calling the counter refresh from there would be a lie about what changed.
        /// <para>
        /// <b>A successful save calls it too</b>, rather than asserting <c>IsDirty = false</c>.
        /// Core moves each saved session's baseline to the lines it just wrote (issue #133,
        /// docs/app/profiles.md), so the sessions themselves already know they are clean — and with
        /// several profiles in play, only one of them may have been written. One source of truth
        /// answers; the flag is never set behind the sessions' backs.
        /// </para>
        /// </summary>
        public void RefreshDirtyState()
        {
            // Across EVERY profile the editor has open, not just the one on screen (issue #133): a
            // switch keeps the profile it leaves, so an edit made in profile 3 is still unsaved work
            // while the user is looking at profile 7 — and the amber Save, whose one press now
            // writes all of them, has to say so.
            //
            // A macro NAME is not in the layout file, so no session's line comparison can see one
            // move. It is still unsaved work the user would lose — hence the second term, the one
            // deliberate exception to "app_settings.txt sits outside the dirty model"
            // (KeyboardEditorViewModel.MacroNames.cs).
            // The open session is asked first (ProfileSessionCache.AnyIsDirty): it is the profile the
            // edit that triggered this refresh landed in, so the common path still costs exactly one
            // serialization and the other eight are never asked.
            _host.IsDirty = _sessionCache.AnyIsDirty(_host.Session) || _host.HasUnsavedMacroNames;
        }

        /// <summary>
        /// Shows a box and returns its outcome, or <b>null</b> when it could not be put on screen.
        /// Every caller that only reports something ignores the answer; the one caller that asks a
        /// question — <see cref="ConfirmCloseAsync"/> — reads null as "the user did not answer".
        /// <para>
        /// It lives here rather than on the editor because every question this app asks about a file
        /// is asked from this class; the profile picker's failure dialog reaches it through the
        /// editor's own one-line forwarder, and the load's through
        /// <see cref="IEditorProfileLoaderHost.TryShowMessageBoxAsync"/>.
        /// </para>
        /// </summary>
        public async Task<MessageBoxOutcome?> TryShowMessageBoxAsync(MessageBoxRequest request)
        {
            try
            {
                return await _notifications.ShowMessageBoxAsync(request).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // A box that cannot be put on screen (the window is already gone) must not bring
                // the app down; the editor state already carries the outcome.
                return null;
            }
        }

        /// <summary>
        /// The <see cref="SaveCommand"/> target. It drops the answer on purpose: a button press has
        /// nowhere to report a failure that <see cref="TrySaveAsync"/> has already put on screen.
        /// <see cref="ConfirmCloseAsync"/> is the caller that needs it.
        /// </summary>
        private async Task SaveAsync()
        {
            await TrySaveAsync().ConfigureAwait(true);
        }

        private void OpenExport()
        {
            if (!CanExport())
            {
                return;
            }

            _host.ShowOverlay(new ExportOverlayViewModel(_host.Session, _folderPicker, _files, _notifications));
        }

        /// <summary>
        /// Runs the import and re-renders whatever it replaced. The refresh goes through the same
        /// <c>Apply</c> the load uses — the imported file built a brand-new model, exactly
        /// as a load would have — so layers, the board, the macro panel, the counters and the
        /// invalid-line list all come from one place.
        /// </summary>
        private async Task ImportAsync()
        {
            var session = _host.Session;

            if (session is null || !CanImport())
            {
                return;
            }

            _host.CancelRemap();
            _host.CancelCopyKey();
            _host.DeactivateInspector();

            var outcome = await _importer.ImportAsync(session, _device.DeviceId).ConfigureAwait(true);

            if (_host.IsDisposed)
            {
                return;
            }

            if (outcome.WasApplied)
            {
                _host.ReapplyProfile(session);
            }

            if (outcome.FailureMessage is not null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = ProfileImporter.DialogTitle,
                    Message = outcome.FailureMessage,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return;
            }

            if (outcome.SuccessMessage is not null)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Title = ProfileImporter.DialogTitle,
                    Message = outcome.SuccessMessage
                });
            }
        }

        /// <summary>
        /// The unsaved-changes question itself, and the one reading of its answer: <b>true means
        /// the caller may go ahead and abandon the open profile</b> — because there was nothing to
        /// lose, because the user discarded it, or because a save actually landed. A failed save is
        /// false: letting the caller through after one would discard the very work the question was
        /// asked about.
        /// <para>
        /// <b>It asks about every profile the editor has open, not only the one on screen</b>
        /// (issue #133). <c>IsDirty</c> aggregates across the session cache and
        /// <see cref="TrySaveAsync"/> writes every dirty profile, so one question and one `Save`
        /// still cover the whole editor — which is what let the <em>profile switch</em> stop asking
        /// it. Its callers are now the three exits where the editor really does go away: Home,
        /// another device, and the window's close, all of them through
        /// <see cref="ConfirmCloseAsync"/>. The wording is unchanged and stays
        /// <see cref="UnsavedChangesPrompt"/>'s, so this board and the pedal ask in the same words;
        /// naming the count would fork a string the pedal shares.
        /// </para>
        /// <para>
        /// Demo mode answers true silently, and the test is explicit rather than falling out of
        /// <c>IsDirty</c>. A demo session is a real session over the fixture drive, so an
        /// edit really does make it dirty — but Save can never run there (03 §3.5), so the question
        /// would offer an answer that does nothing and a Discard for work that was never going
        /// anywhere. A load that produced no session reports itself clean anyway.
        /// </para>
        /// </summary>
        private async Task<bool> ConfirmDiscardingUnsavedWorkAsync()
        {
            if (_device.IsDemoMode || !_host.IsDirty)
            {
                return true;
            }

            // Asked once and carried: it picks the box — a read-only profile can hold edits it can
            // never write, and offering it a Save would be a question with no working answer — and
            // it is also what Yes meant, which is a different button in each of the two shapes.
            var canSave = CanSave();

            var outcome = await TryShowMessageBoxAsync(
                UnsavedChangesPrompt.Build(UnsavedChangesPrompt.KeyboardMessage, canSave, canSuppress: true))
                .ConfigureAwait(true);

            return UnsavedChangesPrompt.Interpret(outcome, canSave) switch
            {
                UnsavedChangesAnswer.Save => await TrySaveAsync().ConfigureAwait(true),
                UnsavedChangesAnswer.Discard => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// What <see cref="EditorSaveCoordinator"/> needs from the editor it writes for: the open
    /// session, the five flags its three gates are decided from, the two facts a save reads about
    /// macro names and advisories, and the six calls a write path makes.
    /// <para>
    /// A narrow interface rather than a bundle of delegates because there are thirteen members, and
    /// a <see cref="KeyboardEditorViewModel"/> back-reference rather than either would let the
    /// coordinator reach the whole god class it was split out of. It is implemented by the editor,
    /// explicitly where the member is not already part of that class's public surface — a split must
    /// not make a private method public.
    /// </para>
    /// <para>
    /// <b><see cref="IsBusy"/> and <see cref="IsDirty"/> are settable, and that is the announcement
    /// contract</b>: the editor declares both, so the coordinator assigns them through here and the
    /// editor's own setter raises the name — and, for <see cref="IsBusy"/>, re-asks every command.
    /// Rule 3, "a collaborator announces nothing", with the moved code left reading as it did.
    /// </para>
    /// <para>
    /// <b><see cref="ReapplyProfile"/> is the knot.</b> The import's refresh is
    /// <see cref="EditorProfileLoader.ApplyReplacedLayout"/>'s, and a collaborator never sees
    /// another collaborator, so the editor forwards it — the same indirection
    /// <c>IEditorSelectionHost.BeginRemap</c> is.
    /// </para>
    /// </summary>
    public interface IEditorSaveHost
    {
        /// <summary>
        /// The profile the editor has open, or null when no session was loaded (a board with no
        /// drive, a load that failed). Every gate here starts with it.
        /// </summary>
        IProfileSession? Session { get; }

        /// <summary>Whether the editor has been torn down; an import that lands afterwards stops.</summary>
        bool IsDisposed { get; }

        /// <summary>Whether the profile is still being read.</summary>
        bool IsLoading { get; }

        /// <summary>Whether a save is in flight. Set here, around the write itself.</summary>
        bool IsBusy { get; set; }

        /// <summary>
        /// Whether anything the editor holds re-serializes to something different from what was
        /// loaded — the amber Save. Set by <see cref="EditorSaveCoordinator.RefreshDirtyState"/>,
        /// which is the only thing that may.
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Whether a feature panel is open over the editor. An import and an export are refused
        /// while one is; a save is not, because the panel that would be open over it is the export.
        /// </summary>
        bool HasActiveOverlay { get; }

        /// <summary>
        /// Whether a macro has been renamed since the last save, in any profile the editor has
        /// opened — the second, session-invisible half of the dirty flag.
        /// </summary>
        bool HasUnsavedMacroNames { get; }

        /// <summary>
        /// The profiles carrying such a rename. The half <see cref="ProfileSessionCache"/> cannot
        /// answer, handed to it by the save so that neither type has to know both.
        /// </summary>
        IReadOnlySet<int> RenamedProfiles { get; }

        /// <summary>
        /// What the app has noticed about the open profile. The save reads nothing but its total,
        /// and only to word and colour the toast: <b>an advisory never blocks a write</b>.
        /// </summary>
        EditorAdvisories Advisories { get; }

        /// <summary>Opens a feature panel over the editor; the export panel is this class's one caller.</summary>
        void ShowOverlay(EditorOverlayViewModel overlay);

        /// <summary>Leaves listening state — the first of the stand-down triple a write runs.</summary>
        void CancelRemap();

        /// <summary>Drops an armed <c>Copy key…</c> — the second.</summary>
        void CancelCopyKey();

        /// <summary>Stands whatever the rail has armed down, without closing it — the third.</summary>
        void DeactivateInspector();

        /// <summary>
        /// Rebuilds the editor from a session whose layout an import just replaced —
        /// <see cref="EditorProfileLoader"/>'s, reached through the editor because collaborators do
        /// not see each other.
        /// </summary>
        void ReapplyProfile(IProfileSession session);

        /// <summary>
        /// Writes the macro names of every profile carrying a rename to <c>app_settings.txt</c>. It
        /// stays on the editor because its per-profile clear raises <c>HasUnsavedMacroNames</c>, and
        /// a property may only be raised by the class that declares it.
        /// </summary>
        void PersistMacroNames();
    }
}
