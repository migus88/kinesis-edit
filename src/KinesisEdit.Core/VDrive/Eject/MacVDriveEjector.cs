namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// macOS <see cref="IVDriveEjector"/> that runs "diskutil unmount &lt;volume root&gt;"
    /// to flush and release the v-Drive so the keyboard firmware reloads its files
    /// (specs/03-vdrive-and-files.md §5.3). Using diskutil's "unmount" verb rather than
    /// "eject" is deliberate: the spec only requires that the volume be flushed and released,
    /// and an unmounted (not ejected) volume can cleanly re-mount when the user next opens
    /// the v-Drive from the keyboard.
    /// </summary>
    public sealed class MacVDriveEjector : IVDriveEjector
    {
        private const string DiskutilFileName = "diskutil";

        private const string UnmountVerb = "unmount";

        /// <summary>Always true: ejection is implemented for macOS (specs/03 §5.3).</summary>
        public bool IsSupported => true;

        private readonly IProcessRunner _processRunner;

        /// <summary>Creates the ejector on top of the given process runner.</summary>
        public MacVDriveEjector(IProcessRunner processRunner)
        {
            ArgumentNullException.ThrowIfNull(processRunner);

            _processRunner = processRunner;
        }

        /// <summary>
        /// Runs "diskutil unmount &lt;volumeRootPath&gt;" (specs/03-vdrive-and-files.md §5.3).
        /// Exit code 0 maps to success with diskutil's standard output as the message; any
        /// other exit code maps to failure with standard error (falling back to standard
        /// output) as the message.
        /// </summary>
        public VDriveEjectResult Eject(string volumeRootPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(volumeRootPath);

            var result = _processRunner.Run(DiskutilFileName, [UnmountVerb, volumeRootPath]);

            if (result.ExitCode == 0)
            {
                return new VDriveEjectResult
                {
                    Succeeded = true,
                    Message = NormalizeMessage(result.StandardOutput),
                };
            }

            return new VDriveEjectResult
            {
                Succeeded = false,
                Message = NormalizeMessage(result.StandardError) ?? NormalizeMessage(result.StandardOutput),
            };
        }

        private static string? NormalizeMessage(string text)
        {
            var trimmed = text.Trim();

            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
