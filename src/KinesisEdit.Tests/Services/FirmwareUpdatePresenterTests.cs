using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The guard clauses of the update dialog's host (specs/09-firmware.md §3, §4) — everything
    /// that can be exercised without a UI runtime. Opening the window itself needs Avalonia and is
    /// covered through <see cref="KinesisEdit.ViewModels.FirmwareUpdateViewModel"/> instead.
    /// </summary>
    public class FirmwareUpdatePresenterTests
    {
        private readonly FakeVersionManifestClient _manifestClient = new();
        private readonly FakeAppVersionProvider _appVersion = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Constructor_WithoutAnOwnerAccessor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FirmwareUpdatePresenter(
                null!,
                _manifestClient,
                _appVersion,
                _notifications,
                _urlLauncher,
                UpdateCheckPlatform.MacOs));
        }

        [Fact]
        public void Constructor_WithoutAManifestClient_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FirmwareUpdatePresenter(
                () => null,
                null!,
                _appVersion,
                _notifications,
                _urlLauncher,
                UpdateCheckPlatform.MacOs));
        }

        [Fact]
        public void Constructor_WithoutAnAppVersionProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FirmwareUpdatePresenter(
                () => null,
                _manifestClient,
                null!,
                _notifications,
                _urlLauncher,
                UpdateCheckPlatform.MacOs));
        }

        [Fact]
        public void Constructor_WithoutANotificationService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FirmwareUpdatePresenter(
                () => null,
                _manifestClient,
                _appVersion,
                null!,
                _urlLauncher,
                UpdateCheckPlatform.MacOs));
        }

        [Fact]
        public void Constructor_WithoutAUrlLauncher_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FirmwareUpdatePresenter(
                () => null,
                _manifestClient,
                _appVersion,
                _notifications,
                null!,
                UpdateCheckPlatform.MacOs));
        }

        [Fact]
        public async Task PresentAsync_WithoutADevice_ThrowsBeforeOpeningAWindow()
        {
            var presenter = CreatePresenter();

            await Assert.ThrowsAsync<ArgumentNullException>(() => presenter.PresentAsync(null!));
        }

        [Fact]
        public async Task PresentAsync_ForADeviceWithoutAnUpdateDialog_OpensNothing()
        {
            // §4: the Advantage2's app has no update dialog. Its window would carry no rows at all,
            // and the check behind it would still spend a request — so the presenter refuses. The
            // completed task (no UI runtime was ever needed) is the proof no window was built.
            var presenter = CreatePresenter();

            await presenter.PresentAsync(TestDevices.CreateSnapshot(
                DeviceId.Advantage2,
                versionFile: TestDevices.CreateVersionFile(DeviceId.Advantage2)));

            Assert.Empty(_manifestClient.RequestedUrls);
            Assert.Empty(_notifications.MessageBoxes);
        }

        private FirmwareUpdatePresenter CreatePresenter()
        {
            return new FirmwareUpdatePresenter(
                () => null,
                _manifestClient,
                _appVersion,
                _notifications,
                _urlLauncher,
                UpdateCheckPlatform.MacOs);
        }
    }
}
