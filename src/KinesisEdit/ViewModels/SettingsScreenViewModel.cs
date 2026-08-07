using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The shell's <b>Settings</b> screen — the app bar's second nav pill. It edits the two
    /// <b>host</b> preferences of <see cref="IHostPreferencesStore"/> that are a choice: the theme
    /// and the motion budget. Everything here belongs to the person at this computer and applies to
    /// every board they ever plug in.
    /// <para>
    /// <b>Two rows on one card, and no section labels.</b> A section label over a single row names
    /// the row twice, so the screen is its title, its subtitle and one unlabelled card holding both
    /// switches. The store's third field — the window's size and position, which the shell writes on
    /// close and restores on open — is not on this screen at all: it is not a choice, and a screen of
    /// choices is the wrong place to narrate something that has no switch.
    /// </para>
    /// <para>
    /// <b>It is not the editor's Settings tab, and the two must not be confused.</b>
    /// <see cref="KeyboardSettingsViewModel"/> edits <c>kbd_settings.txt</c> on a keyboard's
    /// v-Drive and <see cref="AppPreferencesViewModel"/> edits <c>app_settings.txt</c> beside it —
    /// both are <i>per device</i> and both travel with the board. This screen writes a JSON file in
    /// the per-user configuration directory and is <i>per user, per machine</i>. The copy says so,
    /// because a reader arriving from the editor's Settings tab has just been told the opposite
    /// (docs/app/host-preferences.md).
    /// </para>
    /// <para>
    /// <b>Every choice applies live and is persisted.</b> The preference is written to the store
    /// first — the store is the truth — and the matching applier is then run for the value the
    /// store settled on. The appliers arrive as delegates rather than being called directly so this
    /// view model stays toolkit-free (app-shell.md invariant 8): <see cref="ThemeApplier"/> and
    /// <see cref="MotionPreferenceApplier"/> both need an Avalonia <c>Application</c>, and the
    /// composition root is the only thing that has one.
    /// </para>
    /// <para>
    /// It applies from <see cref="IHostPreferencesStore.Changed"/> rather than from the click, so a
    /// preference changed by anything else lands too, and it remembers what it last applied so a
    /// write of some <i>other</i> field — the window's geometry, which the shell writes on close —
    /// re-applies nothing. Both appliers are idempotent, so a composition root that also listens is
    /// harmless.
    /// </para>
    /// </summary>
    public sealed class SettingsScreenViewModel : ViewModelBase, IDisposable
    {
        /// <summary>The screen's title.</summary>
        public const string Title = "Settings";

        /// <summary>
        /// The sentence that keeps this screen apart from the editor's Settings tab. Both exist,
        /// both are called Settings, and only one of them follows the keyboard.
        /// </summary>
        public const string Subtitle =
            "These are yours, not a keyboard's — stored for this user on this computer, and applied "
            + "to every board you open. A board's own settings live on the board, in its editor's "
            + "Settings tab.";

        /// <summary>The theme row's own caption.</summary>
        public const string ThemeCaption = "Theme";

        /// <summary>What the theme row does, in one line.</summary>
        public const string ThemeDescription =
            "Follow the operating system, or pin the app to light or dark. It changes as you pick it.";

        /// <summary>The motion row's own caption.</summary>
        public const string MotionCaption = "Animation";

        /// <summary>What the motion row does, in one line.</summary>
        public const string MotionDescription =
            "The app's animations are a small fixed budget — a card arriving, a status colour "
            + "crossing over, a fade. Reduced keeps the fades and drops the movement.";

        /// <summary>Caption of the "follow the OS" segment, on both switches.</summary>
        public const string FollowSystemCaption = "Follow system";

        /// <summary>Caption of the light-theme segment.</summary>
        public const string LightCaption = "Light";

        /// <summary>Caption of the dark-theme segment.</summary>
        public const string DarkCaption = "Dark";

        /// <summary>Caption of the always-reduce segment.</summary>
        public const string AlwaysReduceCaption = "Reduced";

        /// <summary>Caption of the never-reduce segment.</summary>
        public const string NeverReduceCaption = "Full";

        /// <summary>The three theme segments, in the enum's own order.</summary>
        public IReadOnlyList<AppThemeOption> ThemeOptions { get; }

        /// <summary>The three motion segments, in the enum's own order.</summary>
        public IReadOnlyList<MotionOption> MotionOptions { get; }

        /// <summary>
        /// The theme segment the switch shows as selected. The view binds it <c>OneWay</c> and runs
        /// <see cref="SelectThemeCommand"/> from the selection change, so this property stays the
        /// single owner of what is selected and a refused pick snaps the segment back.
        /// </summary>
        public AppThemeOption SelectedThemeOption
        {
            get => _selectedThemeOption;
            private set => SetProperty(ref _selectedThemeOption, value);
        }

        /// <summary>The motion segment the switch shows as selected. Same contract as the theme's.</summary>
        public MotionOption SelectedMotionOption
        {
            get => _selectedMotionOption;
            private set => SetProperty(ref _selectedMotionOption, value);
        }

        /// <summary>Writes the picked theme through the store and repaints the running app.</summary>
        public IRelayCommand<AppThemeOption> SelectThemeCommand { get; }

        /// <summary>Writes the picked motion budget through the store and re-points the motion aliases.</summary>
        public IRelayCommand<MotionOption> SelectMotionCommand { get; }

        private readonly IHostPreferencesStore _store;
        private readonly Action<AppThemePreference> _applyTheme;
        private readonly Action<MotionPreference> _applyMotion;
        private AppThemeOption _selectedThemeOption;
        private MotionOption _selectedMotionOption;
        private AppThemePreference _appliedTheme;
        private MotionPreference _appliedMotion;
        private bool _isDisposed;

        /// <summary>
        /// Creates the screen over <paramref name="store"/>. <paramref name="applyTheme"/> and
        /// <paramref name="applyMotion"/> are what make a pick visible — in the app they are
        /// <see cref="ThemeApplier.Apply"/> and <see cref="MotionPreferenceApplier.Apply"/> closed
        /// over the running <c>Application</c>.
        /// </summary>
        public SettingsScreenViewModel(
            IHostPreferencesStore store,
            Action<AppThemePreference> applyTheme,
            Action<MotionPreference> applyMotion)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _applyTheme = applyTheme ?? throw new ArgumentNullException(nameof(applyTheme));
            _applyMotion = applyMotion ?? throw new ArgumentNullException(nameof(applyMotion));

            ThemeOptions =
            [
                new AppThemeOption { Value = AppThemePreference.FollowSystem, Caption = FollowSystemCaption },
                new AppThemeOption { Value = AppThemePreference.Light, Caption = LightCaption },
                new AppThemeOption { Value = AppThemePreference.Dark, Caption = DarkCaption }
            ];

            MotionOptions =
            [
                new MotionOption { Value = MotionPreference.FollowSystem, Caption = FollowSystemCaption },
                new MotionOption { Value = MotionPreference.AlwaysReduce, Caption = AlwaysReduceCaption },
                new MotionOption { Value = MotionPreference.NeverReduce, Caption = NeverReduceCaption }
            ];

            var current = _store.Current;

            _selectedThemeOption = FindTheme(current.Theme);
            _selectedMotionOption = FindMotion(current.Motion);

            // What the app is already wearing: the composition root applied it at startup, so the
            // first change is the first thing worth applying.
            _appliedTheme = current.Theme;
            _appliedMotion = current.Motion;

            SelectThemeCommand = new RelayCommand<AppThemeOption>(SelectTheme);
            SelectMotionCommand = new RelayCommand<MotionOption>(SelectMotion);

            _store.Changed += OnStoreChanged;
        }

        /// <summary>Stops listening to the store. The store outlives the screen.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _store.Changed -= OnStoreChanged;
        }

        private void SelectTheme(AppThemeOption? option)
        {
            if (option is null)
            {
                return;
            }

            // A mutation returning an equal record is a no-op in the store, which is what stops an
            // unchanged pick from raising Changed and re-entering every binding.
            _store.Update(current => current with { Theme = option.Value });

            // Read the store back rather than trusting the pick: it, not this screen, is the truth.
            // A store that raised Changed has already run this; it is idempotent, and calling it
            // here is what makes the method correct without depending on the event.
            SyncFromStore();
        }

        private void SelectMotion(MotionOption? option)
        {
            if (option is null)
            {
                return;
            }

            _store.Update(current => current with { Motion = option.Value });

            SyncFromStore();
        }

        private void OnStoreChanged()
        {
            SyncFromStore();
        }

        /// <summary>
        /// Re-reads the store, moves the two selections onto it and applies whichever of the two
        /// preferences actually moved. Runs for every write, including the window geometry's — which
        /// is why the comparison against what was last applied is here rather than at the call site.
        /// </summary>
        private void SyncFromStore()
        {
            var current = _store.Current;

            SelectedThemeOption = FindTheme(current.Theme);
            SelectedMotionOption = FindMotion(current.Motion);

            if (current.Theme != _appliedTheme)
            {
                _appliedTheme = current.Theme;

                _applyTheme(current.Theme);
            }

            if (current.Motion != _appliedMotion)
            {
                _appliedMotion = current.Motion;

                _applyMotion(current.Motion);
            }
        }

        private AppThemeOption FindTheme(AppThemePreference preference)
        {
            return ThemeOptions.FirstOrDefault(option => option.Value == preference) ?? ThemeOptions[0];
        }

        private MotionOption FindMotion(MotionPreference preference)
        {
            return MotionOptions.FirstOrDefault(option => option.Value == preference) ?? MotionOptions[0];
        }
    }
}
