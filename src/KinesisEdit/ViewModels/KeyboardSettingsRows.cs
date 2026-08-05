using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Builds the rows of the keyboard-settings panel from a device's
    /// <see cref="SettingsCapability"/>. Which controls a board shows is decided here and nowhere
    /// else, from the same capability data the serializer writes by (specs/08-settings.md §2
    /// refined by the per-device panels of §5) — so a device gains its settings panel the moment
    /// its editor opens, with no per-device code.
    /// <para>
    /// Row order follows the spec 08 §2 table. The captions are this app's; the spec quotes
    /// control names ("Active profile slider", "Global speed slider + disable checkbox") rather
    /// than labels.
    /// </para>
    /// </summary>
    public static class KeyboardSettingsRows
    {
        /// <summary>Caption of the startup/active profile row (spec 08 §5.1, §5.2).</summary>
        public const string StartupProfileCaption = "Active profile";

        /// <summary>Caption of the Freestyle Edge brightness/mode row (spec 08 §5.3).</summary>
        public const string LedModeCaption = "Lighting";

        /// <summary>Caption of the macro playback speed row (spec 08 §5.1, §5.3, §5.4).</summary>
        public const string MacroSpeedCaption = "Macro playback speed";

        /// <summary>Caption of the status-report play speed row (spec 08 §5.1-§5.4).</summary>
        public const string StatusPlaySpeedCaption = "Status report speed";

        /// <summary>Caption of the <c>v_drive</c> auto/manual row (spec 08 §2).</summary>
        public const string VDriveAutoMountCaption = "Mount the v-Drive automatically";

        /// <summary>Caption of the Advantage2 <c>v_drive_open_on_startup</c> row (spec 08 §5.4).</summary>
        public const string VDriveOpenOnStartupCaption = "Open the v-Drive on startup";

        /// <summary>Caption of the game-mode row (spec 08 §5.1).</summary>
        public const string GameModeCaption = "Game mode";

        /// <summary>Caption of the Advantage 360 program-lock row (spec 08 §5.2).</summary>
        public const string ProgramLockCaption = "Program lock";

        /// <summary>Caption of the Advantage2 key-click row (spec 08 §5.4).</summary>
        public const string KeyClickToneCaption = "Key clicks";

        /// <summary>Caption of the Advantage2 special-action tone row (spec 08 §5.4).</summary>
        public const string ToggleToneCaption = "Key tones";

        /// <summary>Caption of the pitch-black <c>led_mode</c> option (spec 08 §2).</summary>
        public const string PitchBlackCaption = "Pitch black";

        /// <summary>Caption of the breathe <c>led_mode</c> option (spec 08 §2).</summary>
        public const string BreatheCaption = "Breathe";

        private const string BrightnessCaptionPrefix = "Brightness ";

        // The status report slider is rendered uniformly as the 1-4 slider plus a "disable"
        // checkbox of spec 08 §5.1/§5.2. The FS/Adv2 forms of §5.3/§5.4 are drawn as a plain 0-4
        // slider in the legacy apps, which writes exactly the same values — 0 through the floor
        // rather than through a checkbox — so one shape serves every device.
        private const int MinimumEnabledStatusPlaySpeed = SettingsValueRanges.MinimumStatusPlaySpeed + 1;

        /// <summary>
        /// The rows <paramref name="capability"/> designates, in spec 08 §2 table order. A
        /// device with <see cref="SettingsCapability.None"/> yields none, which is what makes the
        /// editor's Settings tab absent for the SE2, the CROSSFIRE and the Advantage 360
        /// Professional.
        /// </summary>
        public static IReadOnlyList<SettingsRowViewModel> Create(SettingsCapability capability)
        {
            ArgumentNullException.ThrowIfNull(capability);

            var rows = new List<SettingsRowViewModel>();

            AppendStartupProfile(rows, capability);
            AppendLedMode(rows, capability);
            AppendMacroSpeed(rows, capability);
            AppendStatusPlaySpeed(rows, capability);
            AppendVDriveSetting(rows, capability);
            AppendFlags(rows, capability);

            return rows;
        }

        private static void AppendStartupProfile(List<SettingsRowViewModel> rows, SettingsCapability capability)
        {
            if (capability.StartupSetting == StartupSettingKind.None)
            {
                return;
            }

            // The paired led file of spec 08 §5.1 ("+ paired led_mode file") is Core's
            // StartupProfileSettings, never re-implemented here: this slider and
            // ProfileSession.SaveAs must not be able to drift apart.
            rows.Add(new SettingsSliderRowViewModel(
                StartupProfileCaption,
                "The profile the keyboard loads at power-on.",
                SettingsValueRanges.MinimumProfileNumber,
                SettingsValueRanges.MaximumProfileNumber,
                canDisable: false,
                settings => settings.StartupProfileNumber,
                (settings, value) => StartupProfileSettings.ApplyStartupProfile(capability, settings, value)));
        }

        private static void AppendLedMode(List<SettingsRowViewModel> rows, SettingsCapability capability)
        {
            // On a LedFileName device led_mode is the startup profile's paired file and has no
            // control of its own (spec 08 §5.1); only the mode-string form is user-chosen.
            if (capability.LedMode != LedModeKind.ModeString)
            {
                return;
            }

            rows.Add(new SettingsChoiceRowViewModel(
                LedModeCaption,
                "Backlight brightness, or one of the two special modes.",
                CreateLedModeOptions(),
                settings => settings.LedMode,
                (settings, value) => settings with { LedMode = value }));
        }

        private static IReadOnlyList<SettingsChoice> CreateLedModeOptions()
        {
            // The values are Core's — LedModeValues is the same domain KeyboardSettingsSerializer
            // validates against and throws on, so the picker cannot offer a value the save would
            // refuse. Only the captions are this layer's.
            var options = new List<SettingsChoice>(LedModeValues.All.Count);

            foreach (var value in LedModeValues.All)
            {
                options.Add(new SettingsChoice
                {
                    Value = value,
                    Caption = GetLedModeCaption(value)
                });
            }

            return options;
        }

        private static string GetLedModeCaption(string value)
        {
            if (value == LedModeValues.PitchBlack)
            {
                return PitchBlackCaption;
            }

            if (value == LedModeValues.Breathe)
            {
                return BreatheCaption;
            }

            return BrightnessCaptionPrefix + value;
        }

        private static void AppendMacroSpeed(List<SettingsRowViewModel> rows, SettingsCapability capability)
        {
            if (!capability.WritesMacroSpeed)
            {
                return;
            }

            // MacroSpeedMinimum is the slider floor per spec 08 §2 (0 for FS/Adv2, 1 for RGB/TKO);
            // a floor above 0 means 0 is only reachable through the "disable" checkbox of §5.1.
            var minimum = capability.MacroSpeedMinimum ?? SettingsValueRanges.MinimumMacroSpeed;

            rows.Add(new SettingsSliderRowViewModel(
                MacroSpeedCaption,
                "How fast the keyboard plays macros back.",
                minimum,
                SettingsValueRanges.MaximumMacroSpeed,
                canDisable: minimum > SettingsValueRanges.MinimumMacroSpeed,
                settings => settings.MacroSpeed,
                (settings, value) => settings with { MacroSpeed = value }));
        }

        private static void AppendStatusPlaySpeed(List<SettingsRowViewModel> rows, SettingsCapability capability)
        {
            if (capability.StatusSetting == StatusSettingKind.None)
            {
                return;
            }

            rows.Add(new SettingsSliderRowViewModel(
                StatusPlaySpeedCaption,
                "How fast the keyboard types out its status report.",
                MinimumEnabledStatusPlaySpeed,
                SettingsValueRanges.MaximumStatusPlaySpeed,
                canDisable: true,
                settings => settings.StatusPlaySpeed,
                (settings, value) => settings with { StatusPlaySpeed = value }));
        }

        private static void AppendVDriveSetting(List<SettingsRowViewModel> rows, SettingsCapability capability)
        {
            if (capability.VDriveSetting == VDriveSettingKind.AutoManual)
            {
                rows.Add(new SettingsToggleRowViewModel(
                    VDriveAutoMountCaption,
                    "Off leaves the v-Drive closed until the onboard shortcut opens it.",
                    settings => settings.IsVDriveAutoMountEnabled,
                    (settings, value) => settings with { IsVDriveAutoMountEnabled = value }));

                return;
            }

            if (capability.VDriveSetting == VDriveSettingKind.OpenOnStartup)
            {
                rows.Add(new SettingsToggleRowViewModel(
                    VDriveOpenOnStartupCaption,
                    "Off leaves the v-Drive closed until the onboard shortcut opens it.",
                    settings => settings.IsVDriveOpenOnStartupEnabled,
                    (settings, value) => settings with { IsVDriveOpenOnStartupEnabled = value }));
            }
        }

        private static void AppendFlags(List<SettingsRowViewModel> rows, SettingsCapability capability)
        {
            if (capability.WritesGameMode)
            {
                rows.Add(new SettingsToggleRowViewModel(
                    GameModeCaption,
                    "Disables the Windows key and other interrupting keys on the keyboard.",
                    settings => settings.IsGameModeEnabled,
                    (settings, value) => settings with { IsGameModeEnabled = value }));
            }

            if (capability.WritesProgramLock)
            {
                rows.Add(new SettingsToggleRowViewModel(
                    ProgramLockCaption,
                    "Disables programming the keyboard from the keyboard itself.",
                    settings => settings.IsProgramLockEnabled,
                    (settings, value) => settings with { IsProgramLockEnabled = value }));
            }

            if (capability.WritesKeyClickTone)
            {
                rows.Add(new SettingsToggleRowViewModel(
                    KeyClickToneCaption,
                    "The keyboard's click sound on every keystroke.",
                    settings => settings.IsKeyClickToneEnabled,
                    (settings, value) => settings with { IsKeyClickToneEnabled = value }));
            }

            if (capability.WritesToggleTone)
            {
                rows.Add(new SettingsToggleRowViewModel(
                    ToggleToneCaption,
                    "The keyboard's tone when a special action is toggled.",
                    settings => settings.IsToggleToneEnabled,
                    (settings, value) => settings with { IsToggleToneEnabled = value }));
            }
        }
    }
}
