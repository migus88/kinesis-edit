using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One control of the keyboard-settings panel (specs/08-settings.md §5): its label, its
    /// explanation, and the two directions it moves a value in — out of a loaded
    /// <see cref="KeyboardSettings"/> and back into one.
    /// <para>
    /// The panel is a list of these rather than one view model with twenty properties: which
    /// rows exist is decided per device by <see cref="KeyboardSettingsRows"/> from the device's
    /// <c>SettingsCapability</c>, so a board picks up exactly the controls spec 08 §2 says the
    /// app writes for it, and adding a setting is adding a row.
    /// </para>
    /// </summary>
    public abstract class SettingsRowViewModel : ViewModelBase
    {
        /// <summary>The row's label.</summary>
        public string Caption { get; }

        /// <summary>One line explaining what the setting does; may be empty.</summary>
        public string Description { get; }

        /// <summary>
        /// Whether the row may be changed. The panel turns every row off together — while it
        /// loads or saves, and permanently on a 2MB Advantage2 (spec 08 §5.4).
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// Whether the row's value is real and current but cannot be written — the 2MB Advantage2
        /// of docs/design/mockups.md 1j, which draws it as a quiet <c>(read-only)</c> marker beside
        /// the value.
        /// <para>
        /// It is <b>not</b> the negation of <see cref="IsEnabled"/> and must not be confused with
        /// it: a row is also disabled while the file is being read, while a save is in flight, and
        /// after a read that failed — none of which is a statement about the hardware, and none of
        /// which may carry the marker. This one says "the board will not accept a write", which is
        /// a fact rather than a failure, and is why the whole screen stays on the advisory ramp.
        /// </para>
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        private bool _isEnabled = true;
        private bool _isReadOnly;

        /// <summary>Creates a row labelled <paramref name="caption"/>.</summary>
        protected SettingsRowViewModel(string caption, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);
            ArgumentNullException.ThrowIfNull(description);

            Caption = caption;
            Description = description;
        }

        /// <summary>
        /// Takes the row's value from a loaded model. A key the file does not carry reads as the
        /// row's unset default — spec 08 §2 notes "missing keys leave zero/false/empty values".
        /// </summary>
        public abstract void ReadFrom(KeyboardSettings settings);

        /// <summary>
        /// Returns <paramref name="settings"/> with this row's value applied. The value is always
        /// inside <see cref="SettingsValueRanges"/> by the time it gets here, because
        /// <c>KeyboardSettingsSerializer</c> throws on an out-of-range one and an exception must
        /// never escape into the UI.
        /// </summary>
        public abstract KeyboardSettings ApplyTo(KeyboardSettings settings);
    }
}
