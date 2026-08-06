using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the macro panel currently has open: the macro instance plus where it lives — the key
    /// that owns it, the layer that key is on, and the slot it sits in (0 on the flat-list
    /// families, specs/06-macros.md §1).
    /// <para>
    /// It is one object rather than four fields because the panel needs a <b>single</b> answer to
    /// "what am I editing?". The board's selection seeds it, but the profile's macro list can move
    /// it to a macro on another key or another layer, and everything the panel then shows or
    /// writes — the trigger caption, the slot strip, Record, Clear, the co-triggers, Delete,
    /// Copy/Paste and Assign's layer stamp — must follow the macro and not the board, or the panel
    /// names one key while it edits another.
    /// </para>
    /// </summary>
    public sealed class MacroEditTarget
    {
        /// <summary>Nothing is open: no macro support, no selection, or a position that refuses macros.</summary>
        public static MacroEditTarget None { get; } = new(null, null, null, 0);

        /// <summary>The macro under edit — a slot's macro, a list row's macro, or a fresh draft.</summary>
        public Macro? Macro { get; }

        /// <summary>The key the macro belongs to, or null when nothing is open.</summary>
        public KeyboardKey? Key { get; }

        /// <summary>The layer <see cref="Key"/> sits on; what Assign stamps onto the macro (04 §4.2).</summary>
        public KeyboardLayer? Layer { get; }

        /// <summary>The key slot the macro occupies, or 0 on the flat-list families (06 §1).</summary>
        public int Slot { get; }

        /// <summary>Whether a macro is open at all.</summary>
        public bool IsOpen => Macro is not null;

        /// <summary>Creates a target over <paramref name="macro"/>.</summary>
        public MacroEditTarget(Macro? macro, KeyboardKey? key, KeyboardLayer? layer, int slot)
        {
            Macro = macro;
            Key = key;
            Layer = layer;
            Slot = slot;
        }
    }
}
