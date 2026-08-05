namespace KinesisEdit.Services
{
    /// <summary>
    /// Which button closed a message box. Deliberately replaces the legacy Pascal encoding
    /// (specs/11-feature-dialogs.md §11.9: "the returned value is the modal result plus 100"):
    /// the suppression checkbox travels in <see cref="MessageBoxOutcome.SuppressRequested"/>
    /// instead of being folded into the result.
    /// </summary>
    public enum MessageBoxResult
    {
        /// <summary>The dialog was dismissed without a button (Escape, window close).</summary>
        None = 0,

        /// <summary>OK.</summary>
        Ok = 1,

        /// <summary>Cancel.</summary>
        Cancel = 2,

        /// <summary>Yes.</summary>
        Yes = 3,

        /// <summary>No.</summary>
        No = 4,

        /// <summary>A caller-supplied custom button; its id is in <see cref="MessageBoxOutcome.CustomButtonId"/>.</summary>
        Custom = 5
    }
}
