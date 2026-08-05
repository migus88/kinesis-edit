using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// A row carrying one on/off setting — game mode, program lock, the two Advantage2 tones, and
    /// the v-Drive switch in either of its two key forms (specs/08-settings.md §2, §5). The key
    /// it reads and writes travels as a pair of delegates, so this class knows no key names and
    /// no device identity.
    /// </summary>
    public sealed class SettingsToggleRowViewModel : SettingsRowViewModel
    {
        /// <summary>Whether the setting is on. A key absent from the file reads as false (spec 08 §2 notes).</summary>
        public bool IsOn
        {
            get => _isOn;
            set => SetProperty(ref _isOn, value);
        }

        private readonly Func<KeyboardSettings, bool?> _reader;
        private readonly Func<KeyboardSettings, bool, KeyboardSettings> _writer;
        private bool _isOn;

        /// <summary>Creates a toggle row over the flag <paramref name="reader"/>/<paramref name="writer"/> address.</summary>
        public SettingsToggleRowViewModel(
            string caption,
            string description,
            Func<KeyboardSettings, bool?> reader,
            Func<KeyboardSettings, bool, KeyboardSettings> writer) : base(caption, description)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <inheritdoc />
        public override void ReadFrom(KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            IsOn = _reader(settings) ?? false;
        }

        /// <inheritdoc />
        public override KeyboardSettings ApplyTo(KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return _writer(settings, IsOn);
        }
    }
}
