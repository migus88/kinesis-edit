using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One segment of the Settings screen's motion switch: the preference the segment stands for and
    /// the word printed on it.
    /// <para>
    /// A plain record, not a <see cref="ViewModelBase"/> — see <see cref="AppThemeOption"/> for why.
    /// </para>
    /// </summary>
    public sealed record MotionOption
    {
        /// <summary>The preference this segment writes to <see cref="HostPreferences.Motion"/>.</summary>
        public required MotionPreference Value { get; init; }

        /// <summary>What the segment reads as.</summary>
        public required string Caption { get; init; }
    }
}
