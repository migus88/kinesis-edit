using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    /// <summary>
    /// The <c>macro_name_*</c> half of app_settings.txt: parse, serialize, and the removal half that
    /// keeps a cleared name from surviving on the drive (invariant 5, the shape the custom-colour
    /// slots already have). The rule guarded hardest here is that <b>a removal is scoped to the
    /// profile being written</b> — one device file holds every profile's names.
    /// </summary>
    public class AppSettingsMacroNameTests
    {
        [Fact]
        public void Parse_ReadsTheMacroNameFamilyByPrefix()
        {
            var settings = AppSettingsParser.Parse(
            [
                "save_msg=on",
                "macro_name_1_0_65_1=Sign-off",
                "macro_name_2_1_11167_5=Layer macro",
                "macro_name_1_4_66_0=Flat macro"
            ]);

            Assert.Equal("Sign-off", settings.GetMacroName(new MacroNameKey(1, 0, 65, 1)));
            Assert.Equal("Layer macro", settings.GetMacroName(new MacroNameKey(2, 1, 11167, 5)));
            Assert.Equal("Flat macro", settings.GetMacroName(new MacroNameKey(1, 4, 66, 0)));
            Assert.Equal(3, settings.MacroNames.Count);
        }

        [Fact]
        public void Parse_IsCaseInsensitiveAndTrimsTheValue()
        {
            var settings = AppSettingsParser.Parse(["MACRO_NAME_1_0_65_1=  Sign-off  "]);

            Assert.Equal("Sign-off", settings.GetMacroName(new MacroNameKey(1, 0, 65, 1)));
        }

        [Fact]
        public void Parse_WithARepeatedKey_TakesTheLastOne()
        {
            var settings = AppSettingsParser.Parse(
            [
                "macro_name_1_0_65_1=First",
                "macro_name_1_0_65_1=Second"
            ]);

            Assert.Equal("Second", settings.GetMacroName(new MacroNameKey(1, 0, 65, 1)));
        }

        [Fact]
        public void Parse_IgnoresLinesThatOnlyLookLikeTheFamily()
        {
            var settings = AppSettingsParser.Parse(
            [
                "macro_speed=5",
                "macro_disable=on",
                "macro_name_=orphan",
                "macro_name_1_0_65=short",
                "macro_name_1_0_65_x=nan"
            ]);

            Assert.Empty(settings.MacroNames);
        }

        [Fact]
        public void Parse_WithABlankValue_KeepsATombstone()
        {
            // A name-less line is not a name: it is a key to delete. Dropping it here would leave it
            // on the drive forever, since a merge cannot tell "skipped" from "cleared".
            var settings = AppSettingsParser.Parse(["macro_name_1_0_65_1="]);

            Assert.Null(settings.GetMacroName(new MacroNameKey(1, 0, 65, 1)));
            Assert.Single(settings.MacroNames);
            Assert.Equal(
                new[] { "macro_name_1_0_65_1" },
                AppSettingsSerializer.SerializeMacroNameRemovals(settings, 1));
        }

        [Fact]
        public void Empty_HasNoMacroNames()
        {
            Assert.Empty(AppSettings.Empty.MacroNames);
            Assert.Empty(AppSettingsSerializer.SerializeMacroNames(AppSettings.Empty, 1));
            Assert.Empty(AppSettingsSerializer.SerializeMacroNameRemovals(AppSettings.Empty, 1));
        }

        [Fact]
        public void WithMacroName_SetsAndTombstones()
        {
            var settings = AppSettings.Empty.WithMacroName(new MacroNameKey(1, 0, 65, 1), "  Sign-off  ");

            Assert.Equal("Sign-off", settings.GetMacroName(new MacroNameKey(1, 0, 65, 1)));

            var cleared = settings.WithMacroName(new MacroNameKey(1, 0, 65, 1), "   ");

            Assert.Null(cleared.GetMacroName(new MacroNameKey(1, 0, 65, 1)));
            Assert.Single(cleared.MacroNames);
        }

        [Fact]
        public void SerializeMacroNames_EmitsOnlyTheGivenProfileInKeyOrder()
        {
            var settings = AppSettings.Empty
                .WithMacroName(new MacroNameKey(1, 1, 70, 2), "Second")
                .WithMacroName(new MacroNameKey(1, 0, 65, 1), "First")
                .WithMacroName(new MacroNameKey(2, 0, 65, 1), "Other profile");

            var pairs = AppSettingsSerializer.SerializeMacroNames(settings, 1);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("macro_name_1_0_65_1", "First"),
                    KeyValuePair.Create("macro_name_1_1_70_2", "Second"),
                },
                pairs);
        }

        [Fact]
        public void SerializeMacroNames_SkipsBlankNames()
        {
            var settings = AppSettings.Empty
                .WithMacroName(new MacroNameKey(1, 0, 65, 1), "Named")
                .WithMacroName(new MacroNameKey(1, 0, 66, 1), null);

            var pair = Assert.Single(AppSettingsSerializer.SerializeMacroNames(settings, 1));

            Assert.Equal("macro_name_1_0_65_1", pair.Key);
        }

        [Fact]
        public void SerializeRemovals_NeverTouchesMacroNames()
        {
            // The swatch/preference save path has no profile to scope itself to, so it must not be
            // able to reach a macro name at all.
            var settings = AppSettings.Empty.WithMacroName(new MacroNameKey(1, 0, 65, 1), null);

            Assert.DoesNotContain(
                AppSettingsSerializer.SerializeRemovals(settings),
                key => key.StartsWith(SettingsKeys.MacroNamePrefix, StringComparison.Ordinal));
        }

        [Fact]
        public void Serialize_NeverWritesMacroNames()
        {
            var settings = (AppSettings.Empty with { IsSaveMessageHidden = true })
                .WithMacroName(new MacroNameKey(1, 0, 65, 1), "Sign-off");

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal(KeyValuePair.Create("save_msg", "on"), Assert.Single(pairs));
        }

        [Fact]
        public void WithMacroNamesForProfile_TombstonesWhatTheNewSetDropsAndLeavesOtherProfilesAlone()
        {
            var settings = AppSettings.Empty
                .WithMacroName(new MacroNameKey(1, 0, 65, 1), "Kept")
                .WithMacroName(new MacroNameKey(1, 0, 66, 1), "Deleted")
                .WithMacroName(new MacroNameKey(2, 0, 65, 1), "Profile two");

            var updated = settings.WithMacroNamesForProfile(
                1,
                [KeyValuePair.Create(new MacroNameKey(1, 0, 65, 1), "Renamed")]);

            Assert.Equal("Renamed", updated.GetMacroName(new MacroNameKey(1, 0, 65, 1)));
            Assert.Null(updated.GetMacroName(new MacroNameKey(1, 0, 66, 1)));
            Assert.Equal("Profile two", updated.GetMacroName(new MacroNameKey(2, 0, 65, 1)));

            Assert.Equal(
                new[] { "macro_name_1_0_66_1" },
                AppSettingsSerializer.SerializeMacroNameRemovals(updated, 1));
            Assert.Empty(AppSettingsSerializer.SerializeMacroNameRemovals(updated, 2));
        }

        [Fact]
        public void WithMacroNamesForProfile_WithAKeyFromAnotherProfile_Throws()
        {
            Assert.Throws<ArgumentException>(() => AppSettings.Empty.WithMacroNamesForProfile(
                1,
                [KeyValuePair.Create(new MacroNameKey(2, 0, 65, 1), "Wrong profile")]));
        }

        [Fact]
        public void WithMacroNamesForProfile_WithProfileBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AppSettings.Empty.WithMacroNamesForProfile(0, []));
        }

        [Fact]
        public void RoundTrip_ParseThenSerialize_KeepsTheNames()
        {
            var lines = new[]
            {
                "macro_name_1_0_65_1=Sign-off",
                "macro_name_1_2_11167_4=Layer macro"
            };

            var settings = AppSettingsParser.Parse(lines);
            var pairs = AppSettingsSerializer.SerializeMacroNames(settings, 1);

            Assert.Equal(lines, pairs.Select(pair => pair.Key + "=" + pair.Value));
        }
    }
}
