using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="ISettingsService"/>: a straight pass-through to one Core
    /// <see cref="SettingsService"/>. It adds no behavior of its own — the parse/serialize rules,
    /// the read-modify-write merge and the Advantage2 gate all stay in Core
    /// (docs/app/settings.md), the same way <see cref="ProfileSessionAdapter"/> adds nothing to
    /// <c>ProfileSession</c>.
    /// </summary>
    public sealed class SettingsServiceAdapter : ISettingsService
    {
        private readonly SettingsService _settings;

        /// <summary>Wraps <paramref name="settings"/>.</summary>
        public SettingsServiceAdapter(SettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public KeyboardSettings LoadKeyboardSettings(VDriveLocation location)
        {
            return _settings.LoadKeyboardSettings(location);
        }

        /// <inheritdoc />
        public void SaveKeyboardSettings(VDriveLocation location, VersionFileInfo versionFileInfo, KeyboardSettings settings)
        {
            _settings.SaveKeyboardSettings(location, versionFileInfo, settings);
        }

        /// <inheritdoc />
        public AppSettings LoadAppSettings(VDriveLocation location)
        {
            return _settings.LoadAppSettings(location);
        }

        /// <inheritdoc />
        public void SaveAppSettings(VDriveLocation location, AppSettings settings)
        {
            _settings.SaveAppSettings(location, settings);
        }
    }
}
