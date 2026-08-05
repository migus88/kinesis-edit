using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The pages an "Update Now" button opens (specs/09-firmware.md §3 step 7): the app never
    /// transfers firmware, it sends the user to the support site. Keyboard and lighting rows
    /// target the device's firmware section — the §2 page already carried by
    /// <see cref="FirmwareSupportUrls"/>, i.e. <see cref="DeviceDefinition.TroubleshootingUrl"/>
    /// plus <c>#firmware</c>, except the Advantage 360, whose page uses <c>#firmware-updates</c>.
    /// The app row targets the same site's <c>#smartset-app</c> section.
    /// </summary>
    public static class UpdateCheckUrls
    {
        /// <summary>Anchor of the SmartSet-app download section (§3 step 7, §4).</summary>
        public const string AppAnchor = "#smartset-app";

        /// <summary>
        /// Firmware page for the keyboard and lighting rows, or null where the spec lists none.
        /// Identical to the §2 "Upgrade Firmware" target, so the two never drift apart.
        /// </summary>
        public static string? FindFirmwareUrl(DeviceId deviceId)
        {
            return FirmwareSupportUrls.FindUrl(deviceId);
        }

        /// <summary>
        /// SmartSet-app page for the app row — the device's troubleshooting URL with
        /// <see cref="AppAnchor"/> appended; null for devices without one.
        /// </summary>
        public static string? FindAppUrl(DeviceId deviceId)
        {
            var device = DeviceCatalog.All.FirstOrDefault(candidate => candidate.Id == deviceId);

            if (device?.TroubleshootingUrl is null)
            {
                return null;
            }

            return device.TroubleshootingUrl + AppAnchor;
        }
    }
}
