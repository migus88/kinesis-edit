using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class AppSettingsSerializerTests
    {
        [Fact]
        public void Serialize_WithEmptySettings_ReturnsEmptyList()
        {
            var pairs = AppSettingsSerializer.Serialize(AppSettings.Empty);

            Assert.Empty(pairs);
        }

        [Fact]
        public void Serialize_WithAllFlagsSet_EmitsAllSeventeenKeysInOrderAsLowercase()
        {
            var settings = new AppSettings
            {
                IsAppIntroMessageHidden = true,
                IsSaveAsMessageHidden = false,
                IsSaveMessageHidden = true,
                IsMultiplayMessageHidden = false,
                IsSpeedMessageHidden = true,
                IsCopyMacroMessageHidden = false,
                IsResetKeyMessageHidden = true,
                IsFirmwareCheckMessageHidden = false,
                IsSaveLightingMessageHidden = true,
                IsSaveSettingsMessageHidden = false,
                IsWindowsCombinationMessageHidden = true,
                IsUpDownKeystrokeMessageHidden = false,
                IsUnsavedChangesWarningHidden = true,
                IsResetLayerConfirmationHidden = false,
                IsCaptureSummaryHidden = true,
                IsSwitchVariantConfirmationHidden = false,
                IsAdvisoryDetailExpanded = true,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("app_intro_msg", "on"),
                    KeyValuePair.Create("saveas_msg", "off"),
                    KeyValuePair.Create("save_msg", "on"),
                    KeyValuePair.Create("multiplay_msg", "off"),
                    KeyValuePair.Create("speed_msg", "on"),
                    KeyValuePair.Create("copy_macro_msg", "off"),
                    KeyValuePair.Create("reset_key_msg", "on"),
                    KeyValuePair.Create("app_checkfirm_msg", "off"),
                    KeyValuePair.Create("savelighting_msg", "on"),
                    KeyValuePair.Create("savesettings_msg", "off"),
                    KeyValuePair.Create("windowscombo_msg", "on"),
                    KeyValuePair.Create("updownkeystroke_msg", "off"),
                    KeyValuePair.Create("warn_unsaved_msg", "on"),
                    KeyValuePair.Create("reset_layer_msg", "off"),
                    KeyValuePair.Create("capture_summary_msg", "on"),
                    KeyValuePair.Create("switch_variant_msg", "off"),
                    KeyValuePair.Create("advisory_detail", "on"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithUnsetFlags_SkipsTheirKeys()
        {
            var settings = new AppSettings
            {
                IsSaveMessageHidden = true,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal([KeyValuePair.Create("save_msg", "on")], pairs);
        }

        [Fact]
        public void Serialize_WithOnlyTheNewKeysSet_SkipsTheSpecKeys()
        {
            var settings = new AppSettings
            {
                IsResetLayerConfirmationHidden = true,
                IsAdvisoryDetailExpanded = false,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("reset_layer_msg", "on"),
                    KeyValuePair.Create("advisory_detail", "off"),
                },
                pairs);
        }

        [Theory]
        [InlineData(true, "on")]
        [InlineData(false, "off")]
        public void Serialize_WithAdvisoryDetail_WritesExpandedAsOnWithNoInversion(bool expanded, string expectedValue)
        {
            // The suppression flags invert between the screen and the file; this one does not. A
            // serializer that "normalised" both onto one hide rule would write it backwards.
            var settings = new AppSettings { IsAdvisoryDetailExpanded = expanded };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal([KeyValuePair.Create("advisory_detail", expectedValue)], pairs);
        }

        [Theory]
        [InlineData(true, "on")]
        [InlineData(false, "off")]
        public void Serialize_WithAHideFlagOfThisApp_WritesHiddenAsOn(bool hidden, string expectedValue)
        {
            var settings = new AppSettings { IsResetLayerConfirmationHidden = hidden };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal([KeyValuePair.Create("reset_layer_msg", expectedValue)], pairs);
        }

        [Fact]
        public void Serialize_WithUnsetNewKeys_SkipsThemToo()
        {
            var pairs = AppSettingsSerializer.Serialize(AppSettings.Empty with { IsSaveMessageHidden = true });

            Assert.DoesNotContain(pairs, pair => pair.Key == "warn_unsaved_msg");
            Assert.DoesNotContain(pairs, pair => pair.Key == "reset_layer_msg");
            Assert.DoesNotContain(pairs, pair => pair.Key == "capture_summary_msg");
            Assert.DoesNotContain(pairs, pair => pair.Key == "switch_variant_msg");
            Assert.DoesNotContain(pairs, pair => pair.Key == "advisory_detail");
        }

        [Fact]
        public void Serialize_WithSetColors_EmitsOnlySetSlotsInFileForm()
        {
            var colors = new SettingsColor?[AppSettings.CustomColorCount];
            colors[0] = new SettingsColor(255, 0, 128);
            colors[9] = new SettingsColor(1, 2, 3);

            var settings = new AppSettings
            {
                CustomColors = colors,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("cust_color_1", "[255][0][128]"),
                    KeyValuePair.Create("cust_color_10", "[1][2][3]"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithUnsetColors_NeverEmitsEmptyColorKeys()
        {
            var pairs = AppSettingsSerializer.Serialize(AppSettings.Empty);

            Assert.DoesNotContain(pairs, pair => pair.Key.StartsWith("cust_color_", StringComparison.Ordinal));
        }

        [Fact]
        public void Serialize_WithNullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => AppSettingsSerializer.Serialize(null!));
        }

        [Fact]
        public void SerializeRemovals_WithEmptySettings_NamesAllTwelveColorSlots()
        {
            // "Skipped" and "cleared" are the same thing to Serialize; only the removal set can
            // tell the merge which slots must come off the file.
            var keys = AppSettingsSerializer.SerializeRemovals(AppSettings.Empty);

            Assert.Equal(
                new[]
                {
                    "cust_color_1",
                    "cust_color_2",
                    "cust_color_3",
                    "cust_color_4",
                    "cust_color_5",
                    "cust_color_6",
                    "cust_color_7",
                    "cust_color_8",
                    "cust_color_9",
                    "cust_color_10",
                    "cust_color_11",
                    "cust_color_12",
                },
                keys);
        }

        [Fact]
        public void SerializeRemovals_WithSetColors_NamesOnlyTheUnsetSlots()
        {
            var colors = new SettingsColor?[AppSettings.CustomColorCount];
            colors[0] = new SettingsColor(255, 0, 128);
            colors[9] = new SettingsColor(1, 2, 3);

            var settings = new AppSettings
            {
                CustomColors = colors,
            };

            var keys = AppSettingsSerializer.SerializeRemovals(settings);

            Assert.DoesNotContain("cust_color_1", keys);
            Assert.DoesNotContain("cust_color_10", keys);
            Assert.Equal(AppSettings.CustomColorCount - 2, keys.Count);
        }

        [Fact]
        public void SerializeRemovals_WithAllColorsSet_NamesNothing()
        {
            var colors = new SettingsColor?[AppSettings.CustomColorCount];

            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = new SettingsColor((byte)index, (byte)index, (byte)index);
            }

            var keys = AppSettingsSerializer.SerializeRemovals(new AppSettings { CustomColors = colors });

            Assert.Empty(keys);
        }

        [Fact]
        public void SerializeRemovals_WithUnsetFlags_NeverNamesAFlagKey()
        {
            // A null flag is "never answered", not "cleared" — AppPreferenceDescriptor.SetValue
            // always writes an explicit on/off. Deleting its key would throw away a line this app
            // never owned, possibly one the legacy Pascal app wrote.
            var keys = AppSettingsSerializer.SerializeRemovals(AppSettings.Empty);

            Assert.All(keys, key => Assert.StartsWith("cust_color_", key, StringComparison.Ordinal));
        }

        [Fact]
        public void SerializeRemovals_NeverNamesAKeyThatSerializeAlsoWrites()
        {
            var colors = new SettingsColor?[AppSettings.CustomColorCount];
            colors[4] = new SettingsColor(7, 7, 7);

            var settings = new AppSettings
            {
                IsSaveMessageHidden = true,
                CustomColors = colors,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);
            var keys = AppSettingsSerializer.SerializeRemovals(settings);

            Assert.Empty(pairs.Select(pair => pair.Key).Intersect(keys, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public void SerializeRemovals_WithNullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => AppSettingsSerializer.SerializeRemovals(null!));
        }
    }
}
