namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One place a macro sits in a layout: the layer, the trigger key it fires from, and the macro
    /// slot it occupies. A site is a <em>location</em>, not an identity — two sites holding macros
    /// that look alike hold two independent copies (06 §1), and nothing groups them
    /// (<see cref="MacroSites"/>).
    /// <para>
    /// <see cref="TriggerKeyCode"/> is the code of <see cref="KeyboardKey.TriggerKey"/>, never of
    /// the position key: 05 §1.3 makes <c>fn1s</c> and <c>keyt</c> trigger as their original action,
    /// and macro lookup follows trigger identity (see <c>keyboard-model.md</c> invariant 4).
    /// </para>
    /// <para>
    /// <see cref="SlotNumber"/> is 1..5 on the slot-based families and <b>0</b> on the Advantage360,
    /// whose macros live in <see cref="KeyboardLayout.Macros"/> instead (06 §1). Which of the two a
    /// site is comes from <see cref="Key"/> being null, never from a device id.
    /// </para>
    /// </summary>
    public sealed record MacroSite
    {
        /// <summary>Index of the layer the macro fires on (05 §1.4).</summary>
        public required int LayerIndex { get; init; }

        /// <summary>Code of the key's <see cref="KeyboardKey.TriggerKey"/> (05 §1.3; 06 §1).</summary>
        public required int TriggerKeyCode { get; init; }

        /// <summary>Macro slot 1..5, or <b>0</b> for a macro in the Gen2 flat list (06 §1).</summary>
        public required int SlotNumber { get; init; }

        /// <summary>The macro instance sitting here. Reference identity — this is the object to edit.</summary>
        public required Macro Macro { get; init; }

        /// <summary>The key position holding the slot; <b>null</b> for a flat-list site.</summary>
        public KeyboardKey? Key { get; init; }

        /// <summary>Whether this site is an entry of <see cref="KeyboardLayout.Macros"/> (06 §1).</summary>
        public bool IsInFlatList => Key is null;
    }
}
