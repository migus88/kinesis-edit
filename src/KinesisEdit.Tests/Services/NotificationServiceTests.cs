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
            var store = new FakeAppPreferencesStore();
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
            var service = CreateService(new FakeAppPreferencesStore(), out var presenter);
            var request = CreateSuppressibleRequest();

            var outcome = await service.ShowMessageBoxAsync(request);

            Assert.Same(request, Assert.Single(presenter.Requests));
            Assert.True(outcome.WasPresented);
        }

        [Fact]
        public async Task ShowMessageBoxAsync_WithoutSuppressionKey_AlwaysPresentsIt()
        {
            var store = new FakeAppPreferencesStore();
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
            var store = new FakeAppPreferencesStore();
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
        public async Task ShowMessageBoxAsync_WithASuppressionResult_PersistsOnlyThatAnswer()
        {
            // Issue #52's opt-out reads "always save on leaving", so the flag is a promise the Save
            // answer keeps and no other answer can.
            var store = new FakeAppPreferencesStore();
            var service = CreateService(store, out var presenter);
            presenter.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = MessageBoxResult.Yes,
                SuppressRequested = true
            };

            await service.ShowMessageBoxAsync(CreateAlwaysSaveRequest());

            Assert.Equal(KeyValuePair.Create(NotificationKeys.UnsavedChanges, true), Assert.Single(store.Writes));
            Assert.True(store.IsHidden(NotificationKeys.UnsavedChanges));
        }

        [Theory]
        [InlineData(MessageBoxResult.No)]
        [InlineData(MessageBoxResult.Cancel)]
        public async Task ShowMessageBoxAsync_WithASuppressionResult_PersistsNothingOnAnyOtherAnswer(
            MessageBoxResult answer)
        {
            // Discard is the other affirmative and Cancel is the way out; ticking the box beside
            // either must not arm auto-save, which would turn one "throw this away" into every
            // future one.
            var store = new FakeAppPreferencesStore();
            var service = CreateService(store, out var presenter);
            presenter.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = answer,
                SuppressRequested = true
            };

            await service.ShowMessageBoxAsync(CreateAlwaysSaveRequest());

            Assert.Empty(store.Writes);
            Assert.False(store.IsHidden(NotificationKeys.UnsavedChanges));
        }

        [Theory]
        [InlineData(MessageBoxResult.Yes)]
        [InlineData(MessageBoxResult.No)]
        [InlineData(MessageBoxResult.Cancel)]
        public async Task ShowMessageBoxAsync_WithoutASuppressionResult_PersistsOnAnyAnswer(
            MessageBoxResult answer)
        {
            // The behaviour every "Don't ask this again" box has always had, held in place: the
            // narrowing above is opt-in, so no existing caller changed.
            var store = new FakeAppPreferencesStore();
            var service = CreateService(store, out var presenter);
            presenter.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = answer,
                SuppressRequested = true
            };

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            Assert.Equal(KeyValuePair.Create(NotificationKeys.Save, true), Assert.Single(store.Writes));
        }

        [Fact]
        public async Task ShowMessageBoxAsync_InDemoMode_PersistsNothing()
        {
            var fileService = new FakeVDriveFileService();
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess);
            fileService.SetFile(VDriveAppPreferencesStore.GetFilePath(snapshot.Location!), "save_msg=off");
            var sessions = new DeviceSessionManager(TestDevices.CreateSettingsService(fileService));
            sessions.Begin(snapshot);
            var presenter = CreateSuppressingPresenter();
            var service = new NotificationService(presenter, sessions);

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            // Asserting on the store the session actually chose as well as on the file service:
            // an inverted policy would hand the session a writing store, and the fake records
            // attempted settings writes instead of throwing them away.
            Assert.IsType<ReadOnlyAppPreferencesStore>(sessions.Active!.SuppressionStore);
            Assert.Single(presenter.Requests);
            Assert.Empty(fileService.WrittenPaths);
            Assert.Empty(fileService.SettingsUpdates);
        }

        [Fact]
        public async Task ShowMessageBoxAsync_InALiveSession_PersistsToTheDevicesDrive()
        {
            var fileService = new FakeVDriveFileService();
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var settingsPath = VDriveAppPreferencesStore.GetFilePath(snapshot.Location!);
            fileService.SetFile(settingsPath, "save_msg=off");
            var sessions = new DeviceSessionManager(TestDevices.CreateSettingsService(fileService));
            sessions.Begin(snapshot);
            var presenter = CreateSuppressingPresenter();
            var service = new NotificationService(presenter, sessions);

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            Assert.IsType<VDriveAppPreferencesStore>(sessions.Active!.SuppressionStore);
            Assert.Equal(settingsPath, Assert.Single(fileService.WrittenPaths));
            Assert.Equal(KeyValuePair.Create(NotificationKeys.Save, "on"), Assert.Single(fileService.SettingsUpdates));
        }

        [Fact]
        public async Task ShowMessageBoxAsync_WithoutActiveSession_PresentsAndPersistsNothing()
        {
            var presenter = CreateSuppressingPresenter();
            var fileService = new FakeVDriveFileService();
            var service = new NotificationService(presenter, new DeviceSessionManager(TestDevices.CreateSettingsService(fileService)));

            await service.ShowMessageBoxAsync(CreateSuppressibleRequest());

            Assert.Single(presenter.Requests);
            Assert.Empty(fileService.WrittenPaths);
        }

        [Fact]
        public void ShowToast_WithoutTimeout_UsesTheFiveSecondDefault()
        {
            var service = CreateService(new FakeAppPreferencesStore(), out _);
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
            var service = CreateService(new FakeAppPreferencesStore(), out _);
            var captions = new List<string?>();
            service.LoadingChanged += caption => captions.Add(caption);

            service.ShowLoading();
            service.HideLoading();

            Assert.Equal(new string?[] { "Loading…", null }, captions);
            Assert.Null(service.LoadingCaption);
        }

        [Fact]
        public void ShowLoading_WithDeviceCaption_ReportsTheDeviceName()
        {
            var service = CreateService(new FakeAppPreferencesStore(), out _);

            service.ShowLoading(LoadingCaptions.ForDevice("TKO"));

            Assert.Equal("Loading TKO…", service.LoadingCaption);
        }

        private static NotificationService CreateService(IAppPreferencesStore store, out FakeMessageBoxPresenter presenter)
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

        /// <summary>
        /// The leave-with-unsaved modal's shape: an opt-out that arms auto-save, and so is recorded
        /// only when the answer beside it was Save.
        /// </summary>
        private static MessageBoxRequest CreateAlwaysSaveRequest()
        {
            return new MessageBoxRequest
            {
                Title = "Save changes before leaving?",
                Message = "You've edited 7 keys across 2 layers.",
                Buttons = MessageBoxButtons.YesNoCancel,
                SuppressionKey = NotificationKeys.UnsavedChanges,
                SuppressionCaption = "Don't ask again — always save on leaving",
                SuppressionResult = MessageBoxResult.Yes
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
