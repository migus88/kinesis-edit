using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Builders for the catalog-backed fixtures the shell tests need: discovered drive
    /// locations, version-file lines in each device's dialect (specs/09-firmware.md §1.1), and
    /// ready-made snapshots.
    /// </summary>
    internal static class TestDevices
    {
        public static VDriveLocation CreateLocation(
            DeviceId deviceId,
            bool isWritable = true,
            VDriveDebugFlags debugFlags = VDriveDebugFlags.None)
        {
            var device = DeviceCatalog.GetById(deviceId);

            return new VDriveLocation
            {
                Device = device,
                RootPath = Path.Combine(Path.DirectorySeparatorChar + "fake", device.VolumeLabels[0]),
                IsWritable = isWritable,
                DebugFlags = debugFlags
            };
        }

        /// <summary>
        /// The real settings seam over <paramref name="fileService"/>. Used where a test asserts
        /// on the file the app touched — the suppression store's round trip most of all — so the
        /// merge rules stay Core's rather than being restated in a fake.
        /// </summary>
        public static ISettingsService CreateSettingsService(IVDriveFileService fileService)
        {
            return new SettingsServiceAdapter(new SettingsService(fileService));
        }

        public static string[] CreateVersionFileLines(
            DeviceId deviceId,
            string? modelName = null,
            string keyboardFirmware = "1.0.100",
            string ledFirmware = "1.0.58")
        {
            var model = modelName ?? DeviceCatalog.GetById(deviceId).DisplayName;

            return deviceId switch
            {
                DeviceId.FreestyleEdgeRgb or DeviceId.Tko =>
                [
                    "Model Name: " + model,
                    "KBD Firmware: " + keyboardFirmware,
                    "LED Firmware: " + ledFirmware
                ],
                DeviceId.Advantage360 =>
                [
                    "model=" + model,
                    "kbd_fw_r=" + keyboardFirmware
                ],
                DeviceId.SavantElite2 =>
                [
                    "Firmware version is " + keyboardFirmware
                ],
                _ =>
                [
                    "Model Name: " + model,
                    "Firmware Version: " + keyboardFirmware
                ]
            };
        }

        public static VersionFileInfo CreateVersionFile(
            DeviceId deviceId,
            string keyboardFirmware = "1.0.100",
            string ledFirmware = "1.0.58")
        {
            return VersionFileParser.Parse(
                deviceId,
                CreateVersionFileLines(deviceId, keyboardFirmware: keyboardFirmware, ledFirmware: ledFirmware));
        }

        public static DeviceSnapshot CreateSnapshot(
            DeviceId deviceId,
            VDriveConnectionStatus status = VDriveConnectionStatus.Connected,
            VDriveHealth health = VDriveHealth.Ok,
            VersionFileInfo? versionFile = null,
            VDriveDebugFlags debugFlags = VDriveDebugFlags.None)
        {
            var isDemoMode = status != VDriveConnectionStatus.Connected;
            var location = status == VDriveConnectionStatus.NotDetected
                ? null
                : CreateLocation(deviceId, status == VDriveConnectionStatus.Connected, debugFlags);

            return new DeviceSnapshot
            {
                ScannedDeviceId = deviceId,
                Device = DeviceCatalog.GetById(deviceId),
                Status = status,
                Location = location,
                VersionFile = versionFile ?? VersionFileInfo.Empty,
                Firmware = FirmwareState.FromVersionFile(versionFile ?? VersionFileInfo.Empty, isDemoMode),
                IsDemoMode = isDemoMode,
                Health = status == VDriveConnectionStatus.NotDetected ? VDriveHealth.Unknown : health
            };
        }
    }
}
