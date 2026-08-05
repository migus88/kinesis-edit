using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Services
{
    /// <summary>
    /// One refresh of <see cref="DeviceMonitorService"/>: the state of every tracked device plus
    /// the status transitions observed during that poll, so the shell can react to the
    /// specs/03-vdrive-and-files.md §3.5 "Keyboard Connection Lost" condition.
    /// </summary>
    public sealed record DeviceMonitorUpdate
    {
        /// <summary>Every tracked device, in device-catalog order.</summary>
        public required IReadOnlyList<DeviceSnapshot> Snapshots { get; init; }

        /// <summary>Status transitions raised by the monitor during this refresh; empty when nothing changed.</summary>
        public required IReadOnlyList<VDriveStatusChange> Changes { get; init; }

        /// <summary>Whether a previously connected drive disappeared during this refresh (03 §3.5).</summary>
        public bool HasConnectionLoss => Changes.Any(change => change.IsConnectionLost);
    }
}
