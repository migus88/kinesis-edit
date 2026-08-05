using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Asynchronous wrapper over the blocking <see cref="IVDriveEjector"/> of
    /// specs/03-vdrive-and-files.md §5.3. The UI is gated on <see cref="IsSupported"/> — never on
    /// an OS check — so a platform gains the eject button the moment its ejector is implemented.
    /// </summary>
    public interface IDeviceEjectService
    {
        /// <summary>Whether app-driven ejection is implemented on this platform.</summary>
        bool IsSupported { get; }

        /// <summary>Flushes and releases the volume mounted at <paramref name="volumeRootPath"/>.</summary>
        Task<VDriveEjectResult> EjectAsync(string volumeRootPath);
    }
}
