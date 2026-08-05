namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One option of a <see cref="SettingsChoiceRowViewModel"/>: the value written to the file and
    /// the caption shown for it. A plain record, not a view model — it never changes, and the
    /// shell's <c>ViewLocator</c> must not try to resolve a view for it.
    /// </summary>
    public sealed record SettingsChoice
    {
        /// <summary>The value written to the settings file, e.g. <c>0</c>, <c>P</c> or <c>B</c> for <c>led_mode</c>.</summary>
        public required string Value { get; init; }

        /// <summary>What the option reads as in the picker.</summary>
        public required string Caption { get; init; }
    }
}
