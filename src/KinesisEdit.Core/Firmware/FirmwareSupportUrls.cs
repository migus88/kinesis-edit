using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The per-device firmware support pages of specs/09-firmware.md §2 — the URLs the legacy
    /// "Upgrade Firmware" buttons open next to a gate refusal dialog. Pure data.
    /// </summary>
    public static class FirmwareSupportUrls
    {
        private static readonly IReadOnlyDictionary<DeviceId, string> _urlsByDevice = new Dictionary<DeviceId, string>
        {
            [DeviceId.FreestylePro] = "https://kinesis-ergo.com/support/freestyle-pro/#firmware-updates",
            [DeviceId.FreestyleEdge] = "https://gaming.kinesis-ergo.com/fs-edge-support/#firmware",
            [DeviceId.Advantage2] = "https://kinesis-ergo.com/support/advantage2/#firmware-updates",
            [DeviceId.FreestyleEdgeRgb] = "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#firmware",
            [DeviceId.Tko] = "https://gaming.kinesis-ergo.com/tko-support/#firmware",
            [DeviceId.Advantage360] = "https://kinesis-ergo.com/support/kb360/#firmware-updates"
        };

        /// <summary>Returns the device's firmware support page, or null where the spec lists none.</summary>
        public static string? FindUrl(DeviceId deviceId)
        {
            return _urlsByDevice.TryGetValue(deviceId, out var url) ? url : null;
        }
    }
}
