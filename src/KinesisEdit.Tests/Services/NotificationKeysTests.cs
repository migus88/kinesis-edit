using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class NotificationKeysTests
    {
        [Fact]
        public void All_Always_IsEverySuppressionKeyOfTheCatalogInCatalogOrder()
        {
            var expected = new[]
            {
                "warn_unsaved_msg",
                "reset_layer_msg",
                "capture_summary_msg",
                "switch_variant_msg",
                "app_intro_msg",
                "saveas_msg",
                "save_msg",
                "multiplay_msg",
                "speed_msg",
                "copy_macro_msg",
                "reset_key_msg",
                "app_checkfirm_msg",
                "savelighting_msg",
                "savesettings_msg",
                "windowscombo_msg",
                "updownkeystroke_msg"
            };

            Assert.Equal(expected, NotificationKeys.All);
        }

        [Fact]
        public void All_Always_ExcludesTheDisplayPreference()
        {
            // advisory_detail stores "on" to mean EXPAND. A message box that carried it as a
            // suppression key would hide itself by turning a display preference on.
            Assert.DoesNotContain(SettingsKeys.AdvisoryDetail, NotificationKeys.All);
            Assert.Equal(
                AppPreferenceCatalog.All.Count - 1,
                NotificationKeys.All.Count);
        }

        [Fact]
        public void EveryConstant_Always_IsTheCoreSettingsKeyOfTheSameName()
        {
            // The constants here are aliases, not a second spelling of the key names.
            Assert.Equal(SettingsKeys.AppIntroMessage, NotificationKeys.AppIntro);
            Assert.Equal(SettingsKeys.SaveAsMessage, NotificationKeys.SaveAs);
            Assert.Equal(SettingsKeys.SaveMessage, NotificationKeys.Save);
            Assert.Equal(SettingsKeys.MultiplayMessage, NotificationKeys.Multiplay);
            Assert.Equal(SettingsKeys.SpeedMessage, NotificationKeys.Speed);
            Assert.Equal(SettingsKeys.CopyMacroMessage, NotificationKeys.CopyMacro);
            Assert.Equal(SettingsKeys.ResetKeyMessage, NotificationKeys.ResetKey);
            Assert.Equal(SettingsKeys.FirmwareCheckMessage, NotificationKeys.CheckFirmware);
            Assert.Equal(SettingsKeys.SaveLightingMessage, NotificationKeys.SaveLighting);
            Assert.Equal(SettingsKeys.SaveSettingsMessage, NotificationKeys.SaveSettings);
            Assert.Equal(SettingsKeys.WindowsCombinationMessage, NotificationKeys.WindowsCombo);
            Assert.Equal(SettingsKeys.UpDownKeystrokeMessage, NotificationKeys.UpDownKeystroke);
            Assert.Equal(SettingsKeys.UnsavedChangesMessage, NotificationKeys.UnsavedChanges);
            Assert.Equal(SettingsKeys.ResetLayerMessage, NotificationKeys.ResetLayer);
            Assert.Equal(SettingsKeys.CaptureSummaryMessage, NotificationKeys.CaptureSummary);
            Assert.Equal(SettingsKeys.SwitchVariantMessage, NotificationKeys.SwitchVariant);
        }

        [Fact]
        public void EveryKey_Always_HasASuppressionDescriptorInTheCatalog()
        {
            Assert.All(
                NotificationKeys.All,
                key =>
                {
                    var descriptor = AppPreferenceCatalog.Find(key);

                    Assert.NotNull(descriptor);
                    Assert.True(descriptor.IsSuppression);
                });
        }
    }
}
