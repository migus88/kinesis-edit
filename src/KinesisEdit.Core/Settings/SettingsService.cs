using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Thin binding of the pure settings parse/serialize layer to a discovered v-Drive:
    /// loads and saves the device's keyboard-settings file and app_settings.txt through
    /// <see cref="IVDriveFileService"/>. Saves go through the read-modify-write of
    /// <c>UpdateSettingsFile</c> (specs/08-settings.md §1) so unknown and reserved lines
    /// survive verbatim; which keys are written comes from the device's
    /// <c>SettingsCapability</c> in the catalog — no device facts live here. The app-settings
    /// save additionally hands that merge the keys of cleared custom-color slots to delete —
    /// the one thing the merge removes, because an empty slot has no value to write.
    /// </summary>
    public sealed class SettingsService
    {
        private const string AppSettingsFolderName = "settings";
        private const string AppSettingsFileName = "app_settings.txt";
        private const char KeyValueSeparator = '=';

        private readonly IVDriveFileService _fileService;

        public SettingsService(IVDriveFileService fileService)
        {
            ArgumentNullException.ThrowIfNull(fileService);

            _fileService = fileService;
        }

        /// <summary>
        /// The path of app_settings.txt on <paramref name="location"/>: always the drive's
        /// <c>settings/</c> folder (specs/08-settings.md §3, specs/03-vdrive-and-files.md §6) —
        /// even for the Advantage2, whose keyboard settings live in <c>active/</c>.
        /// </summary>
        public static string GetAppSettingsFilePath(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            return Path.Combine(location.RootPath, AppSettingsFolderName, AppSettingsFileName);
        }

        /// <summary>
        /// Loads the device's keyboard-settings file (spec 08 §2). Throws
        /// <see cref="FileNotFoundException"/> when the file is missing — device settings files
        /// are firmware-owned and expected to exist (specs/03-vdrive-and-files.md §5.1).
        /// </summary>
        public KeyboardSettings LoadKeyboardSettings(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            return KeyboardSettingsParser.Parse(_fileService.ReadAllLines(location.SettingsFilePath));
        }

        /// <summary>
        /// Saves <paramref name="settings"/> to the device's keyboard-settings file via
        /// read-modify-write (spec 08 §1): only keys the device's capability designates and
        /// that are set in the model are written; every other line survives verbatim. Throws
        /// <see cref="InvalidOperationException"/> for an Advantage2 without the 4MB firmware
        /// marker (specs/09-firmware.md §1.1). When nothing is writable the file is not
        /// touched.
        /// </summary>
        public void SaveKeyboardSettings(VDriveLocation location, VersionFileInfo versionFileInfo, KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(settings);

            KeyboardSettingsGate.EnsureCanEditKeyboardSettings(location.Device.Id, versionFileInfo);

            var pairs = KeyboardSettingsSerializer.Serialize(location.Device.Settings, settings);

            if (pairs.Count == 0)
            {
                return;
            }

            _fileService.UpdateSettingsFile(location.SettingsFilePath, pairs);
        }

        /// <summary>
        /// Loads app_settings.txt (spec 08 §3). A missing file is a fresh drive, not an error:
        /// the result is <see cref="AppSettings.Empty"/> — every notification shown, no custom
        /// colors.
        /// </summary>
        public AppSettings LoadAppSettings(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            try
            {
                return AppSettingsParser.Parse(_fileService.ReadAllLines(GetAppSettingsFilePath(location)));
            }
            catch (FileNotFoundException)
            {
                return AppSettings.Empty;
            }
        }

        /// <summary>
        /// Saves <paramref name="settings"/> to app_settings.txt: read-modify-write when the
        /// file exists (unknown lines survive verbatim, spec 08 §1), created outright when
        /// absent (fresh drives ship without it) — including the <c>settings/</c> folder on
        /// drives that lack one, e.g. Adv2/SE2 (specs/03-vdrive-and-files.md §4.2/§4.4). Unset
        /// flags and unset colors are skipped — their keys are never written (spec 08 §3) — and
        /// an unset color slot is additionally <b>removed</b>, since skipping it would leave the
        /// old line on the drive and the swatch would come back on the next load
        /// (<see cref="AppSettingsSerializer.SerializeRemovals"/>).
        /// <para>
        /// A save with nothing to write but something to remove still reaches the drive; a save
        /// with neither does not. When the file is missing there is nothing to remove from, so
        /// removals alone never create one — only pairs do.
        /// </para>
        /// </summary>
        public void SaveAppSettings(VDriveLocation location, AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(settings);

            var pairs = AppSettingsSerializer.Serialize(settings);
            var removedKeys = AppSettingsSerializer.SerializeRemovals(settings);

            if (pairs.Count == 0 && removedKeys.Count == 0)
            {
                return;
            }

            var path = GetAppSettingsFilePath(location);

            try
            {
                _fileService.UpdateSettingsFile(path, pairs, removedKeys);
            }
            catch (FileNotFoundException)
            {
                if (pairs.Count == 0)
                {
                    return;
                }

                var lines = pairs
                    .Select(pair => pair.Key + KeyValueSeparator + pair.Value)
                    .ToList();

                _fileService.WriteAllLines(path, lines, allowCreate: true);
            }
        }
    }
}
