using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Immutable per-device state the shell renders: what the last poll found, the version-file
    /// data re-read during that poll, and the demo-mode condition of
    /// specs/03-vdrive-and-files.md §3.5 (not connected, or no read/write access). Produced by
    /// <see cref="DeviceMonitorService"/>; carries no behavior beyond the demo factory.
    /// </summary>
    public sealed record DeviceSnapshot
    {
        /// <summary>
        /// Creates the snapshot for a device opened without hardware (troubleshoot dialog
        /// "Launch in Demo Mode", specs/11-feature-dialogs.md §11.8).
        /// </summary>
        public static DeviceSnapshot CreateDemo(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return new DeviceSnapshot
            {
                ScannedDeviceId = device.Id,
                Device = device,
                Status = VDriveConnectionStatus.NotDetected,
                Firmware = FirmwareState.FromVersionFile(VersionFileInfo.Empty, isDemoMode: true),
                IsDemoMode = true,
                Health = VDriveHealth.Unknown
            };
        }

        /// <summary>
        /// The catalog slot this snapshot was scanned from — the identity cards and sessions are
        /// keyed by. Deliberately distinct from <see cref="DeviceId"/>: the scanner yields at most
        /// one location per catalog device, so this is unique within a refresh, whereas the
        /// resolved id is not — the Freestyle Edge and Freestyle Pro slots both re-derive their
        /// model from the version file and can land on the same device.
        /// </summary>
        public required DeviceId ScannedDeviceId { get; init; }

        /// <summary>
        /// The device this snapshot describes, already resolved: for the Freestyle boards the
        /// definition is re-derived from the version file on every poll (specs/10-apps-and-ui.md,
        /// "the app re-reads the device's version file on every connectivity check and resets the
        /// active model").
        /// </summary>
        public required DeviceDefinition Device { get; init; }

        /// <summary>The resolved device id — what the card displays, not what it is keyed by.</summary>
        public DeviceId DeviceId => Device.Id;

        /// <summary>The resolved device's display name.</summary>
        public string DisplayName => Device.DisplayName;

        /// <summary>Connection state reported by the last poll (03 §3.3).</summary>
        public required VDriveConnectionStatus Status { get; init; }

        /// <summary>The discovered drive; null when the device is not detected.</summary>
        public VDriveLocation? Location { get; init; }

        /// <summary>Version-file data re-read during the last poll; empty when nothing could be read.</summary>
        public VersionFileInfo VersionFile { get; init; } = VersionFileInfo.Empty;

        /// <summary>Firmware state for gate queries, carrying <see cref="IsDemoMode"/> (specs/09-firmware.md §2).</summary>
        public FirmwareState Firmware { get; init; }

        /// <summary>Whether editing this device runs in demo mode: not connected, or not writable (03 §3.5).</summary>
        public bool IsDemoMode { get; init; }

        /// <summary>Outcome of the version-file re-check driving the v-Drive OK / v-Drive Error indicator.</summary>
        public VDriveHealth Health { get; init; }

        /// <summary>Whether a drive was found at all — the dashboard shows cards for these devices only.</summary>
        public bool IsDetected => Status is VDriveConnectionStatus.Connected or VDriveConnectionStatus.CannotAccess;
    }
}
