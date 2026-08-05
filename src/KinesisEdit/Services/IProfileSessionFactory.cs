using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Opens a profile of a device's v-Drive. The one seam between the editor view models and
    /// <see cref="Core.Profiles.ProfileSession.Load"/>, whose static-on-a-sealed-type shape would
    /// otherwise pin every editor test to a real drive.
    /// </summary>
    public interface IProfileSessionFactory
    {
        /// <summary>
        /// Loads profile <paramref name="profileNumber"/> of <paramref name="device"/> from
        /// <paramref name="location"/>. Throws whatever the underlying load throws — a missing
        /// file, an unreadable drive, an unsupported device — and the caller reports it.
        /// </summary>
        IProfileSession Load(VDriveLocation location, DeviceId device, int profileNumber);
    }
}
