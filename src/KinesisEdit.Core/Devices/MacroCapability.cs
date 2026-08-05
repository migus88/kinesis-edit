namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Macro support and limits as stated in specs/02-devices.md (master table and per-device
    /// deep dives). Limits the spec does not state for a device are null. Firmware-gated limits
    /// are carried as data only; gate evaluation belongs to specs/09-firmware.md.
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

        /// <summary>Per-macro character/keystroke budget (FS Edge/Pro and Adv2: ~300; Adv360: 500).</summary>
        public int? MaxCharactersPerMacro { get; init; }

        /// <summary>Total keystroke budget per layout (7200 on FS Edge/Pro, RGB, TKO, Adv360).</summary>
        public int? MaxTotalKeystrokes { get; init; }

        /// <summary>Advantage2 only: macros per trigger key (3).</summary>
        public int? MacrosPerTriggerKey { get; init; }

        /// <summary>Advantage2 only: co-trigger modifiers per macro (3).</summary>
        public int? MaxCoTriggersPerMacro { get; init; }
    }
}
