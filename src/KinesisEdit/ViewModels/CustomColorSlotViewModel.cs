using System.Globalization;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One of the twelve custom color slots of the picker (specs/07-lighting.md §4), persisted to
    /// <c>cust_color_1</c>…<c>cust_color_12</c> in <c>app_settings.txt</c> (specs/08-settings.md
    /// §3). A slot is <b>unset</b> rather than black when nothing has been stored in it — that is
    /// the distinction <c>AppSettings.CustomColors</c> keeps with a nullable entry, and it is why
    /// <see cref="Color"/> is nullable here too.
    /// </summary>
    public sealed class CustomColorSlotViewModel : ViewModelBase
    {
        /// <summary>The slot's 1-based number, matching <c>SettingsKeys.GetCustomColorKey</c>.</summary>
        public int SlotNumber { get; }

        /// <summary>The stored color, or null while the slot is empty.</summary>
        public LedColor? Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorHex));
                    OnPropertyChanged(nameof(HasColor));
                }
            }
        }

        /// <summary>The stored color as <c>#RRGGBB</c>, or null while the slot is empty.</summary>
        public string? ColorHex => _color is { } color ? KeyColorOverlay.ToHex(color) : null;

        /// <summary>Whether the slot holds a color.</summary>
        public bool HasColor => _color is not null;

        /// <summary>
        /// Whether this is the slot the settings screen's swatch strip is editing. The picker
        /// never sets it — there a slot is clicked to pick a colour, not to be edited — so it is
        /// false everywhere else, and the strip's accent ring is the one thing it drives.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>The slot's tooltip: its number, so the twelve identical squares are tellable apart.</summary>
        public string Caption => string.Create(CultureInfo.InvariantCulture, $"Custom {SlotNumber}");

        private LedColor? _color;
        private bool _isSelected;

        /// <summary>Creates the empty slot numbered <paramref name="slotNumber"/>.</summary>
        public CustomColorSlotViewModel(int slotNumber)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(slotNumber, 1);

            SlotNumber = slotNumber;
        }
    }
}
