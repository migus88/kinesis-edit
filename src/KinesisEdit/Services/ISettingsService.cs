using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The settings engine as the app layer sees it (docs/app/settings.md): the device-side
    /// keyboard-settings file of specs/08-settings.md §2 and the app-owned
    /// <c>app_settings.txt</c> of §3. Core's <c>SettingsService</c> is a sealed class, so it
    /// cannot be substituted in a test; this interface — implemented for real by
    /// <see cref="SettingsServiceAdapter"/> — is the seam, exactly like
    /// <see cref="IProfileSession"/> is the seam over Core's sealed <c>ProfileSession</c>.
    /// <para>
    /// The app-settings pair carries the whole <see cref="AppSettings"/> model rather than
    /// single keys, so the notification hide flags and the twelve <c>cust_color_N</c> picker
    /// slots travel the same route: <c>app_settings.txt</c> must have exactly one reader and
    /// one writer, or two features clobber each other's state.
    /// </para>
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>Reads the device's keyboard-settings file; throws when it is missing or unreadable.</summary>
        KeyboardSettings LoadKeyboardSettings(VDriveLocation location);

        /// <summary>
        /// Writes the keys <paramref name="location"/>'s device designates, read-modify-write so
        /// unmanaged lines survive. Throws <see cref="InvalidOperationException"/> for an
        /// Advantage2 without the 4MB marker (specs/09-firmware.md §1.1) — the UI must ask
        /// <c>KeyboardSettingsGate.CanEditKeyboardSettings</c> first.
        /// </summary>
        void SaveKeyboardSettings(VDriveLocation location, VersionFileInfo versionFileInfo, KeyboardSettings settings);

        /// <summary>Reads <c>app_settings.txt</c>; a missing file yields <see cref="AppSettings.Empty"/>.</summary>
        AppSettings LoadAppSettings(VDriveLocation location);

        /// <summary>Writes the set flags and colors of <paramref name="settings"/>, creating the file when absent.</summary>
        void SaveAppSettings(VDriveLocation location, AppSettings settings);
    }
}
