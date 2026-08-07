using System.Globalization;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One card of the Macros tab's <b>slot</b> branch (mockup <c>1i</c>, left): "Slot 1 — Sign-off
    /// block" with its keystrokes, badged <c>ACTIVE</c> or offering <c>Make active</c>, and "Slot 4 —
    /// empty" offering <c>＋ Record a macro</c>.
    /// <para>
    /// <b>Which slots exist comes from the device</b>, never from a literal, and it is the count the
    /// dialect <em>persists</em> (<see cref="Core.Devices.MacroCapability.PersistedSlotsPerKey"/>: 3
    /// on the Advantage2 and Freestyle Edge/Pro, 5 on the RGB family), not the five the model owns —
    /// a macro put in slot 4 of a Freestyle Edge is gone at the very next save (06 §1). The
    /// flat-list families have no slots at all and draw the other branch instead.
    /// </para>
    /// <para>
    /// <b>The card names a library entry, not a macro.</b> Its name, its length and its budget come
    /// from <see cref="MacroLibraryRowViewModel"/> — the same row the profile's list shows — so the
    /// two views of one macro cannot disagree about what it is called.
    /// </para>
    /// </summary>
    public sealed class MacroSlotViewModel : ViewModelBase
    {
        /// <summary>The card's heading, verbatim in shape from mockup <c>1i</c>.</summary>
        public const string TitleFormat = "Slot {0} — {1}";

        /// <summary>What an unoccupied card is called (<c>1i</c>: "Slot 4 — empty").</summary>
        public const string EmptyCaption = "empty";

        /// <summary>The badge on the slot the key actually fires (05 §1.3's <c>MacroIdx</c>).</summary>
        public const string ActiveBadge = "ACTIVE";

        /// <summary>What every other occupied card offers instead.</summary>
        public const string MakeActiveCaption = "Make active";

        /// <summary>The empty card's action. The <c>＋</c> is geometry, so only the words are here.</summary>
        public const string RecordCaption = "Record a macro";

        /// <summary>Builds the heading for <paramref name="slot"/> holding <paramref name="name"/>.</summary>
        public static string BuildTitle(int slot, string name)
        {
            return string.Format(CultureInfo.InvariantCulture, TitleFormat, slot, name);
        }

        /// <summary>The slot number 1..N this card writes (06 §1's <c>MacroIdx</c>).</summary>
        public int Slot { get; }

        /// <summary>The library row of the macro sitting here, or null when the slot is empty.</summary>
        public MacroLibraryRowViewModel? Row { get; }

        /// <summary>Whether the slot holds a macro at all.</summary>
        public bool IsOccupied => Row is not null;

        /// <summary>Whether it is empty — bound directly, because a view cannot negate in a template.</summary>
        public bool IsEmpty => Row is null;

        /// <summary>"Slot 1 — Sign-off block", or "Slot 4 — empty".</summary>
        public string Title { get; }

        /// <summary>The macro's keystrokes as the layout file carries them (06 §3), or empty.</summary>
        public string KeystrokesText => Row?.ContentsText ?? string.Empty;

        /// <summary>Whether this is the slot the key fires (<c>KeyboardKey.ActiveMacroIndex</c>).</summary>
        public bool IsActive { get; }

        /// <summary>Whether the card offers <c>Make active</c> — occupied, and not already it.</summary>
        public bool CanMakeActive => IsOccupied && !IsActive;

        /// <summary>Whether the macro here is past the device's per-macro cap. Amber; it still saves.</summary>
        public bool IsOverBudget => Row?.IsOverBudget == true;

        /// <summary>Mockup <c>1i</c>'s budget sentence, or an empty string.</summary>
        public string BudgetAdvisory => Row?.BudgetAdvisory ?? string.Empty;

        /// <summary>
        /// Whether this is the card the <c>this macro</c> meter is reading. The tab's own selection,
        /// deliberately not a <c>SelectingItemsControl</c>'s — the editor's keyboard grammar leaves
        /// the arrow keys to any selecting list in the focused ancestry.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates the card for <paramref name="slot"/> (1..N).</summary>
        public MacroSlotViewModel(int slot, MacroLibraryRowViewModel? row, bool isActive)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(slot, Core.Model.Macro.MinMacroIndex);

            Slot = slot;
            Row = row;
            IsActive = isActive && row is not null;
            Title = BuildTitle(slot, row?.Name ?? EmptyCaption);
        }
    }
}
