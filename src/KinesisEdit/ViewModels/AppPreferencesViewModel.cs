using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Settings tab's second section (docs/design/mockups.md §1j): the seventeen preferences of
    /// <see cref="AppPreferenceCatalog"/> as a check-box list, and the custom-swatch strip under
    /// it. Everything here is stored in this device's <c>app_settings.txt</c> — the section says so
    /// in its own label — and reached through the session's one
    /// <see cref="IAppPreferencesStore"/>.
    /// <para>
    /// The five preferences mockup 1j names lead, in 1j's order; the rest sit behind a disclosure
    /// whose caption carries <b>a count taken from the catalog</b>. The mock's literal "+7 more"
    /// assumed the twelve legacy keys and there are seventeen preferences, so a typed number would
    /// have been wrong the day it was written and wrong again whenever a preference is added.
    /// </para>
    /// <para>
    /// <b>Demo mode.</b> The store discards writes there (spec 08 §3 bans saving app settings in
    /// demo mode, not loading them), so the boxes still move — a demo session exists to be
    /// explored — but nothing is persisted and the section says as much in
    /// <see cref="SettingsMessageCatalog.DemoModePreferencesCaveat"/>'s own words: the answers snap
    /// back when the session ends. The swatch strip is the one part that is genuinely frozen, so
    /// that it agrees with the lighting picker's already-unavailable "Add to Custom Colors".
    /// </para>
    /// </summary>
    public sealed class AppPreferencesViewModel : ViewModelBase
    {
        /// <summary>The section's label (mockup §1j, verbatim).</summary>
        public const string SectionLabel = "App & notifications — stored per device";

        /// <summary>Caption of the disclosure once the remaining preferences are shown.</summary>
        public const string ShowFewerCaption = "Show fewer";

        /// <summary>
        /// Formats the disclosure's caption from <paramref name="count"/>. Mockup 1j writes
        /// "+7 more"; the number is computed, never typed — see the type summary.
        /// </summary>
        public static string FormatMoreCaption(int count)
        {
            return string.Create(CultureInfo.InvariantCulture, $"+{count} more");
        }

        /// <summary>The five preferences mockup 1j names, in 1j's order; always shown.</summary>
        public IReadOnlyList<AppPreferenceRowViewModel> Featured { get; }

        /// <summary>The remaining preferences, shown only while <see cref="AreAdditionalShown"/>.</summary>
        public IReadOnlyList<AppPreferenceRowViewModel> Additional { get; }

        /// <summary>Every row, featured then additional — the catalog's own order.</summary>
        public IReadOnlyList<AppPreferenceRowViewModel> All { get; }

        /// <summary>The twelve <c>cust_color_N</c> slots, over the same store.</summary>
        public CustomSwatchesViewModel Swatches { get; }

        /// <summary>Whether the disclosure is open.</summary>
        public bool AreAdditionalShown
        {
            get => _areAdditionalShown;
            set
            {
                if (SetProperty(ref _areAdditionalShown, value))
                {
                    OnPropertyChanged(nameof(DisclosureCaption));
                }
            }
        }

        /// <summary>The disclosure's caption: "+N more" while closed, "Show fewer" while open.</summary>
        public string DisclosureCaption => _areAdditionalShown
            ? ShowFewerCaption
            : FormatMoreCaption(Additional.Count);

        /// <summary>
        /// Whether this session only reads its preferences — demo mode, or no drive at all. The
        /// boxes are still live; nothing they say reaches the file.
        /// </summary>
        public bool IsReadOnly => !_preferences.IsWritable;

        /// <summary>The demo-mode caveat, in Core's words (mockup §1j, verbatim).</summary>
        public string DemoModeCaveat => SettingsMessageCatalog.DemoModePreferencesCaveat;

        /// <summary>Opens and closes the disclosure.</summary>
        public IRelayCommand ToggleAdditionalCommand { get; }

        private readonly IAppPreferencesStore _preferences;
        private bool _areAdditionalShown;
        private bool _hasLoadStarted;

        /// <summary>
        /// Creates the section over <paramref name="preferences"/>. Construction touches no file —
        /// the rows rest at their catalog defaults — so it is safe inside an editor's constructor;
        /// <see cref="LoadAsync"/> is what reads the drive.
        /// </summary>
        public AppPreferencesViewModel(IAppPreferencesStore preferences)
        {
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));

            Featured = BuildRows(AppPreferenceCatalog.Featured);
            Additional = BuildRows(AppPreferenceCatalog.Additional);
            All = [.. Featured, .. Additional];

            Swatches = new CustomSwatchesViewModel(preferences);

            ToggleAdditionalCommand = new RelayCommand(() => AreAdditionalShown = !AreAdditionalShown);

            // No unsubscribe: the store, this section and the editor that hosts it all belong to
            // one device session and become garbage together.
            _preferences.Changed += Refresh;
        }

        /// <summary>
        /// Reads the preferences off the UI thread and fills the section in. Total and idempotent:
        /// the store already swallows every I/O failure and serves
        /// <see cref="AppSettings.Empty"/> for a file it could not read, which is the same thing as
        /// "every preference at its default".
        /// </summary>
        public async Task LoadAsync()
        {
            if (_hasLoadStarted)
            {
                return;
            }

            _hasLoadStarted = true;

            // The store parses the session's app_settings.txt on the first read of Current and
            // never again, so this is where that read happens.
            try
            {
                await Task.Run(() => _preferences.Current).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Swallowed on purpose; see the summary.
            }

            Refresh();
        }

        /// <summary>Re-reads every preference from the store. Total and idempotent.</summary>
        public void Refresh()
        {
            var settings = _preferences.Current;

            foreach (var row in All)
            {
                row.SetFromStore(row.Descriptor.GetValue(settings));
            }
        }

        private IReadOnlyList<AppPreferenceRowViewModel> BuildRows(
            IReadOnlyList<AppPreferenceDescriptor> descriptors)
        {
            var rows = new List<AppPreferenceRowViewModel>(descriptors.Count);

            foreach (var descriptor in descriptors)
            {
                rows.Add(new AppPreferenceRowViewModel(descriptor, Persist));
            }

            return rows;
        }

        /// <summary>
        /// Writes one answer through the store, applying the descriptor's polarity rather than
        /// this class's idea of it. A store that cannot write does nothing at all, and the row
        /// keeps the value for the rest of the session — which is exactly what the demo caveat
        /// describes.
        /// </summary>
        private void Persist(AppPreferenceDescriptor descriptor, bool value)
        {
            _preferences.Update(settings => descriptor.SetValue(settings, value));
        }
    }
}
