namespace KinesisEdit.Services
{
    /// <summary>
    /// Opens the native directory-selection dialog the Export flow needs
    /// (specs/11-feature-dialogs.md §11.5, "Behavior on Accept" step 1: the chosen directory is
    /// where the serialized layout and led files are written). Implemented by the view layer;
    /// view models depend on this interface only, so the export flow stays testable without a
    /// UI toolkit.
    /// </summary>
    public interface IFolderPickerService
    {
        /// <summary>
        /// Shows the directory picker under <paramref name="title"/> and completes with the
        /// chosen absolute path, or null when the user cancelled — or when there is no window to
        /// own the dialog, which the caller treats the same way.
        /// </summary>
        Task<string?> PickFolderAsync(string title);
    }
}
