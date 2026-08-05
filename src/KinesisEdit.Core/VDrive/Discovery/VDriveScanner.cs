using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Implements the specs/03-vdrive-and-files.md §3 detection rules over an
    /// <see cref="IVolumeEnumerator"/>: a candidate volume is a v-Drive when its label matches a
    /// catalog device (03 §2, via <see cref="DeviceCatalog.FindByVolumeLabel"/>; a macOS
    /// duplicate-mount suffix like <c>ADV360 1</c> also matches its base label) and the device's
    /// marker folder and marker file exist under the root (03 §3.1). Marker, version, and
    /// debug-flag names are resolved case-insensitively by enumerating directory entries,
    /// because field drives are FAT (case-insensitive) while local fixtures may sit on
    /// case-sensitive filesystems. Writability (03 §3.3, "version folder writable AND version
    /// file writable") is probed by opening the version file for read-write and disposing the
    /// handle — a read-only FAT mount or read-only file fails the open, covering both halves of
    /// the rule. I/O errors on a candidate skip that candidate; <see cref="Scan"/> never throws.
    /// </summary>
    public sealed class VDriveScanner : IVDriveScanner
    {
        private const string DebugFileName = "debug.on";
        private const string FirmwareDebugFileName = "debug_firm.on";
        private const string DevModeFileName = "devmode.on";

        private readonly IVolumeEnumerator _volumeEnumerator;

        /// <summary>Creates a scanner over <paramref name="volumeEnumerator"/>.</summary>
        public VDriveScanner(IVolumeEnumerator volumeEnumerator)
        {
            _volumeEnumerator = volumeEnumerator ?? throw new ArgumentNullException(nameof(volumeEnumerator));
        }

        /// <summary>Runs one detection pass; the first matching volume per device wins, so at most one location per device is returned.</summary>
        public IReadOnlyList<VDriveLocation> Scan()
        {
            var locations = new List<VDriveLocation>();
            var foundDeviceIds = new HashSet<DeviceId>();

            foreach (var candidate in _volumeEnumerator.EnumerateVolumes())
            {
                VDriveLocation? location;

                try
                {
                    location = TryResolveCandidate(candidate);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    continue;
                }

                if (location is not null && foundDeviceIds.Add(location.Device.Id))
                {
                    locations.Add(location);
                }
            }

            return locations;
        }

        private static VDriveLocation? TryResolveCandidate(VolumeCandidate candidate)
        {
            var device = MatchDevice(candidate.Label);

            if (device is null || !VDriveDetection.IsDetectable(device))
            {
                return null;
            }

            if (!Directory.Exists(candidate.RootPath))
            {
                return null;
            }

            var markerFolderPath = FindEntryIgnoreCase(candidate.RootPath, device.MarkerFolder!, isDirectory: true);

            if (markerFolderPath is null)
            {
                return null;
            }

            var markerFilePath = FindEntryIgnoreCase(markerFolderPath, device.MarkerFile!, isDirectory: false);

            if (markerFilePath is null)
            {
                return null;
            }

            return new VDriveLocation
            {
                Device = device,
                RootPath = candidate.RootPath,
                IsWritable = ProbeIsWritable(candidate.RootPath, device),
                DebugFlags = CollectDebugFlags(candidate.RootPath)
            };
        }

        private static DeviceDefinition? MatchDevice(string label)
        {
            var device = DeviceCatalog.FindByVolumeLabel(label);

            if (device is not null)
            {
                return device;
            }

            // macOS mounts a second volume with the same name as "<label> 1", "<label> 2", …;
            // strip a trailing all-digit segment and retry against the base label.
            var trimmedLabel = label?.Trim();

            if (string.IsNullOrEmpty(trimmedLabel))
            {
                return null;
            }

            var lastSpaceIndex = trimmedLabel.LastIndexOf(' ');

            if (lastSpaceIndex <= 0)
            {
                return null;
            }

            var suffix = trimmedLabel[(lastSpaceIndex + 1)..];

            if (suffix.Length == 0 || !suffix.All(char.IsAsciiDigit))
            {
                return null;
            }

            return DeviceCatalog.FindByVolumeLabel(trimmedLabel[..lastSpaceIndex]);
        }

        private static bool ProbeIsWritable(string rootPath, DeviceDefinition device)
        {
            var versionFolderPath = FindEntryIgnoreCase(rootPath, device.VersionFolder!, isDirectory: true);

            if (versionFolderPath is null)
            {
                return false;
            }

            var versionFilePath = FindEntryIgnoreCase(versionFolderPath, device.VersionFile!, isDirectory: false);

            if (versionFilePath is null)
            {
                return false;
            }

            try
            {
                using var stream = File.Open(versionFilePath, FileMode.Open, FileAccess.ReadWrite);

                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return false;
            }
        }

        private static VDriveDebugFlags CollectDebugFlags(string rootPath)
        {
            var flags = VDriveDebugFlags.None;

            foreach (var filePath in Directory.EnumerateFiles(rootPath))
            {
                var fileName = Path.GetFileName(filePath);

                if (string.Equals(fileName, DebugFileName, StringComparison.OrdinalIgnoreCase))
                {
                    flags |= VDriveDebugFlags.Debug;
                }
                else if (string.Equals(fileName, FirmwareDebugFileName, StringComparison.OrdinalIgnoreCase))
                {
                    flags |= VDriveDebugFlags.FirmwareDebug;
                }
                else if (string.Equals(fileName, DevModeFileName, StringComparison.OrdinalIgnoreCase))
                {
                    flags |= VDriveDebugFlags.DevMode;
                }
            }

            return flags;
        }

        private static string? FindEntryIgnoreCase(string parentPath, string entryName, bool isDirectory)
        {
            if (!Directory.Exists(parentPath))
            {
                return null;
            }

            var entryPaths = isDirectory
                ? Directory.EnumerateDirectories(parentPath)
                : Directory.EnumerateFiles(parentPath);

            foreach (var entryPath in entryPaths)
            {
                if (string.Equals(Path.GetFileName(entryPath), entryName, StringComparison.OrdinalIgnoreCase))
                {
                    return entryPath;
                }
            }

            return null;
        }
    }
}
