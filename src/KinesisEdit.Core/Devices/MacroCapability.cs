namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Macro support and limits as stated in specs/02-devices.md (master table and per-device
    /// deep dives), specs/04-layout-file-format.md §5.3 and specs/06-macros.md §1, §2.1, §4, §6.
    /// Limits the spec does not state for a device are null. Firmware-gated limits are carried as
    /// data only; gate evaluation belongs to specs/09-firmware.md.
    /// </summary>
    public sealed record MacroCapability
    {
        /// <summary>A capability for devices without macro support.</summary>
        public static MacroCapability None { get; } = new();

        /// <summary>Whether the device supports macros at all.</summary>
        public bool IsSupported { get; init; }

        /// <summary>Baseline macro count (FS Edge/Pro: 24 below the firmware gate; RGB/TKO/Adv360: 100).</summary>
        public int? MaxMacroCount { get; init; }

        /// <summary>Raised macro count once firmware reaches <see cref="MacroCountGateFirmware"/> (FS Edge/Pro: 100).</summary>
        public int? GatedMaxMacroCount { get; init; }

        /// <summary>Minimum firmware for <see cref="GatedMaxMacroCount"/> (FS Edge/Pro: 1.0.340). Data only, not evaluated here.</summary>
        public FirmwareVersion? MacroCountGateFirmware { get; init; }

        /// <summary>
        /// Per-macro budget (06 §6), measured differently per family: Gen1 and Adv2 cap 300
        /// keystrokes, counted the way 04 §5.3 counts them everywhere else — 1 per keystroke plus 2
        /// per attached modifier; the Adv360 caps 500 characters of serialized *macro* text, i.e.
        /// the value side of the 06 §3 line, without the trigger, co-triggers or {sN}/{xN} markers.
        /// </summary>
        public int? MaxCharactersPerMacro { get; init; }

        /// <summary>
        /// Total keystroke budget per layout (7200 on FS Edge/Pro, RGB, TKO, Adv360; 06 §6). Counted
        /// as 1 per keystroke plus 2 per attached modifier. The spec states none for Adv2 and SE2.
        /// </summary>
        public int? MaxTotalKeystrokes { get; init; }

        /// <summary>
        /// Macro slots the model holds per trigger key (5 on every slot-based family; 06 §1,
        /// 04 §5.3). Null where macros are not stored in per-key slots (Adv360 flat list, SE2 pedal).
        /// </summary>
        public int? SlotsPerKey { get; init; }

        /// <summary>
        /// Slots the dialect actually writes to file: Adv2 and FS Edge/Pro persist only slots 1-3,
        /// RGB and TKO persist all 5 (06 §1, 04 §5.3). Null where <see cref="SlotsPerKey"/> is null.
        /// </summary>
        public int? PersistedSlotsPerKey { get; init; }

        /// <summary>
        /// Whether macros live in one flat per-layout list tagged with trigger key and layer
        /// (Adv360/Gen2) instead of per-key slots (06 §1).
        /// </summary>
        public bool UsesFlatMacroList { get; init; }

        /// <summary>Co-trigger modifiers a macro may carry (Adv2: 3; FS Edge/Pro, RGB, TKO, Adv360: 4; 06 §2.1, §5).</summary>
        public int? MaxCoTriggersPerMacro { get; init; }

        /// <summary>
        /// Co-triggers the dialect's parser keeps and its serializer writes: the old FS Edge/Pro
        /// parser keeps only 1, Adv2 up to 3, the RGB family and Adv360 all 4 (06 §2.1, §3).
        /// </summary>
        public int? PersistedCoTriggersPerMacro { get; init; }

        /// <summary>
        /// Playback-speed range and default (06 §4): Adv2 and FS Edge/Pro 0-9 default 0 (0 = the
        /// keyboard's global speed), RGB family and Adv360 1-9 default 5. Null where the device has
        /// no per-macro speed setting (SE2 embeds speed tokens inside the macro instead; 12 §5).
        /// </summary>
        public ValueRange? Speed { get; init; }

        /// <summary>
        /// Repeat/multiplay range and default (06 §4): Adv2 and FS Edge/Pro 0-9 default 0, RGB
        /// family and Adv360 1-9 default 1. The Adv2 serializer writes no repeat token (06 §3).
        /// </summary>
        public ValueRange? Repeat { get; init; }

        /// <summary>
        /// Whether the parser clamps out-of-range speed/repeat values up to the minimum instead of
        /// discarding them (Adv360 only; 06 §2.2, §4).
        /// </summary>
        public bool ClampsOutOfRangeValues { get; init; }
    }
}
