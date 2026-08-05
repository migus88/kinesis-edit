using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Default <see cref="IDeviceEjectService"/>: runs the blocking ejector on the thread pool so
    /// the UI thread stays responsive while the OS flushes and releases the volume
    /// (specs/03-vdrive-and-files.md §5.3).
    /// </summary>
    public sealed class DeviceEjectService : IDeviceEjectService
    {
        /// <summary>Whether the underlying ejector supports this platform.</summary>
        public bool IsSupported => _ejector.IsSupported;

        private readonly IVDriveEjector _ejector;

        /// <summary>Creates the service over <paramref name="ejector"/>.</summary>
        public DeviceEjectService(IVDriveEjector ejector)
        {
            _ejector = ejector ?? throw new ArgumentNullException(nameof(ejector));
        }

        /// <summary>Ejects the volume on a background thread.</summary>
        public Task<VDriveEjectResult> EjectAsync(string volumeRootPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(volumeRootPath);

            return Task.Run(() => _ejector.Eject(volumeRootPath));
        }
    }
}
