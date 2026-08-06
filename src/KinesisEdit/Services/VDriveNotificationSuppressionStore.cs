using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Notification preferences backed by <c>app_settings.txt</c> in the v-Drive's settings folder
    /// (specs/08-settings.md §3). Values are <c>on</c>/<c>off</c> where <c>on</c> means "hide";
    /// a missing key or a missing file means "show". Used only for a connected, writable drive —
    /// demo mode gets <see cref="ReadOnlyNotificationSuppressionStore"/> or
    /// <see cref="NullNotificationSuppressionStore"/>.
    /// <para>
    /// Every read and write goes through <see cref="ISettingsService"/>, i.e. through Core's
    /// <c>AppSettingsParser</c>/<c>AppSettingsSerializer</c> and its read-modify-write merge, so
    /// this file has exactly one reader and one writer. A second, line-based implementation here
    /// would silently clobber the state Core writes (the twelve <c>cust_color_N</c> picker slots
    /// most of all) and would resolve the file's path differently for the Advantage2, whose
    /// keyboard settings live in <c>active/</c> while <c>app_settings.txt</c> always lives in
    /// <c>settings/</c> (spec 08 §3, specs/03-vdrive-and-files.md §6).
    /// </para>
    /// </summary>
    public sealed class VDriveNotificationSuppressionStore : INotificationSuppressionStore
    {
        /// <summary>
        /// The <c>app_settings.txt</c> path of a drive. Delegates to Core so that the path this
        /// store touches and the path <see cref="ISettingsService"/> touches can never diverge.
        /// </summary>
        public static string GetFilePath(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            return SettingsService.GetAppSettingsFilePath(location);
        }

        private readonly ISettingsService _settings;
        private readonly VDriveLocation _location;

        /// <summary>Creates a store over the <c>app_settings.txt</c> file of <paramref name="location"/>.</summary>
        public VDriveNotificationSuppressionStore(ISettingsService settings, VDriveLocation location)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _location = location ?? throw new ArgumentNullException(nameof(location));
        }

        /// <summary>
        /// True only when the file holds <c>&lt;key&gt;=on</c> (key matched case-insensitively,
        /// value compared case-insensitively). A missing file, a missing key, <c>off</c>, an
        /// unreadable file, or a key outside the twelve of spec 08 §3 all mean "show".
        /// </summary>
        public bool IsHidden(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return ReadFlag(Load(), key) == true;
        }

        /// <summary>
        /// Writes <c>&lt;key&gt;=on</c> or <c>&lt;key&gt;=off</c>, preserving every other line;
        /// creates <c>app_settings.txt</c> when the drive does not have one yet. Keys outside the
        /// twelve of spec 08 §3 are not modeled and are therefore never written. Failures are
        /// swallowed — a preference is never worth breaking a dialog over.
        /// </summary>
        public void SetHidden(string key, bool hidden)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var updated = WriteFlag(Load(), key, hidden);

            if (updated is null)
            {
                return;
            }

            try
            {
                _settings.SaveAppSettings(_location, updated);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        }

        private AppSettings Load()
        {
            try
            {
                return _settings.LoadAppSettings(_location);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return AppSettings.Empty;
            }
        }

        private static bool? ReadFlag(AppSettings settings, string key)
        {
            return Normalize(key) switch
            {
                NotificationKeys.AppIntro => settings.IsAppIntroMessageHidden,
                NotificationKeys.SaveAs => settings.IsSaveAsMessageHidden,
                NotificationKeys.Save => settings.IsSaveMessageHidden,
                NotificationKeys.Multiplay => settings.IsMultiplayMessageHidden,
                NotificationKeys.Speed => settings.IsSpeedMessageHidden,
                NotificationKeys.CopyMacro => settings.IsCopyMacroMessageHidden,
                NotificationKeys.ResetKey => settings.IsResetKeyMessageHidden,
                NotificationKeys.CheckFirmware => settings.IsFirmwareCheckMessageHidden,
                NotificationKeys.SaveLighting => settings.IsSaveLightingMessageHidden,
                NotificationKeys.SaveSettings => settings.IsSaveSettingsMessageHidden,
                NotificationKeys.WindowsCombo => settings.IsWindowsCombinationMessageHidden,
                NotificationKeys.UpDownKeystroke => settings.IsUpDownKeystrokeMessageHidden,
                _ => null
            };
        }

        private static AppSettings? WriteFlag(AppSettings settings, string key, bool hidden)
        {
            return Normalize(key) switch
            {
                NotificationKeys.AppIntro => settings with { IsAppIntroMessageHidden = hidden },
                NotificationKeys.SaveAs => settings with { IsSaveAsMessageHidden = hidden },
                NotificationKeys.Save => settings with { IsSaveMessageHidden = hidden },
                NotificationKeys.Multiplay => settings with { IsMultiplayMessageHidden = hidden },
                NotificationKeys.Speed => settings with { IsSpeedMessageHidden = hidden },
                NotificationKeys.CopyMacro => settings with { IsCopyMacroMessageHidden = hidden },
                NotificationKeys.ResetKey => settings with { IsResetKeyMessageHidden = hidden },
                NotificationKeys.CheckFirmware => settings with { IsFirmwareCheckMessageHidden = hidden },
                NotificationKeys.SaveLighting => settings with { IsSaveLightingMessageHidden = hidden },
                NotificationKeys.SaveSettings => settings with { IsSaveSettingsMessageHidden = hidden },
                NotificationKeys.WindowsCombo => settings with { IsWindowsCombinationMessageHidden = hidden },
                NotificationKeys.UpDownKeystroke => settings with { IsUpDownKeystrokeMessageHidden = hidden },
                _ => null
            };
        }

        // Keys are stored lowercase on disk and matched case-insensitively (spec 08 §1), while a
        // switch over string constants is ordinal — so the caller's casing is folded first.
        private static string Normalize(string key)
        {
            return key.Trim().ToLowerInvariant();
        }
    }
}
