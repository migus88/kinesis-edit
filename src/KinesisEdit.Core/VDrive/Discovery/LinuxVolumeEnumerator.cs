namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Linux volume enumerator. The spec's desktop-mode scan (specs/03-vdrive-and-files.md §3.2)
    /// has no Linux variant, so this follows the macOS mount-point model: every subdirectory of
    /// the removable-media mount roots (default <c>/media/&lt;user&gt;</c> and
    /// <c>/run/media/&lt;user&gt;</c>) is a candidate labeled with its directory name. Missing or
    /// unreadable roots yield no candidates.
    /// </summary>
    public sealed class LinuxVolumeEnumerator : IVolumeEnumerator
    {
        private readonly IReadOnlyList<string> _scanRoots;

        /// <summary>Creates the enumerator; <paramref name="scanRoots"/> overrides the default per-user media mount roots (used by tests).</summary>
        public LinuxVolumeEnumerator(IEnumerable<string>? scanRoots = null)
        {
            _scanRoots = scanRoots?.ToArray() ?? CreateDefaultScanRoots();
        }

        /// <summary>Yields one candidate per subdirectory of each scan root; unreadable roots contribute nothing.</summary>
        public IEnumerable<VolumeCandidate> EnumerateVolumes()
        {
            foreach (var scanRoot in _scanRoots)
            {
                foreach (var candidate in VolumeDirectoryLister.ListSubdirectories(scanRoot))
                {
                    yield return candidate;
                }
            }
        }

        private static string[] CreateDefaultScanRoots()
        {
            var userName = Environment.UserName;

            return
            [
                Path.Combine("/media", userName),
                Path.Combine("/run/media", userName)
            ];
        }
    }
}
