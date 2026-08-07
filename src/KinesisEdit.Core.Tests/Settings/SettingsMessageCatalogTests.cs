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

        [Fact]
        public void ReadOnlyStrings_Always_QuoteMockup1jVerbatim()
        {
            Assert.Equal(
                "This Advantage2 has 2 MB firmware — device settings can't be written to it",
                SettingsMessageCatalog.Advantage2ReadOnlyBanner);
            Assert.Equal(
                "The board reports 2MB. Its settings file is read-only in firmware, so the "
                + "controls below show what the keyboard is doing but can't change it. Remaps, "
                + "macros, and layers all still save normally.",
                SettingsMessageCatalog.Advantage2ReadOnlyExplanation);
            Assert.Equal("(read-only)", SettingsMessageCatalog.ReadOnlyRowMarker);
        }

        [Fact]
        public void WhichBoardLinkCaption_Always_LeavesTheArrowToTheIconSystem()
        {
            // Mockup 1j writes "Which board do I have? ↗"; the arrow is IconExternalLink geometry
            // beside the words, exactly as the empty state draws "Troubleshooting tips".
            Assert.Equal("Which board do I have?", SettingsMessageCatalog.WhichBoardLinkCaption);
            Assert.DoesNotContain("↗", SettingsMessageCatalog.WhichBoardLinkCaption);
        }

        [Fact]
        public void DemoModePreferencesCaveat_Always_QuotesMockup1jVerbatim()
        {
            Assert.Equal(
                "In Demo Mode these preferences are readable but never written — toggles snap "
                + "back when the session ends.",
                SettingsMessageCatalog.DemoModePreferencesCaveat);
        }
    }
}
