namespace KinesisEdit.Services
{
    /// <summary>
    /// Where a toast is shown (specs/11-feature-dialogs.md §11.9: an info dialog "can be centered
    /// or positioned in the bottom-right of the primary monitor's work area"). This app is a
    /// single window, so the placement is relative to the window rather than to the monitor.
    /// </summary>
    public enum ToastPosition
    {
        /// <summary>Bottom-right corner — the placement used for the eject progress notices.</summary>
        BottomRight = 0,

        /// <summary>Centered over the content.</summary>
        Center = 1
    }
}
