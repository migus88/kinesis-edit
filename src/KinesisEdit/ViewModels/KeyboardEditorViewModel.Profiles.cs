using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The profile picker: <b>which numbered profile the editor is editing</b>, and the switch that
    /// points it at another one. The sessions it switches between live in
    /// <see cref="ProfileSessionCache"/> — see that type for why every visited profile stays open,
    /// what one press of Save writes and how the wording is built. Split out of
    /// <see cref="KeyboardEditorViewModel"/>'s main file for the reason <c>…Legend.cs</c>,
    /// <c>…Inspector.cs</c> and <c>…MacroNames.cs</c> were — that file is the largest in the app and
    /// docs/guides/Coding Conventions.md forbids growing it into a god class. Everything here is the
    /// same class and runs on the same rules.
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
    /// <para><b>Nothing is half-swapped, and nothing is abandoned (issue #133).</b> The outcome is
    /// decided before <c>Apply</c> is called, because <c>Apply</c> mutates unconditionally — so a
    /// load that failed leaves the previous profile selected, its session intact and its edits still
    /// saveable, and says so in a dialog. A switch that <em>succeeds</em> keeps the outgoing session
    /// too, which is what removed the modal this switch used to raise;
    /// <c>ConfirmDiscardingUnsavedWorkAsync</c> is still asked before Home, a device swap and the
    /// window's close, where the editor really does go away.</para>
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
        /// state</b> (<see cref="ProfileSessionCache"/>). Built in this class's constructor, first
        /// of the collaborators, because <see cref="MacroNameStore"/> is handed it there: a rename
        /// made in a profile the user has since left is found through this cache, and a field
        /// initializer cannot reach another field.
        /// </summary>
        private readonly ProfileSessionCache _sessionCache;

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
                    // The swap is one call, and only ever reached once the new session exists —
                    // which is what makes it all-or-nothing. Apply rebuilds everything a switch
                    // needs (session, layout, layers, selected layer, counters, lighting model),
                    // files the arriving session in the cache and announces the new selection
                    // through RefreshSelectedProfile. The outgoing session is *kept*, not released:
                    // it is already in the cache and holds edits the user has not saved.
                    Apply(outcome);
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
        /// the stored macro names over a session whose names the user has since edited. The cache
        /// answers with the session; <c>LoadOutcome</c> is the editor's own shape, so this wraps it.
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
        /// Re-reads which profile is open off the session — the single source of that fact, so a
        /// load that landed on a different number than the one asked for is reported honestly.
        /// Called from <c>Apply</c>, so the first load, an import and a switch all announce it the
        /// same way.
        /// </summary>
        private void RefreshSelectedProfile()
        {
            SelectedProfile = FindProfileOption(_session?.ProfileNumber);

            // ProfileNumber (KeyboardEditorViewModel.MacroNames.cs) is read off the session too,
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
    }
}
