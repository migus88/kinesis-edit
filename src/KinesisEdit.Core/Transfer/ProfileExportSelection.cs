namespace KinesisEdit.Core.Transfer
{
    /// <summary>
    /// The three mutually exclusive choices of the Export files dialog
    /// (specs/11-feature-dialogs.md §11.5). <see cref="LayoutAndLighting"/> is the dialog's
    /// default.
    /// </summary>
    public enum ProfileExportSelection
    {
        /// <summary>Export both files — the dialog's <c>'Layout and Lighting'</c> checkbox.</summary>
        LayoutAndLighting = 0,

        /// <summary>Export the layout file only — <c>'Layout only'</c>.</summary>
        LayoutOnly = 1,

        /// <summary>Export the led file only — <c>'Lighting only'</c>.</summary>
        LightingOnly = 2
    }
}
