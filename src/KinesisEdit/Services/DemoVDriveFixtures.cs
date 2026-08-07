using System.Reflection;
using System.Text;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The curated v-Drive content a demo session reads, embedded in this assembly and addressed
    /// by the same paths a real drive would use.
    /// <para>
    /// Fixtures live under <c>Assets/Demo/&lt;DeviceId&gt;/</c> as <c>EmbeddedResource</c> items
    /// and are read back through <see cref="Assembly.GetManifestResourceStream(string)"/> —
    /// deliberately <b>not</b> <c>AvaloniaResource</c> and <c>AssetLoader</c>, which would make a
    /// service depend on the toolkit and break invariant 8 of docs/app/app-shell.md.
    /// </para>
    /// <para>
    /// A fixture's on-drive path is <b>derived, never spelled</b>: the folder names
    /// (<c>firmware/</c>, <c>settings/</c>, <c>layouts/</c>, <c>lighting/</c>) and the settings
    /// file name are catalog data, reached through <see cref="VDriveLocation"/>'s computed
    /// properties and <see cref="SettingsService.GetAppSettingsFilePath"/>. The fixture folder
    /// therefore names <em>roles</em> — <c>Version.txt</c>, <c>KeyboardSettings.txt</c>,
    /// <c>AppSettings.txt</c>, <c>Layouts/</c>, <c>Lighting/</c> — and only the per-profile file
    /// names inside the two folder roles (<c>layout1.txt</c>, <c>led1.txt</c>) are the fixture's
    /// own, because those are what the profile number names rather than what the device does.
    /// </para>
    /// </summary>
    public sealed class DemoVDriveFixtures
    {
        /// <summary>Manifest-name prefix of every fixture resource: the project's own namespace plus the asset folder.</summary>
        private const string ResourcePrefix = "KinesisEdit.Assets.Demo.";

        private const char ResourceSeparator = '.';

        /// <summary>Fixture role serving <see cref="VDriveLocation.VersionFilePath"/>.</summary>
        private const string VersionRole = "Version";

        /// <summary>Fixture role serving <see cref="VDriveLocation.SettingsFilePath"/>.</summary>
        private const string KeyboardSettingsRole = "KeyboardSettings";

        /// <summary>Fixture role serving <see cref="SettingsService.GetAppSettingsFilePath"/>.</summary>
        private const string AppSettingsRole = "AppSettings";

        /// <summary>Fixture folder serving <see cref="VDriveLocation.LayoutsFolderPath"/>.</summary>
        private const string LayoutsRole = "Layouts";

        /// <summary>Fixture folder serving <see cref="VDriveLocation.LightingFolderPath"/>.</summary>
        private const string LightingRole = "Lighting";

        /// <summary>
        /// The fixtures embedded in this assembly. Built once and eagerly, so a mis-filed fixture
        /// (an unknown device folder, an unknown role, a folder role on a device that has no such
        /// folder) fails loudly at first use rather than turning into a demo session that quietly
        /// reads nothing.
        /// </summary>
        public static DemoVDriveFixtures Default { get; } = new();

        /// <summary>
        /// The devices that have fixtures — today the Freestyle Edge RGB and nothing else. Taken
        /// from the device folders actually present, never from a hard-coded list, so adding a
        /// board's fixtures is the whole of adding that board to demo mode.
        /// </summary>
        public IReadOnlyCollection<DeviceId> Devices => _devices;

        /// <summary>Every on-drive path these fixtures answer, across every device.</summary>
        public IReadOnlyCollection<string> Paths => _resourcesByPath.Keys;

        private readonly Assembly _assembly;
        private readonly Dictionary<string, string> _resourcesByPath = new(StringComparer.Ordinal);
        private readonly HashSet<DeviceId> _devices = [];

        /// <summary>Reads the fixtures embedded in this assembly.</summary>
        public DemoVDriveFixtures()
            : this(typeof(DemoVDriveFixtures).Assembly)
        {
        }

        /// <summary>Reads the fixtures embedded in <paramref name="assembly"/>. For tests that supply their own set.</summary>
        public DemoVDriveFixtures(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            _assembly = assembly;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var (deviceId, path) = ResolveFixture(resourceName);

                _resourcesByPath.Add(path, resourceName);
                _devices.Add(deviceId);
            }
        }

        /// <summary>Whether <paramref name="deviceId"/> has a fixture drive at all.</summary>
        public bool Has(DeviceId deviceId)
        {
            return _devices.Contains(deviceId);
        }

        /// <summary>
        /// The lines of the fixture at <paramref name="path"/>, decoded as
        /// <see cref="Encoding.Latin1"/> and split on CRLF, LF or a lone CR — the same raw 8-bit
        /// reading rule the real service implements (specs/03-vdrive-and-files.md §5.1), so a
        /// fixture parses byte for byte the way the drive's own file would.
        /// </summary>
        public bool TryReadLines(string path, out IReadOnlyList<string> lines)
        {
            lines = [];

            if (path is null || !_resourcesByPath.TryGetValue(path, out var resourceName))
            {
                return false;
            }

            lines = ReadResourceLines(resourceName);

            return true;
        }

        /// <summary>
        /// <see cref="TryReadLines"/> or <see cref="FileNotFoundException"/> — the same failure
        /// the real service reports for a missing file, so callers that already handle it
        /// (<see cref="SettingsService.LoadAppSettings"/> treats it as a fresh drive) behave
        /// identically against a fixture set that has no such file.
        /// </summary>
        public IReadOnlyList<string> ReadLines(string path)
        {
            if (!TryReadLines(path, out var lines))
            {
                throw new FileNotFoundException($"{path} is not a demo fixture", path);
            }

            return lines;
        }

        private IReadOnlyList<string> ReadResourceLines(string resourceName)
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Demo fixture '{resourceName}' could not be opened.");

            // StreamReader.ReadLine splits on CRLF, LF and a lone CR and yields no empty last
            // line for a trailing newline — exactly VDriveFileService's tolerance, borrowed
            // rather than reimplemented.
            using var reader = new StreamReader(stream, Encoding.Latin1);

            var lines = new List<string>();

            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// The device a fixture resource belongs to and the on-drive path it answers. The manifest
        /// name is <c>KinesisEdit.Assets.Demo.&lt;DeviceId&gt;.&lt;role&gt;[.&lt;file&gt;].&lt;ext&gt;</c>,
        /// which is unambiguous because no fixture folder or file name contains a dot of its own.
        /// </summary>
        private static (DeviceId DeviceId, string Path) ResolveFixture(string resourceName)
        {
            var segments = resourceName[ResourcePrefix.Length..].Split(ResourceSeparator);

            if (segments.Length is < 3 or > 4)
            {
                throw new InvalidOperationException(
                    $"Demo fixture '{resourceName}' is not <DeviceId>.<role>[.<file>].<extension>.");
            }

            if (!Enum.TryParse<DeviceId>(segments[0], out var deviceId) || !Enum.IsDefined(deviceId))
            {
                throw new InvalidOperationException(
                    $"Demo fixture '{resourceName}' is filed under '{segments[0]}', which is not a DeviceId.");
            }

            var location = DemoVDrive.CreateLocation(DeviceCatalog.GetById(deviceId));
            var role = segments[1];

            if (segments.Length == 3)
            {
                return (deviceId, ResolveFilePath(location, role, resourceName));
            }

            var fileName = segments[2] + ResourceSeparator + segments[3];

            return (deviceId, Path.Combine(ResolveFolderPath(location, role, resourceName), fileName));
        }

        private static string ResolveFilePath(VDriveLocation location, string role, string resourceName)
        {
            return role switch
            {
                VersionRole => location.VersionFilePath,
                KeyboardSettingsRole => location.SettingsFilePath,
                AppSettingsRole => SettingsService.GetAppSettingsFilePath(location),
                _ => throw new InvalidOperationException(
                    $"Demo fixture '{resourceName}' names no known role; expected {VersionRole}, {KeyboardSettingsRole} or {AppSettingsRole}.")
            };
        }

        private static string ResolveFolderPath(VDriveLocation location, string role, string resourceName)
        {
            var folderPath = role switch
            {
                LayoutsRole => location.LayoutsFolderPath,
                LightingRole => location.LightingFolderPath,
                _ => throw new InvalidOperationException(
                    $"Demo fixture '{resourceName}' names no known folder; expected {LayoutsRole} or {LightingRole}.")
            };

            return folderPath ?? throw new InvalidOperationException(
                $"Demo fixture '{resourceName}' targets a folder {location.Device.DisplayName} does not have.");
        }
    }
}
