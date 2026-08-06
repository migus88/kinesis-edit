using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The one way this app reaches a native file dialog: through the storage provider of the
    /// window that owns it. The owner is resolved lazily through a callback for the same reason
    /// <see cref="MessageBoxPresenter"/> does it — services are wired before the shell window
    /// exists (docs/app/app-shell.md, "Composition root"). Shared by
    /// <see cref="AvaloniaFolderPickerService"/> and <see cref="AvaloniaFilePickerService"/> so
    /// the ownerless fallback cannot drift between them.
    /// </summary>
    internal static class StorageProviderHost
    {
        /// <summary>
        /// The storage provider of the current owner window, or null when there is no owner yet
        /// and therefore no dialog to open — the pickers report that as a cancel.
        /// </summary>
        public static IStorageProvider? Resolve(Func<Window?> ownerAccessor)
        {
            ArgumentNullException.ThrowIfNull(ownerAccessor);

            var owner = ownerAccessor();

            if (owner is null)
            {
                return null;
            }

            return TopLevel.GetTopLevel(owner)?.StorageProvider;
        }
    }
}
