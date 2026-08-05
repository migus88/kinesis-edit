namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Typed in-memory model of a device-side keyboard-settings file
    /// (specs/08-settings.md §2: <c>settings/kbd_settings.txt</c>, Advantage2
    /// <c>active/state.txt</c>, Advantage 360 <c>settings/settings.txt</c>). Every property is
    /// nullable; null means the key is absent from the file — the spec defines no defaults
    /// (spec 08 §2 notes). Reserved keys are read into state but never written
    /// (spec 08 §2); which writable keys apply to a device is
    /// <c>KinesisEdit.Core.Devices.SettingsCapability</c>.
    /// </summary>
    public sealed record KeyboardSettings
    {
        /// <summary>A model with every key absent.</summary>
        public static KeyboardSettings Empty { get; } = new();

        /// <summary>
        /// Startup profile number 1-9, carried by <c>startup_file=layout&lt;N&gt;.txt</c>
        /// (Edge RGB, TKO) or <c>profile=&lt;N&gt;</c> (Advantage 360); both forms are parsed
        /// with the same file-number logic (spec 08 §2).
        /// </summary>
        public int? StartupProfileNumber { get; init; }

        /// <summary>
        /// Raw dual-typed <c>led_mode</c> value: a led file name <c>led&lt;N&gt;.txt</c> on
        /// Edge RGB/TKO, or brightness <c>0</c>-<c>9</c> / <c>P</c> (pitch black) /
        /// <c>B</c> (breathe) on the Freestyle Edge (spec 08 §2). The device's
        /// <c>SettingsCapability.LedMode</c> selects the form; serialization canonicalizes
        /// casing.
        /// </summary>
        public string? LedMode { get; init; }

        /// <summary>Global macro playback speed, 0-9 with 0 = playback disabled (spec 08 §2).</summary>
        public int? MacroSpeed { get; init; }

        /// <summary>
        /// Status-report play speed 0-4 (0 = disabled), carried by <c>status_play_speed</c> or
        /// the Advantage 360 short key <c>status</c> (spec 08 §2).
        /// </summary>
        public int? StatusPlaySpeed { get; init; }

        /// <summary>Whether the v-Drive auto-mounts on startup; <c>v_drive</c>, value <c>auto</c> = true (spec 08 §2).</summary>
        public bool? IsVDriveAutoMountEnabled { get; init; }

        /// <summary>Advantage2 v-Drive auto-open variant; <c>v_drive_open_on_startup</c>, value <c>on</c> = true (spec 08 §2).</summary>
        public bool? IsVDriveOpenOnStartupEnabled { get; init; }

        /// <summary>Game mode — disables the Windows key etc. on the keyboard; <c>game_mode</c> (spec 08 §2).</summary>
        public bool? IsGameModeEnabled { get; init; }

        /// <summary>Advantage 360 program lock — disables onboard programming; <c>lock</c> (spec 08 §2).</summary>
        public bool? IsProgramLockEnabled { get; init; }

        /// <summary>Advantage2 key click sound; <c>key_click_tone</c> (spec 08 §2).</summary>
        public bool? IsKeyClickToneEnabled { get; init; }

        /// <summary>Advantage2 toggle (special action) tone; <c>toggle_tone</c> (spec 08 §2).</summary>
        public bool? IsToggleToneEnabled { get; init; }

        /// <summary>Reserved <c>thumb_mode</c> string, read into state but never written (spec 08 §2).</summary>
        public string? ThumbMode { get; init; }

        /// <summary>Reserved <c>macro_disable</c> flag (<c>on</c> = true), read but never written (spec 08 §2).</summary>
        public bool? IsMacroDisabled { get; init; }

        /// <summary>Reserved <c>power_user</c> flag (<c>true</c> = true), read but never written (spec 08 §2).</summary>
        public bool? IsPowerUser { get; init; }

        /// <summary>Reserved <c>country</c> string, read into state but never written (spec 08 §2).</summary>
        public string? Country { get; init; }

        /// <summary>Reserved <c>program_key_lock</c> flag from factory-shipped files, read but never written (spec 08 §2).</summary>
        public bool? IsProgramKeyLockEnabled { get; init; }

        /// <summary>Reserved <c>profile_sync_mode</c> flag from factory-shipped files, read but never written (spec 08 §2).</summary>
        public bool? IsProfileSyncModeEnabled { get; init; }
    }
}
