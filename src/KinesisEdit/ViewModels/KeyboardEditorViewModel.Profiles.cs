using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The profile picker: <b>which numbered profile the editor is editing</b>, and the switch that
    /// points it at another one. Split out of <see cref="KeyboardEditorViewModel"/>'s main file for
    /// the reason <c>…Legend.cs</c>, <c>…Inspector.cs</c> and <c>…MacroLibrary.cs</c> were — that
    /// file is the largest in the app and docs/guides/Coding Conventions.md forbids growing it into
    /// a god class. Everything here is the same class and runs on the same rules.
    ///
    /// <para><b>This is <c>ProfileSession.ProfileNumber</c>, not
    /// <c>KeyboardSettings.StartupProfileNumber</c>.</b> The picker chooses which
    /// <c>layout&lt;n&gt;.txt</c> + <c>led&lt;n&gt;.txt</c> pair is open in the editor — the pair
    /// <c>SmartSet + &lt;n&gt;</c> switches on the board. Which profile the keyboard powers on into
    /// is a settings key, edited by a slider on the Settings tab (docs/app/settings.md), and this
    /// switch never touches it: nothing here writes anything, anywhere. Until issue #128 the editor
    /// could only ever open <c>LayoutScheme.FirstProfileNumber</c>, and the other eight profiles on
    /// the drive were unreachable.</para>
    ///
    /// <para><b>The switch is a load, not a mutation.</b> It runs the same
    /// <see cref="IProfileSessionFactory"/> read the first load runs, off the UI thread, and hands
    /// the result to the same <c>Apply</c> — which is what rebuilds the session, the layout, the
    /// layers, the macro library, the counters and the lighting model in one place. It deliberately
    /// does <b>not</b> re-enter <c>LoadAsync</c>: that method's <c>_hasLoadStarted</c> latch is
    /// once-only, and it must stay so.</para>
    ///
    /// <para><b>Nothing is half-swapped.</b> The outcome is decided before <c>Apply</c> is called,
    /// because <c>Apply</c> mutates unconditionally — so a load that failed leaves the previous
    /// profile selected, its session intact and its edits still saveable, and says so in a
    /// dialog.</para>
    ///
    /// <para><b>Switching asks nothing and abandons nothing (issue #133).</b> Every profile the user
    /// has opened stays alive in <c>_sessions</c>, keyed by number, so a switch is a move between
    /// windows onto the same drive rather than a hand-off: the outgoing session keeps its unsaved
    /// edits, the incoming one is <b>reused</b> where it has been visited before, and the toolbar's
    /// one Save writes every profile that changed. That is what removed the modal this switch used
    /// to raise — <c>ConfirmDiscardingUnsavedWorkAsync</c> is still asked before Home, a device swap
    /// and the window's close, where the editor really does go away, and it now asks about
    /// <em>every</em> dirty profile because <c>IsDirty</c> aggregates across the cache.</para>
    ///
    /// <para><b>The cache is lazy — load on first visit, never eagerly.</b> A drive may legitimately
    /// ship with gaps (specs/03 §5.3) and <c>IVDriveFileService.ReadAllLines</c> throws for a missing
    /// file, so reading all nine up front would need an "empty slot" concept Core does not have. It
    /// is also what makes "a profile with no changes is never rewritten" true by construction: a
    /// profile nobody opened has no session, so there is nothing for a save to walk.</para>
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <summary>
        /// Every profile of this device, in file order — one per number in
        /// <see cref="LayoutFileScheme.FirstProfileNumber"/>..<see cref="LayoutFileScheme.LastProfileNumber"/>,
        /// empty on a device that has no numbered profiles at all (the Advantage2's named
        /// positions, the Savant Elite2's one file, a board with no layout files). Built once from
        /// device facts, exactly like <see cref="Tabs"/>: a profile exists whether or not it has
        /// ever been opened, so nothing here waits for a load.
        /// </summary>
        public IReadOnlyList<ProfileOptionViewModel> Profiles { get; }

        /// <summary>
        /// The profile the editor has open, or null when no session was loaded (a board with no
        /// drive, a load that failed). It <b>follows the session</b> rather than the click: the
        /// picker is a report of what is being edited, so a refused or failed switch leaves it
        /// where it was and the ComboBox snaps back.
        /// </summary>
        public ProfileOptionViewModel? SelectedProfile
        {
            get => _selectedProfile;
            private set => SetProperty(ref _selectedProfile, value);
        }

        /// <summary>
        /// Whether the picker is rendered at all — a feature the board lacks is not rendered
        /// rather than disabled (docs/design/README.md). False in demo mode (nothing may be
        /// switched to, because nothing may be written and the fixture drive holds one profile),
        /// false without a drive to read another profile from, and false on a device with a single
        /// profile, where a picker of one row would be furniture.
        /// <para>
        /// Every term is fixed for as long as the editor is open — the snapshot's location and demo
        /// flag do not move, and the list is built from the device — so this announces nothing.
        /// </para>
        /// </summary>
        public bool HasProfiles => !IsDemoMode && Device.Location is not null && Profiles.Count > 1;

        /// <summary>
        /// Opens another profile. <b>The one write path</b>: the picker's selection is one-way, so
        /// this command is the only thing that can move the editor to a different profile, and
        /// every guard lives in it rather than being restated at a binding.
        /// </summary>
        public IAsyncRelayCommand<ProfileOptionViewModel> SelectProfileCommand { get; }

        /// <summary>
        /// Every profile this editor has opened, by number — <b>the editor's whole multi-profile
        /// state</b>. A session enters it in <c>Apply</c>, which is the one place a session becomes
        /// the open one, and leaves it only in <see cref="Dispose"/>. Nothing else may add to it: a
        /// session that was never applied was never the user's, and a second dictionary of
        /// "sessions we happen to be holding" is exactly the thing that would let the picker and the
        /// save loop disagree.
        /// </summary>
        private readonly Dictionary<int, IProfileSession> _sessions = [];

        private ProfileOptionViewModel? _selectedProfile;

        /// <summary>
        /// The picker's rows, from the device's file-naming scheme alone. A device whose scheme is
        /// not <see cref="LayoutSchemeKind.NumberedProfiles"/> gets none: its
        /// first/last profile numbers are both 0, and a "Profile 0" row for a board that has no
        /// numbered profiles would be an invention.
        /// </summary>
        private IReadOnlyList<ProfileOptionViewModel> BuildProfileOptions()
        {
            var scheme = Device.Device.LayoutScheme;

            if (scheme.Kind != LayoutSchemeKind.NumberedProfiles)
            {
                return [];
            }

            var options = new List<ProfileOptionViewModel>();

            for (var number = scheme.FirstProfileNumber; number <= scheme.LastProfileNumber; number++)
            {
                options.Add(new ProfileOptionViewModel(number, IsReadOnlyProfile(scheme, number)));
            }

            return options;
        }

        /// <summary>
        /// Core's own read-only rule (<c>ProfileSession.IsReadOnlyProfile</c>), asked here so the
        /// picker states it once. It is what <see cref="IProfileSession.CanSave"/> answers for a
        /// loaded session; this is the same question about a profile nobody has opened yet.
        /// </summary>
        private static bool IsReadOnlyProfile(LayoutFileScheme scheme, int profileNumber)
        {
            return profileNumber == 0 && scheme.HasReadOnlyFactoryProfile;
        }

        /// <summary>
        /// Whether <paramref name="option"/> may be opened <em>right now</em>. Deliberately not a
        /// test of "is it a different profile": the picker renders every row from this, and a row
        /// that cannot be re-selected would read as broken — re-selecting the open profile is a
        /// no-op inside <see cref="SelectProfileAsync"/> instead, where it belongs, because a
        /// reload would throw away unsaved edits to answer a click that asked for nothing.
        /// <para>
        /// The three "busy" terms are the same ones every editing command carries: a save
        /// serializes the model on a background thread, a load is already replacing it, and an open
        /// feature panel owns the screen. A <b>read-only</b> profile is refused because it has no
        /// file on the drive at all — Core's <c>Load</c> throws <c>ProfileReadOnlyException</c> for
        /// it (docs/app/profiles.md, "Profile-0 guard"), so opening it could only ever produce an
        /// error dialog.
        /// </para>
        /// </summary>
        private bool CanSelectProfile(ProfileOptionViewModel? option)
        {
            return option is not null
                   && !_isDisposed
                   && HasProfiles
                   && !option.IsReadOnly
                   && !IsLoading
                   && !IsBusy
                   && ActiveOverlay is null;
        }

        /// <summary>
        /// The command's target, and the one place a refusal is <b>reported back to the control</b>.
        /// <para>
        /// The picker binds one way, so by the time this runs the ComboBox is already showing the
        /// row the user clicked while the editor is still on the profile it loaded. Re-announcing
        /// the unchanged <see cref="SelectedProfile"/> is what pushes the truth back through that
        /// binding — <c>SetProperty</c> would not, because nothing moved — so a Cancel, a refusal
        /// and a failed load all leave the control agreeing with the editor.
        /// </para>
        /// </summary>
        private async Task SelectProfileAsync(ProfileOptionViewModel? option)
        {
            if (!await TrySelectProfileAsync(option).ConfigureAwait(true))
            {
                OnPropertyChanged(nameof(SelectedProfile));
            }
        }

        /// <summary>
        /// Points the editor at <paramref name="option"/>: stand every in-flight interaction down,
        /// take the profile from the session cache or read it off the UI thread, and hand it to
        /// <c>Apply</c>. The order is the whole design — see the type's remarks. <b>True only when
        /// the editor really is on the new profile.</b>
        /// <para>
        /// <b>It asks nothing.</b> The unsaved-work question used to be the first thing here, and it
        /// was right while a switch abandoned the open session; with the session cached, the switch
        /// loses nothing and a modal in front of it was a question with no stakes (issue #133). The
        /// re-read of <see cref="CanSelectProfile"/> that guarded the far side of that modal went
        /// with it: with nothing awaited before the stand-downs, no state can have moved.
        /// </para>
        /// </summary>
        private async Task<bool> TrySelectProfileAsync(ProfileOptionViewModel? option)
        {
            if (option is null || Device.Location is not { } location || !CanSelectProfile(option))
            {
                return false;
            }

            // Asking for the profile that is already open is not a reload: it would drop every
            // unsaved edit to end up exactly where the user already is.
            if (option.Number == SelectedProfile?.Number)
            {
                return false;
            }

            // The stand-downs SelectTab performs, for the same reasons and through the same
            // methods: a listening key and an armed record button in the rail both own the next
            // keystroke, and an armed copy is finished with a click on a board that is about to be
            // rebuilt under it.
            CancelRemap();
            CancelCopyKey();
            DeactivateInspector();

            IsLoading = true;

            LoadOutcome outcome;

            try
            {
                // A profile the user has already opened is handed back from the cache with its
                // edits intact and no drive touched at all — which is what makes a return trip
                // instant and lossless. Only a first visit reads, and it reads off the UI thread
                // like the first load: on a real v-Drive that is two files parsed, and doing it
                // inline would stall the app mid-click.
                outcome = FindCachedProfile(option.Number)
                          ?? await Task.Run(() => LoadProfileFrom(location, option.Number)).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                outcome = new LoadOutcome { Error = exception };
            }

            try
            {
                if (!_isDisposed && outcome.Session is not null)
                {
                    SwapSession(outcome);
                }
            }
            finally
            {
                IsLoading = false;
            }

            if (_isDisposed)
            {
                return false;
            }

            if (outcome.Session is null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = LoadFailureTitle,
                    Message = LoadFailureMessagePrefix + (outcome.Error?.Message ?? string.Empty),
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            // Apply has already re-parented the lighting panel onto the new profile's model; this
            // re-reads the picker's stored swatches. Both are needed, in this order — the same
            // order LoadAsync uses.
            await Lighting.LoadAsync().ConfigureAwait(true);

            return true;
        }

        /// <summary>
        /// The profile as the editor already holds it, or null when this is its first visit. A hit
        /// is marked as one (<c>LoadOutcome.IsCacheHit</c>) because <c>Apply</c> must not re-stamp
        /// the stored macro names over a session whose names the user has since edited.
        /// </summary>
        private LoadOutcome? FindCachedProfile(int profileNumber)
        {
            if (!_sessions.TryGetValue(profileNumber, out var cached))
            {
                return null;
            }

            return new LoadOutcome { Session = cached, Layout = cached.Layout, IsCacheHit = true };
        }

        /// <summary>
        /// Reads one profile off <paramref name="location"/> and reports what it got.
        /// <para>
        /// Unlike <c>LoadProfile</c>, a failure here degrades to <b>nothing</b> rather than to a
        /// factory-default layout: the first load has no editor to keep, so an empty board is the
        /// best it can do, while a switch already has a perfectly good profile open and must leave
        /// it exactly as it was. A null <c>Session</c> is what tells the caller so.
        /// </para>
        /// </summary>
        private LoadOutcome LoadProfileFrom(VDriveLocation location, int profileNumber)
        {
            try
            {
                var session = _profileSessions.Load(location, Device.DeviceId, profileNumber);

                return new LoadOutcome { Session = session, Layout = session.Layout };
            }
            catch (Exception exception)
            {
                return new LoadOutcome { Error = exception };
            }
        }

        /// <summary>
        /// Puts the loaded profile in place of the open one. Called only once the new session
        /// exists, which is what makes the switch all-or-nothing.
        /// <para>
        /// The outgoing session is <b>kept</b>, not released: it is already in <c>_sessions</c>
        /// (<c>Apply</c> filed it there when it became the open one) and it holds edits the user has
        /// not saved. Nothing about a switch is a hand-off any more — see the type's remarks — so
        /// this method is now the one line it always wanted to be.
        /// </para>
        /// </summary>
        private void SwapSession(LoadOutcome outcome)
        {
            // Rebuilds everything a switch needs — session, layout, layers, macro library, the
            // selected layer, the counters and the lighting model — files the arriving session in
            // the cache, and announces the new selection through RefreshSelectedProfile.
            Apply(outcome);
        }

        /// <summary>
        /// Files a session under its own profile number as it becomes the open one. Called from
        /// <c>Apply</c> and nowhere else, so the first load, a switch, an import and a discard all
        /// keep the cache correct without any of them having to remember to.
        /// </summary>
        private void CacheSession(IProfileSession? session)
        {
            if (session is null)
            {
                return;
            }

            _sessions[session.ProfileNumber] = session;
        }

        /// <summary>
        /// Whether any profile the user has opened re-serializes to something different from what
        /// was read — the layout half of <c>IsDirty</c>, across the whole cache rather than the open
        /// profile alone.
        /// <para>
        /// Each answer re-serializes that profile's layout and led model (<c>ProfileSession.IsDirty</c>
        /// is a pull), so this is called from <c>RefreshDirtyState</c> and never from a binding, and
        /// it stops at the first dirty one.
        /// </para>
        /// </summary>
        private bool AnyProfileIsDirty()
        {
            // The open one first. It is the profile the edit that triggered this refresh landed in,
            // so on the overwhelmingly common path the answer costs exactly what it always cost —
            // one serialization — and the other eight are never asked.
            if (_session is { IsDirty: true })
            {
                return true;
            }

            foreach (var session in _sessions.Values)
            {
                if (!ReferenceEquals(session, _session) && session.IsDirty)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The profiles one press of <c>Save</c> writes, in file order: <b>every opened profile that
        /// changed, and nothing else.</b> Empty is a legitimate answer — with nothing changed
        /// anywhere no file is written at all, which is the user's own request verbatim (*"if there
        /// were no changes - no need to override the file"*) and matters because a v-Drive is flash.
        /// <see cref="TrySaveAsync"/> reports that case rather than leaving the press silent.
        /// <para>
        /// So a clean profile is never rewritten — not while another one is dirty, and not when it
        /// is the one on screen — and a profile nobody opened is never written at all, because it
        /// has no session. <b>This reads the sessions directly</b>, which is only correct because
        /// Core moves a saved session's dirty baseline to the lines it wrote
        /// (docs/app/profiles.md): with a baseline frozen at load, every profile written in this
        /// sitting would report itself dirty for ever and each later press of Save would rewrite the
        /// lot.
        /// </para>
        /// <para>
        /// <b>A macro rename counts as a change, and the session cannot see it.</b> Names ride
        /// <c>app_settings.txt</c>, not <c>layout&lt;n&gt;.txt</c>, so a profile whose only edit is a
        /// rename re-serializes identically and answers <c>IsDirty</c> false — while
        /// <c>PersistMacroNames</c> only ever runs after a save that wrote something. Without the
        /// second term a rename would therefore be unsaveable: the write set would be empty, the
        /// press would report nothing to save, and the name would sit in memory until the editor
        /// closed. The profile's files are rewritten with identical content in that case, which is
        /// the one rewrite left and is the honest reading of "this profile changed".
        /// </para>
        /// <para>
        /// Read-only profiles are excluded here as well as by <c>CanSave()</c>: the Advantage 360's
        /// factory profile cannot be reached from the picker, but a write set that could contain it
        /// would be one guard away from a <c>ProfileReadOnlyException</c> mid-loop.
        /// </para>
        /// </summary>
        private IReadOnlyList<IProfileSession> CollectSessionsToSave()
        {
            var pending = new List<IProfileSession>();

            foreach (var session in _sessions.Values)
            {
                if (!session.CanSave)
                {
                    continue;
                }

                if (session.IsDirty || _renamedProfiles.Contains(session.ProfileNumber))
                {
                    pending.Add(session);
                }
            }

            pending.Sort(static (left, right) => left.ProfileNumber.CompareTo(right.ProfileNumber));

            return pending;
        }

        /// <summary>
        /// The violation report of a multi-profile save that must not start, or <b>null</b> when
        /// there is nothing to stop.
        /// <para>
        /// This is the pre-pass that makes a save of several profiles <b>all-or-nothing</b>. Core
        /// validates inside its own <c>Save</c> and refuses without writing (04 §5.3) — perfectly
        /// correct for one file, and not enough for five: profiles 1 and 3 would already be on the
        /// drive by the time profile 5 was refused, leaving a state nothing on screen could explain.
        /// Asking the very same question first — <c>KeyboardLayout.Validate()</c>, the call
        /// <c>ProfileSession</c> itself makes — costs one extra walk per profile and keeps the drive
        /// consistent.
        /// </para>
        /// <para>
        /// <b>It deliberately does nothing when the set holds one profile</b>, and that is not an
        /// optimization. With one file there is nothing to be atomic about, Core's own gate already
        /// writes nothing, and a second gate in the app would be a second policy over the same
        /// question — the exact shape of duplication this app refuses everywhere else. So the
        /// common press of Save is byte-for-byte the sequence it always was.
        /// </para>
        /// </summary>
        private static string? BuildPreflightViolationMessage(IReadOnlyList<IProfileSession> sessions)
        {
            if (sessions.Count < 2)
            {
                return null;
            }

            var lines = new List<string>();

            foreach (var session in sessions)
            {
                foreach (var violation in session.Layout.Validate())
                {
                    lines.Add(BuildProfileCaption(session.ProfileNumber) + ": " + violation.Message);
                }
            }

            if (lines.Count == 0)
            {
                return null;
            }

            lines.Insert(0, SaveRejectedProfilesMessage);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Runs one <c>Save</c> per profile in file order and pairs each result with the number it
        /// came from. It runs on a background thread, so it touches nothing but the sessions.
        /// </summary>
        private static List<ProfileSaveOutcome> SaveAll(IReadOnlyList<IProfileSession> sessions)
        {
            var outcomes = new List<ProfileSaveOutcome>(sessions.Count);

            foreach (var session in sessions)
            {
                outcomes.Add(new ProfileSaveOutcome(session.ProfileNumber, session.Save()));
            }

            return outcomes;
        }

        /// <summary>
        /// The report of a save Core refused after the pre-pass let it through — a race the pre-pass
        /// cannot close (the model is not frozen between the two calls), so it is reported rather
        /// than asserted away. Null when every write landed.
        /// </summary>
        private static string? BuildRejectedSaveMessage(IReadOnlyList<ProfileSaveOutcome> outcomes)
        {
            var lines = new List<string>();
            var isSingle = outcomes.Count == 1;

            foreach (var outcome in outcomes)
            {
                if (outcome.Result.Success)
                {
                    continue;
                }

                foreach (var violation in outcome.Result.Violations)
                {
                    lines.Add(isSingle
                        ? violation.Message
                        : BuildProfileCaption(outcome.ProfileNumber) + ": " + violation.Message);
                }
            }

            if (lines.Count == 0)
            {
                return null;
            }

            lines.Insert(0, isSingle ? SaveRejectedMessage : SaveRejectedProfilesMessage);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The toast's sentence for a save that landed. <b>One profile keeps Core's wording exactly
        /// as it always arrived</b> — that is the common case and nothing about it moved. Several
        /// get one aggregated line naming them, followed by the <em>distinct</em> refresh wordings:
        /// <c>ProfileSaveMessageCatalog</c> is per profile number, so nine profiles would otherwise
        /// be nine toasts, and dropping the wording entirely would lose the one instruction that
        /// tells the user how to get the profile onto the board. Identical wordings collapse (the FS
        /// Edge/Pro family has a single one for every profile).
        /// </summary>
        private static string? BuildSavedProfilesMessage(IReadOnlyList<ProfileSaveOutcome> outcomes)
        {
            if (outcomes.Count == 0)
            {
                return null;
            }

            if (outcomes.Count == 1)
            {
                return outcomes[0].Result.PostSaveMessage;
            }

            var numbers = new List<int>(outcomes.Count);
            var wordings = new List<string>();

            foreach (var outcome in outcomes)
            {
                numbers.Add(outcome.ProfileNumber);

                if (outcome.Result.PostSaveMessage is { Length: > 0 } wording && !wordings.Contains(wording))
                {
                    wordings.Add(wording);
                }
            }

            wordings.Insert(0, BuildSavedProfilesSentence(numbers));

            return string.Join(" ", wordings);
        }

        /// <summary>
        /// <c>Saved profiles 1, 3 and 5.</c> — the app's own sentence, in the design's plain voice.
        /// Two numbers read <c>1 and 3</c>; three or more are comma-separated with <c>and</c> before
        /// the last. It is the only list this app builds, so the rule lives here and nowhere else.
        /// </summary>
        private static string BuildSavedProfilesSentence(IReadOnlyList<int> profileNumbers)
        {
            var names = new List<string>(profileNumbers.Count);

            foreach (var number in profileNumbers)
            {
                names.Add(number.ToString(CultureInfo.InvariantCulture));
            }

            var list = names.Count == 2
                ? names[0] + SavedProfilesConjunction + names[1]
                : string.Join(", ", names.Take(names.Count - 1)) + SavedProfilesConjunction + names[^1];

            return SavedProfilesPrefix + list + ".";
        }

        /// <summary>
        /// Releases every session the editor has opened. <see cref="IProfileSession"/> is not
        /// <see cref="IDisposable"/> today — Core's <c>ProfileSession</c> holds parsed lines and
        /// no handle, reading and writing one <c>IVDriveFileService</c> call at a time — so this is
        /// the guard rather than the call. Since the cache holds every profile the user visited,
        /// <c>Dispose</c> is now the <b>only</b> path that lets a session go: a switch keeps them
        /// all.
        /// </summary>
        private void DisposeSessions()
        {
            foreach (var session in _sessions.Values)
            {
                if (session is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _sessions.Clear();
        }

        /// <summary>
        /// Re-reads which profile is open off the session — the single source of that fact, so a
        /// load that landed on a different number than the one asked for is reported honestly.
        /// Called from <c>Apply</c>, so the first load, an import and a switch all announce it the
        /// same way.
        /// </summary>
        private void RefreshSelectedProfile()
        {
            SelectedProfile = FindProfileOption(_session?.ProfileNumber);

            // ProfileNumber (KeyboardEditorViewModel.MacroLibrary.cs) is read off the session too,
            // and it is what scopes a macro name to a profile. A switch moves it and nothing else
            // announces it.
            OnPropertyChanged(nameof(ProfileNumber));

            SelectProfileCommand.NotifyCanExecuteChanged();
        }

        private ProfileOptionViewModel? FindProfileOption(int? profileNumber)
        {
            if (profileNumber is not { } number)
            {
                return null;
            }

            foreach (var option in Profiles)
            {
                if (option.Number == number)
                {
                    return option;
                }
            }

            return null;
        }

        /// <summary>One profile's half of a multi-profile save: which number, and what Core said.</summary>
        private sealed record ProfileSaveOutcome(int ProfileNumber, ProfileSaveResult Result);
    }
}
