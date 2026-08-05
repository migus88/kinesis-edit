namespace KinesisEdit.Services
{
    /// <summary>
    /// Dialog type of a message box, selecting its icon (specs/11-feature-dialogs.md §11.9:
    /// "Icon selected by dialog type: confirmation, warning, error, information").
    /// </summary>
    public enum MessageBoxIcon
    {
        /// <summary>No icon.</summary>
        None = 0,

        /// <summary>Informational message.</summary>
        Information = 1,

        /// <summary>Question awaiting a confirmation.</summary>
        Confirmation = 2,

        /// <summary>Warning.</summary>
        Warning = 3,

        /// <summary>Error.</summary>
        Error = 4
    }
}
