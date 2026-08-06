using KinesisEdit.Core.Devices;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The dashboard card for a board this app does not edit (docs/design/mockups.md §1b): the
    /// Advantage 360 Professional, configured in Kinesis' ZMK web GUI.
    /// </summary>
    public class WebToolCardViewModelTests
    {
        [Fact]
        public void WebToolDevices_IsExactlyTheBoardWithNoSmartSetSupportAndAWebConfigurator()
        {
            var device = Assert.Single(WebToolCardViewModel.WebToolDevices());

            Assert.Equal(DeviceId.Advantage360Professional, device.Id);
            Assert.False(device.IsProgrammable);
            Assert.Empty(device.VolumeLabels);
        }

        [Fact]
        public void Card_CarriesTheMockupsCopy()
        {
            var card = CreateCard(out _);

            Assert.Equal("Advantage 360 Professional", card.DisplayName);
            Assert.Equal(
                "Configured in Kinesis' web tool — KinesisEdit doesn't edit this board.",
                card.Description);
            Assert.Equal("Open web tool ↗", card.OpenWebToolCaption);
        }

        [Fact]
        public void MetaLine_ComesFromTheSameCatalogComposerAsEveryOtherCard()
        {
            var device = DeviceCatalog.GetById(DeviceId.Advantage360Professional);
            var card = CreateCard(out _);

            Assert.Equal(DeviceMetaLine.Describe(device), card.MetaLine);
        }

        [Fact]
        public void Key_IsTheCatalogId()
        {
            var card = CreateCard(out _);

            Assert.Equal(DeviceId.Advantage360Professional.ToString(), card.Key);
        }

        [Fact]
        public void OpenWebToolCommand_WhenExecuted_LaunchesTheConfigurationUrl()
        {
            var card = CreateCard(out var urlLauncher);

            Assert.True(card.OpenWebToolCommand.CanExecute(null));

            card.OpenWebToolCommand.Execute(null);

            Assert.Equal(card.ConfigurationUrl, Assert.Single(urlLauncher.OpenedUrls));
            Assert.Equal(
                "https://kinesiscorporation.github.io/Adv360-Pro-GUI/",
                Assert.Single(urlLauncher.OpenedUrls));
        }

        [Fact]
        public void OpenWebToolCommand_WithoutAConfigurationUrl_IsUnavailableAndLaunchesNothing()
        {
            var urlLauncher = new FakeUrlLauncher();
            var device = DeviceCatalog.GetById(DeviceId.Advantage360Professional) with { ConfigurationUrl = null };
            var card = new WebToolCardViewModel(device, urlLauncher);

            Assert.False(card.OpenWebToolCommand.CanExecute(null));

            card.OpenWebToolCommand.Execute(null);

            Assert.Empty(urlLauncher.OpenedUrls);
        }

        [Fact]
        public void Card_IsADashboardCardButNotADeviceCard()
        {
            // The dashboard's HasDevices counts device cards only; if this type were one, the
            // troubleshoot empty state of issue #89 could never render again.
            var card = CreateCard(out _);

            Assert.IsAssignableFrom<DashboardCardViewModel>(card);
            Assert.IsNotType<DeviceCardViewModel>(card);
        }

        private static WebToolCardViewModel CreateCard(out FakeUrlLauncher urlLauncher)
        {
            urlLauncher = new FakeUrlLauncher();

            return new WebToolCardViewModel(
                DeviceCatalog.GetById(DeviceId.Advantage360Professional),
                urlLauncher);
        }
    }
}
