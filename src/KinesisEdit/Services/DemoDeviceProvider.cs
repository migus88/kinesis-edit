using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="IDemoDeviceProvider"/>: a device has demo content exactly when
    /// <see cref="DemoVDriveFixtures"/> carries a fixture drive for it. There is no list of demo
    /// devices anywhere — the embedded fixtures <i>are</i> the list, so authoring a board's
    /// fixtures is the whole of enabling demo mode for it, and forgetting to author them cannot
    /// produce a demo session with nothing behind it.
    /// </summary>
    public sealed class DemoDeviceProvider : IDemoDeviceProvider
    {
        private readonly DemoVDriveFixtures _fixtures;

        /// <summary>Creates the provider over <paramref name="fixtures"/>, defaulting to the embedded set.</summary>
        public DemoDeviceProvider(DemoVDriveFixtures? fixtures = null)
        {
            _fixtures = fixtures ?? DemoVDriveFixtures.Default;
        }

        /// <inheritdoc />
        public bool HasDemoContent(DeviceId deviceId)
        {
            return _fixtures.Has(deviceId);
        }

        /// <inheritdoc />
        public VDriveLocation? TryCreateLocation(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return HasDemoContent(device.Id) ? DemoVDrive.CreateLocation(device) : null;
        }
    }
}
