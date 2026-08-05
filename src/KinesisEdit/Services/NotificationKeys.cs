namespace KinesisEdit.Services
{
    /// <summary>
    /// The <c>*_msg</c> keys of <c>app_settings.txt</c> (specs/08-settings.md §3). Each is a
    /// boolean persisted as <c>on</c>/<c>off</c> where <c>on</c> means "hide this notification";
    /// a missing key defaults to <c>off</c> = show. Keys are quoted verbatim from the spec table.
    /// </summary>
    public static class NotificationKeys
    {
        /// <summary>Startup welcome/introduction dialog.</summary>
        public const string AppIntro = "app_intro_msg";

        /// <summary>"Save As" informational dialog.</summary>
        public const string SaveAs = "saveas_msg";

        /// <summary>"Profile N Saved" / "Settings Saved" notifications.</summary>
        public const string Save = "save_msg";

        /// <summary>Multiplay macro notification.</summary>
        public const string Multiplay = "multiplay_msg";

        /// <summary>Macro speed notification.</summary>
        public const string Speed = "speed_msg";

        /// <summary>"Macro copied. Now select a new trigger key…" notification.</summary>
        public const string CopyMacro = "copy_macro_msg";

        /// <summary>"Do you want to reset the current Macro?" confirmation.</summary>
        public const string ResetKey = "reset_key_msg";

        /// <summary>Startup firmware-update reminder.</summary>
        public const string CheckFirmware = "app_checkfirm_msg";

        /// <summary>Lighting-saved notification (RGB legacy UI).</summary>
        public const string SaveLighting = "savelighting_msg";

        /// <summary>Settings-saved notification.</summary>
        public const string SaveSettings = "savesettings_msg";

        /// <summary>"Windows Combination Active" macro-recording hint.</summary>
        public const string WindowsCombo = "windowscombo_msg";

        /// <summary>"Downstroke/Upstroke Active" half-keystroke hint.</summary>
        public const string UpDownKeystroke = "updownkeystroke_msg";

        /// <summary>Every suppression key of specs/08-settings.md §3, in spec table order.</summary>
        public static IReadOnlyList<string> All { get; } =
        [
            AppIntro,
            SaveAs,
            Save,
            Multiplay,
            Speed,
            CopyMacro,
            ResetKey,
            CheckFirmware,
            SaveLighting,
            SaveSettings,
            WindowsCombo,
            UpDownKeystroke
        ];
    }
}
