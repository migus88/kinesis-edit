using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Which devices offer the "Check for Updates" dialog of specs/09-firmware.md §3, and which
    /// of them show its lighting row. §3 names Edge RGB, TKO, and the Advantage 360; §4 states
    /// the FS Edge/Pro, Advantage2, and Savant Elite 2 apps have no update dialog at all.
    /// The Advantage 360 Professional follows the Advantage 360 rules wherever it appears, but is
    /// excluded here because <see cref="DeviceDefinition.IsProgrammable"/> is false for it — it is
    /// configured through the ZMK web GUI and exposes no v-Drive to read local versions from.
    /// </summary>
    public static class UpdateCheckEligibility
    {
        /// <summary>Devices offering the update dialog, in device-catalog order: Edge RGB, TKO, Advantage 360.</summary>
        public static IReadOnlyList<DeviceId> SupportedDevices { get; } = BuildSupportedDevices();

        /// <summary>Whether the device offers the update dialog.</summary>
        public static bool IsSupported(DeviceId deviceId)
        {
            return SupportedDevices.Contains(deviceId);
        }

        /// <summary>
        /// Whether the dialog shows a lighting row for the device. False for the Advantage 360
        /// family, whose lighting key repeats the keyboard value — §3 hides the row and shrinks
        /// the form. False for every unsupported device.
        /// </summary>
        public static bool HasLightingRow(DeviceId deviceId)
        {
            if (!IsSupported(deviceId))
            {
                return false;
            }

            return deviceId is not (DeviceId.Advantage360 or DeviceId.Advantage360Professional);
        }

        private static IReadOnlyList<DeviceId> BuildSupportedDevices()
        {
            DeviceId[] candidates =
            [
                DeviceId.FreestyleEdgeRgb,
                DeviceId.Tko,
                DeviceId.Advantage360,
                DeviceId.Advantage360Professional
            ];

            return DeviceCatalog.All
                .Where(device => candidates.Contains(device.Id) && device.IsProgrammable)
                .Select(device => device.Id)
                .ToArray();
        }
    }
}
