namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Immutable description of one device from the master device table in specs/02-devices.md,
    /// augmented with the v-Drive detection and path data of specs/03-vdrive-and-files.md §1-§4.
    /// All instances live in <see cref="DeviceCatalog"/>; this type carries data only.
    /// </summary>
    public sealed record DeviceDefinition
    {
        /// <summary>Stable identifier of the device.</summary>
        public required DeviceId Id { get; init; }

        /// <summary>Human-readable device name.</summary>
        public required string DisplayName { get; init; }

        /// <summary>The legacy app's numeric device id, 0-8 in master-table order (specs/02-devices.md).</summary>
        public required int LegacyAppId { get; init; }

        /// <summary>Which legacy SmartSet application serves the device (specs/02-devices.md).</summary>
        public required ServingApp ServingApp { get; init; }

        /// <summary>
        /// Ordered v-Drive volume label candidates (primary first, up to 3), stored uppercase and
        /// matched uppercased + trimmed per specs/03-vdrive-and-files.md §2. Empty when the device
        /// exposes no v-Drive (Advantage 360 Professional).
        /// </summary>
        public IReadOnlyList<string> VolumeLabels { get; init; } = [];

        /// <summary>Folder that must exist on a candidate volume (03 §3.1); null when the device is not detectable.</summary>
        public string? MarkerFolder { get; init; }

        /// <summary>File that must exist inside <see cref="MarkerFolder"/> (03 §3.1); null when the device is not detectable.</summary>
        public string? MarkerFile { get; init; }

        /// <summary>Folder holding the version file (default "firmware"; Adv2/SE2 "active", Adv360 "settings"; 03 §3.3).</summary>
        public string? VersionFolder { get; init; }

        /// <summary>Version file name (default "version.txt"; Adv360 "settings.txt" doubles as version file; 03 §3.3).</summary>
        public string? VersionFile { get; init; }

        /// <summary>Folder holding the settings file (default "settings"; Adv2 "active"; 03 §3.3).</summary>
        public string? SettingsFolder { get; init; }

        /// <summary>Settings file name (default "kbd_settings.txt"; Adv2 "state.txt", Adv360 "settings.txt"; 03 §3.3).</summary>
        public string? SettingsFile { get; init; }

        /// <summary>Programmable key layers (Adv2 and Gen1 boards: 2; Adv360: 5); null where not applicable.</summary>
        public int? LayerCount { get; init; }

        /// <summary>How layout/profile files are named on the v-Drive (specs/02 master table, 03 §4).</summary>
        public LayoutFileScheme LayoutScheme { get; init; } = LayoutFileScheme.None;

        /// <summary>Macro support and limits (specs/02-devices.md, 04 §5.3, 06 §1, §2.1, §4, §6).</summary>
        public MacroCapability Macros { get; init; } = MacroCapability.None;

        /// <summary>Tap-and-hold support and limits (specs/11-feature-dialogs.md §11.1, 04 §5.3).</summary>
        public TapAndHoldCapability TapAndHold { get; init; } = TapAndHoldCapability.None;

        /// <summary>
        /// Whether a key position may hold one of the 11 multi-modifier combination codes
        /// (specs/11-feature-dialogs.md §11.2 "Advantage360 only", "Adv360 format only";
        /// specs/05-key-model.md §1.3 <c>Multimodifiers</c> and §5.7 "Multimodifiers (Adv360)").
        /// True for the Advantage 360 alone — the Advantage 360 Professional is configured through
        /// the ZMK web GUI and is not SmartSet-programmable at all.
        /// </summary>
        public bool SupportsMultiModifiers { get; init; }

        /// <summary>Lighting hardware (specs/02-devices.md).</summary>
        public LightingCapability Lighting { get; init; } = LightingCapability.None;

        /// <summary>Keyboard-settings keys the app writes for the device, and their forms (specs/08-settings.md §2, §5).</summary>
        public SettingsCapability Settings { get; init; } = SettingsCapability.None;

        /// <summary>Special hardware notes carried by the master table (specs/02-devices.md).</summary>
        public string? HardwareNotes { get; init; }

        /// <summary>External configuration site, where the device is configured outside SmartSet (Adv360 Pro ZMK GUI).</summary>
        public string? ConfigurationUrl { get; init; }

        /// <summary>Support/help URL given in specs/02-devices.md (Adv360 Pro only); null elsewhere.</summary>
        public string? SupportUrl { get; init; }

        /// <summary>Hint text for the on-board shortcut that opens the v-Drive (03 §1); null where the spec gives none.</summary>
        public string? VDriveShortcutHint { get; init; }

        /// <summary>Whether the device is programmable via SmartSet (false: CROSSFIRE, Adv360 Professional).</summary>
        public bool IsProgrammable { get; init; }

        /// <summary>Whether the device is a reserved, never-shipped entry (CROSSFIRE keypad; specs/02, 03 §2).</summary>
        public bool IsFutureDevice { get; init; }
    }
}
