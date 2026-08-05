namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Platform abstraction over "list the mounted volumes" — the enumeration half of the
    /// specs/03-vdrive-and-files.md §3.2 desktop-mode scan. Implementations must never throw
    /// from enumeration; an unreadable platform yields no candidates.
    /// </summary>
    public interface IVolumeEnumerator
    {
        /// <summary>Enumerates the currently mounted volumes as label + root-path candidates.</summary>
        IEnumerable<VolumeCandidate> EnumerateVolumes();
    }
}
