using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The view layer's <see cref="IFilePickerService"/>, over <c>TopLevel.StorageProvider</c>.
    /// Deliberately thin: the dialog call is all that is here, and every decoding rule lives in
    /// <see cref="PickedFileReader"/>, which is unit-testable on its own.
    /// </summary>
    public sealed class AvaloniaFilePickerService : IFilePickerService
    {
        /// <summary>
        /// The filter of specs/07-lighting.md §1.4's "arbitrary imported .txt". The Apple and
        /// MIME identifiers are carried alongside the glob because macOS and the Linux portal
        /// filter on those rather than on the pattern.
        /// </summary>
        private static readonly FilePickerFileType TextFileType = new("Text files")
        {
            Patterns = ["*.txt"],
            AppleUniformTypeIdentifiers = ["public.plain-text"],
            MimeTypes = ["text/plain"]
        };

        private readonly Func<Window?> _ownerAccessor;

        /// <summary>Creates the picker; <paramref name="ownerAccessor"/> returns the window the dialog opens over, or null when there is none yet.</summary>
        public AvaloniaFilePickerService(Func<Window?> ownerAccessor)
        {
            _ownerAccessor = ownerAccessor ?? throw new ArgumentNullException(nameof(ownerAccessor));
        }

        /// <summary>
        /// Opens the native <c>.txt</c> picker and completes with the file decoded by
        /// <see cref="PickedFileReader"/>, or null on cancel. The file is read whole (up to the
        /// reader's cap) and its true length reported; the 50 KB import maximum is the caller's
        /// to enforce.
        /// </summary>
        public async Task<PickedFile?> PickTextFileAsync(string title)
        {
            var storageProvider = StorageProviderHost.Resolve(_ownerAccessor);

            if (storageProvider is null)
            {
                return null;
            }

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [TextFileType]
            }).ConfigureAwait(true);

            if (files.Count == 0)
            {
                return null;
            }

            var file = files[0];

            // Read through the storage item's own stream rather than through the local path: a
            // pick may have no local path at all (sandboxed and portal-backed providers), and
            // the stream is the one thing every provider offers.
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);

            return await PickedFileReader
                .ReadAsync(file.Name, file.TryGetLocalPath(), stream)
                .ConfigureAwait(true);
        }
    }
}
