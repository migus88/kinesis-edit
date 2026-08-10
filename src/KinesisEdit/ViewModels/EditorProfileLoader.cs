using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Where a profile comes from and what it means for one to arrive: the first read off the drive
    /// (<c>LoadAsync</c>, total and idempotent), the picker's read of another number, the session
    /// cache's answer when the editor already holds that profile — and <see cref="Apply"/>, the one
    /// place any of those becomes the picture on screen.
    /// <para>
    /// It is a type of its own rather than eight more members on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is that all of it answers <b>one</b> question — <i>what does a profile arriving do to
    /// the editor?</i> — and that the answer has to be given in exactly one place: a load, a profile
    /// switch, an import and the layout half of <c>Discard changes</c> are four ways of asking it,
    /// and every one of them ends here. The private <c>LoadOutcome</c> record was the seam's own
    /// evidence: it lived in the root partial, and issue #115 had to leave <c>FindCachedProfile</c>
    /// behind because moving it would have carried the record out.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes <c>Layout</c>, <c>ProfileCaption</c>,
    /// <c>InvalidLineMessages</c>, <c>Layers</c> and <c>IsLoading</c> under its own names, so a view
    /// never sees this type; everything it reads or writes about the editor arrives through
    /// <see cref="IEditorProfileLoaderHost"/> rather than a back-reference, and it
    /// <b>announces nothing</b> — every one of those properties is assigned through the host, whose
    /// own setter raises the name, because a property may only be raised by the class that declares
    /// it.
    /// </para>
    /// <para>
    /// <b>It never sees <see cref="EditorSelection"/>, <see cref="EditorKeystrokeRouter"/> or
    /// <see cref="EditorSaveCoordinator"/>.</b> The layers it builds and the layer it selects are
    /// the selection's, and it reaches both through the host. It <em>is</em> handed
    /// <see cref="ProfileSessionCache"/>, for the reason <see cref="MacroNameStore"/> is: the cache
    /// is plain state with no editor of its own, and a session enters it in <see cref="Apply"/> and
    /// nowhere else — routing that one line through the host would put the rule two hops from the
    /// method it is a rule about.
    /// </para>
    /// <para>
    /// <b>Nothing here is disposable and nothing here subscribes.</b> The editor's <c>Dispose</c>
    /// therefore has nothing to say to it; the sessions it opened are released by the cache, which
    /// is the only path that lets one go.
    /// </para>
    /// </summary>
    public sealed class EditorProfileLoader
    {
        private readonly IEditorProfileLoaderHost _host;
        private readonly DeviceSnapshot _device;
        private readonly IProfileSessionFactory _profileSessions;
        private readonly ProfileSessionCache _sessionCache;

        /// <summary>
        /// The board picture, which belongs to the <b>device</b> and not to the profile: the editor
        /// resolves it once in its constructor and this holds the very same instance, never a copy.
        /// <see cref="Apply"/> is what needs it here — the layer view models are built over it —
        /// while the editor keeps reading it for <c>BoardWidth</c>/<c>BoardHeight</c> and
        /// <see cref="EditorSelection"/> for the arrow keys' geometry.
        /// </summary>
        private readonly KeyboardVisual? _visual;

        /// <summary>
        /// Whether <see cref="LoadAsync"/> has ever run. It is the idempotence half of invariant 12:
        /// the shell fires the load and forgets it, and a second call must not re-read the drive.
        /// The profile picker deliberately does not re-enter <see cref="LoadAsync"/> for that very
        /// reason — it goes through <see cref="OpenProfileAsync"/> instead.
        /// </summary>
        private bool _hasLoadStarted;

        /// <summary>
        /// Builds the loader over the editor it fills in, the device whose drive it reads, the
        /// factory that does the reading, the cache every opened profile is filed in, and the board
        /// picture the layers are built over.
        /// <para>
        /// Nothing here touches a file: construction is cheap on purpose, so the shell can swap the
        /// editor's view in immediately and let <see cref="LoadAsync"/> do the reading.
        /// </para>
        /// </summary>
        public EditorProfileLoader(
            IEditorProfileLoaderHost host,
            DeviceSnapshot device,
            IProfileSessionFactory profileSessions,
            ProfileSessionCache sessionCache,
            KeyboardVisual? visual)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _sessionCache = sessionCache ?? throw new ArgumentNullException(nameof(sessionCache));
            _visual = visual;
        }

        /// <summary>
        /// Reads the profile off the UI thread and fills the picture in. <b>Total</b>: a drive that
        /// vanished, an unreadable file, an unsupported profile — every failure is reported through
        /// the notification service and degrades to the factory-default layout with saving disabled,
        /// because the shell fires this and forgets it. <b>Idempotent</b>: a second call is a no-op
        /// (invariant 12).
        /// </summary>
        public async Task LoadAsync()
        {
            if (_host.IsDisposed || _hasLoadStarted)
            {
                return;
            }

            _hasLoadStarted = true;
            _host.IsLoading = true;

            LoadOutcome outcome;

            try
            {
                outcome = await Task.Run(LoadProfile).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                outcome = new LoadOutcome { Error = exception };
            }

            try
            {
                Apply(outcome);
            }
            finally
            {
                _host.IsLoading = false;
            }

            // The Settings tab reads its own file, and reports its own failures inline, so a
            // settings read can never stop the picture from appearing. The Lighting tab only
            // reads the picker's stored swatches; its model came with the profile above.
            await _host.Settings.LoadAsync().ConfigureAwait(true);
            await _host.Lighting.LoadAsync().ConfigureAwait(true);

            if (outcome.Error is not null)
            {
                await _host.TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = KeyboardEditorViewModel.LoadFailureTitle,
                    Message = KeyboardEditorViewModel.LoadFailureMessagePrefix + outcome.Error.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);
            }
        }

        /// <summary>
        /// The profile picker's read: the profile as the editor already holds it, or a first visit
        /// off the drive — and never a throw, because the outcome has to be decided before
        /// <see cref="Apply"/> is called (invariant 30).
        /// <para>
        /// <b>The cache is asked first</b>, and a hit touches no file at all: the session comes back
        /// with its edits intact, which is what makes a return trip both instant and lossless. Only
        /// a first visit reads, and it reads <b>off the UI thread</b> like the first load — on a real
        /// v-Drive that is two files parsed, and doing it inline would stall the app mid-click.
        /// </para>
        /// <para>
        /// It deliberately does <b>not</b> go through <see cref="LoadAsync"/>: that method's latch is
        /// once-only and must stay so.
        /// </para>
        /// </summary>
        public async Task<LoadOutcome> OpenProfileAsync(VDriveLocation location, int profileNumber)
        {
            try
            {
                return FindCachedProfile(profileNumber)
                       ?? await Task.Run(() => LoadProfileFrom(location, profileNumber)).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                return new LoadOutcome { Error = exception };
            }
        }

        /// <summary>
        /// Makes <paramref name="outcome"/> the editor: the open session, the model, the caption,
        /// the invalid-line list, the picture, the stored macro names, the shown layer and the
        /// counters — in that order, all of it.
        /// <para>
        /// <b>It mutates unconditionally, and the outcome is decided before it is called</b>
        /// (invariant 30). A guard in here would be a half-swap: a failed profile switch is refused
        /// by its caller, which is what leaves the previous profile selected, its session intact and
        /// its edits still saveable.
        /// </para>
        /// <para>
        /// <b>The one delicate thing left in the ordering is
        /// <c>applyStoredNames: !outcome.IsCacheHit</c></b>: a freshly parsed layout carries no macro
        /// names at all (they ride <c>app_settings.txt</c>, not <c>layout&lt;n&gt;.txt</c>) so they
        /// are stamped on before anything reads one — but <b>never on a cache hit</b>, where the
        /// layout is the very one the user has been renaming macros on and
        /// <c>MacroSites.ApplyNames</c> writes every site unconditionally. Re-stamping there would
        /// silently undo every unsaved rename, with nothing on screen to say so.
        /// </para>
        /// </summary>
        public void Apply(LoadOutcome outcome)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            _host.Session = outcome.Session;

            // The one place a session enters the editor's per-profile cache, so the first load, a
            // switch, an import and a discard all keep it correct without any of them saying so
            // (ProfileSessionCache). Re-filing a cached session is a no-op, and a null one — a load
            // that failed, a board with no drive — files nothing.
            _sessionCache.Add(outcome.Session);

            _host.Layout = outcome.Layout;
            _host.ProfileCaption = outcome.Session is null
                ? string.Empty
                : KeyboardEditorViewModel.BuildProfileCaption(outcome.Session.ProfileNumber);
            _host.InvalidLineMessages = BuildInvalidLineMessages(outcome.Session);

            // Which profile the picker reports is read off the session that just arrived
            // (KeyboardEditorViewModel.Profiles.cs), so a first load, an import and a profile
            // switch all announce it from one place.
            _host.RefreshSelectedProfile();

            // The picture is empty when either half of it is missing — a load that failed outright,
            // or a device whose board has not been authored yet (#39–#42).
            _host.Layers = outcome.Layout is not null && _visual is not null
                ? KeyboardLayerViewModel.BuildAll(outcome.Layout, _visual, outcome.Session?.Lighting)
                : [];

            // Before the panels: a freshly parsed layout carries no macro names at all (they ride
            // app_settings.txt, not layoutN.txt), so the stored names have to be stamped onto its
            // macros before anything reads one (KeyboardEditorViewModel.MacroNames.cs). NOT on a
            // cache hit: that layout is the one the user has been renaming macros on, and
            // MacroSites.ApplyNames writes every site unconditionally, so re-stamping it would
            // silently undo every unsaved rename.
            _host.AttachMacroNames(outcome.Layout, applyStoredNames: !outcome.IsCacheHit);

            _host.SelectLayer(_host.Layers.Count > 0 ? _host.Layers[0] : null);
            _host.RefreshCounters();

            // The lighting panel edits the very model the session hands out, so mutating it is
            // all a lighting save takes (ProfileSession.Save writes led<n>.txt whenever Lighting
            // is non-null). It shares these layer view models, so a recoloured key repaints
            // without the picture being rebuilt — on the lighting board, which is the only
            // picture that draws an LED strip (KeyboardView.ShowsLedStrips).
            _host.Lighting.Attach(outcome.Session?.Lighting, _host.Layers);
        }

        /// <summary>
        /// Rebuilds everything from a session whose <b>layout was just replaced in place</b> — a
        /// successful import and the layout half of <c>Discard changes</c>, the two paths that build
        /// a brand-new model out of an existing session exactly as a load would have.
        /// <para>
        /// It is <see cref="Apply"/> over the session's own state, and it is a method rather than
        /// two identical literals at the call sites because both of them mean the same sentence:
        /// this session's model moved, re-read all of it. Neither is a cache hit — the layout really
        /// is fresh — so the stored macro names are stamped on, which is right for both: an import
        /// replaced the macros, and a discard is going back to what the drive says.
        /// </para>
        /// </summary>
        public void ApplyReplacedLayout(IProfileSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            Apply(new LoadOutcome { Session = session, Layout = session.Layout });
        }

        /// <summary>
        /// The first read, off the UI thread. Every failure comes back as an outcome rather than a
        /// throw, which is half of what makes <see cref="LoadAsync"/> total.
        /// </summary>
        private LoadOutcome LoadProfile()
        {
            try
            {
                // A device with no location has no file to read: it edits a factory-default model
                // in memory. Demo mode is deliberately *not* asked about here — a demo board with
                // fixtures carries the synthetic DemoVDrive location (docs/app/app-shell.md, "The
                // demo v-Drive") and is loaded through this very path, by a file service that
                // serves the fixtures and refuses every write back to them. What demo mode forbids
                // is writing (03 §3.5), which CanSave and CanImport still answer; refusing to read
                // is what left demo mode showing an empty board.
                if (_device.Location is null)
                {
                    return new LoadOutcome { Layout = KeyboardLayout.Create(_device.DeviceId) };
                }

                var session = _profileSessions.Load(
                    _device.Location,
                    _device.DeviceId,
                    _device.Device.LayoutScheme.FirstProfileNumber);

                return new LoadOutcome { Session = session, Layout = session.Layout };
            }
            catch (Exception exception)
            {
                return new LoadOutcome { Layout = TryCreateFallbackLayout(), Error = exception };
            }
        }

        /// <summary>
        /// The profile as the editor already holds it, or null when this is its first visit. A hit
        /// is marked as one (<see cref="LoadOutcome.IsCacheHit"/>) because <see cref="Apply"/> must
        /// not re-stamp the stored macro names over a session whose names the user has since edited.
        /// The cache answers with the session; <see cref="LoadOutcome"/> is this class's own shape,
        /// so this wraps it — which is exactly why the wrapper had to stay behind at issue #115 and
        /// comes home now that the record has.
        /// </summary>
        private LoadOutcome? FindCachedProfile(int profileNumber)
        {
            if (_sessionCache.Find(profileNumber) is not { } cached)
            {
                return null;
            }

            return new LoadOutcome { Session = cached, Layout = cached.Layout, IsCacheHit = true };
        }

        /// <summary>
        /// Reads one profile off <paramref name="location"/> and reports what it got.
        /// <para>
        /// Unlike <see cref="LoadProfile"/>, a failure here degrades to <b>nothing</b> rather than to
        /// a factory-default layout: the first load has no editor to keep, so an empty board is the
        /// best it can do, while a switch already has a perfectly good profile open and must leave
        /// it exactly as it was. A null <see cref="LoadOutcome.Session"/> is what tells the caller so.
        /// </para>
        /// </summary>
        private LoadOutcome LoadProfileFrom(VDriveLocation location, int profileNumber)
        {
            try
            {
                var session = _profileSessions.Load(location, _device.DeviceId, profileNumber);

                return new LoadOutcome { Session = session, Layout = session.Layout };
            }
            catch (Exception exception)
            {
                return new LoadOutcome { Error = exception };
            }
        }

        private KeyboardLayout? TryCreateFallbackLayout()
        {
            try
            {
                return KeyboardLayout.Create(_device.DeviceId);
            }
            catch (Exception)
            {
                // A device with no geometry has nothing to fall back to; the editor then shows the
                // failure and an empty picture rather than crashing the shell.
                return null;
            }
        }

        private static IReadOnlyList<string> BuildInvalidLineMessages(IProfileSession? session)
        {
            if (session is null || session.InvalidLines.Count == 0)
            {
                return [];
            }

            var messages = new List<string>(session.InvalidLines.Count);

            foreach (var line in session.InvalidLines)
            {
                messages.Add(KeyboardEditorViewModel.BuildInvalidLineMessage(line));
            }

            return messages;
        }

        /// <summary>What one load attempt produced: a session (or none, in demo mode), a model, and a failure.</summary>
        public sealed record LoadOutcome
        {
            public IProfileSession? Session { get; init; }

            public KeyboardLayout? Layout { get; init; }

            public Exception? Error { get; init; }

            /// <summary>
            /// Whether this profile came out of the editor's session cache rather than off the
            /// drive. It changes exactly one thing in <see cref="Apply"/> — the stored macro names
            /// are not re-stamped — and that one thing is the difference between a switch that keeps
            /// the user's renames and one that wipes them.
            /// </summary>
            public bool IsCacheHit { get; init; }
        }
    }

    /// <summary>
    /// What <see cref="EditorProfileLoader"/> needs from the editor it fills in: the one flag a
    /// load is refused by, the six pieces of state a profile arriving replaces, the two panels it
    /// re-parents, and the five calls it ends in.
    /// <para>
    /// A narrow interface rather than a bundle of delegates because there are thirteen members, and
    /// a <see cref="KeyboardEditorViewModel"/> back-reference rather than either would let the
    /// loader reach the whole god class it was split out of. It is implemented by the editor,
    /// explicitly where the member is not already part of that class's public surface — a split must
    /// not make a private method public.
    /// </para>
    /// <para>
    /// <b>Six of these are write-only, and that is the announcement contract</b>, not an oversight:
    /// the editor declares <c>IsLoading</c>, <c>Layout</c>, <c>ProfileCaption</c>,
    /// <c>InvalidLineMessages</c> and <c>Layers</c>, so the loader assigns them through here and the
    /// editor's own setter raises the name — rule 3, "a collaborator announces nothing", with the
    /// moved code left reading exactly as it did.
    /// </para>
    /// </summary>
    public interface IEditorProfileLoaderHost
    {
        /// <summary>
        /// Whether the editor has been torn down. A load that lands after the user has already
        /// navigated away must not fill a picture nobody is looking at.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>Whether the profile is still being read; the picture is empty until it is not.</summary>
        bool IsLoading { set; }

        /// <summary>The loaded model, or null while loading (and after a load that failed outright).</summary>
        KeyboardLayout? Layout { set; }

        /// <summary>"Profile n" for a loaded profile; empty with no session, which includes a failed load.</summary>
        string ProfileCaption { set; }

        /// <summary>Lines of the loaded file that could not be applied (04 §5); shown, never dropped silently.</summary>
        IReadOnlyList<string> InvalidLineMessages { set; }

        /// <summary>
        /// The profile the editor has open. Assigned by <c>Apply</c> and read by everything that
        /// asks what is being edited — the picker, the dirty flag, the save, the discard.
        /// </summary>
        IProfileSession? Session { set; }

        /// <summary>
        /// The device's layers, in model order — <see cref="EditorSelection"/>'s state, reached
        /// through the editor because a collaborator never sees another collaborator. Read back
        /// straight after it is written: the shown layer and the lighting panel are both handed the
        /// list that was actually built.
        /// </summary>
        IReadOnlyList<KeyboardLayerViewModel> Layers { get; set; }

        /// <summary>The Settings tab, which reads its own file after the picture is up.</summary>
        KeyboardSettingsViewModel Settings { get; }

        /// <summary>
        /// The Lighting tab: re-parented onto each arriving profile's led model by <c>Apply</c>, and
        /// asked to re-read the picker's stored swatches by the load.
        /// </summary>
        LightingTabViewModel Lighting { get; }

        /// <summary>
        /// Re-reads which profile is open off the session — the single source of that fact, so a
        /// load that landed on a different number than the one asked for is reported honestly.
        /// </summary>
        void RefreshSelectedProfile();

        /// <summary>
        /// Stamps the stored macro names onto a freshly parsed layout, and <b>only</b> a freshly
        /// parsed one: <c>applyStoredNames</c> is false for a cache hit, where re-stamping would
        /// silently undo every unsaved rename.
        /// </summary>
        void AttachMacroNames(KeyboardLayout? layout, bool applyStoredNames);

        /// <summary>Switches the picture to a layer — <see cref="EditorSelection"/>'s, through the editor.</summary>
        void SelectLayer(KeyboardLayerViewModel? layer);

        /// <summary>
        /// The editor's one post-write funnel (docs/app/keyboard-editor.md, invariant 16). A profile
        /// arriving ends in it, as a call rather than an inlined copy.
        /// </summary>
        void RefreshCounters();

        /// <summary>
        /// Shows a box and returns its outcome, or null when it could not be put on screen. The load
        /// only ever <em>reports</em> a failure, so it ignores the answer.
        /// </summary>
        Task<MessageBoxOutcome?> TryShowMessageBoxAsync(MessageBoxRequest request);
    }
}
