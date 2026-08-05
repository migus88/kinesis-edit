namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// The exact settings key names of specs/08-settings.md: the device-side keyboard-settings
    /// keys and reserved keys of §2, and the app-settings notification flags and custom-color
    /// keys of §3. Keys are stored lowercase as written on disk; matching is always
    /// case-insensitive (spec 08 §1).
    /// </summary>
    public static class SettingsKeys
    {
        /// <summary>Startup layout profile as a file name, <c>layout&lt;N&gt;.txt</c> (spec 08 §2).</summary>
        public const string StartupFile = "startup_file";

        /// <summary>Advantage 360 startup profile as a bare number (spec 08 §2).</summary>
        public const string Profile = "profile";

        /// <summary>Dual-typed startup lighting file / brightness mode (spec 08 §2).</summary>
        public const string LedMode = "led_mode";

        /// <summary>Global macro playback speed (spec 08 §2).</summary>
        public const string MacroSpeed = "macro_speed";

        /// <summary>Status-report play speed, long key (spec 08 §2).</summary>
        public const string StatusPlaySpeed = "status_play_speed";

        /// <summary>Status-report play speed, Advantage 360 short key (spec 08 §2).</summary>
        public const string Status = "status";

        /// <summary>v-Drive auto-mount, <c>auto</c>/<c>manual</c> (spec 08 §2). Prefix of <see cref="VDriveOpenOnStartup"/>.</summary>
        public const string VDrive = "v_drive";

        /// <summary>Advantage2 v-Drive auto-open variant, <c>ON</c>/<c>off</c> (spec 08 §2).</summary>
        public const string VDriveOpenOnStartup = "v_drive_open_on_startup";

        /// <summary>Game mode (spec 08 §2).</summary>
        public const string GameMode = "game_mode";

        /// <summary>Advantage 360 program lock (spec 08 §2).</summary>
        public const string Lock = "lock";

        /// <summary>Advantage2 key click sound (spec 08 §2).</summary>
        public const string KeyClickTone = "key_click_tone";

        /// <summary>Advantage2 toggle (special action) tone (spec 08 §2).</summary>
        public const string ToggleTone = "toggle_tone";

        /// <summary>Reserved key, read into state but never written (spec 08 §2).</summary>
        public const string ThumbMode = "thumb_mode";

        /// <summary>Reserved key, read into state but never written (spec 08 §2).</summary>
        public const string MacroDisable = "macro_disable";

        /// <summary>Reserved key, read into state but never written (spec 08 §2).</summary>
        public const string PowerUser = "power_user";

        /// <summary>Reserved key, read into state but never written (spec 08 §2).</summary>
        public const string Country = "country";

        /// <summary>Reserved key from factory-shipped settings files, never written (spec 08 §2).</summary>
        public const string ProgramKeyLock = "program_key_lock";

        /// <summary>Reserved key from factory-shipped settings files, never written (spec 08 §2).</summary>
        public const string ProfileSyncMode = "profile_sync_mode";

        /// <summary>Hide flag for the startup welcome/introduction dialog (spec 08 §3).</summary>
        public const string AppIntroMessage = "app_intro_msg";

        /// <summary>Hide flag for the "Save As" informational dialog (spec 08 §3).</summary>
        public const string SaveAsMessage = "saveas_msg";

        /// <summary>Hide flag for the "Profile N Saved" / "Settings Saved" notifications (spec 08 §3).</summary>
        public const string SaveMessage = "save_msg";

        /// <summary>Hide flag for the multiplay macro notification (spec 08 §3).</summary>
        public const string MultiplayMessage = "multiplay_msg";

        /// <summary>Hide flag for the macro speed notification (spec 08 §3).</summary>
        public const string SpeedMessage = "speed_msg";

        /// <summary>Hide flag for the "Macro copied…" notification (spec 08 §3).</summary>
        public const string CopyMacroMessage = "copy_macro_msg";

        /// <summary>Hide flag for the macro reset confirmation (spec 08 §3).</summary>
        public const string ResetKeyMessage = "reset_key_msg";

        /// <summary>Hide flag for the startup firmware-update reminder (spec 08 §3).</summary>
        public const string FirmwareCheckMessage = "app_checkfirm_msg";

        /// <summary>Hide flag for the lighting-saved notification (spec 08 §3).</summary>
        public const string SaveLightingMessage = "savelighting_msg";

        /// <summary>Hide flag for the settings-saved notification (spec 08 §3).</summary>
        public const string SaveSettingsMessage = "savesettings_msg";

        /// <summary>Hide flag for the "Windows Combination Active" macro-recording hint (spec 08 §3).</summary>
        public const string WindowsCombinationMessage = "windowscombo_msg";

        /// <summary>Hide flag for the "Downstroke/Upstroke Active" half-keystroke hint (spec 08 §3).</summary>
        public const string UpDownKeystrokeMessage = "updownkeystroke_msg";

        /// <summary>
        /// Custom-color key prefix; <c>cust_color_1</c>…<c>cust_color_3</c> are prefixes of
        /// <c>cust_color_10</c>…<c>cust_color_12</c> and must never be matched by bare prefix
        /// (spec 08 §1, §3).
        /// </summary>
        public const string CustomColorPrefix = "cust_color_";

        /// <summary>Builds the key <c>cust_color_&lt;number&gt;</c> for a color slot 1-12 (spec 08 §3).</summary>
        public static string GetCustomColorKey(int number)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(number, AppSettings.CustomColorCount);

            return CustomColorPrefix + number;
        }
    }
}
