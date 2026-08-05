namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// Ejects the mounted v-Drive volume after a save so the OS flushes the files and the
    /// keyboard firmware can take the drive back and reload them
    /// (specs/03-vdrive-and-files.md §5.3).
    /// </summary>
    public interface IVDriveEjector
    {
        /// <summary>Whether app-driven ejection is implemented for the current platform (specs/03 §5.3).</summary>
        bool IsSupported { get; }

        /// <summary>
        /// Attempts to flush and release the volume mounted at <paramref name="volumeRootPath"/>
        /// (specs/03-vdrive-and-files.md §5.3).
        /// </summary>
        VDriveEjectResult Eject(string volumeRootPath);
    }
}
