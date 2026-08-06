using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    /// <summary>
    /// Pins the settings-panel strings of specs/08-settings.md §5 the way
    /// <c>ProfileSaveMessageCatalogTests</c> pins the post-save wording.
    /// </summary>
    public class SettingsMessageCatalogTests
    {
        [Fact]
        public void SettingsSavedMessage_Always_QuotesTheSpec51PostSaveDialog()
        {
            Assert.Equal("Settings Saved", SettingsMessageCatalog.SettingsSavedTitle);
            Assert.Equal(
                "Changes will be implemented when v-Drive is closed.",
                SettingsMessageCatalog.SettingsSavedMessage);
        }

        [Fact]
        public void Advantage2SettingsDisabledHint_Always_ExplainsTheMissingFourMegabyteMarker()
        {
            Assert.Contains("4MB", SettingsMessageCatalog.Advantage2SettingsDisabledHint);
            Assert.Contains("disabled", SettingsMessageCatalog.Advantage2SettingsDisabledHint);
        }
    }
}
