using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The dashboard's empty state — mockup §1d, the screen the app shows while nothing has been
    /// detected. It is the troubleshoot dialog of specs/11-feature-dialogs.md §11.8 rebuilt as a
    /// working surface: a headline, the reassurance that the app is already watching, a picker of
    /// all seven programmable boards, the picked board's connection steps, and three actions —
    /// launch it in demo mode, scan now, open its support page.
    /// <para>
    /// The picker is this app's own addition: the legacy apps each served one device, so their
    /// text was fixed. Here the selection drives the title, the steps, the troubleshooting URL and
    /// which board demo mode opens.
    /// </para>
    /// <para>
    /// Nothing here asks the user to press anything to make detection happen. The loop is already
    /// running, so the screen says so and counts its own passes; it replaces itself with the
    /// device card the moment a drive is seen.
    /// </para>
    /// </summary>
    public sealed class NoDeviceViewModel : ViewModelBase
    {
        /// <summary>Title for keyboards (specs/11-feature-dialogs.md §11.8; mockup §1d).</summary>
        public const string KeyboardTitle = "Keyboard not detected";

        /// <summary>
        /// Title for the Savant Elite 2 pedals (specs/12-savant-elite.md §3). Mockup §1d draws
        /// only the keyboard case; the pedal title is kept because the app serves that device too.
        /// </summary>
        public const string PedalTitle = "Pedal not detected";

        /// <summary>The headline's body copy (mockup §1d, verbatim).</summary>
        public const string BodyText =
            "KinesisEdit is watching for a v-Drive and will pick one up the moment it appears — "
            + "no need to press anything. Meanwhile, pick your device for connection steps, or "
            + "work without hardware.";

        /// <summary>Title over the device picker (mockup §1d, verbatim).</summary>
        public const string PickerTitle = "Which device do you have?";

        /// <summary>The line under the picker (mockup §1d, verbatim).</summary>
        public const string PickerHelperText =
            "Your pick drives the steps at right and which board Demo Mode opens.";

        /// <summary>The tag on the board the screen starts on (mockup §1d).</summary>
        public const string DefaultTagCaption = "default";

        /// <summary>Caption of the rescan button (mockup §1d).</summary>
        public const string ScanButtonCaption = "Scan now";

        /// <summary>
        /// What the rescan button reads while a pass is already in flight. The same word the
        /// device card uses, because it is the same fact about the same loop.
        /// </summary>
        public const string ScanningButtonCaption = "Scanning";

        /// <summary>Caption of the support-link button (mockup §1d; the mock's ↗ is an icon here).</summary>
        public const string TroubleshootingButtonCaption = "Troubleshooting tips";

        /// <summary>The board the screen starts on, and the one the picker tags "default".</summary>
        public const DeviceId DefaultDevice = DeviceId.Advantage2;

        // "Get an Advantage2 into a detectable state - 3 steps" (mockup §1d). The article and the
        // step count are both data: the Advantage 360 takes "an", the Freestyle boards take "a",
        // and a device whose v-Drive needs Power User Mode first has one step more.
        private const string StepsTitleTemplate = "Get {0} {1} into a detectable state — {2} step{3}";

        // "Still watching · rescanned 8 times since you opened this window" (mockup §1d). U+00B7,
        // spaced, as everywhere else in this app.
        private const string RescanTemplate = "Still watching · rescanned {0} time{1} since you opened this window";

        // "Launch Advantage2 in Demo Mode" (mockup §1d) - the primary action names the pick, so
        // there is never a question about which board is about to open.
        private const string DemoModeCaptionTemplate = "Launch {0} in Demo Mode";

        private const string VowelLetters = "AEIOU";

        /// <summary>
        /// The picker's order, which is the mockup's rather than the catalog's: the two contoured
        /// boards, then the three Freestyles, then the TKO, then the pedal.
        /// <see cref="DeviceCatalog.All"/> is in legacy-app-id order, which puts the pedal first
        /// and interleaves the families.
        /// </summary>
        private static readonly DeviceId[] _pickerOrder =
        [
            DeviceId.Advantage2,
            DeviceId.Advantage360,
            DeviceId.FreestyleEdge,
            DeviceId.FreestylePro,
            DeviceId.FreestyleEdgeRgb,
            DeviceId.Tko,
            DeviceId.SavantElite2
        ];

        /// <summary>
        /// The indefinite article for <paramref name="deviceName"/>. The written vowel is the whole
        /// rule, which is right for every catalog name — including the TKO, whose leading T reads
        /// "tee" and so takes "a".
        /// </summary>
        public static string Article(string deviceName)
        {
            ArgumentNullException.ThrowIfNull(deviceName);

            if (deviceName.Length == 0)
            {
                return "a";
            }

            return VowelLetters.Contains(char.ToUpperInvariant(deviceName[0]), StringComparison.Ordinal)
                ? "an"
                : "a";
        }

        /// <summary>Formats the steps panel's title. Static so the wording is asserted on its own.</summary>
        public static string FormatStepsTitle(string deviceName, int stepCount)
        {
            ArgumentNullException.ThrowIfNull(deviceName);

            return string.Format(
                CultureInfo.InvariantCulture,
                StepsTitleTemplate,
                Article(deviceName),
                deviceName,
                stepCount,
                stepCount == 1 ? string.Empty : "s");
        }

        /// <summary>Formats the live reassurance line.</summary>
        public static string FormatRescanText(int rescanCount)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                RescanTemplate,
                rescanCount,
                rescanCount == 1 ? string.Empty : "s");
        }

        /// <summary>Formats the primary action, which names the picked board.</summary>
        public static string FormatDemoModeCaption(string deviceName)
        {
            ArgumentNullException.ThrowIfNull(deviceName);

            return string.Format(CultureInfo.InvariantCulture, DemoModeCaptionTemplate, deviceName);
        }

        /// <summary>The seven programmable boards, in the mockup's order.</summary>
        public IReadOnlyList<DevicePickerOption> Devices { get; }

        /// <summary>
        /// The picked row. The picker binds its selection here.
        /// <para>
        /// <b>A null is refused rather than stored.</b> A <c>ListBox</c> in single-selection mode
        /// clears its selection when the row that is already selected is Ctrl/Cmd-clicked, and the
        /// two-way binding writes that through as null — but this screen has no "nothing picked"
        /// state: the title, the steps, the demo target and the support link all describe one
        /// board. The pick is therefore kept and pushed straight back at the list, which restores
        /// the row's selected face. <c>SelectionMode="AlwaysSelected"</c> is not the fix — it
        /// re-selects the *first* row, so a Cmd-click would silently move the pick to the
        /// Advantage2.
        /// </para>
        /// </summary>
        public DevicePickerOption SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (value is null)
                {
                    OnPropertyChanged(nameof(SelectedOption));

                    return;
                }

                if (!SetProperty(ref _selectedOption, value))
                {
                    return;
                }

                // Rebuilt once here rather than on every read: the list is what an ItemsControl
                // binds to, and handing it a fresh instance per get would rebuild the rows for
                // any reason at all.
                _steps = ConnectionSteps.Create(value.Device);

                OnPropertyChanged(nameof(SelectedDevice));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Steps));
                OnPropertyChanged(nameof(StepsTitle));
                OnPropertyChanged(nameof(DemoModeCaption));
                OnPropertyChanged(nameof(TroubleshootingUrl));

                TroubleshootingTipsCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// The device the empty state currently describes. A façade over
        /// <see cref="SelectedOption"/>: the picker's rows are what the list selects, and the
        /// device is what everything else asks about.
        /// </summary>
        public DeviceDefinition SelectedDevice
        {
            get => _selectedOption.Device;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                SelectedOption = FindOption(value.Id);
            }
        }

        /// <summary>'Pedal not detected' for the pedals, 'Keyboard not detected' for every keyboard.</summary>
        public string Title => ConnectionSteps.IsPedal(SelectedDevice) ? PedalTitle : KeyboardTitle;

        /// <summary>The headline's body copy.</summary>
        public string Body => BodyText;

        /// <summary>"Get an Advantage2 into a detectable state — 3 steps".</summary>
        public string StepsTitle => FormatStepsTitle(SelectedDevice.DisplayName, _steps.Count);

        /// <summary>The picked board's numbered connection steps.</summary>
        public IReadOnlyList<ConnectionStep> Steps => _steps;

        /// <summary>"Still watching · rescanned 8 times since you opened this window".</summary>
        public string RescanText => FormatRescanText(RescanCount);

        /// <summary>
        /// How many detection passes have completed since this screen was built. It is a delta
        /// against the baseline captured in the constructor, which is what makes "since you opened
        /// this window" true: the screen and the window are built together and live as long as
        /// each other.
        /// </summary>
        public int RescanCount => _completedRefreshCount - _completedRefreshBaseline;

        /// <summary>Whether a detection pass is in flight right now.</summary>
        public bool IsRefreshing => _isRefreshing;

        /// <summary>'Scan now', or 'Scanning' while the pass it would start is already running.</summary>
        public string ScanCaption => _isRefreshing ? ScanningButtonCaption : ScanButtonCaption;

        /// <summary>Caption of the primary action, naming the picked board.</summary>
        public string DemoModeCaption => FormatDemoModeCaption(SelectedDevice.DisplayName);

        /// <summary>Page opened by 'Troubleshooting tips'; null when the catalog has none.</summary>
        public string? TroubleshootingUrl => SelectedDevice.TroubleshootingUrl;

        /// <summary>Re-runs v-Drive detection.</summary>
        public IAsyncRelayCommand ScanCommand { get; }

        /// <summary>Opens the selected device's editor without hardware (03 §3.5).</summary>
        public IRelayCommand LaunchDemoModeCommand { get; }

        /// <summary>Opens the selected device's support page.</summary>
        public IRelayCommand TroubleshootingTipsCommand { get; }

        private readonly IUrlLauncher _urlLauncher;
        private readonly Action<DeviceSnapshot> _demoModeRequested;
        private readonly Func<Task> _scanRequested;
        private readonly int _completedRefreshBaseline;
        private DevicePickerOption _selectedOption;
        private IReadOnlyList<ConnectionStep> _steps;
        private int _completedRefreshCount;
        private bool _isRefreshing;

        /// <summary>
        /// Creates the empty state. <paramref name="completedRefreshBaseline"/> is the detection
        /// loop's completed-pass count at this moment — the zero the reassurance line counts up
        /// from. <paramref name="initialDevice"/> selects the initially shown board; the default is
        /// the Advantage2, which is also the row the picker tags "default".
        /// </summary>
        public NoDeviceViewModel(
            IUrlLauncher urlLauncher,
            Action<DeviceSnapshot> demoModeRequested,
            Func<Task> scanRequested,
            int completedRefreshBaseline = 0,
            DeviceId initialDevice = DefaultDevice)
        {
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _demoModeRequested = demoModeRequested ?? throw new ArgumentNullException(nameof(demoModeRequested));
            _scanRequested = scanRequested ?? throw new ArgumentNullException(nameof(scanRequested));
            _completedRefreshBaseline = completedRefreshBaseline;
            _completedRefreshCount = completedRefreshBaseline;

            Devices = CreateOptions();
            _selectedOption = FindOption(initialDevice);
            _steps = ConnectionSteps.Create(_selectedOption.Device);

            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !_isRefreshing);
            LaunchDemoModeCommand = new RelayCommand(LaunchDemoMode);
            TroubleshootingTipsCommand = new RelayCommand(OpenTroubleshootingTips, () => TroubleshootingUrl is not null);
        }

        /// <summary>
        /// Pushes the detection loop's current state down from the dashboard, which is the one
        /// object subscribed to it. <paramref name="completedRefreshCount"/> is the loop's absolute
        /// count; the baseline captured at construction turns it into "since you opened this
        /// window".
        /// </summary>
        public void SetRefreshActivity(bool isRefreshing, int completedRefreshCount)
        {
            if (SetProperty(ref _isRefreshing, isRefreshing, nameof(IsRefreshing)))
            {
                OnPropertyChanged(nameof(ScanCaption));

                ScanCommand.NotifyCanExecuteChanged();
            }

            if (SetProperty(ref _completedRefreshCount, completedRefreshCount, nameof(RescanCount)))
            {
                OnPropertyChanged(nameof(RescanText));
            }
        }

        private static IReadOnlyList<DevicePickerOption> CreateOptions()
        {
            // Ordered by the mockup, but sourced from the catalog: a programmable device the order
            // above has never heard of still appears, at the end, rather than vanishing from the
            // picker because a list in this file was not updated.
            var devices = DeviceCatalog.All
                .Where(device => device.IsProgrammable)
                .OrderBy(device => IndexOf(device.Id))
                .ToList();

            return [.. devices.Select(device => new DevicePickerOption(device, device.Id == DefaultDevice))];
        }

        private static int IndexOf(DeviceId deviceId)
        {
            var index = Array.IndexOf(_pickerOrder, deviceId);

            return index < 0 ? _pickerOrder.Length : index;
        }

        private DevicePickerOption FindOption(DeviceId deviceId)
        {
            return Devices.FirstOrDefault(option => option.Id == deviceId) ?? Devices[0];
        }

        private Task ScanAsync()
        {
            return _scanRequested();
        }

        private void LaunchDemoMode()
        {
            _demoModeRequested(DeviceSnapshot.CreateDemo(SelectedDevice));
        }

        private void OpenTroubleshootingTips()
        {
            var url = TroubleshootingUrl;

            if (url is null)
            {
                return;
            }

            _urlLauncher.Open(url);
        }
    }
}
