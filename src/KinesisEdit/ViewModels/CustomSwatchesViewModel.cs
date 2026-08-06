using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The settings screen's custom-swatch strip (docs/design/mockups.md §1j, "Your custom
    /// swatches — reused in every color picker"): the twelve <c>cust_color_1</c>…
    /// <c>cust_color_12</c> slots of specs/08-settings.md §3, shown, replaceable and clearable.
    /// <para>
    /// It is the <b>second</b> consumer of those slots — <see cref="ColorPickerViewModel"/> is the
    /// first — and both sit on the session's one <see cref="IAppPreferencesStore"/>. Each reads
    /// <see cref="IAppPreferencesStore.Current"/> and re-reads it on
    /// <see cref="IAppPreferencesStore.Changed"/>, so a swatch stored in the lighting picker is on
    /// this strip immediately and a swatch cleared here disappears from the picker, with no reload
    /// on either side.
    /// </para>
    /// <para>
    /// The colour to store is typed as <c>#RRGGBB</c> rather than picked from a ring: the ring
    /// belongs to the lighting tab, where a colour is being chosen for something, and this strip
    /// exists to <i>manage</i> the twelve values a config file carries. The hex box is the mono
    /// law's own case — a value that exists verbatim in <c>app_settings.txt</c>.
    /// </para>
    /// </summary>
    public sealed class CustomSwatchesViewModel : ViewModelBase
    {
        /// <summary>The section's label (mockup §1j, verbatim).</summary>
        public const string SectionLabel = "Your custom swatches — reused in every color picker";

        /// <summary>Caption of the button that writes <see cref="ColorHex"/> into the selected slot.</summary>
        public const string ReplaceCaption = "Replace";

        /// <summary>Caption of the button that unsets the selected slot.</summary>
        public const string ClearCaption = "Clear";

        /// <summary>The line under the strip while the slots may be edited.</summary>
        public const string EditHint =
            "Pick a slot, type a colour and Replace it — or Clear it back to empty. "
            + "Every colour picker in the app reads these twelve.";

        /// <summary>Formats the caption naming the slot being edited.</summary>
        public static string FormatSelection(string slotCaption)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Editing {slotCaption}");
        }

        /// <summary>The twelve slots, in slot order; an unset slot holds no colour.</summary>
        public IReadOnlyList<CustomColorSlotViewModel> Slots { get; }

        /// <summary>The slot Replace and Clear act on. Never null: the strip opens on slot 1.</summary>
        public CustomColorSlotViewModel SelectedSlot
        {
            get => _selectedSlot;
            private set
            {
                if (ReferenceEquals(_selectedSlot, value))
                {
                    return;
                }

                _selectedSlot.IsSelected = false;
                _selectedSlot = value;
                _selectedSlot.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectionCaption));

                RefreshCommands();
            }
        }

        /// <summary>The caption naming the slot being edited.</summary>
        public string SelectionCaption => FormatSelection(_selectedSlot.Caption);

        /// <summary>
        /// The colour Replace stores, as <c>#RRGGBB</c>. Kept as text rather than as a colour so an
        /// in-progress edit is never silently corrected; <see cref="ReplaceCommand"/> is simply
        /// unavailable until it parses.
        /// </summary>
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                if (SetProperty(ref _colorHex, value ?? string.Empty))
                {
                    RefreshCommands();
                }
            }
        }

        /// <summary>
        /// Whether the strip may write. False in demo mode and with no drive, matching
        /// <see cref="ColorPickerViewModel.CanStoreCustomColors"/>: spec 08 §3 bans saving app
        /// settings there, and a swatch that vanished at the end of the session would be worse
        /// than a hidden button.
        /// </summary>
        public bool CanEdit { get; }

        /// <summary>Makes a slot the one Replace and Clear act on.</summary>
        public IRelayCommand<CustomColorSlotViewModel> SelectSlotCommand { get; }

        /// <summary>Writes <see cref="ColorHex"/> into <see cref="SelectedSlot"/>.</summary>
        public IRelayCommand ReplaceCommand { get; }

        /// <summary>Unsets <see cref="SelectedSlot"/> — an empty slot is "no swatch stored", not black.</summary>
        public IRelayCommand ClearCommand { get; }

        private readonly IAppPreferencesStore _preferences;
        private CustomColorSlotViewModel _selectedSlot;
        private string _colorHex = KeyColorOverlay.ToHex(LedColor.DefaultEffectColor);

        /// <summary>
        /// Creates the strip over <paramref name="preferences"/>. Construction touches no file;
        /// <see cref="Refresh"/> reads the stored slots, and the caller triggers that first read
        /// off the UI thread (see <see cref="AppPreferencesViewModel.LoadAsync"/>).
        /// </summary>
        public CustomSwatchesViewModel(IAppPreferencesStore preferences)
        {
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));

            var slots = new List<CustomColorSlotViewModel>(AppSettings.CustomColorCount);

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                slots.Add(new CustomColorSlotViewModel(slotNumber));
            }

            Slots = slots;
            CanEdit = preferences.IsWritable;

            _selectedSlot = slots[0];
            _selectedSlot.IsSelected = true;

            SelectSlotCommand = new RelayCommand<CustomColorSlotViewModel>(SelectSlot);
            ReplaceCommand = new RelayCommand(Replace, CanReplace);
            ClearCommand = new RelayCommand(Clear, CanClear);

            // No unsubscribe: the store, this strip and the editor that hosts it all belong to one
            // device session and become garbage together.
            _preferences.Changed += Refresh;
        }

        /// <summary>Re-reads the twelve slots from the store. Total and idempotent.</summary>
        public void Refresh()
        {
            var settings = _preferences.Current;

            for (var index = 0; index < Slots.Count; index++)
            {
                Slots[index].Color = LedColorConverter.ToLedColor(settings.CustomColors[index]);
            }

            RefreshCommands();
        }

        private void SelectSlot(CustomColorSlotViewModel? slot)
        {
            if (slot is null)
            {
                return;
            }

            SelectedSlot = slot;

            // A filled slot loads its colour into the box, so "look at it, then change it" is one
            // gesture; an empty slot leaves whatever the user was about to store alone.
            if (slot.ColorHex is { } hex)
            {
                ColorHex = hex;
            }
        }

        private void Replace()
        {
            if (!CanReplace())
            {
                return;
            }

            var slot = SelectedSlot;

            KeyColorOverlay.TryParseHex(_colorHex, out var color);

            _preferences.Update(settings => settings.WithCustomColor(
                slot.SlotNumber,
                LedColorConverter.ToSettingsColor(color)));
        }

        private void Clear()
        {
            if (!CanClear())
            {
                return;
            }

            var slot = SelectedSlot;

            _preferences.Update(settings => settings.WithCustomColor(slot.SlotNumber, null));
        }

        private bool CanReplace()
        {
            return CanEdit && KeyColorOverlay.TryParseHex(_colorHex, out _);
        }

        private bool CanClear()
        {
            return CanEdit && SelectedSlot.HasColor;
        }

        private void RefreshCommands()
        {
            ReplaceCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
        }
    }
}
