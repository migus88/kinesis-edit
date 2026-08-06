using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The dashboard card for a board this app does not edit — the Advantage 360 Professional,
    /// which is configured in Kinesis' ZMK web GUI (specs/02-devices.md; docs/design/mockups.md
    /// §1b). It carries the device name, the meta line, one sentence of explanation and a single
    /// action that opens the web tool.
    /// <para>
    /// A separate type rather than a fifth <see cref="DeviceCardState"/>: the device exposes no
    /// v-Drive, so the scanner never yields a status for it and it can never have a
    /// <c>DeviceSnapshot</c>. Synthesising one would be a lie in the data — a snapshot means
    /// "what the scanner found" — and this card has no status, no mount path and nothing to
    /// eject or scan for.
    /// </para>
    /// </summary>
    public sealed class WebToolCardViewModel : DashboardCardViewModel
    {
        /// <summary>The card's whole body copy (docs/design/mockups.md §1b, verbatim).</summary>
        public const string DescriptionText = "Configured in Kinesis' web tool — KinesisEdit doesn't edit this board.";

        /// <summary>Caption of the only action the card offers (docs/design/mockups.md §1b, verbatim).</summary>
        public const string OpenWebToolActionCaption = "Open web tool ↗";

        /// <summary>
        /// The devices this card kind exists for: not SmartSet-programmable, yet configurable
        /// somewhere else. Derived from the catalog rather than named per device, so a second such
        /// board would be picked up without touching this class.
        /// </summary>
        public static IEnumerable<DeviceDefinition> WebToolDevices()
        {
            return DeviceCatalog.All.Where(device => !device.IsProgrammable && device.ConfigurationUrl is not null);
        }

        /// <summary>Identity of the card; the catalog id is unique and never resolved from a version file.</summary>
        public override string Key => _device.Id.ToString();

        /// <summary>Device name shown as the card's title.</summary>
        public override string DisplayName => _device.DisplayName;

        /// <summary>Hardware summary under the title, composed from the same catalog data as every other card.</summary>
        public override string MetaLine => DeviceMetaLine.Describe(_device);

        /// <summary>Why the card offers nothing to edit.</summary>
        public string Description => DescriptionText;

        /// <summary>Caption of the card's single action.</summary>
        public string OpenWebToolCaption => OpenWebToolActionCaption;

        /// <summary>The page the action opens; null would leave the action unavailable.</summary>
        public string? ConfigurationUrl => _device.ConfigurationUrl;

        /// <summary>Opens the vendor's web configurator in the user's browser.</summary>
        public IRelayCommand OpenWebToolCommand { get; }

        private readonly DeviceDefinition _device;
        private readonly IUrlLauncher _urlLauncher;

        /// <summary>Creates the card for <paramref name="device"/>.</summary>
        public WebToolCardViewModel(DeviceDefinition device, IUrlLauncher urlLauncher)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));

            OpenWebToolCommand = new RelayCommand(OpenWebTool, () => ConfigurationUrl is not null);
        }

        private void OpenWebTool()
        {
            var url = ConfigurationUrl;

            if (url is null)
            {
                return;
            }

            _urlLauncher.Open(url);
        }
    }
}
