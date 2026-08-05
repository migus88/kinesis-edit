namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Which of the two action fields of the Assign Tap and Hold Action overlay
    /// (specs/11-feature-dialogs.md §11.1) is armed for the next captured keystroke. Exactly one
    /// field can be armed at a time, so a keypress can never land in both.
    /// </summary>
    public enum TapAndHoldField
    {
        /// <summary>Neither field is armed; the overlay wants no keystrokes.</summary>
        None = 0,

        /// <summary>The Tap Action field takes the next keystroke.</summary>
        Tap = 1,

        /// <summary>The Hold Action field takes the next keystroke.</summary>
        Hold = 2
    }
}
