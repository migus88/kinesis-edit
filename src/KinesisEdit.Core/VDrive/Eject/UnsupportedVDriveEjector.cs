namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// <see cref="IVDriveEjector"/> for platforms where app-driven ejection is not implemented
    /// yet (the Windows lock–dismount–eject sequence of specs/03-vdrive-and-files.md §5.3 and
    /// Linux support land in later issues). Never throws; per specs/03 §5.3 the user can
    /// always eject/unmount the volume in the OS or close the v-Drive with the on-board
    /// shortcut instead.
    /// </summary>
    public sealed class UnsupportedVDriveEjector : IVDriveEjector
    {
        private const string NotSupportedMessage =
            "Ejecting the v-Drive from the app is not supported on this platform yet. " +
            "Eject or unmount the volume in the operating system, or close the v-Drive " +
            "with the keyboard's on-board shortcut.";

        /// <summary>Always false: this platform has no eject implementation yet.</summary>
        public bool IsSupported => false;

        /// <summary>Always returns a failed result explaining the platform gap; never throws.</summary>
        public VDriveEjectResult Eject(string volumeRootPath)
        {
            return new VDriveEjectResult
            {
                Succeeded = false,
                Message = NotSupportedMessage,
            };
        }
    }
}
