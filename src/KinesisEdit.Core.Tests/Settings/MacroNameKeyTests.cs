using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    /// <summary>
    /// The identity of one macro name on disk: profile + layer + trigger + slot, spelled
    /// <c>macro_name_&lt;p&gt;_&lt;l&gt;_&lt;t&gt;_&lt;s&gt;</c>. Build and parse must be exact
    /// inverses, and the parse must refuse anything that is not this app's key — the prefix scan
    /// that finds these lines has no fixed key name to check against.
    /// </summary>
    public class MacroNameKeyTests
    {
        [Fact]
        public void GetMacroNameKey_SpellsTheFourComponentsAsUnsignedDecimals()
        {
            var key = new MacroNameKey(2, 1, 11167, 3);

            Assert.Equal("macro_name_2_1_11167_3", SettingsKeys.GetMacroNameKey(key));
            Assert.Equal("macro_name_2_1_11167_3", key.ToSettingsKey());
        }

        [Fact]
        public void GetMacroNameKey_ForAFlatListMacro_WritesSlotZero()
        {
            Assert.Equal("macro_name_1_4_65_0", new MacroNameKey(1, 4, 65, 0).ToSettingsKey());
        }

        [Fact]
        public void MacroNamePrefix_IsNotSharedWithAnyOtherManagedKey()
        {
            // The prefix scan claims every line starting with it, so the family must not overlap the
            // fixed keys of spec 08 — macro_speed and macro_disable diverge before the underscore.
            Assert.StartsWith("macro_", SettingsKeys.MacroSpeed);
            Assert.False(SettingsKeys.MacroSpeed.StartsWith(SettingsKeys.MacroNamePrefix, StringComparison.OrdinalIgnoreCase));
            Assert.False(SettingsKeys.MacroDisable.StartsWith(SettingsKeys.MacroNamePrefix, StringComparison.OrdinalIgnoreCase));
            Assert.False(SettingsKeys.MacroNamePrefix.StartsWith(SettingsKeys.MacroSpeed, StringComparison.OrdinalIgnoreCase));
            Assert.False(SettingsKeys.MacroNamePrefix.StartsWith(SettingsKeys.MacroDisable, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TryParse_RoundTripsEveryKeyItBuilds()
        {
            var key = new MacroNameKey(9, 0, 11166, 5);

            Assert.True(MacroNameKey.TryParse(key.ToSettingsKey(), out var parsed));
            Assert.Equal(key, parsed);
        }

        [Fact]
        public void TryParse_IsCaseInsensitiveOnThePrefix()
        {
            Assert.True(MacroNameKey.TryParse("MACRO_NAME_1_0_65_2", out var parsed));
            Assert.Equal(new MacroNameKey(1, 0, 65, 2), parsed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("macro_speed")]
        [InlineData("macro_name_")]
        [InlineData("macro_name_1_0_65")]
        [InlineData("macro_name_1_0_65_2_9")]
        [InlineData("macro_name_1_0_65_x")]
        [InlineData("macro_name_1_0_-65_2")]
        [InlineData("macro_name_0_0_65_2")]
        [InlineData("macro_name_ 1_0_65_2")]
        public void TryParse_ForAnythingElse_IsFalse(string? settingsKey)
        {
            Assert.False(MacroNameKey.TryParse(settingsKey, out _));
        }

        [Fact]
        public void TryCreate_ForAnUnboundMacro_IsFalse()
        {
            // A Gen2 flat-list macro with no trigger or layer (06 §1) has no place to be named
            // after, so it declines to be persisted rather than inventing a key.
            Assert.False(MacroNameKey.TryCreate(1, -1, -1, 0, out _));
            Assert.False(MacroNameKey.TryCreate(0, 0, 65, 1, out _));
            Assert.True(MacroNameKey.TryCreate(1, 0, 65, 1, out _));
        }

        [Fact]
        public void Constructor_WithAnOutOfRangeComponent_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MacroNameKey(0, 0, 65, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MacroNameKey(1, -1, 65, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MacroNameKey(1, 0, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MacroNameKey(1, 0, 65, -1));
        }

        [Fact]
        public void Equality_IsByValue()
        {
            Assert.Equal(new MacroNameKey(1, 0, 65, 1), new MacroNameKey(1, 0, 65, 1));
            Assert.NotEqual(new MacroNameKey(1, 0, 65, 1), new MacroNameKey(2, 0, 65, 1));
        }
    }
}
