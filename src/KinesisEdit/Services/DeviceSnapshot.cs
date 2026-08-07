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
        /// The gate consulted when no provider is supplied. There is exactly one rule for "does
        /// this board have demo content", and it is the embedded fixture set — so the default is
        /// the real provider rather than "no location", which would leave the app's own demo mode
        /// empty until a composition root remembered to wire something in.
        /// </summary>
        private static readonly IDemoDeviceProvider _defaultDemoDevices = new DemoDeviceProvider();

        /// <summary>
        /// Creates the snapshot for a device opened without hardware (troubleshoot dialog
        /// "Launch in Demo Mode", specs/11-feature-dialogs.md §11.8).
        /// <para>
        /// <b>This is the one place a demo snapshot gets its drive.</b> Both producers — the empty
        /// state's <i>Launch ‹device› in Demo Mode</i> and <see cref="DeviceMonitorService"/>'s
        /// poll result for an undetected device — come through here, so the question "may this
        /// board be edited without hardware" is asked once and answered by
        /// <paramref name="demoDevices"/>. A board with no fixtures gets <see langword="null"/>,
        /// which is exactly the snapshot this factory produced before demo content existed: no
        /// location, nothing read, an empty editor.
        /// </para>
        /// </summary>
        public static DeviceSnapshot CreateDemo(DeviceDefinition device, IDemoDeviceProvider? demoDevices = null)
        {
            ArgumentNullException.ThrowIfNull(device);

            return new DeviceSnapshot
            {
                ScannedDeviceId = device.Id,
                Device = device,
                Status = VDriveConnectionStatus.NotDetected,
                Location = (demoDevices ?? _defaultDemoDevices).TryCreateLocation(device),
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

        /// <summary>
        /// The discovered drive; null when the device is not detected <b>and</b> has no demo
        /// content. An undetected board that <see cref="IDemoDeviceProvider"/> answers for carries
        /// the synthetic <see cref="DemoVDrive"/> location instead, which is what lets its editor
        /// read a profile through the ordinary code path.
        /// </summary>
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
