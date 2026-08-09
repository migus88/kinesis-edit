namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The one thing a key position is being told to do — the key inspector's mode tabs
    /// (docs/design/handoff.md § "Key inspector panels", mockups <c>1e</c>/<c>2a</c>/<c>2h</c>).
    /// <para>
    /// <b>These are one slot, not four independent settings.</b> Core says so:
    /// <c>KeyboardKey.SetTapAndHold</c> clears the position's remap and its multi-modifier, and
    /// <c>Remap</c> clears the tap-and-hold — "one rule per position"
    /// (docs/app/keyboard-model.md). The inspector therefore warns before switching rather than
    /// after (mockup <c>2h</c>), and this enum is what it warns <em>about</em>.
    /// </para>
    /// <para>
    /// <b><see cref="MultiModifier"/> is a mode, not a tab this board draws.</b> The Freestyle
    /// Edge RGB has no multi-modifier firmware, so the tab is <b>not rendered at all</b> rather
    /// than rendered dim — the capability law of docs/design/README.md, and what
    /// docs/design/handoff.md:137 asks for where mockup <c>1e</c> instead draws the tab beside an
    /// "Advantage 360 only" note. <c>KeyboardKey.SupportsMultiModifiers</c> is the flag.
    /// </para>
    /// </summary>
    public enum KeyInspectorMode
    {
        /// <summary>The position sends a different key (specs/05-key-model.md §1.3).</summary>
        Remap = 0,

        /// <summary>A quick press sends one key, a held press another (§5.6).</summary>
        TapAndHold = 1,

        /// <summary>
        /// The position fires a macro (specs/06-macros.md). The rail's Macro panel is <b>the</b>
        /// macro editor since issue #140 — the slot selector, the Trigger strip, the step composer
        /// and the budget meters are all here, and there is no second surface for any of it.
        /// </summary>
        Macro = 2,

        /// <summary>
        /// The position sends one of the firmware's eleven fixed modifier combinations
        /// (<c>KeyboardKey.MultiModifiers</c>). Drawn only where the device has it — see the
        /// type's remarks.
        /// </summary>
        MultiModifier = 3
    }
}
