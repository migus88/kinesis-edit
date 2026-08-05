namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// Factory for the platform-appropriate <see cref="IVDriveEjector"/>
    /// (specs/03-vdrive-and-files.md §5.3).
    /// </summary>
    public static class VDriveEject
    {
        /// <summary>
        /// Returns a <see cref="MacVDriveEjector"/> on macOS and an
        /// <see cref="UnsupportedVDriveEjector"/> everywhere else (specs/03 §5.3: Windows and
        /// Linux eject support land in later issues).
        /// </summary>
        public static IVDriveEjector CreateForCurrentPlatform()
        {
            if (OperatingSystem.IsMacOS())
            {
                return new MacVDriveEjector(new SystemProcessRunner());
            }

            return new UnsupportedVDriveEjector();
        }
    }
}
