namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Declarative description of where a device's layout/profile (and lighting) files
    /// live on the v-Drive and how they are named, per specs/02-devices.md (master table)
    /// and specs/03-vdrive-and-files.md §4.
    /// </summary>
    public sealed record LayoutFileScheme
    {
        /// <summary>A scheme for devices that have no layout files.</summary>
        public static LayoutFileScheme None { get; } = new();

        /// <summary>The file-naming scheme in use.</summary>
        public LayoutSchemeKind Kind { get; init; }

        /// <summary>Folder holding layout files, relative to the drive root (e.g. "layouts", "active"); null when none.</summary>
        public string? LayoutFolder { get; init; }

        /// <summary>
        /// Folder holding led files for <see cref="LayoutSchemeKind.NumberedProfiles"/> devices with
        /// lighting ("lighting"); null when the device has no lighting files (03 §4.1: FS Pro has none).
        /// </summary>
        public string? LightingFolder { get; init; }

        /// <summary>First numbered profile position with an on-disk file (1 for numbered schemes; 0 otherwise).</summary>
        public int FirstProfileNumber { get; init; }

        /// <summary>Last numbered profile position with an on-disk file (9 for numbered schemes; 0 otherwise).</summary>
        public int LastProfileNumber { get; init; }

        /// <summary>
        /// True for the Advantage 360, whose profile 0 is a factory, non-programmable profile with no
        /// on-disk file; only profiles 1-9 are backed by files (specs/02-devices.md §Advantage 360).
        /// </summary>
        public bool HasReadOnlyFactoryProfile { get; init; }

        /// <summary>The single fixed layout file name for <see cref="LayoutSchemeKind.PedalFile"/> ("pedals.txt"); null otherwise.</summary>
        public string? FixedLayoutFileName { get; init; }
    }
}
