using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="ISettingsService"/> recording every load and save and handing back
    /// staged models. The parse/serialize rules themselves are Core's and are tested there; what
    /// the app layer has to get right is <b>which</b> model it writes, for which location, and
    /// whether it writes at all.
    /// </summary>
    internal sealed class FakeSettingsService : ISettingsService
    {
        public KeyboardSettings KeyboardSettingsToReturn { get; set; } = KeyboardSettings.Empty;

        public AppSettings AppSettingsToReturn { get; set; } = AppSettings.Empty;

        /// <summary>When set, <see cref="LoadKeyboardSettings"/> throws it — an unreadable file.</summary>
        public Exception? LoadKeyboardExceptionToThrow { get; set; }

        /// <summary>When set, <see cref="SaveKeyboardSettings"/> throws it — a vanished drive.</summary>
        public Exception? SaveKeyboardExceptionToThrow { get; set; }

        /// <summary>When set, <see cref="LoadAppSettings"/> throws it — an unreadable app_settings.txt.</summary>
        public Exception? LoadAppExceptionToThrow { get; set; }

        /// <summary>When set, <see cref="SaveAppSettings"/> throws it — a read-only or vanished drive.</summary>
        public Exception? SaveAppExceptionToThrow { get; set; }

        public int LoadKeyboardCallCount { get; private set; }

        public List<KeyboardSaveCall> KeyboardSaves { get; } = [];

        public List<AppSettings> AppSettingsSaves { get; } = [];

        /// <summary>Every macro-name write, with the profile it was scoped to (issue #93).</summary>
        public List<MacroNameSaveCall> MacroNameSaves { get; } = [];

        public KeyboardSettings LoadKeyboardSettings(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            LoadKeyboardCallCount++;

            if (LoadKeyboardExceptionToThrow is not null)
            {
                throw LoadKeyboardExceptionToThrow;
            }

            return KeyboardSettingsToReturn;
        }

        public void SaveKeyboardSettings(VDriveLocation location, VersionFileInfo versionFileInfo, KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(settings);

            if (SaveKeyboardExceptionToThrow is not null)
            {
                throw SaveKeyboardExceptionToThrow;
            }

            KeyboardSaves.Add(new KeyboardSaveCall(location, versionFileInfo, settings));
        }

        public AppSettings LoadAppSettings(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            if (LoadAppExceptionToThrow is not null)
            {
                throw LoadAppExceptionToThrow;
            }

            return AppSettingsToReturn;
        }

        public void SaveAppSettings(VDriveLocation location, AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(settings);

            if (SaveAppExceptionToThrow is not null)
            {
                throw SaveAppExceptionToThrow;
            }

            AppSettingsToReturn = settings;
            AppSettingsSaves.Add(settings);
        }

        public void SaveMacroNames(VDriveLocation location, AppSettings settings, int profileNumber)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(settings);

            if (SaveAppExceptionToThrow is not null)
            {
                throw SaveAppExceptionToThrow;
            }

            AppSettingsToReturn = settings;
            MacroNameSaves.Add(new MacroNameSaveCall(location, settings, profileNumber));
        }

        internal sealed record KeyboardSaveCall(VDriveLocation Location, VersionFileInfo VersionFile, KeyboardSettings Settings);

        internal sealed record MacroNameSaveCall(VDriveLocation Location, AppSettings Settings, int ProfileNumber);
    }
}
