using System.Globalization;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// A row carrying one numeric setting as a slider, optionally with the "disable" checkbox of
    /// specs/08-settings.md §5.1 that writes 0 (macro speed, status report speed). The startup
    /// profile slider uses the same row without the checkbox.
    /// <para>
    /// <b>This is where clamping happens.</b> <c>KeyboardSettingsSerializer</c> throws
    /// <see cref="ArgumentOutOfRangeException"/> on a value outside
    /// <see cref="SettingsValueRanges"/>, so both the setter and <see cref="ApplyTo"/> pin the
    /// value into <see cref="Minimum"/>..<see cref="Maximum"/> — a slider bound to a stale or
    /// hand-edited value can never reach the serializer.
    /// </para>
    /// </summary>
    public sealed class SettingsSliderRowViewModel : SettingsRowViewModel
    {
        /// <summary>Caption of the checkbox that writes 0 (specs/08-settings.md §5.1).</summary>
        public const string DisableCaption = "Disable";

        /// <summary>What <see cref="ValueCaption"/> reads while the setting is disabled.</summary>
        public const string DisabledValueCaption = "Off";

        /// <summary>The value written when the row is disabled — 0 means "disabled" for every such key (spec 08 §2).</summary>
        public const int DisabledValue = 0;

        /// <summary>Lowest value the slider offers.</summary>
        public int Minimum { get; }

        /// <summary>Highest value the slider offers.</summary>
        public int Maximum { get; }

        /// <summary>The slider's value, always clamped into <see cref="Minimum"/>..<see cref="Maximum"/>.</summary>
        public int Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, Clamp(value)))
                {
                    OnPropertyChanged(nameof(ValueCaption));
                }
            }
        }

        /// <summary>Whether the row offers the "disable" checkbox at all.</summary>
        public bool CanDisable { get; }

        /// <summary>Whether the setting is switched off; only meaningful when <see cref="CanDisable"/>.</summary>
        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                if (SetProperty(ref _isDisabled, CanDisable && value))
                {
                    OnPropertyChanged(nameof(ValueCaption));
                }
            }
        }

        /// <summary>The value beside the slider, or <see cref="DisabledValueCaption"/> when it is off.</summary>
        public string ValueCaption => CanDisable && IsDisabled
            ? DisabledValueCaption
            : Value.ToString(CultureInfo.InvariantCulture);

        private readonly Func<KeyboardSettings, int?> _reader;
        private readonly Func<KeyboardSettings, int, KeyboardSettings> _writer;
        private int _value;
        private bool _isDisabled;

        /// <summary>Creates a slider row over the numeric key <paramref name="reader"/>/<paramref name="writer"/> address.</summary>
        public SettingsSliderRowViewModel(
            string caption,
            string description,
            int minimum,
            int maximum,
            bool canDisable,
            Func<KeyboardSettings, int?> reader,
            Func<KeyboardSettings, int, KeyboardSettings> writer) : base(caption, description)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);

            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));

            Minimum = minimum;
            Maximum = maximum;
            CanDisable = canDisable;

            _value = minimum;
        }

        /// <inheritdoc />
        public override void ReadFrom(KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var stored = _reader(settings);

            // A disable-able key that is absent or 0 is off (spec 08 §2: 0 = disabled, and a
            // missing key "leaves zero values"); the slider itself then rests at its floor,
            // because a row without a value must still show a legal one.
            if (CanDisable && stored is null or DisabledValue)
            {
                IsDisabled = true;
                Value = Minimum;

                return;
            }

            IsDisabled = false;
            Value = stored ?? Minimum;
        }

        /// <inheritdoc />
        public override KeyboardSettings ApplyTo(KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return _writer(settings, CanDisable && IsDisabled ? DisabledValue : Clamp(Value));
        }

        private int Clamp(int value)
        {
            return Math.Clamp(value, Minimum, Maximum);
        }
    }
}
