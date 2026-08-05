namespace KinesisEdit.Services
{
    /// <summary>
    /// The user-facing eject flow of specs/10-apps-and-ui.md ("Ejecting … the app dismounts the
    /// volume, showing 'Disconnecting v-Drive' then 'Safe To Remove Hardware', or on failure
    /// 'Cannot eject v-Drive — …'"), shared by the dashboard card's Eject button and by Home.
    /// The legacy apps hid ejection on macOS; this build gates it on
    /// <see cref="IDeviceEjectService.IsSupported"/> instead (specs/03-vdrive-and-files.md §5.3).
    /// </summary>
    public sealed class VDriveEjectNotifier
    {
        /// <summary>Notice shown while the volume is being dismounted.</summary>
        public const string DisconnectingMessage = "Disconnecting v-Drive";

        /// <summary>Notice shown once the volume has been released.</summary>
        public const string SafeToRemoveMessage = "Safe To Remove Hardware";

        /// <summary>Failure message, quoted verbatim from specs/10-apps-and-ui.md.</summary>
        public const string FailureMessage = "Cannot eject v-Drive — Close all open files and folders on the v-Drive, and try ejecting again.";

        /// <summary>Title of the failure dialog; the spec quotes the message only.</summary>
        public const string FailureTitle = "Eject";

        /// <summary>Whether app-driven ejection is available on this platform.</summary>
        public bool IsSupported => _ejectService.IsSupported;

        private readonly IDeviceEjectService _ejectService;
        private readonly INotificationService _notifications;

        /// <summary>Creates the flow over the eject service and the notification surface.</summary>
        public VDriveEjectNotifier(IDeviceEjectService ejectService, INotificationService notifications)
        {
            _ejectService = ejectService ?? throw new ArgumentNullException(nameof(ejectService));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        /// <summary>
        /// Ejects the volume at <paramref name="volumeRootPath"/>, showing the spec's progress
        /// notices and the failure dialog. Returns whether the volume was released; does nothing
        /// and returns false when ejection is unsupported on this platform.
        /// </summary>
        public async Task<bool> EjectAsync(string volumeRootPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(volumeRootPath);

            if (!IsSupported)
            {
                return false;
            }

            _notifications.ShowToast(new ToastRequest
            {
                Message = DisconnectingMessage
            });

            var result = await _ejectService.EjectAsync(volumeRootPath).ConfigureAwait(true);

            if (result.Succeeded)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Message = SafeToRemoveMessage
                });

                return true;
            }

            await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
            {
                Title = FailureTitle,
                Message = FailureMessage,
                Icon = MessageBoxIcon.Error,
                Buttons = MessageBoxButtons.Ok
            }).ConfigureAwait(true);

            return false;
        }
    }
}
