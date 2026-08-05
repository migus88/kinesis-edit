using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// One device's v-Drive status transition observed by a monitor poll
    /// (specs/03-vdrive-and-files.md §3.3/§3.5).
    /// </summary>
    public sealed record VDriveStatusChange
    {
        /// <summary>The device whose status changed.</summary>
        public required DeviceId DeviceId { get; init; }

        /// <summary>The status before this poll.</summary>
        public required VDriveStatus Previous { get; init; }

        /// <summary>The status after this poll.</summary>
        public required VDriveStatus Current { get; init; }

        /// <summary>
        /// True when a previously Connected drive is no longer Connected — the 03 §3.5
        /// "Keyboard Connection Lost" condition.
        /// </summary>
        public bool IsConnectionLost => Previous.Status == VDriveConnectionStatus.Connected
            && Current.Status != VDriveConnectionStatus.Connected;
    }
}
