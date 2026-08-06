using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// A row carrying one setting whose value comes from a fixed list of strings — today only the
    /// Freestyle Edge's <c>led_mode</c>, whose domain is Core's <see cref="LedModeValues"/>
    /// (specs/08-settings.md §2, §5.3). Matching a loaded value is case-insensitive, as all
    /// settings parsing is (spec 08 §1).
    /// <para>
    /// The row has a genuine <b>unset</b> state, because the model property is nullable and
    /// "absent" is a value the file can carry: a key the file does not hold, or one holding a
    /// value this app does not know (a form newer firmware introduced), leaves
    /// <see cref="SelectedOption"/> null and <see cref="ApplyTo"/> writing null. Selecting the
    /// first option instead would switch a Freestyle Edge's backlight off on the first save.
    /// </para>
    /// <para>
    /// Writing null is also what <b>preserves</b> an unrecognised value: a null property emits no
    /// pair, and a save only passes managed pairs to <c>UpdateSettingsFile</c>, so the device's
    /// own line survives verbatim (spec 08 §1). Handing the unknown text back to
    /// <see cref="KeyboardSettingsSerializer"/> would not preserve anything — it throws on a value
    /// outside <see cref="LedModeValues"/>, which would make the whole panel unsavable.
    /// </para>
    /// </summary>
    public sealed class SettingsChoiceRowViewModel : SettingsRowViewModel
    {
        /// <summary>What the picker shows while the file carries no value for this key.</summary>
        public const string UnsetCaption = "Not set";

        /// <summary>Prefix of the placeholder shown for a value this app does not recognise.</summary>
        public const string UnrecognizedCaptionPrefix = "Kept as written: ";

        /// <summary>The options, in the order the picker shows them.</summary>
        public IReadOnlyList<SettingsChoice> Options { get; }

        /// <summary>
        /// The chosen option, or null when the row is unset — either because the file carries no
        /// value for this key or because the value it carries is not one of <see cref="Options"/>.
        /// </summary>
        public SettingsChoice? SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (!SetProperty(ref _selectedOption, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(IsUnset));

                if (value is not null)
                {
                    // Picking an option ends the "leave what is on the device alone" state; from
                    // here on the row writes what the user chose.
                    SetUnrecognizedValue(null);
                }
            }
        }

        /// <summary>
        /// The value read from the file that matched no option, or null. It is shown in
        /// <see cref="Placeholder"/> and never written: leaving the key out of the save is what
        /// keeps the device's own line intact.
        /// </summary>
        public string? UnrecognizedValue => _unrecognizedValue;

        /// <summary>Whether no option is selected — the file's value is absent or unrecognised.</summary>
        public bool IsUnset => SelectedOption is null;

        /// <summary>What the picker reads as while <see cref="IsUnset"/>.</summary>
        public string Placeholder => _unrecognizedValue is null
            ? UnsetCaption
            : UnrecognizedCaptionPrefix + _unrecognizedValue;

        private readonly Func<KeyboardSettings, string?> _reader;
        private readonly Func<KeyboardSettings, string?, KeyboardSettings> _writer;
        private SettingsChoice? _selectedOption;
        private string? _unrecognizedValue;

        /// <summary>Creates a choice row over the string key <paramref name="reader"/>/<paramref name="writer"/> address.</summary>
        public SettingsChoiceRowViewModel(
            string caption,
            string description,
            IReadOnlyList<SettingsChoice> options,
            Func<KeyboardSettings, string?> reader,
            Func<KeyboardSettings, string?, KeyboardSettings> writer) : base(caption, description)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options.Count == 0)
            {
                throw new ArgumentException("A choice row needs at least one option.", nameof(options));
            }

            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));

            Options = options;
        }

        /// <inheritdoc />
        public override void ReadFrom(KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var value = _reader(settings);
            var option = FindOption(value);

            SelectedOption = option;

            // Only meaningful while nothing is selected — it is what the placeholder explains;
            // the setter above already cleared it when an option matched.
            SetUnrecognizedValue(option is null ? value : null);
        }

        /// <inheritdoc />
        public override KeyboardSettings ApplyTo(KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            // Null while unset: the key is then not emitted at all, so an absent key stays absent
            // and an unrecognised one keeps whatever the device wrote (see the summary).
            return _writer(settings, SelectedOption?.Value);
        }

        private void SetUnrecognizedValue(string? value)
        {
            if (SetProperty(ref _unrecognizedValue, value, nameof(UnrecognizedValue)))
            {
                OnPropertyChanged(nameof(Placeholder));
            }
        }

        private SettingsChoice? FindOption(string? value)
        {
            if (value is null)
            {
                return null;
            }

            var trimmedValue = value.Trim();

            foreach (var option in Options)
            {
                if (string.Equals(option.Value, trimmedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return null;
        }
    }
}
