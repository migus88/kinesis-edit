namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Shared directory-listing helper for the mount-point based enumerators (macOS and Linux):
    /// each subdirectory of a mount root is one volume candidate whose label is the directory
    /// name (specs/03-vdrive-and-files.md §3.2 — the label check is implicit in the mount-point
    /// name). Missing or unreadable roots yield nothing; errors are never thrown.
    /// </summary>
    internal static class VolumeDirectoryLister
    {
        /// <summary>Yields one candidate per subdirectory of <paramref name="scanRoot"/>, labeled with the directory name.</summary>
        internal static IEnumerable<VolumeCandidate> ListSubdirectories(string scanRoot)
        {
            string[] directories;

            try
            {
                directories = Directory.GetDirectories(scanRoot);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (var directory in directories)
            {
                yield return new VolumeCandidate(directory, Path.GetFileName(directory));
            }
        }
    }
}
