using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The macro panel's internal one-key clipboard (specs/10-apps-and-ui.md: "Copy/Paste transfer
    /// a macro between keys via an internal clipboard").
    /// <para>
    /// It holds a <b>snapshot</b>, never the source key: between a Copy and a Paste the user can
    /// delete the copied macro, or run Reset Key / Reset Layer / Reset Layout over it, and a live
    /// reference would then paste the source's <em>current</em> — emptied — slots and wipe the
    /// target. Both the copy and the paste deep-clone (05 §1.3: assignment paths work on copies,
    /// never shared instances), so pasting the same clipboard onto two keys gives each its own
    /// macros.
    /// </para>
    /// </summary>
    public sealed class MacroClipboard
    {
        /// <summary>Whether anything has been copied yet.</summary>
        public bool HasContent => _slots is not null;

        /// <summary>How many macros a paste would put on the target key.</summary>
        public int MacroCount { get; private set; }

        private Macro?[]? _slots;
        private int _activeSlot = Macro.MinMacroIndex;

        /// <summary>Snapshots every macro slot of <paramref name="key"/>, and its active slot.</summary>
        public void Copy(KeyboardKey key)
        {
            ArgumentNullException.ThrowIfNull(key);

            var slots = new Macro?[KeyboardKey.MacroSlotCount];
            var count = 0;

            for (var slot = Macro.MinMacroIndex; slot <= KeyboardKey.MacroSlotCount; slot++)
            {
                var macro = key.GetMacro(slot)?.Clone();

                slots[slot - 1] = macro;

                if (macro is not null)
                {
                    count++;
                }
            }

            _slots = slots;
            _activeSlot = key.ActiveMacroIndex;

            MacroCount = count;
        }

        /// <summary>
        /// Replaces every macro slot of <paramref name="key"/> with a fresh clone of the snapshot,
        /// exactly as <see cref="KeyboardKey.CopyFrom"/> with
        /// <see cref="KeyCopyScopes.Macros"/> would — the target ends up with the source's macros
        /// and none of its own. False when nothing has been copied.
        /// </summary>
        public bool PasteInto(KeyboardKey key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (_slots is not { } slots)
            {
                return false;
            }

            for (var slot = Macro.MinMacroIndex; slot <= KeyboardKey.MacroSlotCount; slot++)
            {
                key.SetMacro(slot, slots[slot - 1]?.Clone());
            }

            key.ActiveMacroIndex = _activeSlot;

            return true;
        }
    }
}
