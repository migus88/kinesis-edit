namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// macOS volume enumerator. The legacy apps probe fixed <c>/VOLUMES/&lt;label&gt;/</c> paths per
    /// candidate label (specs/03-vdrive-and-files.md §3.2); this implementation instead lists every
    /// subdirectory of the scan root (default <c>/Volumes</c>) as a candidate, using the directory
    /// name as the volume label — the label check itself is done by the scanner against the catalog.
    /// A missing or unreadable scan root yields no candidates.
    /// </summary>
    public sealed class MacVolumeEnumerator : IVolumeEnumerator
    {
        private const string DefaultScanRoot = "/Volumes";

        private readonly string _scanRoot;

        /// <summary>Creates the enumerator; <paramref name="scanRoot"/> overrides the default <c>/Volumes</c> mount root (used by tests).</summary>
        public MacVolumeEnumerator(string? scanRoot = null)
        {
            _scanRoot = scanRoot ?? DefaultScanRoot;
        }

        /// <summary>Yields one candidate per subdirectory of the scan root; yields nothing when the root is missing or unreadable.</summary>
        public IEnumerable<VolumeCandidate> EnumerateVolumes()
        {
            return VolumeDirectoryLister.ListSubdirectories(_scanRoot);
        }
    }
}
