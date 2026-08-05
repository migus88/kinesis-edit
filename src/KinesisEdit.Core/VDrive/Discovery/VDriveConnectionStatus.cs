namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Per-device connection state shown by the dashboard scan, per
    /// specs/03-vdrive-and-files.md §3.3 ("Connected" / "Not Detected" / a no-access state).
    /// </summary>
    public enum VDriveConnectionStatus
    {
        /// <summary>No status has been determined.</summary>
        None = 0,

        /// <summary>No mounted volume matched the device (03 §3.3 "Not Detected").</summary>
        NotDetected = 1,

        /// <summary>The drive was found but is not writable — demo-mode territory per 03 §3.5 (demo mode = not connected, or no read/write access).</summary>
        CannotAccess = 2,

        /// <summary>The drive was found and is writable (03 §3.3 "Connected").</summary>
        Connected = 3
    }
}
