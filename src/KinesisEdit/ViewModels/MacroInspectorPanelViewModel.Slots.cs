using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro panel's <b>slot selector</b> (issue #137, redrawn by #146): one numbered chip per
    /// slot the dialect persists — its own number when it holds a macro, a bare <c>+</c> when it does
    /// not — with the slot under edit wearing the accent outline.
    ///
    /// <para><b>Why the panel needs one at all.</b> A key holds up to five macros told apart by
    /// their co-triggers (06 §1), and until issue #137 the rail could edit exactly one of them —
    /// the rest were reachable only from a macro section that no longer exists (issue #140). Without
    /// this selector "the rail is where a macro is edited" would be true of one slot in five.</para>
    ///
    /// <para><b>Chips, not dots plus a dropdown</b> (issue #146). The strip used to be a row of
    /// filled/hollow dots beside a <c>Slot 3 — empty</c> ComboBox: two controls saying the same
    /// thing, one of which had to be opened to find out which slot was being edited. A chip carries
    /// the number, the occupancy and the selection at once and is a single click away from any other
    /// slot. <see cref="MacroSlotOption.Caption"/> survives as its <c>ToolTip.Tip</c> — a bare
    /// numeral is not accessible text.</para>
    ///
    /// <para><b>Selecting a slot does not dirty the session.</b>
    /// <see cref="KeyboardKey.ActiveMacroIndex"/> is an in-memory field that is never serialized
    /// (05 §1.3), so the selection re-reads the panel and deliberately does <em>not</em> reach the
    /// editor's funnel through <c>Assigned</c>. An unsaved-changes prompt for a choice no save could
    /// persist would be a lie.</para>
    ///
    /// <para><b>There is no <c>ACTIVE</c> badge and no <c>Make active</c>.</b> The strip is a pure
    /// <em>editing</em> selector: every populated slot is live on the keyboard, and what tells them
    /// apart is their co-triggers — which is what the Trigger strip beside it shows. When two of them
    /// fail to be told apart, every chip involved wears the advisory ring
    /// (<see cref="MacroSlotOption.IsColliding"/>) beside the one amber line the strip draws.</para>
    ///
    /// <para><b>Absent, never disabled.</b> A flat-list board (<c>UsesFlatMacroList</c>, the
    /// Advantage 360) and a position that refuses macros (05 §5.3) render no selector at all.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>
        /// The section label over the slot strip. This app's wording, <b>plural</b> since issue #146
        /// drew the whole strip on one row: it labels five chips, not the one slot under edit.
        /// </summary>
        public const string SlotSectionLabel = "SLOTS";

        /// <summary>
        /// Whether the slot strip is drawn at all: a slot-based device, and a position that can
        /// carry a macro. A flat-list board and a macro-refusing position get <b>nothing</b> here
        /// rather than a dead control (docs/design/README.md's capability law).
        /// </summary>
        public bool HasSlotSelector => _usesMacroSlots && IsAvailable;

        /// <summary>
        /// One entry per <b>persisted</b> slot, in slot order — the chips, and the whole of what the
        /// strip draws. Rebuilt whole on every read, because <see cref="MacroSlotOption"/> is
        /// immutable: which slots exist is a device fact, and which are occupied, selected or
        /// colliding are facts about the key.
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
        /// <para>
        /// It survives the chips as the property tests and any future one-of-N control bind to; the
        /// chips run <see cref="SelectSlotCommand"/>. Both funnel through the one
        /// <see cref="SelectSlot"/>, so there is a single write path whichever way the slot moves.
        /// </para>
        /// </summary>
        public MacroSlotOption? SelectedSlot
        {
            get => _selectedSlot;
            set => SelectSlot(value);
        }

        /// <summary>
        /// The same answer as a bare number, and what the <b>editor</b> reads when it arms a macro
        /// copy: which slot of the selected position the panel is editing, or
        /// <see cref="MacroSites.FlatListSlot"/> on a board that keeps its macros in one per-layout
        /// list (06 §1) and has no slots to name. A caller outside this panel wants the slot, not
        /// the chip that carries it — and would otherwise have to spell the flat-list fallback
        /// itself, in a second place, from a null.
        /// </summary>
        public int SelectedSlotNumber => _selectedSlot?.Slot ?? MacroSites.FlatListSlot;

        /// <summary>Points the panel at one slot — what a chip runs. See <see cref="SelectedSlot"/>.</summary>
        public IRelayCommand<MacroSlotOption> SelectSlotCommand { get; private set; } = null!;

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

        /// <summary>
        /// Builds the strip's command. Called from the constructor, which is why the property above
        /// carries a private setter rather than being get-only — the same shape
        /// <c>CreateTriggerStrip</c> and <c>CreateComposer</c> use, and for the same reason: a
        /// get-only auto-property can be assigned in the declaring constructor alone, and putting
        /// the assignment there would be exactly the spill into the main file this partial prevents.
        /// </summary>
        private void CreateSlotSelector()
        {
            SelectSlotCommand = new RelayCommand<MacroSlotOption>(SelectSlot);
        }

        /// <summary>Drops the slot choice; called when the rail moves to another position.</summary>
        private void ResetSlotChoice()
        {
            _hasChosenSlot = false;
        }

        /// <summary>
        /// Pins the panel to the slot it is on without the user having touched a chip. One caller:
        /// <c>DeleteMacro</c>. Emptying a slot is as clear a statement about which slot is meant as
        /// picking it, and without this the fallback in <see cref="ReadMacro"/> would answer the
        /// delete by opening some <em>other</em> populated slot of the same key — so the panel would
        /// jump to a macro the user did not ask for and the deletion would leave nothing on screen
        /// to show for itself.
        /// </summary>
        private void ClaimSlotChoice()
        {
            _hasChosenSlot = true;
        }

        /// <summary>
        /// Rebuilds the strip off the model and the collision the caller already resolved. It writes
        /// nothing: which slots exist is a device fact, and which are occupied, selected or colliding
        /// are facts about the key.
        /// </summary>
        private void RefreshSlots(MacroCollision collision)
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
                var option = new MacroSlotOption(
                    slot,
                    key.Key.GetMacro(slot) is not null,
                    slot == key.Key.ActiveMacroIndex,
                    collision.Includes(slot));

                options.Add(option);

                if (option.IsSelected)
                {
                    selected = option;
                }
            }

            SlotOptions = options;

            SetSelectedSlot(selected);
        }

        /// <summary>
        /// The strip's write path — and the whole of it, whichever control reached it. It moves the
        /// active slot and re-reads; it never raises <c>Assigned</c>, which is the one route to the
        /// editor's funnel and so to the dirty flag (see the type's remarks).
        /// </summary>
        private void SelectSlot(MacroSlotOption? option)
        {
            // A one-of-N control pushes null through its binding while its items are being replaced.
            // That is not a choice the user made, and answering it would clear the active slot.
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
            OnPropertyChanged(nameof(SelectedSlotNumber));
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
    /// One persisted macro slot of the selected position, as the panel's header draws it — a chip
    /// carrying the slot's number when it holds a macro and a bare <c>+</c> when it does not.
    ///
    /// <para><b>Immutable, and rebuilt rather than mutated</b>, exactly as
    /// <see cref="MacroChordModifier"/> is: the strip is a projection of the key's slots and of
    /// nothing else, so a changed selection or a changed collision replaces the whole list.
    /// <b>Not a view model</b>, for the same reason — it is rendered only through a
    /// <c>DataTemplate</c> in <c>Views/MacroInspectorPanelView.axaml</c>, and a type that never
    /// resolves to a view of its own should not have to be excused in <c>ViewResolutionTests</c>.</para>
    ///
    /// <para><b>The chip's face is sans, not mono.</b> A slot ordinal is 06 §1's <c>MacroIdx</c>,
    /// which is a position <em>within</em> a macro line rather than a value the file spells out, so
    /// the mono law ("this is literally a value in this board's config file") does not reach it.</para>
    ///
    /// <para><b>It is a chip, not a card and not a dropdown row.</b> The deleted macro section drew
    /// each slot as a card with an <c>ACTIVE</c> badge and a <c>Make active</c> action; this panel
    /// refuses both, so nothing of that shape was carried over when the section went (issue #140).
    /// The dropdown that replaced them went with issue #146 — but its wording did not, because a
    /// numeral alone is not accessible text: <see cref="Caption"/> is the chip's tooltip.</para>
    /// </summary>
    public sealed class MacroSlotOption
    {
        /// <summary>The tooltip's wording. This app's.</summary>
        public const string CaptionFormat = "Slot {0} — {1}";

        /// <summary>What an unoccupied slot is called.</summary>
        public const string EmptyCaption = "empty";

        /// <summary>
        /// What an occupied one is called. Deliberately not the macro's name: nothing in the app
        /// names a macro since issue #146, and a slot chip is about which of the key's five macros
        /// is being edited rather than about what is in it.
        /// </summary>
        public const string OccupiedCaption = "in use";

        /// <summary>
        /// What an <b>empty</b> chip reads — the mock's own <c>+</c>. A plain ASCII plus rather than
        /// the fullwidth <c>＋</c> the <c>insert step</c> row is drawn with: that one is geometry
        /// beside a caption, this one is the chip's whole face and has to sit on the same baseline
        /// as the numerals in the chips beside it.
        /// </summary>
        public const string EmptyChipText = "+";

        /// <summary>Builds the tooltip for one slot in one state.</summary>
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

        /// <summary>Whether the slot holds a macro — what decides the chip's face.</summary>
        public bool IsOccupied { get; }

        /// <summary>The chip's own caption: the slot number, or <see cref="EmptyChipText"/>.</summary>
        public string ChipText { get; }

        /// <summary>Whether this is the slot the panel is editing — the accent outline.</summary>
        public bool IsSelected { get; }

        /// <summary>
        /// Whether this slot takes part in a co-trigger collision with another populated slot of the
        /// same key (06 §5) — the advisory ring. <b>Both sides of a clash carry it</b>, because a
        /// ring on one of two indistinguishable macros would name a culprit where there is only a
        /// pair.
        /// </summary>
        public bool IsColliding { get; }

        /// <summary>"Slot 3 — empty". The chip's <c>ToolTip.Tip</c>.</summary>
        public string Caption { get; }

        /// <summary>Builds the entry for <paramref name="slot"/> (1..N) in one state.</summary>
        public MacroSlotOption(int slot, bool isOccupied, bool isSelected = false, bool isColliding = false)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(slot, Macro.MinMacroIndex);

            Slot = slot;
            IsOccupied = isOccupied;
            IsSelected = isSelected;
            IsColliding = isColliding;
            ChipText = isOccupied ? slot.ToString(CultureInfo.InvariantCulture) : EmptyChipText;
            Caption = BuildCaption(slot, isOccupied);
        }
    }
}
