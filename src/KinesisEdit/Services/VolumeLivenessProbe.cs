namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="IVolumeLivenessProbe"/>: one <see cref="Directory.Exists"/> against the
    /// mount root, and deliberately nothing else.
    /// <para>
    /// A v-Drive that has been unmounted — by <c>diskutil unmount</c>, by an unplug, or by the
    /// Finder — loses its mount point, so the directory stops existing. That single stat is the
    /// whole check: re-reading the version file or re-probing writability would turn a cheap
    /// liveness tick into the per-tick file read #118 removed, and both facts are re-derived by the
    /// scan this probe's answer triggers anyway.
    /// </para>
    /// <para>
    /// <see cref="Directory.Exists"/> swallows its own I/O and permission failures and answers
    /// false, which is the behaviour this seam wants: the cost of a wrong "gone" is one scan that
    /// finds the drive still there.
    /// </para>
    /// </summary>
    public sealed class VolumeLivenessProbe : IVolumeLivenessProbe
    {
        /// <summary>Whether the mount root still exists on this machine.</summary>
        public bool IsPresent(string rootPath)
        {
            return !string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath);
        }
    }
}
