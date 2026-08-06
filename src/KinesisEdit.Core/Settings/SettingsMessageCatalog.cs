namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// The user-facing strings of the settings panel (specs/08-settings.md §5), held as plain
    /// data with no UI framework — the same pattern as
    /// <c>KinesisEdit.Core.Profiles.ProfileSaveMessageCatalog</c> and
    /// <c>KinesisEdit.Core.Firmware.FirmwareGateCatalog</c>.
    /// </summary>
    public static class SettingsMessageCatalog
    {
        /// <summary>
        /// Title of the post-save notification shown after settings are written and before the
        /// device is ejected (spec 08 §5.1, §5.2; suppressed by the <c>savesettings_msg</c> /
        /// <c>save_msg</c> hide flags of §3).
        /// </summary>
        public const string SettingsSavedTitle = "Settings Saved";

        /// <summary>Body of that notification, quoted from spec 08 §5.1.</summary>
        public const string SettingsSavedMessage = "Changes will be implemented when v-Drive is closed.";

        /// <summary>
        /// The explanatory hint shown on the disabled Advantage2 settings panel: spec 08 §5.4
        /// prescribes the hint ("on 2MB firmware they are disabled with an explanatory hint")
        /// without quoting it, so the wording states the condition
        /// <see cref="KeyboardSettingsGate.CanEditKeyboardSettings"/> actually tests — the
        /// version file carries no <c>4MB</c> marker (spec 09 §1.1).
        /// </summary>
        public const string Advantage2SettingsDisabledHint =
            "Keyboard settings can only be changed on the 4MB Advantage2 firmware. "
            + "This keyboard's version file carries no 4MB marker, so the settings below are disabled.";
    }
}
