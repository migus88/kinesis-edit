using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The catalog is the single source for what a preference is called, how it reads and which
    /// way round it is stored, so these tests pin the shape the settings screen and the preference
    /// store both build on — the ordering it renders in, and above all the two polarities, since
    /// getting one of them backwards ships an inverted checkbox that no other test would catch.
    /// </summary>
    public class AppPreferenceCatalogTests
    {
        [Fact]
        public void All_Always_HasSeventeenDescriptorsFeaturedFirst()
        {
            Assert.Equal(17, AppPreferenceCatalog.All.Count);
            Assert.Equal(5, AppPreferenceCatalog.Featured.Count);
            Assert.Equal(12, AppPreferenceCatalog.Additional.Count);
            Assert.Equal(
                AppPreferenceCatalog.All,
                [.. AppPreferenceCatalog.Featured, .. AppPreferenceCatalog.Additional]);
        }

        [Fact]
        public void Featured_Always_IsMockup1jsFivePreferencesInMockupOrder()
        {
            var expected = new[]
            {
                "warn_unsaved_msg",
                "reset_layer_msg",
                "capture_summary_msg",
                "switch_variant_msg",
                "advisory_detail"
            };

            Assert.Equal(expected, AppPreferenceCatalog.Featured.Select(descriptor => descriptor.Key));
        }

        [Fact]
        public void Featured_Always_CarriesMockup1jsCaptionsVerbatim()
        {
            var expected = new[]
            {
                "Warn before leaving with unsaved changes",
                "Confirm before resetting a layer",
                "Show the 'keystrokes captured' summary after recording",
                "Confirm before switching keyboard variant",
                "Explain advisory warnings in full instead of one line"
            };

            Assert.Equal(expected, AppPreferenceCatalog.Featured.Select(descriptor => descriptor.Caption));
        }

        [Fact]
        public void Additional_Always_IsTheTwelveSpecKeysInSpecTableOrder()
        {
            var expected = new[]
            {
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

            Assert.Equal(expected, AppPreferenceCatalog.Additional.Select(descriptor => descriptor.Key));
        }

        [Fact]
        public void All_Always_HasNoDuplicateKeys()
        {
            var keys = AppPreferenceCatalog.All.Select(descriptor => descriptor.Key).ToList();

            Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void All_Always_CoversEveryNotificationKey()
        {
            Assert.All(
                NotificationKeys.All,
                key => Assert.NotNull(AppPreferenceCatalog.Find(key)));
        }

        [Fact]
        public void All_Always_UsesLowercaseKeysAsWrittenOnDisk()
        {
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor => Assert.Equal(descriptor.Key.ToLowerInvariant(), descriptor.Key));
        }

        [Fact]
        public void EveryDescriptor_Always_HasWordsForTheScreen()
        {
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(descriptor.Caption));
                    Assert.False(string.IsNullOrWhiteSpace(descriptor.Description));
                });
        }

        [Fact]
        public void Polarity_Always_IsSuppressionForEveryMsgKeyAndDisplayForTheOne()
        {
            // The on-disk convention is legible in the key itself: a *_msg key is a spec 08 §3
            // hide flag, and the single key without that suffix is the display preference.
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor => Assert.Equal(
                    descriptor.Key.EndsWith("_msg", StringComparison.Ordinal),
                    descriptor.IsSuppression));

            Assert.Equal(
                [SettingsKeys.AdvisoryDetail],
                AppPreferenceCatalog.All
                    .Where(descriptor => descriptor.Polarity == AppPreferencePolarity.Display)
                    .Select(descriptor => descriptor.Key));
        }

        [Fact]
        public void GetValue_ForASuppressionPreference_IsTheNegationOfTheStoredFlag()
        {
            // on = hide, so the ticked checkbox "Confirm before resetting a layer" is the ABSENCE
            // of reset_layer_msg. Inverting this is the single most likely bug on this screen.
            var descriptor = AppPreferenceCatalog.ResetLayerConfirmation;

            Assert.True(descriptor.GetValue(AppSettings.Empty));
            Assert.False(descriptor.GetValue(AppSettings.Empty with { IsResetLayerConfirmationHidden = true }));
            Assert.True(descriptor.GetValue(AppSettings.Empty with { IsResetLayerConfirmationHidden = false }));
        }

        [Fact]
        public void GetValue_ForTheDisplayPreference_IsTheStoredFlagItself()
        {
            // on = expand. The opposite convention, in the same file, on the same screen.
            var descriptor = AppPreferenceCatalog.AdvisoryDetail;

            Assert.False(descriptor.GetValue(AppSettings.Empty));
            Assert.True(descriptor.GetValue(AppSettings.Empty with { IsAdvisoryDetailExpanded = true }));
            Assert.False(descriptor.GetValue(AppSettings.Empty with { IsAdvisoryDetailExpanded = false }));
        }

        [Fact]
        public void SetValue_ForASuppressionPreference_StoresTheNegation()
        {
            var descriptor = AppPreferenceCatalog.ResetLayerConfirmation;

            var confirming = descriptor.SetValue(AppSettings.Empty, true);
            var silent = descriptor.SetValue(AppSettings.Empty, false);

            Assert.False(confirming.IsResetLayerConfirmationHidden);
            Assert.True(silent.IsResetLayerConfirmationHidden);
        }

        [Fact]
        public void SetValue_ForTheDisplayPreference_StoresTheValueItself()
        {
            var descriptor = AppPreferenceCatalog.AdvisoryDetail;

            Assert.True(descriptor.SetValue(AppSettings.Empty, true).IsAdvisoryDetailExpanded);
            Assert.False(descriptor.SetValue(AppSettings.Empty, false).IsAdvisoryDetailExpanded);
        }

        [Fact]
        public void SetValue_ForEveryDescriptor_RoundTripsThroughGetValue()
        {
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor =>
                {
                    Assert.True(descriptor.GetValue(descriptor.SetValue(AppSettings.Empty, true)));
                    Assert.False(descriptor.GetValue(descriptor.SetValue(AppSettings.Empty, false)));
                });
        }

        [Fact]
        public void SetValue_ForEveryDescriptor_TouchesOnlyItsOwnKey()
        {
            // Every descriptor must reach a different AppSettings property: two descriptors
            // sharing one would silently make two rows of the screen the same switch.
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor =>
                {
                    var written = descriptor.SetValue(AppSettings.Empty, !descriptor.DefaultValue);

                    Assert.All(
                        AppPreferenceCatalog.All.Where(other => other != descriptor),
                        other => Assert.Equal(other.DefaultValue, other.GetValue(written)));
                });
        }

        [Fact]
        public void DefaultValue_Always_IsWhatAnAbsentKeyReads()
        {
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor => Assert.Equal(descriptor.DefaultValue, descriptor.GetValue(AppSettings.Empty)));

            // Suppression preferences default to "ask me"; the display preference defaults to off.
            Assert.All(AppPreferenceCatalog.All.Where(d => d.IsSuppression), d => Assert.True(d.DefaultValue));
            Assert.False(AppPreferenceCatalog.AdvisoryDetail.DefaultValue);
        }

        [Fact]
        public void HasLiveConsumer_Always_MarksOnlyThePreferencesSomethingActuallyReads()
        {
            // Fourteen of the seventeen are forward-declared: the key exists so the feature that
            // lands later does not invent a second one. Three are wired — and the order is All's,
            // which puts mockup 1j's first row (the unsaved-changes prompt) ahead of the rest.
            var live = AppPreferenceCatalog.All
                .Where(descriptor => descriptor.HasLiveConsumer)
                .Select(descriptor => descriptor.Key);

            Assert.Equal(
                [SettingsKeys.UnsavedChangesMessage, SettingsKeys.ResetLayerMessage, SettingsKeys.AdvisoryDetail],
                live);
        }

        [Fact]
        public void Find_WithAnyCasingOrPadding_ResolvesTheDescriptor()
        {
            // Keys are matched case-insensitively (spec 08 §1).
            Assert.Same(AppPreferenceCatalog.AdvisoryDetail, AppPreferenceCatalog.Find("ADVISORY_DETAIL"));
            Assert.Same(AppPreferenceCatalog.AdvisoryDetail, AppPreferenceCatalog.Find("  advisory_detail  "));
        }

        [Fact]
        public void Find_WithAnUnmodelledKey_ReturnsNull()
        {
            Assert.Null(AppPreferenceCatalog.Find("not_a_spec_msg"));
            Assert.Throws<ArgumentException>(() => AppPreferenceCatalog.Find("  "));
        }

        [Fact]
        public void Keys_Always_CannotCollideByPrefixWithAnyAppSettingsKey()
        {
            // SettingsLineReader matches on the '=' separator precisely because cust_color_1 is a
            // prefix of cust_color_10. A new preference key that is a prefix of another key (or
            // vice versa) would lean on that rule far harder than it needs to, so none is.
            var otherKeys = AppPreferenceCatalog.All
                .Select(descriptor => descriptor.Key)
                .Concat(Enumerable
                    .Range(1, AppSettings.CustomColorCount)
                    .Select(SettingsKeys.GetCustomColorKey))
                .ToList();

            foreach (var key in AppPreferenceCatalog.All.Select(descriptor => descriptor.Key))
            {
                foreach (var other in otherKeys.Where(candidate => !string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase)))
                {
                    Assert.False(
                        other.StartsWith(key, StringComparison.OrdinalIgnoreCase),
                        $"'{key}' is a prefix of '{other}'.");
                }
            }
        }
    }
}
