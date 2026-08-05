namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Which keyboard-settings keys the app writes for a device, and in which form, per the
    /// specs/08-settings.md §2 table refined by the per-device settings UIs of §5. Parsing is
    /// common to all devices (spec 08 §2); this capability governs the write side only.
    /// Reserved keys (spec 08 §2: thumb_mode, macro_disable, power_user, country,
    /// program_key_lock, profile_sync_mode) are never written for any device and therefore
    /// have no flags here.
    /// </summary>
    public sealed record SettingsCapability
    {
        /// <summary>A capability for devices without an app-managed settings file (SE2 pedal, CROSSFIRE, Adv360 Pro).</summary>
        public static SettingsCapability None { get; } = new();

        /// <summary>Startup-profile key form: <c>startup_file</c> file name or bare <c>profile</c> number (spec 08 §2).</summary>
        public StartupSettingKind StartupSetting { get; init; }

        /// <summary>The dual-typed <c>led_mode</c> value form: led file name or brightness/mode string (spec 08 §2).</summary>
        public LedModeKind LedMode { get; init; }

        /// <summary>Whether the app writes <c>macro_speed</c> (Edge RGB, TKO, FS Edge/Pro, Advantage2; spec 08 §2).</summary>
        public bool WritesMacroSpeed { get; init; }

        /// <summary>
        /// The macro-speed slider floor per spec 08 §2: 0 for FS Edge/Pro and Advantage2, 1 for
        /// Edge RGB and TKO (whose UIs write 0 only via the "disable" checkbox). Null when the
        /// device does not write <c>macro_speed</c>. The maximum is 9 for every device.
        /// </summary>
        public int? MacroSpeedMinimum { get; init; }

        /// <summary>Status-report play-speed key variant: <c>status_play_speed</c> or the Adv360 short key <c>status</c> (spec 08 §2).</summary>
        public StatusSettingKind StatusSetting { get; init; }

        /// <summary>v-Drive auto-open key form: <c>v_drive</c> auto/manual or the Adv2 <c>v_drive_open_on_startup</c> (spec 08 §2).</summary>
        public VDriveSettingKind VDriveSetting { get; init; }

        /// <summary>
        /// Whether the app writes <c>game_mode</c> (Edge RGB, TKO, FS Edge; spec 08 §2). False
        /// for the FS Pro, whose game-mode switch is hidden (spec 08 §5.3; spec 02 "no game mode").
        /// </summary>
        public bool WritesGameMode { get; init; }

        /// <summary>Whether the app writes the program-lock key <c>lock</c> (Advantage 360 only; spec 08 §2).</summary>
        public bool WritesProgramLock { get; init; }

        /// <summary>Whether the app writes <c>key_click_tone</c> (Advantage2; spec 08 §2).</summary>
        public bool WritesKeyClickTone { get; init; }

        /// <summary>Whether the app writes <c>toggle_tone</c> (Advantage2; spec 08 §2).</summary>
        public bool WritesToggleTone { get; init; }
    }
}
