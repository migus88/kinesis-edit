using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What one position's macros looked like the moment the key inspector was pointed at it — the
    /// baseline the Macro panel's <c>Revert key</c> restores (issue #122, AC 1).
    ///
    /// <para><b>Why it exists at all.</b> Core is a plain mutable model and every macro edit writes
    /// straight into it (<c>EnsureMacro</c>, <c>ReceiveKeystroke</c>, the step editor). Nothing
    /// anywhere kept a "before", so the rail's <c>Revert key</c> — which runs the editor's
    /// <c>ClearRemap()</c> — was a no-op on this panel: it touches the remap and never a macro.</para>
    ///
    /// <para><b>It is a deep copy, and it is copied again on the way back.</b> Capture clones every
    /// macro (<see cref="Macro.Clone"/>) so a later edit to the live macro cannot reach into the
    /// baseline, and <see cref="RestoreTo"/> clones once more so the baseline is never handed to the
    /// model. That is what makes reverting twice mean the same as reverting once — the snapshot is
    /// read on restore, never consumed.</para>
    ///
    /// <para><b>Both macro stores are covered.</b> On the slot families (05 §1.3) the five
    /// <c>MacroN</c> slots and the active slot are the state; on the Advantage360 the macros live in
    /// <see cref="KeyboardLayout.Macros"/> and are identified by layer + trigger key (06 §1), so the
    /// snapshot holds that list instead. Restoring goes through the model's own write paths —
    /// <see cref="KeyboardKey.SetMacro"/>/<see cref="KeyboardKey.ClearMacros"/> and
    /// <see cref="KeyboardLayout.AddMacro"/>/<see cref="KeyboardLayout.RemoveMacro"/> — never
    /// through the read-only slot properties.</para>
    /// </summary>
    internal sealed class MacroKeySnapshot
    {
        /// <summary>The layer the position sat on, which is half of a flat-list macro's identity.</summary>
        public int LayerIndex { get; }

        private readonly Macro?[] _slots;
        private readonly IReadOnlyList<Macro> _flatList;
        private readonly int _activeSlot;

        private MacroKeySnapshot(Macro?[] slots, IReadOnlyList<Macro> flatList, int activeSlot, int layerIndex)
        {
            _slots = slots;
            _flatList = flatList;
            _activeSlot = activeSlot;

            LayerIndex = layerIndex;
        }

        /// <summary>
        /// Reads the position's macro state. A <b>read</b>, and only a read: the panel takes it from
        /// <c>Refresh</c>, which the contract forbids to write (<see cref="KeyInspectorPanelViewModel"/>).
        /// </summary>
        /// <param name="key">The position the inspector is about.</param>
        /// <param name="layout">The open profile's layout, or null when nothing is loaded.</param>
        /// <param name="layerIndex">
        /// The layer the position sits on, or <see cref="Macro.UnassignedIndex"/> when there is none
        /// — the same convention the panel's own writes stamp onto a new macro (04 §4.2).
        /// </param>
        public static MacroKeySnapshot Capture(KeyboardKey key, KeyboardLayout? layout, int layerIndex)
        {
            ArgumentNullException.ThrowIfNull(key);

            var slots = new Macro?[Macro.MaxMacroIndex];
            var flatList = new List<Macro>();

            if (key.UsesMacroSlots)
            {
                for (var slot = Macro.MinMacroIndex; slot <= Macro.MaxMacroIndex; slot++)
                {
                    slots[slot - 1] = key.GetMacro(slot)?.Clone();
                }
            }
            else if (layout is not null)
            {
                foreach (var macro in layout.FindMacros(layerIndex, key.TriggerKey.Code))
                {
                    flatList.Add(macro.Clone());
                }
            }

            return new MacroKeySnapshot(slots, flatList, key.ActiveMacroIndex, layerIndex);
        }

        /// <summary>
        /// Puts the snapshotted state back. A position that carried nothing when it was snapshotted
        /// is left carrying nothing, which is the only honest answer to "revert a macro the user
        /// created after selecting the key".
        /// </summary>
        public void RestoreTo(KeyboardKey key, KeyboardLayout? layout)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (key.UsesMacroSlots)
            {
                RestoreSlots(key);

                return;
            }

            if (layout is not null)
            {
                RestoreFlatList(key, layout);
            }
        }

        private void RestoreSlots(KeyboardKey key)
        {
            key.ClearMacros();

            for (var slot = Macro.MinMacroIndex; slot <= Macro.MaxMacroIndex; slot++)
            {
                if (_slots[slot - 1] is { } macro)
                {
                    // The tolerant path on purpose: it restores exactly the slot the macro was read
                    // out of, where AssignMacro would shuffle a gap-carrying set into the first free
                    // slots and change what the file says.
                    key.SetMacro(slot, macro.Clone());
                }
            }

            key.ActiveMacroIndex = _activeSlot;
        }

        private void RestoreFlatList(KeyboardKey key, KeyboardLayout layout)
        {
            // FindMacros hands back a fresh list, so removing from the layout while walking it is
            // safe — and identity is by reference, so only this position's macros go.
            foreach (var macro in layout.FindMacros(LayerIndex, key.TriggerKey.Code))
            {
                layout.RemoveMacro(macro);
            }

            foreach (var macro in _flatList)
            {
                layout.AddMacro(macro.Clone());
            }
        }
    }
}
