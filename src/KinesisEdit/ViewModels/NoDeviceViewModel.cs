using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The empty state of the dashboard — the Troubleshoot dialog of
    /// specs/11-feature-dialogs.md §11.8, rendered inline: a title, the per-device instruction
    /// text, and the buttons 'Scan for v-Drive', 'Launch in Demo Mode', 'Troubleshooting Tips'.
    /// The legacy apps each served one device, so their text was fixed; this app serves the whole
    /// catalog and therefore carries a device picker that drives the title, the instruction, the
    /// troubleshooting URL, and which device demo mode opens.
    /// </summary>
    public sealed class NoDeviceViewModel : ViewModelBase
    {
        /// <summary>Title for keyboards (specs/11-feature-dialogs.md §11.8).</summary>
        public const string KeyboardTitle = "Keyboard not detected";

        /// <summary>Title for the Savant Elite 2 pedals (specs/12-savant-elite.md §3).</summary>
        public const string PedalTitle = "Pedal not detected";

        /// <summary>Caption of the rescan button.</summary>
        public const string ScanButtonCaption = "Scan for v-Drive";

        /// <summary>Caption of the demo-mode button.</summary>
        public const string DemoModeButtonCaption = "Launch in Demo Mode";

        /// <summary>Caption of the support-link button.</summary>
        public const string TroubleshootingButtonCaption = "Troubleshooting Tips";

        // Spec 11.8 gives two instruction texts. The first row (Adv2, FS Pro, FS Edge, SE2) is
        // quoted in full; the rows for RGB/TKO and the Adv360 are abbreviated with "..." and are
        // completed here with the first row's closing sentence, which is identical wording. Which
        // template a device gets is decided in BuildInstructionText, not by the spec's grouping.
        private const string PowerUserInstructionTemplate = "Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by first enabling Power User Mode (if necessary) using the onboard shortcut Program + Shift + Esc, and then connecting the v-Drive using the shortcut {0}. Please connect the v-Drive and then click the \"Scan for v-Drive\" button below.";
        private const string ShortcutInstructionTemplate = "Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by using the onboard shortcut {0}. Please connect the v-Drive and then click the \"Scan for v-Drive\" button below.";
        private const string UnknownShortcut = "the device's onboard v-Drive shortcut";

        /// <summary>The programmable devices offered by the picker, in device-catalog order.</summary>
        public IReadOnlyList<DeviceDefinition> Devices { get; }

        /// <summary>The device the empty state currently describes.</summary>
        public DeviceDefinition SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(InstructionText));
                    OnPropertyChanged(nameof(TroubleshootingUrl));

                    TroubleshootingTipsCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>'Pedal not detected' for the pedals, 'Keyboard not detected' for every keyboard.</summary>
        public string Title => _selectedDevice.Id == DeviceId.SavantElite2 ? PedalTitle : KeyboardTitle;

        /// <summary>The spec 11.8 instruction text for the selected device, with its own v-Drive shortcut.</summary>
        public string InstructionText => BuildInstructionText(_selectedDevice);

        /// <summary>Page opened by 'Troubleshooting Tips'; null when the catalog has none.</summary>
        public string? TroubleshootingUrl => _selectedDevice.TroubleshootingUrl;

        /// <summary>Re-runs v-Drive detection.</summary>
        public IAsyncRelayCommand ScanCommand { get; }

        /// <summary>Opens the selected device's editor without hardware (03 §3.5).</summary>
        public IRelayCommand LaunchDemoModeCommand { get; }

        /// <summary>Opens the selected device's support page.</summary>
        public IRelayCommand TroubleshootingTipsCommand { get; }

        private readonly IUrlLauncher _urlLauncher;
        private readonly Action<DeviceSnapshot> _demoModeRequested;
        private readonly Func<Task> _scanRequested;
        private DeviceDefinition _selectedDevice;

        /// <summary>
        /// Creates the empty state. <paramref name="initialDevice"/> selects the initially shown
        /// device; the default is the Advantage2, the first keyboard in catalog order.
        /// </summary>
        public NoDeviceViewModel(
            IUrlLauncher urlLauncher,
            Action<DeviceSnapshot> demoModeRequested,
            Func<Task> scanRequested,
            DeviceId initialDevice = DeviceId.Advantage2)
        {
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _demoModeRequested = demoModeRequested ?? throw new ArgumentNullException(nameof(demoModeRequested));
            _scanRequested = scanRequested ?? throw new ArgumentNullException(nameof(scanRequested));

            Devices = [.. DeviceCatalog.All.Where(device => device.IsProgrammable)];
            _selectedDevice = Devices.FirstOrDefault(device => device.Id == initialDevice) ?? Devices[0];

            ScanCommand = new AsyncRelayCommand(ScanAsync);
            LaunchDemoModeCommand = new RelayCommand(LaunchDemoMode);
            TroubleshootingTipsCommand = new RelayCommand(OpenTroubleshootingTips, () => TroubleshootingUrl is not null);
        }

        private static string BuildInstructionText(DeviceDefinition device)
        {
            // Two deliberate deviations from specs/11-feature-dialogs.md §11.8, both from the same
            // legacy copy-paste artifact: that table's first row lumps the FS Edge and FS Pro in
            // with the Adv2/SE2 text, which hard-codes the Advantage2's "Program + F1" shortcut
            // and its Power User Mode preamble, and its second row quotes "+ F8" for the TKO too.
            //
            // 1. The shortcut is templated from the catalog, so the TKO shows its real
            //    "SmartSet + Right Shift + V" (specs/03-vdrive-and-files.md §1) instead of F8.
            // 2. Only the Advantage2 and the SE2 pedals get the Power User Mode preamble. Power
            //    User Mode is an Advantage2/SE2 concept; the Freestyle boards have no such mode
            //    and simply open their v-Drive with "SmartSet + F8", so gluing the preamble to
            //    their shortcut would instruct the user to press keys that do not exist. They get
            //    the same short template as the other SmartSet boards.
            var shortcut = device.VDriveShortcutHint ?? UnknownShortcut;

            var template = device.Id switch
            {
                DeviceId.Advantage2 or DeviceId.SavantElite2 => PowerUserInstructionTemplate,
                _ => ShortcutInstructionTemplate
            };

            return string.Format(template, shortcut);
        }

        private Task ScanAsync()
        {
            return _scanRequested();
        }

        private void LaunchDemoMode()
        {
            _demoModeRequested(DeviceSnapshot.CreateDemo(_selectedDevice));
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
