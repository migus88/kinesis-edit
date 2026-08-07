using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Answers whether a device can be edited with no hardware attached, and hands out the
    /// synthetic <see cref="VDriveLocation"/> that makes it possible.
    /// <para>
    /// The gate is not decoration. The detection loop builds a demo snapshot for <b>every</b>
    /// undetected catalog device, so a provider that answered for a board with no fixtures would
    /// point that board's editor at files which are not there and turn an empty demo session into
    /// a failed one. A device without fixtures must behave exactly as it did before demo content
    /// existed: no location, nothing read.
    /// </para>
    /// </summary>
    public interface IDemoDeviceProvider
    {
        /// <summary>Whether <paramref name="deviceId"/> has demo content to open.</summary>
        bool HasDemoContent(DeviceId deviceId);

        /// <summary>
        /// The synthetic drive for <paramref name="device"/>, or <see langword="null"/> when it
        /// has no demo content. The location reports <see cref="VDriveLocation.IsWritable"/>
        /// false, so every writability-driven decision in the app (spec 08 §3's "never saved in
        /// demo mode" most of all) already resolves correctly for it.
        /// </summary>
        VDriveLocation? TryCreateLocation(DeviceDefinition device);
    }
}
