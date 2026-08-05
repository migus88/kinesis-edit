using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class NotificationServiceTests
    {
        [Fact]
        public async Task ShowMessageBoxAsync_WithHiddenNotification_DoesNotPresentIt()
        {
            var store = new FakeNotificationSuppressionStore();
            store.SetInitiallyHidden(NotificationKeys.Save);
            var service = CreateService(store, out var presenter);
            var request = CreateSuppressibleRequest();

            var outcome = await service.ShowMessageBoxAsync(request);

            Assert.Empty(presenter.Requests);
            Assert.Equal(request.SuppressedResult, outcome.Result);
            Assert.False(outcome.WasPresented);
        }

        [Fact]
        public async Task ShowMessageBoxAsync_WithShownNotification_PresentsIt()
        {
            var service = CreateService(new FakeNotificationSuppressionStore(), out var presenter);
            var request = CreateSuppressibleRequest();

            var outcome = await service.ShowMessageBoxAsync(request);

            Assert.Same(request, Assert.Single(presenter.Requests));
            Assert.True(outcome.WasPresented);
        }

        [Fact]
        public async Task ShowMessageBoxAsync_WithoutSuppressionKey_AlwaysPresentsIt()
        {
            var store = new FakeNotificationSuppressionStore();
            store.SetInitiallyHidden(NotificationKeys.Save);
            var service = CreateService(store, out var presenter);

            await service.ShowMessageBoxAsync(new MessageBoxRequest
            {
                Title = "Eject",
                Message = "Cannot eject"
            });

            Assert.Single(presenter.Requests);
            Assert.Empty(store.Writes);
        }

        [Fact]
        public async Task ShowMessageBoxAsync_WhenUserAsksToHide_PersistsTheSuppression()
        {
            var store = new FakeNotificationSuppressionStore();
            var service = CreateService(store, out var presenter);
            presenter.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = MessageBoxResult.Ok,
                SuppressRequested = true
            };

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            Assert.Equal(KeyValuePair.Create(NotificationKeys.Save, true), Assert.Single(store.Writes));
            Assert.True(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public async Task ShowMessageBoxAsync_InDemoMode_PersistsNothing()
        {
            var fileService = new FakeVDriveFileService();
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess);
            fileService.SetFile(VDriveNotificationSuppressionStore.GetFilePath(snapshot.Location!), "save_msg=off");
            var sessions = new DeviceSessionManager(fileService);
            sessions.Begin(snapshot);
            var presenter = CreateSuppressingPresenter();
            var service = new NotificationService(presenter, sessions);

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            // Asserting on the store the session actually chose as well as on the file service:
            // an inverted policy would hand the session a writing store, and the fake records
            // attempted settings writes instead of throwing them away.
            Assert.IsType<ReadOnlyNotificationSuppressionStore>(sessions.Active!.SuppressionStore);
            Assert.Single(presenter.Requests);
            Assert.Empty(fileService.WrittenPaths);
            Assert.Empty(fileService.SettingsUpdates);
        }

        [Fact]
        public async Task ShowMessageBoxAsync_InALiveSession_PersistsToTheDevicesDrive()
        {
            var fileService = new FakeVDriveFileService();
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var settingsPath = VDriveNotificationSuppressionStore.GetFilePath(snapshot.Location!);
            fileService.SetFile(settingsPath, "save_msg=off");
            var sessions = new DeviceSessionManager(fileService);
            sessions.Begin(snapshot);
            var presenter = CreateSuppressingPresenter();
            var service = new NotificationService(presenter, sessions);

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            Assert.IsType<VDriveNotificationSuppressionStore>(sessions.Active!.SuppressionStore);
            Assert.Equal(settingsPath, Assert.Single(fileService.WrittenPaths));
            Assert.Equal(KeyValuePair.Create(NotificationKeys.Save, "on"), Assert.Single(fileService.SettingsUpdates));
        }

        [Fact]
        public async Task ShowMessageBoxAsync_WithoutActiveSession_PresentsAndPersistsNothing()
        {
            var presenter = CreateSuppressingPresenter();
            var fileService = new FakeVDriveFileService();
            var service = new NotificationService(presenter, new DeviceSessionManager(fileService));

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            Assert.Single(presenter.Requests);
            Assert.Empty(fileService.WrittenPaths);
        }

        [Fact]
        public void ShowToast_WithoutTimeout_UsesTheFiveSecondDefault()
        {
            var service = CreateService(new FakeNotificationSuppressionStore(), out _);
            var toasts = new List<ToastRequest>();
            service.ToastRequested += toast => toasts.Add(toast);

            service.ShowToast(new ToastRequest
            {
                Message = VDriveEjectNotifier.SafeToRemoveMessage
            });

            var toast = Assert.Single(toasts);
            Assert.Equal(TimeSpan.FromSeconds(5), toast.Timeout);
            Assert.Equal(VDriveEjectNotifier.SafeToRemoveMessage, toast.Message);
        }

        [Fact]
        public void ShowLoading_WithoutCaption_UsesTheDefaultCaption()
        {
            var service = CreateService(new FakeNotificationSuppressionStore(), out _);
            var captions = new List<string?>();
            service.LoadingChanged += caption => captions.Add(caption);

            service.ShowLoading();
            service.HideLoading();

            Assert.Equal(new string?[] { "Loading...", null }, captions);
            Assert.Null(service.LoadingCaption);
        }

        [Fact]
        public void ShowLoading_WithDeviceCaption_ReportsTheDeviceName()
        {
            var service = CreateService(new FakeNotificationSuppressionStore(), out _);

            service.ShowLoading(LoadingCaptions.ForDevice("TKO"));

            Assert.Equal("Loading TKO...", service.LoadingCaption);
        }

        private static NotificationService CreateService(INotificationSuppressionStore store, out FakeMessageBoxPresenter presenter)
        {
            presenter = new FakeMessageBoxPresenter();
            var accessor = new StubSessionAccessor(new DeviceSession(TestDevices.CreateSnapshot(DeviceId.Tko), store));

            return new NotificationService(presenter, accessor);
        }

        private static FakeMessageBoxPresenter CreateSuppressingPresenter()
        {
            return new FakeMessageBoxPresenter
            {
                OutcomeToReturn = new MessageBoxOutcome
                {
                    Result = MessageBoxResult.Ok,
                    SuppressRequested = true
                }
            };
        }

        private static MessageBoxRequest CreateSuppressibleRequest()
        {
            return new MessageBoxRequest
            {
                Title = "Save",
                Message = "Profile 1 Saved",
                SuppressionKey = NotificationKeys.Save
            };
        }

        private sealed class StubSessionAccessor : IDeviceSessionAccessor
        {
            public DeviceSession? Active { get; }

            public StubSessionAccessor(DeviceSession? active)
            {
                Active = active;
            }
        }
    }
}
