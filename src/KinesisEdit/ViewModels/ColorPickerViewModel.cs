using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The shared color picker of specs/07-lighting.md §4: one current color, the premixed swatch
    /// row, and the twelve custom slots persisted to <c>cust_color_1</c>…<c>cust_color_12</c> in
    /// <c>app_settings.txt</c> (specs/08-settings.md §3).
    /// <para>
    /// The ring, the R/G/B sliders and the hex field are Avalonia's own <c>ColorView</c>, wrapped
    /// by <c>Controls/ColorPickerView</c>; the conversion between Avalonia colors and
    /// <see cref="LedColor"/> lives in that control, so this view model only ever sees
    /// <see cref="LedColor"/> and <c>#RRGGBB</c> strings (docs/app/app-shell.md, invariant 6).
    /// </para>
    /// </summary>
    public sealed class ColorPickerViewModel : ViewModelBase
    {
        /// <summary>Caption of the button that writes the current color into a custom slot.</summary>
        public const string StoreCustomColorCaption = "Add to Custom Colors";

        /// <summary>
        /// The premixed swatches of specs/07-lighting.md §4, in the spec's order: "white default,
        /// yellow, red, lime, blue, fuchsia, orange RGB(255,128,0), azure RGB(0,128,255), aqua,
        /// black". Black is the "no color" value (§2.1), so painting a key with it erases that
        /// key's assignment — the swatch row doubles as the eraser.
        /// </summary>
        public static IReadOnlyList<ColorSwatch> PremixedColors { get; } =
        [
            new ColorSwatch { Name = "White", Color = new LedColor(255, 255, 255) },
            new ColorSwatch { Name = "Yellow", Color = new LedColor(255, 255, 0) },
            new ColorSwatch { Name = "Red", Color = new LedColor(255, 0, 0) },
            new ColorSwatch { Name = "Lime", Color = new LedColor(0, 255, 0) },
            new ColorSwatch { Name = "Blue", Color = new LedColor(0, 0, 255) },
            new ColorSwatch { Name = "Fuchsia", Color = new LedColor(255, 0, 255) },
            new ColorSwatch { Name = "Orange", Color = new LedColor(255, 128, 0) },
            new ColorSwatch { Name = "Azure", Color = new LedColor(0, 128, 255) },
            new ColorSwatch { Name = "Aqua", Color = new LedColor(0, 255, 255) },
            new ColorSwatch { Name = "Black", Color = LedColor.Black }
        ];

        /// <summary>The twelve custom slots, in slot order; an unset slot holds no color.</summary>
        public IReadOnlyList<CustomColorSlotViewModel> CustomColors { get; }

        /// <summary>The color everything the picker feeds is set from.</summary>
        public LedColor Color
        {
            get => _color;
            set
            {
                if (_color == value)
                {
                    return;
                }

                _color = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(ColorHex));

                ColorChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// <see cref="Color"/> as <c>#RRGGBB</c>. Two-way: the control writes the user's pick back
        /// through here, and anything unparseable is ignored rather than resetting the color.
        /// </summary>
        public string ColorHex
        {
            get => KeyColorOverlay.ToHex(_color);
            set
            {
                if (KeyColorOverlay.TryParseHex(value, out var parsed))
                {
                    Color = parsed;
                }
            }
        }

        /// <summary>
        /// Whether custom slots may be written. False in demo mode and without a drive: spec 08 §3
        /// bans <b>saving</b> app settings there, and a slot that silently forgets itself on the
        /// next launch would be worse than a disabled button. Reading still happens on any drive.
        /// </summary>
        public bool CanStoreCustomColors { get; }

        /// <summary>Sets <see cref="Color"/> from a premixed swatch.</summary>
        public IRelayCommand<ColorSwatch> SelectSwatchCommand { get; }

        /// <summary>Sets <see cref="Color"/> from a filled custom slot; an empty slot is a no-op.</summary>
        public IRelayCommand<CustomColorSlotViewModel> SelectCustomColorCommand { get; }

        /// <summary>Writes <see cref="Color"/> into the next custom slot and persists the twelve.</summary>
        public IAsyncRelayCommand StoreCustomColorCommand { get; }

        /// <summary>Raised whenever <see cref="Color"/> changed, however it was set.</summary>
        public event Action<LedColor>? ColorChanged;

        private readonly DeviceSnapshot _device;
        private readonly ISettingsService _settings;
        private LedColor _color = LedColor.DefaultEffectColor;
        private int _rotationIndex;
        private bool _hasLoadStarted;

        /// <summary>
        /// Creates the picker for <paramref name="device"/>. Construction touches no file;
        /// <see cref="LoadAsync"/> reads the stored slots.
        /// </summary>
        public ColorPickerViewModel(DeviceSnapshot device, ISettingsService settings)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            var slots = new List<CustomColorSlotViewModel>(AppSettings.CustomColorCount);

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                slots.Add(new CustomColorSlotViewModel(slotNumber));
            }

            CustomColors = slots;
            CanStoreCustomColors = !device.IsDemoMode && device.Location is not null;

            SelectSwatchCommand = new RelayCommand<ColorSwatch>(SelectSwatch);
            SelectCustomColorCommand = new RelayCommand<CustomColorSlotViewModel>(SelectCustomColor);
            StoreCustomColorCommand = new AsyncRelayCommand(StoreCustomColorAsync, () => CanStoreCustomColors);
        }

        /// <summary>
        /// Reads the stored custom slots off the UI thread. Total and idempotent: a missing or
        /// unreadable <c>app_settings.txt</c> simply leaves the twelve slots empty — a picker
        /// preference is never worth a dialog.
        /// </summary>
        public async Task LoadAsync()
        {
            if (_hasLoadStarted)
            {
                return;
            }

            _hasLoadStarted = true;

            var location = _device.Location;

            if (location is null)
            {
                return;
            }

            AppSettings? loaded = null;

            try
            {
                loaded = await Task.Run(() => _settings.LoadAppSettings(location)).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Swallowed on purpose; see the summary.
            }

            if (loaded is not null)
            {
                Apply(loaded);
            }
        }

        private void Apply(AppSettings settings)
        {
            for (var index = 0; index < CustomColors.Count; index++)
            {
                CustomColors[index].Color = LedColorConverter.ToLedColor(settings.CustomColors[index]);
            }

            _rotationIndex = 0;
        }

        private void SelectSwatch(ColorSwatch? swatch)
        {
            if (swatch is not null)
            {
                Color = swatch.Color;
            }
        }

        private void SelectCustomColor(CustomColorSlotViewModel? slot)
        {
            if (slot?.Color is { } color)
            {
                Color = color;
            }
        }

        /// <summary>
        /// Stores the current color in the first empty slot, or — when all twelve are full — in
        /// the next one round-robin, and writes the twelve back. The write is a read-modify-write
        /// through <see cref="ISettingsService"/> so the notification hide flags another feature
        /// may have changed meanwhile survive: <c>app_settings.txt</c> has one reader and one
        /// writer (docs/app/app-shell.md).
        /// </summary>
        private async Task StoreCustomColorAsync()
        {
            if (!CanStoreCustomColors)
            {
                return;
            }

            var slot = FindStoreTarget();
            var color = Color;

            slot.Color = color;

            var location = _device.Location!;
            var stored = LedColorConverter.ToSettingsColor(color);

            try
            {
                await Task.Run(() =>
                {
                    var settings = _settings.LoadAppSettings(location).WithCustomColor(slot.SlotNumber, stored);

                    _settings.SaveAppSettings(location, settings);
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Swallowed on purpose; the slot still carries the color for this session.
            }
        }

        private CustomColorSlotViewModel FindStoreTarget()
        {
            foreach (var slot in CustomColors)
            {
                if (!slot.HasColor)
                {
                    return slot;
                }
            }

            var target = CustomColors[_rotationIndex];

            _rotationIndex = (_rotationIndex + 1) % CustomColors.Count;

            return target;
        }
    }
}
