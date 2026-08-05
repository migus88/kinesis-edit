using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.VDrive
{
    /// <summary>
    /// A discovered, mounted v-Drive: which device it belongs to, where its root is, and the
    /// working paths derived from that root per specs/03-vdrive-and-files.md §3.4. Instances
    /// are produced by the discovery scanner only for detectable devices (marker and version
    /// folder/file all defined in the catalog), so the derived paths are always resolvable.
    /// </summary>
    public sealed record VDriveLocation
    {
        /// <summary>The catalog device this drive belongs to (matched by volume label, 03 §2).</summary>
        public required DeviceDefinition Device { get; init; }

        /// <summary>Absolute root path of the mounted volume (e.g. <c>/Volumes/ADV360</c> or <c>E:\</c>; 03 §3.3).</summary>
        public required string RootPath { get; init; }

        /// <summary>
        /// Whether the drive accepts writes, per the 03 §3.3 rule "version folder writable AND
        /// version file writable". False routes the app toward demo mode (03 §3.5).
        /// </summary>
        public bool IsWritable { get; init; }

        /// <summary>Debug switches found as empty marker files at the drive root (03 §3.4).</summary>
        public VDriveDebugFlags DebugFlags { get; init; }

        /// <summary>Full path of the device's version file (03 §3.1/§3.3; for the Adv360 this is <c>settings/settings.txt</c>).</summary>
        public string VersionFilePath => Path.Combine(RootPath, Device.VersionFolder!, Device.VersionFile!);

        /// <summary>Full path of the folder holding the device's settings file (03 §3.3/§3.4).</summary>
        public string SettingsFolderPath => Path.Combine(RootPath, Device.SettingsFolder!);

        /// <summary>Full path of the device's keyboard-settings file (03 §3.3; e.g. <c>settings/kbd_settings.txt</c>, Adv2 <c>active/state.txt</c>).</summary>
        public string SettingsFilePath => Path.Combine(RootPath, Device.SettingsFolder!, Device.SettingsFile!);

        /// <summary>
        /// Full path of the folder holding layout files (03 §3.4/§4; <c>layouts</c>, or <c>active</c>
        /// for Adv2/SE2); null when the device's layout scheme defines no layout folder.
        /// </summary>
        public string? LayoutsFolderPath => Device.LayoutScheme.LayoutFolder is null
            ? null
            : Path.Combine(RootPath, Device.LayoutScheme.LayoutFolder);

        /// <summary>
        /// Full path of the folder holding led files (03 §3.4/§4.1); null when the device has no
        /// lighting files (e.g. FS Pro, Adv2).
        /// </summary>
        public string? LightingFolderPath => Device.LayoutScheme.LightingFolder is null
            ? null
            : Path.Combine(RootPath, Device.LayoutScheme.LightingFolder);
    }
}
