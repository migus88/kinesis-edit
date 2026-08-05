using KinesisEdit.Core.VDrive.Eject;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class VDriveEjectNotifierTests
    {
        [Fact]
        public async Task EjectAsync_OnSuccess_ShowsTheProgressAndCompletionNotices()
        {
            var notifier = CreateNotifier(out var ejectService, out var notifications);

            var succeeded = await notifier.EjectAsync("/Volumes/TKO");

            Assert.True(succeeded);
            Assert.Equal("/Volumes/TKO", Assert.Single(ejectService.EjectedPaths));
            Assert.Equal(
                new[] { "Disconnecting v-Drive", "Safe To Remove Hardware" },
                notifications.Toasts.Select(toast => toast.Message));
            Assert.Empty(notifications.MessageBoxes);
        }

        [Fact]
        public async Task EjectAsync_OnFailure_ShowsTheSpecFailureMessage()
        {
            var notifier = CreateNotifier(out var ejectService, out var notifications);
            ejectService.ResultToReturn = new VDriveEjectResult
            {
                Succeeded = false,
                Message = "busy"
            };

            var succeeded = await notifier.EjectAsync("/Volumes/TKO");

            Assert.False(succeeded);
            var messageBox = Assert.Single(notifications.MessageBoxes);
            Assert.Equal(
                "Cannot eject v-Drive — Close all open files and folders on the v-Drive, and try ejecting again.",
                messageBox.Message);
            Assert.Equal(MessageBoxIcon.Error, messageBox.Icon);
        }

        [Fact]
        public async Task EjectAsync_WhenEjectionIsUnsupported_DoesNothing()
        {
            var notifier = CreateNotifier(out var ejectService, out var notifications);
            ejectService.IsSupported = false;

            var succeeded = await notifier.EjectAsync("/Volumes/TKO");

            Assert.False(succeeded);
            Assert.Empty(ejectService.EjectedPaths);
            Assert.Empty(notifications.Toasts);
            Assert.Empty(notifications.MessageBoxes);
        }

        private static VDriveEjectNotifier CreateNotifier(out FakeDeviceEjectService ejectService, out FakeNotificationService notifications)
        {
            ejectService = new FakeDeviceEjectService();
            notifications = new FakeNotificationService();

            return new VDriveEjectNotifier(ejectService, notifications);
        }
    }
}
