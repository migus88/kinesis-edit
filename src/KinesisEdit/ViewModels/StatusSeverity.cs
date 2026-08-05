namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// How a status reads to the user, so the view can color it. The legacy apps paint
    /// "Connected" lime and "Not Detected" / "Cannot Access" red (specs/10-apps-and-ui.md,
    /// "Detection loop") and use the same green/red pair for the "v-Drive OK" / "v-Drive Error"
    /// indicator. View models never expose brushes; XAML maps these values to the palette.
    /// </summary>
    public enum StatusSeverity
    {
        /// <summary>Nothing is known yet.</summary>
        Unknown = 0,

        /// <summary>Everything is working — rendered green.</summary>
        Ok = 1,

        /// <summary>Degraded but usable, e.g. demo mode.</summary>
        Warning = 2,

        /// <summary>Broken or unusable — rendered red.</summary>
        Error = 3
    }
}
