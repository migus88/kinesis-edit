namespace KinesisEdit.Services
{
    /// <summary>
    /// Opens the native open-file dialog the Import flow needs (specs/07-lighting.md §1.4: an
    /// arbitrary imported <c>.txt</c> is classified as a led file or tried as a layout).
    /// Implemented by the view layer; view models depend on this interface only, so the import
    /// flow — including its size and classification rules — stays testable without a UI toolkit.
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// Shows the <c>.txt</c> open dialog under <paramref name="title"/> and completes with
        /// the picked file already decoded, or null when the user cancelled — or when there is
        /// no window to own the dialog, which the caller treats the same way.
        /// </summary>
        Task<PickedFile?> PickTextFileAsync(string title);
    }
}
