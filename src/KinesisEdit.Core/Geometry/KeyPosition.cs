namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// One physical key position inside a layer, per specs/05-key-model.md §1.3 and §4.
    /// Immutable: geometry describes the factory shape of a device; remaps and macros
    /// are stored elsewhere, never by replacing positions (spec 05 §7.2).
    /// </summary>
    public sealed record KeyPosition
    {
        /// <summary>
        /// Ordinal of the position inside its layer — the GUI button id and the
        /// file-ordering key. Dense (0..N-1) and unique within a layer; identical
        /// positions share the same index across a device's layers (spec 05 §7.4).
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// File token of the layer's factory-default action for this position, in the
        /// device family's dialect (spec 05 §3). Empty for the Advantage2 Keypad and
        /// Program buttons, whose tokens are never written to files (spec 05 §3.9/§3.10).
        /// </summary>
        public string DefaultToken { get; }

        /// <summary>
        /// File token naming the physical position where it differs from
        /// <see cref="DefaultToken"/>, or null when they are identical. Only the
        /// Advantage360 layers use distinct position tokens, e.g. the Base layer's
        /// keypad toggle "keyt" at position "kp" (spec 05 §1.3, §4.7).
        /// </summary>
        public string? PositionToken { get; }

        /// <summary>
        /// False for locked positions that can never be remapped: the Advantage2
        /// Keypad/Program buttons and the SmartSet key on TKO and Advantage360
        /// (spec 05 §5.3, the "†" marker in §4).
        /// </summary>
        public bool CanEdit { get; }

        /// <summary>
        /// False for positions that cannot carry a macro: the physical modifier
        /// positions on FS Edge/RGB/Pro, TKO, and Advantage2 (the "*" marker in
        /// spec 05 §4) and the locked positions. The TKO "fnshf" position and all
        /// Advantage360 modifiers allow macros (spec 05 §5.3).
        /// </summary>
        public bool CanAssignMacro { get; }

        /// <summary>
        /// Default-action token used instead of <see cref="DefaultToken"/> when the
        /// layout is hosted by the combined Kinesis master application. Only the
        /// Advantage2 top-layer foot-pedal positions 86-88 carry this: "tab"/"kpshft"/
        /// "kpenter" in the master app versus the pedal tokens standalone (spec 05 §4.5).
        /// </summary>
        public string? MasterAppDefaultToken { get; }

        public KeyPosition(
            int index,
            string defaultToken,
            string? positionToken = null,
            bool canEdit = true,
            bool canAssignMacro = true,
            string? masterAppDefaultToken = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentNullException.ThrowIfNull(defaultToken);

            Index = index;
            DefaultToken = defaultToken;
            PositionToken = positionToken;
            CanEdit = canEdit;
            CanAssignMacro = canAssignMacro;
            MasterAppDefaultToken = masterAppDefaultToken;
        }
    }
}
