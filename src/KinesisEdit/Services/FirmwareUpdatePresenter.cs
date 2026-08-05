using Avalonia.Controls;
using KinesisEdit.Core.Firmware;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The view layer's <see cref="IFirmwareUpdatePresenter"/>: builds a
    /// <see cref="FirmwareUpdateViewModel"/> from the app-level services the check needs and shows
    /// it in a modal <see cref="FirmwareUpdateWindow"/> over the shell. Like
    /// <see cref="MessageBoxPresenter"/> it resolves its owner lazily, because it is constructed
    /// before the main window exists.
    /// </summary>
    public sealed class FirmwareUpdatePresenter : IFirmwareUpdatePresenter
    {
        private readonly Func<Window?> _ownerAccessor;
        private readonly IVersionManifestClient _manifestClient;
        private readonly IAppVersionProvider _appVersionProvider;
        private readonly INotificationService _notifications;
        private readonly IUrlLauncher _urlLauncher;
        private readonly UpdateCheckPlatform _platform;

        /// <summary>Creates the presenter; <paramref name="ownerAccessor"/> returns the modal owner, or null when there is none yet.</summary>
        public FirmwareUpdatePresenter(
            Func<Window?> ownerAccessor,
            IVersionManifestClient manifestClient,
            IAppVersionProvider appVersionProvider,
            INotificationService notifications,
            IUrlLauncher urlLauncher,
            UpdateCheckPlatform platform)
        {
            _ownerAccessor = ownerAccessor ?? throw new ArgumentNullException(nameof(ownerAccessor));
            _manifestClient = manifestClient ?? throw new ArgumentNullException(nameof(manifestClient));
            _appVersionProvider = appVersionProvider ?? throw new ArgumentNullException(nameof(appVersionProvider));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _platform = platform;
        }

        /// <summary>
        /// Presents the dialog for <paramref name="device"/> and completes once it closes. A device
        /// that offers no update dialog (specs/09-firmware.md §4) opens nothing and completes
        /// immediately: its window would carry no rows and its check no comparison, while still
        /// spending a request on the endpoint of whichever master app the device belongs to.
        /// </summary>
        public async Task PresentAsync(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (!UpdateCheckEligibility.IsSupported(device.DeviceId))
            {
                return;
            }

            var viewModel = new FirmwareUpdateViewModel(
                device,
                _manifestClient,
                _appVersionProvider,
                _notifications,
                _urlLauncher,
                _platform);

            await DialogWindowHost.ShowAsync(new FirmwareUpdateWindow(viewModel), _ownerAccessor()).ConfigureAwait(true);
        }
    }
}
