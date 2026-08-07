namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One numbered profile the editor can be pointed at — a row of the toolbar's profile picker.
    /// A profile is a <c>layout&lt;n&gt;.txt</c> + <c>led&lt;n&gt;.txt</c> pair on the v-Drive
    /// (specs/03-vdrive-and-files.md §4.1/§4.3), i.e. the thing <c>SmartSet + &lt;n&gt;</c> switches
    /// on the board — <b>not</b> the power-on profile of the settings file, which is a slider on the
    /// Settings tab (docs/app/settings.md).
    /// <para>
    /// Immutable, and deliberately <b>not</b> a <see cref="ViewModelBase"/> — like
    /// <see cref="Advisories.AdvisoryViewModel"/>, an option is never edited. The set is built once
    /// from device facts, and which of them is open is
    /// <see cref="KeyboardEditorViewModel.SelectedProfile"/>'s business rather than a flag on each
    /// row: two places holding "the selected one" is how a picker starts disagreeing with the board
    /// it is pointing at.
    /// </para>
    /// </summary>
    public sealed class ProfileOptionViewModel
    {
        /// <summary>The profile's number, as it is spelled in its file names and on the keyboard.</summary>
        public int Number { get; }

        /// <summary>
        /// What the picker shows — <see cref="KeyboardEditorViewModel.BuildProfileCaption"/>'s
        /// wording rather than a second spelling of it. That caption used to be the toolbar's
        /// read-only <c>Profile n</c> label; the picker replaced it in issue #128, so this is now
        /// its only call site and the shared builder is what keeps it the same words the rest of
        /// the app uses for a profile.
        /// </summary>
        public string Caption { get; }

        /// <summary>
        /// Whether this profile is the device's non-programmable factory one
        /// (<see cref="Core.Devices.LayoutFileScheme.HasReadOnlyFactoryProfile"/> — the Advantage
        /// 360's profile 0, specs/02-devices.md "Profiles 0-9"). It has no file on the drive, so
        /// Core refuses to read it and the editor refuses to open it; the row states the rule
        /// rather than leaving the refusal unexplained.
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>Creates the option for <paramref name="number"/>.</summary>
        public ProfileOptionViewModel(int number, bool isReadOnly)
        {
            Number = number;
            Caption = KeyboardEditorViewModel.BuildProfileCaption(number);
            IsReadOnly = isReadOnly;
        }
    }
}
