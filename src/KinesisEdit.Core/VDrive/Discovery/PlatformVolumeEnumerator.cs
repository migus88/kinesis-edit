namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Factory choosing the <see cref="IVolumeEnumerator"/> for the current operating system
    /// (specs/03-vdrive-and-files.md §3.2). On an unrecognized platform it returns an empty
    /// enumerator, so discovery finds nothing and the app degrades to demo mode (03 §3.5)
    /// instead of crashing.
    /// </summary>
    public static class PlatformVolumeEnumerator
    {
        /// <summary>Returns the volume enumerator for the current OS; an empty enumerator on unsupported platforms.</summary>
        public static IVolumeEnumerator Create()
        {
            if (OperatingSystem.IsMacOS())
            {
                return new MacVolumeEnumerator();
            }

            if (OperatingSystem.IsWindows())
            {
                return new WindowsVolumeEnumerator();
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxVolumeEnumerator();
            }

            return new EmptyVolumeEnumerator();
        }

        /// <summary>Fallback enumerator for platforms without a scan implementation; always yields nothing.</summary>
        private sealed class EmptyVolumeEnumerator : IVolumeEnumerator
        {
            public IEnumerable<VolumeCandidate> EnumerateVolumes()
            {
                return [];
            }
        }
    }
}
