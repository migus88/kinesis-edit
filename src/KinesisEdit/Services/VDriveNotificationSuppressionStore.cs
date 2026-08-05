using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Notification preferences backed by <c>app_settings.txt</c> in the v-Drive's settings folder
    /// (specs/08-settings.md §3). Values are <c>on</c>/<c>off</c> where <c>on</c> means "hide";
    /// a missing key or a missing file means "show". Writes go through
    /// <see cref="IVDriveFileService.UpdateSettingsFile"/> so unknown and reserved lines (custom
    /// colors, future keys) survive verbatim (specs/08-settings.md §1); the file is created only
    /// when it does not exist yet. Used only for a connected, writable drive — demo mode gets
    /// <see cref="NullNotificationSuppressionStore"/>.
    /// </summary>
    public sealed class VDriveNotificationSuppressionStore : INotificationSuppressionStore
    {
        /// <summary>Name of the app-settings file on the v-Drive (specs/08-settings.md §3).</summary>
        public const string FileName = "app_settings.txt";

        private const string HiddenValue = "on";
        private const string ShownValue = "off";
        private const char KeyValueSeparator = '=';

        /// <summary>Resolves the <c>app_settings.txt</c> path inside a drive's settings folder (03 §3.4).</summary>
        public static string GetFilePath(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            return Path.Combine(location.SettingsFolderPath, FileName);
        }

        private readonly IVDriveFileService _fileService;
        private readonly string _filePath;

        /// <summary>Creates a store over the <c>app_settings.txt</c> file at <paramref name="filePath"/>.</summary>
        public VDriveNotificationSuppressionStore(IVDriveFileService fileService, string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _filePath = filePath;
        }

        /// <summary>
        /// True only when the file holds <c>&lt;key&gt;=on</c> (key matched case-insensitively,
        /// value compared case-insensitively). A missing file, a missing key, <c>off</c>, or an
        /// unreadable file all mean "show".
        /// </summary>
        public bool IsHidden(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var value = ReadValue(key);

            return string.Equals(value, HiddenValue, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes <c>&lt;key&gt;=on</c> or <c>&lt;key&gt;=off</c>, preserving every other line;
        /// creates <c>app_settings.txt</c> when the drive does not have one yet. Failures are
        /// swallowed — a preference is never worth breaking a dialog over.
        /// </summary>
        public void SetHidden(string key, bool hidden)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var value = hidden ? HiddenValue : ShownValue;

            try
            {
                _fileService.UpdateSettingsFile(_filePath, [KeyValuePair.Create(key, value)]);
            }
            catch (FileNotFoundException)
            {
                CreateFile(key, value);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        }

        private string? ReadValue(string key)
        {
            IReadOnlyList<string> lines;

            try
            {
                lines = _fileService.ReadAllLines(_filePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }

            foreach (var line in lines)
            {
                // Same match rule as the settings merge of specs/08-settings.md §1: the key,
                // case-insensitive, immediately followed by the '=' separator.
                if (line.Length > key.Length
                    && line[key.Length] == KeyValueSeparator
                    && line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line[(key.Length + 1)..].Trim();
                }
            }

            return null;
        }

        private void CreateFile(string key, string value)
        {
            try
            {
                _fileService.WriteAllLines(_filePath, [key + KeyValueSeparator + value], allowCreate: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        }
    }
}
