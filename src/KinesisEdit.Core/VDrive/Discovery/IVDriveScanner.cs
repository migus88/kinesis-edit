namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// One pass of the specs/03-vdrive-and-files.md §3 v-Drive detection: turn the currently
    /// mounted volumes into discovered drive locations.
    /// </summary>
    public interface IVDriveScanner
    {
        /// <summary>Scans the mounted volumes and returns at most one location per detectable device; never throws.</summary>
        IReadOnlyList<VDriveLocation> Scan();
    }
}
