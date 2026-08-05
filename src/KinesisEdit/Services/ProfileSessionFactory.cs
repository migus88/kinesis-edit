using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="IProfileSessionFactory"/>: delegates to
    /// <see cref="ProfileSession.Load"/> and wraps the result in a
    /// <see cref="ProfileSessionAdapter"/>.
    /// </summary>
    public sealed class ProfileSessionFactory : IProfileSessionFactory
    {
        /// <inheritdoc />
        public IProfileSession Load(VDriveLocation location, DeviceId device, int profileNumber)
        {
            ArgumentNullException.ThrowIfNull(location);

            return new ProfileSessionAdapter(ProfileSession.Load(location, device, profileNumber));
        }
    }
}
