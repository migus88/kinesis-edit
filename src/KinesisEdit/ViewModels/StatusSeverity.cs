namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// How a status reads to the user, so the view can color it. The legacy apps paint
    /// "Connected" lime and "Not Detected" / "Cannot Access" red (specs/10-apps-and-ui.md,
    /// "Detection loop") and use the same green/red pair for the "v-Drive OK" / "v-Drive Error"
    /// indicator. View models never expose brushes; XAML maps these values to the palette.
    /// <para>
    /// The redesign fixes the vocabulary at four states, each with one meaning it never shares
    /// (docs/design/, mockup 1a): green is connected and writable, red is gone or unwritable,
    /// purple is demo mode — nothing is written — and amber is an advisory that never blocks.
    /// Demo mode is therefore <see cref="Demo"/> and not <see cref="Warning"/>: it is not a
    /// degraded version of working, it is a different thing the app is doing, and the design law
    /// is that "amber is the *only* warning color".
    /// </para>
    /// </summary>
    public enum StatusSeverity
    {
        /// <summary>Nothing is known yet.</summary>
        Unknown = 0,

        /// <summary>Everything is working — rendered green.</summary>
        Ok = 1,

        /// <summary>An advisory: over a device limit, say. Rendered amber, and never blocking.</summary>
        Warning = 2,

        /// <summary>Broken or unusable — rendered red.</summary>
        Error = 3,

        /// <summary>No drive is being written to — demo mode. Rendered purple.</summary>
        Demo = 4
    }
}
