using System.Globalization;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro panel's <b>slot selector</b> (issue #137): one dot per slot the dialect persists,
    /// filled for the slots that hold a macro, and a dropdown naming the slot under edit.
    ///
    /// <para><b>Why the panel needs one at all.</b> A key holds up to five macros told apart by
    /// their co-triggers (06 §1), and until now the only way to reach slots 2-5 was the Macros tab's
    /// slot cards. The rail could edit exactly one of them, so "the Macros tab is a library, not the
    /// editor" was true of everything except the one thing only the tab could do.</para>
    ///
    /// <para><b>What the dialect writes, never what the model holds</b>
    /// (<see cref="Core.Devices.MacroCapability.PersistedSlotsPerKey"/>: 3 on the Advantage2 and
    /// Freestyle Edge/Pro, 5 on the RGB family). A macro put in slot 4 of a Freestyle Edge is gone
    /// at the very next save, so the strip never offers it — the same rule the tab's cards follow,
    /// and <see cref="KeyboardEditorViewModel"/>'s invariant 14.</para>
    ///
    /// <para><b>Selecting a slot does not dirty the session.</b>
    /// <see cref="KeyboardKey.ActiveMacroIndex"/> is an in-memory field that is never serialized
    /// (05 §1.3), so the selection re-reads the panel and deliberately does <em>not</em> reach the
    /// editor's funnel through <c>Assigned</c>. An unsaved-changes prompt for a choice no save could
    /// persist would be a lie — the Macros tab's <c>Make active</c> already gets this right and this
    /// matches it.</para>
    ///
    /// <para><b>There is no <c>ACTIVE</c> badge and no <c>Make active</c>.</b> The dropdown is a
    /// pure <em>editing</em> selector: every populated slot is live on the keyboard, and what tells
    /// them apart is their co-triggers — which is what the Trigger strip beside it shows. That is
    /// also why <see cref="MacroSlotViewModel"/> is <b>not</b> reused here: it carries the tab's
    /// badge and its make-active action, both of which this panel refuses.</para>
    ///
    /// <para><b>Absent, never disabled.</b> A flat-list board (<c>UsesFlatMacroList</c>, the
    /// Advantage 360) and a position that refuses macros (05 §5.3) render no selector at all.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>The section label over the slot strip. This app's wording.</summary>
        public const string SlotSectionLabel = "SLOT";

        /// <summary>
        /// Whether the slot strip is drawn at all: a slot-based device, and a position that can
        /// carry a macro. A flat-list board and a macro-refusing position get <b>nothing</b> here
        /// rather than a dead control (docs/design/README.md's capability law).
        /// </summary>
        public bool HasSlotSelector => _usesMacroSlots && IsAvailable;

        /// <summary>
        /// One entry per <b>persisted</b> slot, in slot order. The same list backs the dots and the
        /// dropdown, so the two can never disagree about which slots exist or which are occupied.
        /// Rebuilt whole on every read, exactly as the name options are.
        /// </summary>
        public IReadOnlyList<MacroSlotOption> SlotOptions
        {
            get => _slotOptions;
            private set => SetProperty(ref _slotOptions, value);
        }

        /// <summary>
        /// The slot the panel is editing. Setting it writes <see cref="KeyboardKey.ActiveMacroIndex"/>
        /// and re-reads the panel — and writes nothing else, so the session stays clean (see the
        /// type's remarks). Selecting an <em>empty</em> slot yields an empty step list with
        /// recording live; no macro is created until a keystroke lands.
        /// </summary>
        public MacroSlotOption? SelectedSlot
        {
            get => _selectedSlot;
            set => SelectSlot(value);
        }

        private IReadOnlyList<MacroSlotOption> _slotOptions = [];
        private MacroSlotOption? _selectedSlot;

        /// <summary>
        /// Whether the user has named the slot under edit for <em>this</em> position. It is what
        /// stops <see cref="ReadMacro"/> jumping off an explicitly chosen empty slot onto the first
        /// populated one — the fallback exists so a cap that carries a macro opens on it, and
        /// "the user asked for slot 3" is the one case where it must not fire. Reset whenever the
        /// selection moves, because the choice belongs to the position it was made on.
        /// </summary>
        private bool _hasChosenSlot;

        /// <summary>Drops the slot choice; called when the rail moves to another position.</summary>
        private void ResetSlotChoice()
        {
            _hasChosenSlot = false;
        }

        /// <summary>
        /// Rebuilds the strip off the model. It writes nothing: which slots exist is a device fact
        /// and which are occupied is a fact about the key.
        /// </summary>
        private void RefreshSlots()
        {
            OnPropertyChanged(nameof(HasSlotSelector));

            if (!HasSlotSelector || _key is not { } key)
            {
                SlotOptions = [];

                SetSelectedSlot(null);

                return;
            }

            var options = new List<MacroSlotOption>(_persistedSlots);
            MacroSlotOption? selected = null;

            for (var slot = Macro.MinMacroIndex; slot <= _persistedSlots; slot++)
            {
                var option = new MacroSlotOption(slot, key.Key.GetMacro(slot) is not null);

                options.Add(option);

                if (slot == key.Key.ActiveMacroIndex)
                {
                    selected = option;
                }
            }

            SlotOptions = options;

            SetSelectedSlot(selected);
        }

        /// <summary>
        /// The dropdown's write path — and the whole of it. It moves the active slot and re-reads;
        /// it never raises <c>Assigned</c>, which is the one route to the editor's funnel and so to
        /// the dirty flag (see the type's remarks).
        /// </summary>
        private void SelectSlot(MacroSlotOption? option)
        {
            // A ComboBox pushes null through its binding while its items are being replaced. That
            // is not a choice the user made, and answering it would clear the active slot.
            if (option is null || ReferenceEquals(option, _selectedSlot))
            {
                return;
            }

            if (!HasSlotSelector || _key is not { } key)
            {
                OnPropertyChanged(nameof(SelectedSlot));

                return;
            }

            key.Key.ActiveMacroIndex = option.Slot;

            _hasChosenSlot = true;

            Message = string.Empty;

            ReadFromModel();
        }

        private void SetSelectedSlot(MacroSlotOption? option)
        {
            if (ReferenceEquals(_selectedSlot, option))
            {
                return;
            }

            _selectedSlot = option;

            OnPropertyChanged(nameof(SelectedSlot));
        }

        /// <summary>
        /// Which slot a macro created right now would occupy: <b>the slot under edit</b> when it is
        /// empty, and otherwise the first empty persisted one. It is deliberately not
        /// <see cref="KeyboardKey.AssignMacro"/>, which always takes the first free slot of the five
        /// the <em>model</em> owns — that would ignore an explicit pick and could put a macro in a
        /// slot the dialect never writes. Returns 0 when there is nowhere to put one.
        /// </summary>
        private int ResolveSlotForNewMacro(KeyboardKey key)
        {
            if (!key.CanAssignMacro || !key.UsesMacroSlots)
            {
                return 0;
            }

            var active = key.ActiveMacroIndex;

            if (active >= Macro.MinMacroIndex && active <= _persistedSlots && key.GetMacro(active) is null)
            {
                return active;
            }

            for (var slot = Macro.MinMacroIndex; slot <= _persistedSlots; slot++)
            {
                if (key.GetMacro(slot) is null)
                {
                    return slot;
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// One persisted macro slot of the selected position, as the panel's header draws it — a dot
    /// that is filled when the slot holds a macro, and a row of the dropdown that names it.
    ///
    /// <para><b>Immutable, and rebuilt rather than mutated</b>, exactly as
    /// <see cref="MacroChordModifier"/> is: the strip is a projection of the key's slots and of
    /// nothing else. <b>Not a view model</b>, for the same reason — it is rendered only through a
    /// <c>DataTemplate</c> in <c>Views/MacroInspectorPanelView.axaml</c>, and a type that never
    /// resolves to a view of its own should not have to be excused in <c>ViewResolutionTests</c>.</para>
    ///
    /// <para><b>It is not <see cref="MacroSlotViewModel"/>.</b> That one is the Macros tab's card:
    /// it carries an <c>ACTIVE</c> badge, a <c>Make active</c> action and a library row, and this
    /// panel deliberately refuses all three.</para>
    /// </summary>
    public sealed class MacroSlotOption
    {
        /// <summary>The dropdown row's wording. This app's, in the shape the tab's cards use.</summary>
        public const string CaptionFormat = "Slot {0} — {1}";

        /// <summary>What an unoccupied slot is called.</summary>
        public const string EmptyCaption = "empty";

        /// <summary>
        /// What an occupied one is called. Deliberately not the macro's name: the name dropdown
        /// directly beneath is what names macros, and repeating it here would leave two controls
        /// saying the same thing and one of them wrong after a rename.
        /// </summary>
        public const string OccupiedCaption = "in use";

        /// <summary>Builds the row caption for one slot in one state.</summary>
        public static string BuildCaption(int slot, bool isOccupied)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                CaptionFormat,
                slot,
                isOccupied ? OccupiedCaption : EmptyCaption);
        }

        /// <summary>The slot number 1..N this entry stands for (06 §1's <c>MacroIdx</c>).</summary>
        public int Slot { get; }

        /// <summary>Whether the slot holds a macro — what fills the dot.</summary>
        public bool IsOccupied { get; }

        /// <summary>"Slot 3 — empty". The dropdown's row, and the dot's tooltip.</summary>
        public string Caption { get; }

        /// <summary>Builds the entry for <paramref name="slot"/> (1..N).</summary>
        public MacroSlotOption(int slot, bool isOccupied)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(slot, Macro.MinMacroIndex);

            Slot = slot;
            IsOccupied = isOccupied;
            Caption = BuildCaption(slot, isOccupied);
        }
    }
}
