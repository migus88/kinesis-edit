namespace KinesisEdit.Services
{
    /// <summary>
    /// The standard button set of a message box (specs/11-feature-dialogs.md §11.9: buttons are
    /// created from the standard set Yes/No/OK/Cancel and/or custom buttons).
    /// </summary>
    public enum MessageBoxButtons
    {
        /// <summary>Only custom buttons are shown.</summary>
        None = 0,

        /// <summary>A single OK button.</summary>
        Ok = 1,

        /// <summary>OK and Cancel.</summary>
        OkCancel = 2,

        /// <summary>Yes and No.</summary>
        YesNo = 3,

        /// <summary>Yes, No, and Cancel.</summary>
        YesNoCancel = 4
    }
}
