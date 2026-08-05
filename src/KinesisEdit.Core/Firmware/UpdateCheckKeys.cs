using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The key table of specs/09-firmware.md §3 step 4: which <see cref="VersionManifest"/> key
    /// holds the remote version for each row. Keyboard/lighting keys are per device, the app key
    /// is per master app × operating system. Pure lookup — it does not decide whether a device
    /// offers the dialog (that is <see cref="UpdateCheckEligibility"/>).
    /// </summary>
    public static class UpdateCheckKeys
    {
        /// <summary>
        /// Manifest key for the device's keyboard firmware: TKO <c>tko_keyboard_version</c>,
        /// Edge RGB <c>keyboard_ver</c>, Advantage 360 family <c>kb360_version</c>; null elsewhere.
        /// </summary>
        public static string? FindKeyboardKey(DeviceId deviceId)
        {
            return deviceId switch
            {
                DeviceId.Tko => VersionManifestKeys.TkoKeyboardVersion,
                DeviceId.FreestyleEdgeRgb => VersionManifestKeys.KeyboardVersion,
                DeviceId.Advantage360 => VersionManifestKeys.Advantage360Version,
                DeviceId.Advantage360Professional => VersionManifestKeys.Advantage360Version,
                _ => null
            };
        }

        /// <summary>
        /// Manifest key for the device's lighting firmware: TKO <c>tko_lighting_version</c>,
        /// Edge RGB <c>lighting_ver</c>, Advantage 360 family <c>kb360_version</c> — the same
        /// value as its keyboard key, which is why that row is hidden; null elsewhere.
        /// </summary>
        public static string? FindLightingKey(DeviceId deviceId)
        {
            return deviceId switch
            {
                DeviceId.Tko => VersionManifestKeys.TkoLightingVersion,
                DeviceId.FreestyleEdgeRgb => VersionManifestKeys.LightingVersion,
                DeviceId.Advantage360 => VersionManifestKeys.Advantage360Version,
                DeviceId.Advantage360Professional => VersionManifestKeys.Advantage360Version,
                _ => null
            };
        }

        /// <summary>
        /// Manifest key for the SmartSet app's own version: Windows gaming <c>app_ver</c>,
        /// Windows office <c>pc_app_version</c>, macOS gaming <c>mac_app_ver</c>, macOS office
        /// <c>mac_app_version</c>. Linux deliberately reuses the macOS keys — the legacy app had
        /// no Linux build and the endpoint publishes no Linux key (see
        /// <see cref="UpdateCheckPlatform.Linux"/>). Null when either argument is
        /// <c>None</c> or the app is the standalone FS Edge/Pro binary, which has no dialog.
        /// </summary>
        public static string? FindAppKey(ServingApp servingApp, UpdateCheckPlatform platform)
        {
            return (servingApp, platform) switch
            {
                (ServingApp.SmartSetMasterGaming, UpdateCheckPlatform.Windows) => VersionManifestKeys.WindowsGamingAppVersion,
                (ServingApp.SmartSetMasterOffice, UpdateCheckPlatform.Windows) => VersionManifestKeys.WindowsOfficeAppVersion,
                (ServingApp.SmartSetMasterGaming, UpdateCheckPlatform.MacOs) => VersionManifestKeys.MacGamingAppVersion,
                (ServingApp.SmartSetMasterOffice, UpdateCheckPlatform.MacOs) => VersionManifestKeys.MacOfficeAppVersion,
                (ServingApp.SmartSetMasterGaming, UpdateCheckPlatform.Linux) => VersionManifestKeys.MacGamingAppVersion,
                (ServingApp.SmartSetMasterOffice, UpdateCheckPlatform.Linux) => VersionManifestKeys.MacOfficeAppVersion,
                _ => null
            };
        }

        /// <summary>
        /// Manifest key for the app version, resolving the master app from the device's
        /// <see cref="DeviceDefinition.ServingApp"/>. Null for unknown ids.
        /// </summary>
        public static string? FindAppKey(DeviceId deviceId, UpdateCheckPlatform platform)
        {
            var device = DeviceCatalog.All.FirstOrDefault(candidate => candidate.Id == deviceId);

            return device is null ? null : FindAppKey(device.ServingApp, platform);
        }
    }
}
