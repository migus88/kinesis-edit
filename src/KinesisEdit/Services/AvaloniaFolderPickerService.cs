using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The view layer's <see cref="IFolderPickerService"/>, over <c>TopLevel.StorageProvider</c>.
    /// Deliberately thin: everything testable about a pick lives in its caller, because the
    /// platform dialog cannot be driven from a unit test.
    /// </summary>
    public sealed class AvaloniaFolderPickerService : IFolderPickerService
    {
        private readonly Func<Window?> _ownerAccessor;

        /// <summary>Creates the picker; <paramref name="ownerAccessor"/> returns the window the dialog opens over, or null when there is none yet.</summary>
        public AvaloniaFolderPickerService(Func<Window?> ownerAccessor)
        {
            _ownerAccessor = ownerAccessor ?? throw new ArgumentNullException(nameof(ownerAccessor));
        }

        /// <summary>
        /// Opens the native directory picker and completes with the chosen absolute path, or
        /// null on cancel — and equally on a folder the platform exposes no local path for, which
        /// is nothing this app could write export files into.
        /// </summary>
        public async Task<string?> PickFolderAsync(string title)
        {
            var storageProvider = StorageProviderHost.Resolve(_ownerAccessor);

            if (storageProvider is null)
            {
                return null;
            }

            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            }).ConfigureAwait(true);

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
    }
}
