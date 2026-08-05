using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The refusal path shared by every firmware-gated editor feature
    /// (specs/11-feature-dialogs.md §11.1, specs/09-firmware.md §2): ask
    /// <see cref="FirmwareGateService"/>, and when the gate refuses show the gate's own wording
    /// with an extra <c>'Update Firmware'</c> button that opens the device's support page.
    /// <para>
    /// It lives next to the overlays rather than in <c>Services/</c> because it is presentation:
    /// Core carries the gate rows, their messages and the support URLs as data and deliberately
    /// shows no dialog (docs/app/firmware.md, "No refusal-dialog UI"). Every overlay that needs a
    /// gate exposes its own one-line static wrapper over this, so a caller never has to know
    /// which <see cref="FirmwareFeature"/> a feature maps to.
    /// </para>
    /// </summary>
    public static class FirmwareFeatureGate
    {
        /// <summary>Id reported in <see cref="MessageBoxOutcome.CustomButtonId"/> for the update button.</summary>
        public const string UpdateFirmwareButtonId = "update-firmware";

        /// <summary>Caption of the extra button of §11.1's refusal dialog.</summary>
        public const string UpdateFirmwareButtonCaption = "Update Firmware";

        /// <summary>
        /// Whether <paramref name="feature"/> may be used on <paramref name="deviceId"/>. Returns
        /// true — showing nothing — when the gate passes, which includes demo mode (09 §2: "every
        /// gate also passes in demo mode"). On a refusal it shows the message box and returns
        /// false.
        /// </summary>
        /// <param name="deviceId">The device whose firmware is gated.</param>
        /// <param name="feature">The gated feature.</param>
        /// <param name="firmware">The device's firmware state, e.g. <see cref="DeviceSnapshot.Firmware"/>.</param>
        /// <param name="title">Title of the refusal dialog; the specs quote none, so the feature's own title is used.</param>
        /// <param name="fallbackMessage">
        /// Wording used when the gate row carries no <see cref="FirmwareGate.Message"/>. Spec 09 §2
        /// quotes the refusal only under the Freestyle rows, so the Advantage2 and Edge RGB
        /// tap-and-hold gates have none; §11.1 nevertheless describes one refusal for every app.
        /// An empty fallback on a message-less gate shows no dialog and only refuses.
        /// </param>
        /// <param name="notifications">Where the refusal dialog is shown.</param>
        /// <param name="urlLauncher">Opens the support page behind the update button.</param>
        public static async Task<bool> EnsureAvailableAsync(
            DeviceId deviceId,
            FirmwareFeature feature,
            FirmwareState firmware,
            string title,
            string fallbackMessage,
            INotificationService notifications,
            IUrlLauncher urlLauncher)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentNullException.ThrowIfNull(notifications);
            ArgumentNullException.ThrowIfNull(urlLauncher);

            if (FirmwareGateService.IsAvailable(deviceId, feature, firmware))
            {
                return true;
            }

            var message = FirmwareGateCatalog.Find(deviceId, feature)?.Message ?? fallbackMessage;

            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            var supportUrl = FirmwareSupportUrls.FindUrl(deviceId);
            var outcome = await notifications.ShowMessageBoxAsync(BuildRequest(title, message, supportUrl));

            if (supportUrl is not null && WasUpdateRequested(outcome))
            {
                urlLauncher.Open(supportUrl);
            }

            return false;
        }

        private static MessageBoxRequest BuildRequest(string title, string message, string? supportUrl)
        {
            return new MessageBoxRequest
            {
                Title = title,
                Message = message,
                Icon = MessageBoxIcon.Warning,
                Buttons = MessageBoxButtons.Ok,
                CustomButtons = supportUrl is null
                    ? []
                    : [new MessageBoxButton { Id = UpdateFirmwareButtonId, Caption = UpdateFirmwareButtonCaption }]
            };
        }

        private static bool WasUpdateRequested(MessageBoxOutcome outcome)
        {
            return outcome.Result == MessageBoxResult.Custom
                   && string.Equals(outcome.CustomButtonId, UpdateFirmwareButtonId, StringComparison.Ordinal);
        }
    }
}
