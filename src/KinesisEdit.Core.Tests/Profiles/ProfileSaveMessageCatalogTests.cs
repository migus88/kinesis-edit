using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// The post-save wording of <see cref="ProfileSaveMessageCatalog"/>, quoted verbatim from
    /// specs/03-vdrive-and-files.md §5.3, specs/07-lighting.md §1.3, and specs/10-apps-and-ui.md,
    /// one device family at a time.
    /// </summary>
    public sealed class ProfileSaveMessageCatalogTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        public void GetMessage_ForFreestyleFamily_IsTheSameRegardlessOfStartupProfile(DeviceId device)
        {
            const string expected =
                "…use the Refresh Shortcut (SmartSet + Layout) or simply close the v-Drive (SmartSet + F8). "
                + "To load this layout to the keyboard press SmartSet + 3.";

            Assert.Equal(expected, ProfileSaveMessageCatalog.GetMessage(device, 3, isStartupProfile: true));
            Assert.Equal(expected, ProfileSaveMessageCatalog.GetMessage(device, 3, isStartupProfile: false));
        }

        [Fact]
        public void GetMessage_ForRgbStartupProfile_NamesTheDriveAndTheF8Shortcut()
        {
            Assert.Equal(
                "Use the Refresh Shortcut (SmartSet + Profile) to preview your Layout and Lighting updates "
                + "or simply Eject the \"FS EDGE RGB\" drive in File Explorer and then disconnect the v-Drive (SmartSet + F8).",
                ProfileSaveMessageCatalog.GetMessage(DeviceId.FreestyleEdgeRgb, 4, isStartupProfile: true));
        }

        [Fact]
        public void GetMessage_ForRgbNonStartupProfile_NamesTheProfileNumberTwice()
        {
            Assert.Equal(
                "To load Profile 4 to the keyboard, hold the SmartSet key and tap the 4 key.",
                ProfileSaveMessageCatalog.GetMessage(DeviceId.FreestyleEdgeRgb, 4, isStartupProfile: false));
        }

        [Fact]
        public void GetMessage_ForTkoStartupProfile_UsesTheRightShiftShortcuts()
        {
            Assert.Equal(
                "Use the Refresh Shortcut (SmartSet + Right Shift + B) to preview your Layout and Lighting updates "
                + "or simply Eject the \"TKO\" drive in File Explorer and then disconnect the v-Drive (SmartSet + Right Shift + V).",
                ProfileSaveMessageCatalog.GetMessage(DeviceId.Tko, 2, isStartupProfile: true));
        }

        [Fact]
        public void GetMessage_ForTkoNonStartupProfile_UsesTheRightShiftLoadWording()
        {
            Assert.Equal(
                "To load Profile 2 to the keyboard, hold the SmartSet key + Right Shift and tap the 2 key.",
                ProfileSaveMessageCatalog.GetMessage(DeviceId.Tko, 2, isStartupProfile: false));
        }

        [Fact]
        public void GetMessage_ForAdvantage360StartupProfile_UsesTheRefreshShortcut()
        {
            Assert.Equal(
                "Use the Refresh Shortcut (SmartSet + 'Refresh')…",
                ProfileSaveMessageCatalog.GetMessage(DeviceId.Advantage360, 6, isStartupProfile: true));
        }

        [Fact]
        public void GetMessage_ForAdvantage360NonStartupProfile_NamesTheProfileNumberTwice()
        {
            Assert.Equal(
                "To load Profile 6…, hold the SmartSet key and tap the 6 key.",
                ProfileSaveMessageCatalog.GetMessage(DeviceId.Advantage360, 6, isStartupProfile: false));
        }
    }
}
