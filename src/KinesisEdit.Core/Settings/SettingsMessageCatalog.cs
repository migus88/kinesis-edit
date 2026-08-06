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

        /// <summary>
        /// Banner above a read-only device-settings panel, verbatim from docs/design/mockups.md
        /// mockup 1j. States the hardware fact, not an error: the panel renders it on the
        /// advisory ramp, never on the error ramp.
        /// </summary>
        public const string Advantage2ReadOnlyBanner =
            "This Advantage2 has 2 MB firmware — device settings can't be written to it";

        /// <summary>
        /// The banner's explanation, verbatim from mockup 1j. Says what still works, so the
        /// screen reads as a limitation rather than a failure.
        /// </summary>
        public const string Advantage2ReadOnlyExplanation =
            "The board reports 2MB. Its settings file is read-only in firmware, so the controls "
            + "below show what the keyboard is doing but can't change it. Remaps, macros, and "
            + "layers all still save normally.";

        /// <summary>
        /// Caption of the link beside that explanation. Mockup 1j writes it as
        /// "Which board do I have? ↗"; the arrow is stored <b>here without it</b> and drawn by the
        /// view as <c>IconExternalLink</c> geometry — the same split the empty state uses for
        /// "Troubleshooting tips" (<c>NoDeviceViewModel.TroubleshootingButtonCaption</c>), because
        /// a Latin-1 arrow in a caption is a glyph the shipped font may not carry.
        /// </summary>
        public const string WhichBoardLinkCaption = "Which board do I have?";

        /// <summary>
        /// The quiet per-row marker on a read-only settings row (mockup 1j: "1 (read-only)"). Not
        /// a disabled-control affordance — the value is real and current, it just cannot be
        /// written.
        /// </summary>
        public const string ReadOnlyRowMarker = "(read-only)";

        /// <summary>
        /// The demo-mode caveat over the app-preferences section, verbatim from mockup 1j. Spec 08
        /// §3 bans <b>saving</b> app settings in demo mode, not loading them, so the section still
        /// shows what the drive holds.
        /// </summary>
        public const string DemoModePreferencesCaveat =
            "In Demo Mode these preferences are readable but never written — toggles snap back "
            + "when the session ends.";
    }
}
