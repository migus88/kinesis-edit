namespace KinesisEdit.Core.VDrive
{
    /// <summary>
    /// Debug/dev switches enabled by dropping empty marker files at the v-Drive root,
    /// per specs/03-vdrive-and-files.md §3.4. Each flag corresponds to one file name;
    /// detection is case-insensitive.
    /// </summary>
    [Flags]
    public enum VDriveDebugFlags
    {
        /// <summary>No debug marker files are present.</summary>
        None = 0,

        /// <summary>An empty <c>debug.on</c> file at the drive root enables debug mode (03 §3.4).</summary>
        Debug = 1 << 0,

        /// <summary>An empty <c>debug_firm.on</c> file at the drive root enables firmware-debug mode (03 §3.4).</summary>
        FirmwareDebug = 1 << 1,

        /// <summary>An empty <c>devmode.on</c> file at the drive root enables dev mode (03 §3.4).</summary>
        DevMode = 1 << 2
    }
}
