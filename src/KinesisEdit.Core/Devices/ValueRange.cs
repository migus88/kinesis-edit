namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// An inclusive integer range plus the value that applies when the setting is absent from the
    /// file. Carries the macro playback-speed and repeat settings of specs/06-macros.md §4 and the
    /// tap-and-hold timing delay of specs/11-feature-dialogs.md §11.1. Pure data — clamping and
    /// validation of parsed values belong to the parsers.
    /// </summary>
    /// <param name="Minimum">Lowest accepted value, inclusive.</param>
    /// <param name="Maximum">Highest accepted value, inclusive.</param>
    /// <param name="Default">Value applied when the file carries no explicit value.</param>
    public sealed record ValueRange(int Minimum, int Maximum, int Default)
    {
        /// <summary>Whether <paramref name="value"/> lies inside the inclusive range.</summary>
        public bool Contains(int value)
        {
            return value >= Minimum && value <= Maximum;
        }
    }
}
