namespace KinesisEdit.Services
{
    /// <summary>
    /// The property names of the host-preferences file, written down once so the tolerant reader
    /// and the writer can never disagree about a spelling (settings.md invariant 9).
    /// <para>
    /// Reading matches them <b>case-insensitively</b> and writing emits exactly these spellings —
    /// the same "read tolerantly, write canonical" rule every file engine in this repo follows.
    /// </para>
    /// </summary>
    internal static class HostPreferencesJsonNames
    {
        /// <summary><see cref="AppThemePreference"/>, by enum name.</summary>
        public const string Theme = "theme";

        /// <summary><see cref="MotionPreference"/>, by enum name.</summary>
        public const string Motion = "motion";

        /// <summary>The object carrying <see cref="WindowGeometry"/>.</summary>
        public const string Window = "window";

        /// <summary><see cref="WindowGeometry.Width"/>, in DIPs.</summary>
        public const string Width = "width";

        /// <summary><see cref="WindowGeometry.Height"/>, in DIPs.</summary>
        public const string Height = "height";

        /// <summary><see cref="WindowGeometry.X"/>, in screen pixels; may be negative.</summary>
        public const string X = "x";

        /// <summary><see cref="WindowGeometry.Y"/>, in screen pixels; may be negative.</summary>
        public const string Y = "y";

        /// <summary><see cref="WindowGeometry.IsMaximized"/>.</summary>
        public const string Maximized = "maximized";
    }
}
