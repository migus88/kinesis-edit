using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IDemoDeviceProvider"/> whose answer is the set of devices the test
    /// named, and which records every device it was asked about.
    /// <para>
    /// The record is the point. "The demo gate is consulted for <b>every</b> undetected board" and
    /// "no view model special-cases the Freestyle Edge RGB by id" are the same claim seen from two
    /// sides, and neither is observable from the location alone — a caller that hard-coded the RGB
    /// would produce exactly the same snapshots as one that asked properly, right up until a second
    /// board's fixtures were authored.
    /// </para>
    /// </summary>
    internal sealed class FakeDemoDeviceProvider : IDemoDeviceProvider
    {
        /// <summary>Every device this provider was asked to build a location for, in call order.</summary>
        public List<DeviceId> AskedFor { get; } = [];

        private readonly HashSet<DeviceId> _devices;

        /// <summary>Creates a provider that answers for <paramref name="devices"/> and nothing else.</summary>
        public FakeDemoDeviceProvider(params DeviceId[] devices)
        {
            _devices = [.. devices];
        }

        public bool HasDemoContent(DeviceId deviceId)
        {
            return _devices.Contains(deviceId);
        }

        public VDriveLocation? TryCreateLocation(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            AskedFor.Add(device.Id);

            return HasDemoContent(device.Id) ? DemoVDrive.CreateLocation(device) : null;
        }
    }
}
