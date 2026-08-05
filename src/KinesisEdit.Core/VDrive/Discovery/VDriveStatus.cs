namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// The monitored state of one device's v-Drive: its connection status and, when a drive was
    /// found, where it is (specs/03-vdrive-and-files.md §3.3).
    /// </summary>
    public sealed record VDriveStatus
    {
        /// <summary>Shared "not detected" state used before a device's drive has ever been seen.</summary>
        public static VDriveStatus NotDetected { get; } = new()
        {
            Status = VDriveConnectionStatus.NotDetected
        };

        /// <summary>The device's connection state (03 §3.3).</summary>
        public VDriveConnectionStatus Status { get; init; }

        /// <summary>The discovered drive when <see cref="Status"/> is Connected or CannotAccess; null when not detected.</summary>
        public VDriveLocation? Location { get; init; }
    }
}
