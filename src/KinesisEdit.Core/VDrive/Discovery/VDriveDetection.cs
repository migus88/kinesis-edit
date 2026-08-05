using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Shared detectability rule for discovery: per specs/03-vdrive-and-files.md §3.1/§3.3 a
    /// device can only be found on a v-Drive when its marker folder/file and version folder/file
    /// are all defined. Catalog entries with any of them unset (CROSSFIRE keypad, Advantage 360
    /// Professional) are never matched by the scanner and never tracked by the monitor.
    /// </summary>
    internal static class VDriveDetection
    {
        /// <summary>Whether the device carries all four detection fields and can appear as a v-Drive.</summary>
        internal static bool IsDetectable(DeviceDefinition device)
        {
            return device.MarkerFolder is not null
                && device.MarkerFile is not null
                && device.VersionFolder is not null
                && device.VersionFile is not null;
        }
    }
}
