using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One segment of the Settings screen's theme switch: the preference the segment stands for and
    /// the word printed on it.
    /// <para>
    /// A plain record, not a <see cref="ViewModelBase"/> — it never changes, and the shell's
    /// <c>ViewLocator</c> must not try to resolve a view for it. Same shape and same reason as
    /// <see cref="SettingsChoice"/>.
    /// </para>
    /// </summary>
    public sealed record AppThemeOption
    {
        /// <summary>The preference this segment writes to <see cref="HostPreferences.Theme"/>.</summary>
        public required AppThemePreference Value { get; init; }

        /// <summary>What the segment reads as.</summary>
        public required string Caption { get; init; }
    }
}
