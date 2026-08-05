namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Per-family macro and tap-and-hold capability data of the catalog: the limits table of
    /// specs/06-macros.md §6 and specs/04-layout-file-format.md §5.3, the speed/repeat ranges of
    /// specs/06-macros.md §4, the slot and co-trigger model of specs/06-macros.md §1 and §2.1, and
    /// the tap-and-hold data of specs/11-feature-dialogs.md §11.1.
    /// </summary>
    public static partial class DeviceCatalog
    {
        private const int MacroSlotsPerKey = 5;
        private const int LegacyPersistedMacroSlots = 3;
        private const int Gen1MaxCharactersPerMacro = 300;
        private const int Advantage360MaxCharactersPerMacro = 500;
        private const int MaxTotalKeystrokesPerLayout = 7200;
        private const int MaxMacroCountPerLayout = 100;
        private const int MaxCoTriggerSlots = 4;
        private const int TapAndHoldMaxPerLayout = 10;
        private const int TapAndHoldMinDelayMilliseconds = 1;
        private const int TapAndHoldMaxDelayMilliseconds = 999;
        private const int TapAndHoldDefaultDelayMilliseconds = 250;
        private const int Advantage360TapAndHoldDefaultDelayMilliseconds = 150;

        private static ValueRange LegacySpeedRange { get; } = new(Minimum: 0, Maximum: 9, Default: 0);

        private static ValueRange LegacyRepeatRange { get; } = new(Minimum: 0, Maximum: 9, Default: 0);

        private static ValueRange RgbSpeedRange { get; } = new(Minimum: 1, Maximum: 9, Default: 5);

        private static ValueRange RgbRepeatRange { get; } = new(Minimum: 1, Maximum: 9, Default: 1);

        /// <summary>
        /// Savant Elite 2 pedal macros (specs/12-savant-elite.md §1, §4.4, §6): one action or one
        /// macro per input, no slots, no per-macro speed setting — speed is embedded as `speedN`
        /// tokens inside the macro itself. The spec states no count or length limits.
        /// </summary>
        private static MacroCapability CreateSavantElite2Macros()
        {
            return new MacroCapability
            {
                IsSupported = true
            };
        }

        /// <summary>
        /// Advantage2 macros (specs/02-devices.md master table, 06 §1, §2.1, §4, §6): 5 model slots
        /// of which 3 are persisted, up to 3 co-triggers, ~300 characters per macro, speed and
        /// repeat 0-9 defaulting to 0. The spec states no macro count or layout keystroke budget.
        /// </summary>
        private static MacroCapability CreateAdvantage2Macros()
        {
            return new MacroCapability
            {
                IsSupported = true,
                MaxCharactersPerMacro = Gen1MaxCharactersPerMacro,
                SlotsPerKey = MacroSlotsPerKey,
                PersistedSlotsPerKey = LegacyPersistedMacroSlots,
                MaxCoTriggersPerMacro = 3,
                PersistedCoTriggersPerMacro = 3,
                Speed = LegacySpeedRange,
                Repeat = LegacyRepeatRange
            };
        }

        /// <summary>
        /// Freestyle Edge and Freestyle Pro macros (specs/02-devices.md, 06 §1, §2.1, §3, §4, §6):
        /// 24 macros below firmware 1.0.340 and 100 from it, 300 characters per macro, 7200
        /// keystrokes per layout, 5 model slots of which 3 are persisted, 4 co-triggers modelled
        /// but only the first parsed and written, speed and repeat 0-9 defaulting to 0.
        /// </summary>
        private static MacroCapability CreateFreestyleMacros()
        {
            return new MacroCapability
            {
                IsSupported = true,
                MaxMacroCount = 24,
                GatedMaxMacroCount = MaxMacroCountPerLayout,
                MacroCountGateFirmware = new FirmwareVersion(1, 0, 340),
                MaxCharactersPerMacro = Gen1MaxCharactersPerMacro,
                MaxTotalKeystrokes = MaxTotalKeystrokesPerLayout,
                SlotsPerKey = MacroSlotsPerKey,
                PersistedSlotsPerKey = LegacyPersistedMacroSlots,
                MaxCoTriggersPerMacro = MaxCoTriggerSlots,
                PersistedCoTriggersPerMacro = 1,
                Speed = LegacySpeedRange,
                Repeat = LegacyRepeatRange
            };
        }

        /// <summary>
        /// Freestyle Edge RGB and TKO macros (specs/02-devices.md, 06 §1, §2.1, §4, §6): 100 macros,
        /// 300 characters per macro (Gen1), 7200 keystrokes per layout, all 5 slots persisted, 4
        /// co-triggers parsed and written, speed and repeat 1-9 defaulting to 5 and 1.
        /// </summary>
        private static MacroCapability CreateRgbFamilyMacros()
        {
            return new MacroCapability
            {
                IsSupported = true,
                MaxMacroCount = MaxMacroCountPerLayout,
                MaxCharactersPerMacro = Gen1MaxCharactersPerMacro,
                MaxTotalKeystrokes = MaxTotalKeystrokesPerLayout,
                SlotsPerKey = MacroSlotsPerKey,
                PersistedSlotsPerKey = MacroSlotsPerKey,
                MaxCoTriggersPerMacro = MaxCoTriggerSlots,
                PersistedCoTriggersPerMacro = MaxCoTriggerSlots,
                Speed = RgbSpeedRange,
                Repeat = RgbRepeatRange
            };
        }

        /// <summary>
        /// Advantage 360 macros (specs/02-devices.md, 06 §1, §2.2, §4, §6): 100 macros in one flat
        /// per-layout list instead of per-key slots, 500 serialized characters per macro, 7200
        /// keystrokes per layout, 4 co-triggers, speed and repeat 1-9 defaulting to 5 and 1 with
        /// out-of-range values clamped up to the minimum.
        /// </summary>
        private static MacroCapability CreateAdvantage360Macros()
        {
            return new MacroCapability
            {
                IsSupported = true,
                MaxMacroCount = MaxMacroCountPerLayout,
                MaxCharactersPerMacro = Advantage360MaxCharactersPerMacro,
                MaxTotalKeystrokes = MaxTotalKeystrokesPerLayout,
                UsesFlatMacroList = true,
                MaxCoTriggersPerMacro = MaxCoTriggerSlots,
                PersistedCoTriggersPerMacro = MaxCoTriggerSlots,
                Speed = RgbSpeedRange,
                Repeat = RgbRepeatRange,
                ClampsOutOfRangeValues = true
            };
        }

        /// <summary>
        /// Tap-and-hold capability shared by every supporting device (specs/11-feature-dialogs.md
        /// §11.1, 04 §5.3): 10 keys per layout, delay 1-999 ms.
        /// </summary>
        /// <param name="minimumFirmware">Firmware gate from 11 §11.1; null where the spec lists none (TKO, Adv360).</param>
        /// <param name="defaultDelayMilliseconds">Delay applied to a key with no delay yet (250; Adv360: 150).</param>
        private static TapAndHoldCapability CreateTapAndHold(
            FirmwareVersion? minimumFirmware = null,
            int defaultDelayMilliseconds = TapAndHoldDefaultDelayMilliseconds)
        {
            return new TapAndHoldCapability
            {
                IsSupported = true,
                MaxPerLayout = TapAndHoldMaxPerLayout,
                DelayMilliseconds = new ValueRange(
                    Minimum: TapAndHoldMinDelayMilliseconds,
                    Maximum: TapAndHoldMaxDelayMilliseconds,
                    Default: defaultDelayMilliseconds),
                MinimumFirmware = minimumFirmware
            };
        }
    }
}
