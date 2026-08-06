using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One check box in the settings screen's "App &amp; notifications" list: an
    /// <see cref="AppPreferenceDescriptor"/>, the value the user sees, and the callback that
    /// persists a change.
    /// <para>
    /// <b>It never touches the stored flag.</b> <see cref="IsChecked"/> is the option as the user
    /// states it, and the descriptor's <c>GetValue</c>/<c>SetValue</c> apply
    /// <see cref="AppPreferencePolarity"/> on the way to and from <c>app_settings.txt</c>. That is
    /// what makes one row type correct for both conventions in that file: for the sixteen
    /// <c>*_msg</c> keys a tick is the <i>absence</i> of the flag ("ask me"), and for
    /// <c>advisory_detail</c> a tick <i>is</i> the flag. A row that read the property directly
    /// would be inverted for one of the two, and every test about it would still pass.
    /// </para>
    /// </summary>
    public sealed class AppPreferenceRowViewModel : ViewModelBase
    {
        /// <summary>The catalog entry this row renders.</summary>
        public AppPreferenceDescriptor Descriptor { get; }

        /// <summary>The <c>app_settings.txt</c> key, for tests and diagnostics rather than the screen.</summary>
        public string Key => Descriptor.Key;

        /// <summary>The option as the settings screen states it.</summary>
        public string Caption => Descriptor.Caption;

        /// <summary>One sentence saying which prompt or behaviour the option governs.</summary>
        public string Description => Descriptor.Description;

        /// <summary>
        /// The check box. Setting it persists through the store; setting it from
        /// <see cref="SetFromStore"/> does not, which is what stops a refresh writing the value it
        /// just read straight back.
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (!SetProperty(ref _isChecked, value) || _isSynchronizing)
                {
                    return;
                }

                _persist(Descriptor, value);
            }
        }

        private readonly Action<AppPreferenceDescriptor, bool> _persist;
        private bool _isChecked;
        private bool _isSynchronizing;

        /// <summary>
        /// Creates the row for <paramref name="descriptor"/>, resting at its
        /// <see cref="AppPreferenceDescriptor.DefaultValue"/> until the file has been read.
        /// <paramref name="persist"/> is called with the user-facing value whenever the user moves
        /// the box.
        /// </summary>
        public AppPreferenceRowViewModel(
            AppPreferenceDescriptor descriptor,
            Action<AppPreferenceDescriptor, bool> persist)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));

            _isChecked = descriptor.DefaultValue;
        }

        /// <summary>
        /// Sets the box from <paramref name="value"/> as the file reports it, without writing
        /// anything back.
        /// </summary>
        public void SetFromStore(bool value)
        {
            _isSynchronizing = true;

            try
            {
                IsChecked = value;
            }
            finally
            {
                _isSynchronizing = false;
            }
        }
    }
}
