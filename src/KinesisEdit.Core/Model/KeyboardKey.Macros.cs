using System.Collections.ObjectModel;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The macro-slot half of a key position: the five slots every <c>TKBKey</c> owns, the active
    /// slot, and the two assignment paths (specs/05-key-model.md §1.3, specs/06-macros.md §1, §2.2).
    /// The Advantage360 keeps its macros in <see cref="KeyboardLayout.Macros"/> instead, so these
    /// slots have no meaning there (<see cref="MacroCapability.UsesFlatMacroList"/>,
    /// <see cref="UsesMacroSlots"/>).
    /// <para>
    /// The <c>MacroN</c> properties are read-only on purpose. A property setter reads like the
    /// ordinary editor path, but writing a slot has two of them — guarded
    /// <see cref="AssignMacro"/> and tolerant <see cref="SetMacro"/> — and picking silently would
    /// invert that convention. Callers name the one they mean.
    /// </para>
    /// </summary>
    public sealed partial class KeyboardKey
    {
        /// <summary>Macro slot 1 (§1.3 <c>Macro1</c>); write it with <see cref="AssignMacro"/> or <see cref="SetMacro"/>.</summary>
        public Macro? Macro1 => GetMacro(1);

        /// <summary>Macro slot 2 (§1.3 <c>Macro2</c>).</summary>
        public Macro? Macro2 => GetMacro(2);

        /// <summary>Macro slot 3 (§1.3 <c>Macro3</c>) — the last slot the Advantage2/Freestyle dialects persist (06 §1).</summary>
        public Macro? Macro3 => GetMacro(3);

        /// <summary>Macro slot 4 (§1.3 <c>Macro4</c>).</summary>
        public Macro? Macro4 => GetMacro(4);

        /// <summary>Macro slot 5 (§1.3 <c>Macro5</c>).</summary>
        public Macro? Macro5 => GetMacro(5);

        /// <summary>
        /// Whether this device stores macros in the five per-key slots at all (06 §1). False on the
        /// Advantage360, whose macros live in <see cref="KeyboardLayout.Macros"/>: 04 §4.2 appends
        /// every Gen2 macro to that flat list and 04 §4.3 serializes per-key macro lines on non-Gen2
        /// devices only, so a macro left in a slot there would never be written.
        /// </summary>
        public bool UsesMacroSlots { get; }

        /// <summary>
        /// All five macro slots in slot order; empty slots are null (§1.3). A read-only view over
        /// the backing array, so writes always go through <see cref="SetMacro"/> and its slot
        /// stamping cannot be bypassed.
        /// </summary>
        public IReadOnlyList<Macro?> Macros => _macrosView;

        /// <summary>Slot number 1..5 currently being edited; 1 by default (§1.3 <c>ActiveMacro</c>).</summary>
        public int ActiveMacroIndex
        {
            get => _activeMacroIndex;
            set
            {
                ThrowIfSlotOutOfRange(value);

                _activeMacroIndex = value;
            }
        }

        /// <summary>The macro in <see cref="ActiveMacroIndex"/>, or null when that slot is empty (§1.3).</summary>
        public Macro? ActiveMacro => _macros[_activeMacroIndex - 1];

        /// <summary>Whether at least one macro is assigned (§1.3 <c>IsMacro</c>).</summary>
        public bool IsMacro => MacroCount > 0;

        /// <summary>Number of populated macro slots (§1.3).</summary>
        public int MacroCount
        {
            get
            {
                var count = 0;

                foreach (var macro in _macros)
                {
                    if (macro is not null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private int _activeMacroIndex = 1;

        private readonly Macro?[] _macros = new Macro?[MacroSlotCount];

        private readonly ReadOnlyCollection<Macro?> _macrosView;

        /// <summary>Returns the macro in slot <paramref name="slot"/> (1..5), or null (§1.3).</summary>
        public Macro? GetMacro(int slot)
        {
            ThrowIfSlotOutOfRange(slot);

            return _macros[slot - 1];
        }

        /// <summary>
        /// Puts <paramref name="macro"/> into slot <paramref name="slot"/> (1..5), stamping its
        /// <see cref="Macro.MacroIndex"/>, or empties the slot with null (§1.3). The tolerant load
        /// path: it checks neither <see cref="CanAssignMacro"/> nor <see cref="UsesMacroSlots"/>, so
        /// a field file that carries a macro on a restricted position or on a flat-list device still
        /// loads and <see cref="KeyboardLayout.Validate"/> reports
        /// <see cref="ModelViolationKind.MacroOnRestrictedKey"/> /
        /// <see cref="ModelViolationKind.MacroOutsideFlatList"/> (06 §2.2, §1; 05 §5.3).
        /// </summary>
        public void SetMacro(int slot, Macro? macro)
        {
            ThrowIfSlotOutOfRange(slot);

            if (macro is not null)
            {
                macro.MacroIndex = slot;
            }

            _macros[slot - 1] = macro;
        }

        /// <summary>
        /// The guarded assignment path: places <paramref name="macro"/> in the first empty slot and
        /// returns that slot number (06 §2.2). Returns 0 when the position does not accept macros
        /// (06 §2.2: "assignment requires the key to accept macros"), when the device has no per-key
        /// slots (<see cref="UsesMacroSlots"/>) — stamping a slot number on a flat-list macro would
        /// corrupt its <see cref="Macro.MacroIndex"/>, which is 0 there — or when all five slots
        /// are full.
        /// </summary>
        public int AssignMacro(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            if (!CanAssignMacro || !UsesMacroSlots)
            {
                return 0;
            }

            for (var slot = 1; slot <= MacroSlotCount; slot++)
            {
                if (_macros[slot - 1] is not null)
                {
                    continue;
                }

                SetMacro(slot, macro);

                return slot;
            }

            return 0;
        }

        /// <summary>Empties all five macro slots and returns the active slot to 1 (§1.3).</summary>
        public void ClearMacros()
        {
            Array.Clear(_macros);
            _activeMacroIndex = 1;
        }

        private void CopyMacrosFrom(KeyboardKey other)
        {
            for (var slot = 1; slot <= MacroSlotCount; slot++)
            {
                SetMacro(slot, other.GetMacro(slot)?.Clone());
            }

            _activeMacroIndex = other._activeMacroIndex;
        }

        private static void ThrowIfSlotOutOfRange(int slot)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(slot, Macro.MinMacroIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(slot, Macro.MaxMacroIndex);
        }
    }
}
