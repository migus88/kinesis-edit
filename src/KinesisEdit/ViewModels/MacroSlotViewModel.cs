using System.Globalization;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One of the trigger key's macro slots (specs/05-key-model.md §1.3, specs/06-macros.md §1):
    /// the radio button of the legacy macro panel. How many exist comes from the device — never
    /// from a literal — and it is the count the dialect <b>persists</b>
    /// (<see cref="Core.Devices.MacroCapability.PersistedSlotsPerKey"/>: 3 on the Advantage2 and
    /// Freestyle Edge/Pro, 5 on the RGB family), not the five the model owns; the flat-list
    /// families (Advantage360) have none at all.
    /// </summary>
    public sealed class MacroSlotViewModel : ViewModelBase
    {
        /// <summary>Builds <paramref name="slotCount"/> empty slots, numbered from 1 (§1.3).</summary>
        public static IReadOnlyList<MacroSlotViewModel> CreateAll(int slotCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(slotCount);

            var slots = new List<MacroSlotViewModel>(slotCount);

            for (var slot = 1; slot <= slotCount; slot++)
            {
                slots.Add(new MacroSlotViewModel(slot));
            }

            return slots;
        }

        /// <summary>The slot number 1..N this entry writes (§1.3 <c>MacroIdx</c>).</summary>
        public int Slot { get; }

        /// <summary>The slot number as the button caption.</summary>
        public string Caption { get; }

        /// <summary>The macro sitting in the slot, or null when it is empty.</summary>
        public Macro? Macro
        {
            get => _macro;
            private set
            {
                if (SetProperty(ref _macro, value))
                {
                    OnPropertyChanged(nameof(IsOccupied));
                }
            }
        }

        /// <summary>Whether the slot holds a macro.</summary>
        public bool IsOccupied => _macro is not null;

        /// <summary>The slot's macro as the file would carry it, or an empty string (06 §3).</summary>
        public string Preview
        {
            get => _preview;
            private set => SetProperty(ref _preview, value);
        }

        /// <summary>Whether this is the slot the panel is editing.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private Macro? _macro;
        private string _preview = string.Empty;
        private bool _isSelected;

        /// <summary>Creates the entry for slot <paramref name="slot"/> (1..5).</summary>
        public MacroSlotViewModel(int slot)
        {
            // Fully qualified: the Macro property shadows the type name inside this class.
            ArgumentOutOfRangeException.ThrowIfLessThan(slot, KinesisEdit.Core.Model.Macro.MinMacroIndex);

            Slot = slot;
            Caption = slot.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Re-reads the slot's content, rendering it in <paramref name="dialect"/>.</summary>
        public void RefreshFromModel(Macro? macro, TokenDialect dialect)
        {
            Macro = macro;
            Preview = macro is null ? string.Empty : MacroKeystrokeRenderer.RenderKeystrokes(macro, dialect);
        }
    }
}
