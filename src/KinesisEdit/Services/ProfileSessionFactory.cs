using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="IProfileSessionFactory"/>: delegates to
    /// <see cref="ProfileSession.Load"/> and wraps the result in a
    /// <see cref="ProfileSessionAdapter"/>.
    /// <para>
    /// Its one dependency is optional and is forwarded to <see cref="ProfileSession.Load"/>
    /// exactly as given — null included — so a factory constructed with no arguments produces
    /// sessions on the real drive, byte for byte as before. Defaulting is Core's job, not this
    /// factory's: resolving it here would fork the decision in two places. There is no ejector to
    /// forward, because a save never ejects (docs/app/profiles.md).
    /// </para>
    /// </summary>
    public sealed class ProfileSessionFactory : IProfileSessionFactory
    {
        private readonly IVDriveFileService? _fileService;

        /// <summary>
        /// Creates a factory whose sessions read and write through <paramref name="fileService"/>.
        /// Omit it to get the shared real service.
        /// </summary>
        public ProfileSessionFactory(IVDriveFileService? fileService = null)
        {
            _fileService = fileService;
        }

        /// <inheritdoc />
        public IProfileSession Load(VDriveLocation location, DeviceId device, int profileNumber)
        {
            ArgumentNullException.ThrowIfNull(location);

            return new ProfileSessionAdapter(
                ProfileSession.Load(location, device, profileNumber, _fileService));
        }
    }
}
