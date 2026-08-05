namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// A limit or rule the runtime model may breach. The model never enforces these — the tolerant
    /// load paths store what a field file carries and <see cref="KeyboardLayout.Validate"/> reports
    /// what is out of bounds, because files written by older firmware may exceed the limits of
    /// today's device catalog (limits: specs/04-layout-file-format.md §5.3,
    /// specs/06-macros.md §6, specs/11-feature-dialogs.md §11.1; the read-tolerantly rule of the
    /// project's CLAUDE.md).
    /// </summary>
    public enum ModelViolationKind
    {
        /// <summary>No violation.</summary>
        None = 0,

        /// <summary>More macros than <see cref="Devices.MacroCapability.MaxMacroCount"/> (06 §6, 04 §5.3).</summary>
        MacroCountExceeded = 1,

        /// <summary>
        /// A macro longer than <see cref="Devices.MacroCapability.MaxCharactersPerMacro"/> — measured
        /// as the serialized text length on the Advantage360 and as the keystroke count elsewhere (06 §6).
        /// </summary>
        MacroLengthExceeded = 2,

        /// <summary>
        /// The layout's keystroke budget (7200) is exceeded, counted as 1 per keystroke plus 2 per
        /// attached modifier (04 §5.3, 06 §6).
        /// </summary>
        MacroKeystrokeBudgetExceeded = 3,

        /// <summary>More co-triggers than <see cref="Devices.MacroCapability.MaxCoTriggersPerMacro"/> (06 §2.1, §5).</summary>
        MacroCoTriggerLimitExceeded = 4,

        /// <summary>Two macros share the same trigger and co-trigger set — the collision rule of 06 §5.</summary>
        MacroTriggerCollision = 5,

        /// <summary>A macro sits on a position that does not accept macros (05 §5.3, 06 §2.2).</summary>
        MacroOnRestrictedKey = 6,

        /// <summary>Playback speed outside <see cref="Devices.MacroCapability.Speed"/> (06 §4).</summary>
        MacroSpeedOutOfRange = 7,

        /// <summary>Repeat factor outside <see cref="Devices.MacroCapability.Repeat"/> (06 §4).</summary>
        MacroRepeatOutOfRange = 8,

        /// <summary>A macro with no keystrokes — a Gen2 validation failure (06 §5).</summary>
        EmptyMacro = 9,

        /// <summary>A macro with a hanging key-down that never gets a matching key-up (Gen2; 06 §5, §6).</summary>
        UnbalancedMacroKeyDirection = 10,

        /// <summary>
        /// A Gen2 macro triggered by <c>fn1s</c> or <c>keyt</c> without the co-trigger those
        /// reserved triggers require (06 §5).
        /// </summary>
        ReservedMacroTriggerWithoutCoTrigger = 11,

        /// <summary>More tap-and-hold keys than <see cref="Devices.TapAndHoldCapability.MaxPerLayout"/> (11 §11.1, 04 §5.3).</summary>
        TapAndHoldCountExceeded = 12,

        /// <summary>A hold threshold outside <see cref="Devices.TapAndHoldCapability.DelayMilliseconds"/> (11 §11.1).</summary>
        TapAndHoldDelayOutOfRange = 13,

        /// <summary>A tap-and-hold assignment on a device that does not support the feature (11 §11.1).</summary>
        TapAndHoldNotSupported = 14,

        /// <summary>
        /// A remap, tap-and-hold assignment, or multi-modifier code on a position that can never be
        /// remapped (05 §5.3, the "†" marker of §4). Such a position must never produce a layout
        /// line, so the state is reported rather than written.
        /// </summary>
        EditOnLockedKey = 15,

        /// <summary>
        /// A multi-modifier code on a device that does not offer the feature
        /// (<see cref="Devices.DeviceDefinition.SupportsMultiModifiers"/>; 11 §11.2 "Advantage360
        /// only", "Adv360 format only"; 05 §5.7).
        /// </summary>
        MultiModifiersNotSupported = 16,

        /// <summary>
        /// A Gen2 flat-list macro that names no trigger key or no layer (both still
        /// <see cref="Macro.UnassignedIndex"/>). 06 §1 tags every flat-list macro with a trigger key
        /// and a layer, and 06 §5's Gen2 validation requires that "a layer is selected".
        /// </summary>
        UnboundMacro = 17,

        /// <summary>
        /// A position holding more than one of the three single-key rules of 04 §2. 04 §4.3 writes
        /// at most one line per position — "tap-and-hold line, else multi-modifier line, else remap
        /// line" — so the lower-precedence rules would be lost on the next save.
        /// </summary>
        ConflictingSingleKeyRules = 18,

        /// <summary>
        /// A macro sitting in a per-key slot on a device whose macros live in the flat list
        /// (<see cref="Devices.MacroCapability.UsesFlatMacroList"/>; 06 §1). 04 §4.3 writes per-key
        /// macro lines on non-Gen2 devices only, so such a macro would be lost on save.
        /// </summary>
        MacroOutsideFlatList = 19
    }
}
