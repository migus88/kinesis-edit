using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The export flow's directory picker (specs/11-feature-dialogs.md §11.5). Only the
    /// ownerless path is unit-testable — the native dialog itself needs a running Avalonia
    /// application and is hand-verified.
    /// </summary>
    public sealed class AvaloniaFolderPickerServiceTests
    {
        [Fact]
        public void Constructor_WithoutAnOwnerAccessor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AvaloniaFolderPickerService(null!));
        }

        [Fact]
        public async Task PickFolderAsync_WithoutAnOwnerWindow_ReportsACancel()
        {
            // The pickers are wired before the shell window exists (docs/app/app-shell.md), so
            // an owner-less pick must report a cancel rather than throw.
            var picker = new AvaloniaFolderPickerService(() => null);

            Assert.Null(await picker.PickFolderAsync("Export files"));
        }
    }
}
