using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class KeyboardSettingsSerializerTests
    {
        [Fact]
        public void Serialize_WithNoneCapability_ReturnsEmptyList()
        {
            var settings = new KeyboardSettings
            {
                MacroSpeed = 5,
                IsGameModeEnabled = true,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(SettingsCapability.None, settings);

            Assert.Empty(pairs);
        }

        [Fact]
        public void Serialize_WithAllNullSettings_ReturnsEmptyList()
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;

            var pairs = KeyboardSettingsSerializer.Serialize(capability, KeyboardSettings.Empty);

            Assert.Empty(pairs);
        }

        [Fact]
        public void Serialize_WithRgbCapabilityAndFullModel_EmitsGen1KeysInSpecOrder()
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;
            var settings = new KeyboardSettings
            {
                StartupProfileNumber = 3,
                LedMode = "led3.txt",
                MacroSpeed = 5,
                StatusPlaySpeed = 2,
                IsVDriveAutoMountEnabled = true,
                IsGameModeEnabled = false,
                IsProgramLockEnabled = true,
                IsKeyClickToneEnabled = true,
                IsToggleToneEnabled = true,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("startup_file", "layout3.txt"),
                    KeyValuePair.Create("led_mode", "led3.txt"),
                    KeyValuePair.Create("macro_speed", "5"),
                    KeyValuePair.Create("status_play_speed", "2"),
                    KeyValuePair.Create("v_drive", "auto"),
                    KeyValuePair.Create("game_mode", "OFF"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithAdvantage2CapabilityAndFullModel_EmitsStateKeysOnly()
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage2).Settings;
            var settings = new KeyboardSettings
            {
                StartupProfileNumber = 3,
                LedMode = "led3.txt",
                MacroSpeed = 0,
                StatusPlaySpeed = 4,
                IsVDriveAutoMountEnabled = true,
                IsVDriveOpenOnStartupEnabled = true,
                IsGameModeEnabled = true,
                IsKeyClickToneEnabled = true,
                IsToggleToneEnabled = false,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("macro_speed", "0"),
                    KeyValuePair.Create("status_play_speed", "4"),
                    KeyValuePair.Create("v_drive_open_on_startup", "ON"),
                    KeyValuePair.Create("key_click_tone", "ON"),
                    KeyValuePair.Create("toggle_tone", "OFF"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithAdvantage360CapabilityAndFullModel_EmitsProfileStatusAndLock()
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage360).Settings;
            var settings = new KeyboardSettings
            {
                StartupProfileNumber = 5,
                MacroSpeed = 5,
                StatusPlaySpeed = 2,
                IsProgramLockEnabled = true,
                IsGameModeEnabled = true,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("profile", "5"),
                    KeyValuePair.Create("status", "2"),
                    KeyValuePair.Create("lock", "ON"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithAdvantage2VDriveOpenOnStartupFalse_WritesLowercaseOff()
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage2).Settings;
            var settings = new KeyboardSettings
            {
                IsVDriveOpenOnStartupEnabled = false,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("v_drive_open_on_startup", "off")], pairs);
        }

        [Theory]
        [InlineData(true, "auto")]
        [InlineData(false, "manual")]
        public void Serialize_WithVDriveAutoMountValue_WritesAutoOrManual(bool isEnabled, string expectedValue)
        {
            var capability = DeviceCatalog.GetById(DeviceId.Tko).Settings;
            var settings = new KeyboardSettings
            {
                IsVDriveAutoMountEnabled = isEnabled,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("v_drive", expectedValue)], pairs);
        }

        [Theory]
        [InlineData(true, "ON")]
        [InlineData(false, "OFF")]
        public void Serialize_WithGameModeValue_WritesUppercase(bool isEnabled, string expectedValue)
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Settings;
            var settings = new KeyboardSettings
            {
                IsGameModeEnabled = isEnabled,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("game_mode", expectedValue)], pairs);
        }

        [Theory]
        [InlineData("led3.txt", "led3.txt")]
        [InlineData("LED7.TXT", "led7.txt")]
        [InlineData(" led9.txt ", "led9.txt")]
        public void Serialize_WithRgbLedFileName_NormalizesToLowercase(string ledMode, string expectedValue)
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;
            var settings = new KeyboardSettings
            {
                LedMode = ledMode,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("led_mode", expectedValue)], pairs);
        }

        [Theory]
        [InlineData("led0.txt")]
        [InlineData("led10.txt")]
        [InlineData("brightness")]
        [InlineData("P")]
        public void Serialize_WithInvalidRgbLedFileName_ThrowsArgumentException(string ledMode)
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;
            var settings = new KeyboardSettings
            {
                LedMode = ledMode,
            };

            Assert.Throws<ArgumentException>(() => KeyboardSettingsSerializer.Serialize(capability, settings));
        }

        [Theory]
        [InlineData("0", "0")]
        [InlineData("9", "9")]
        [InlineData("P", "P")]
        [InlineData("p", "P")]
        [InlineData("b", "B")]
        [InlineData(" B ", "B")]
        public void Serialize_WithFreestyleEdgeLedModeString_WritesCanonicalMode(string ledMode, string expectedValue)
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Settings;
            var settings = new KeyboardSettings
            {
                LedMode = ledMode,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("led_mode", expectedValue)], pairs);
        }

        [Theory]
        [InlineData("led3.txt")]
        [InlineData("X")]
        [InlineData("10")]
        [InlineData("")]
        public void Serialize_WithInvalidFreestyleEdgeLedMode_ThrowsArgumentException(string ledMode)
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Settings;
            var settings = new KeyboardSettings
            {
                LedMode = ledMode,
            };

            Assert.Throws<ArgumentException>(() => KeyboardSettingsSerializer.Serialize(capability, settings));
        }

        [Fact]
        public void Serialize_WithFreestyleProCapability_NeverEmitsLedModeOrGameMode()
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestylePro).Settings;
            var settings = new KeyboardSettings
            {
                LedMode = "5",
                MacroSpeed = 3,
                IsGameModeEnabled = true,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("macro_speed", "3")], pairs);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        public void Serialize_WithMacroSpeedOutOfRange_ThrowsArgumentOutOfRangeException(int macroSpeed)
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;
            var settings = new KeyboardSettings
            {
                MacroSpeed = macroSpeed,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardSettingsSerializer.Serialize(capability, settings));
        }

        [Fact]
        public void Serialize_WithMacroSpeedZeroOnRgb_WritesDisabledValueDespiteSliderMinimumOne()
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;
            var settings = new KeyboardSettings
            {
                MacroSpeed = 0,
            };

            var pairs = KeyboardSettingsSerializer.Serialize(capability, settings);

            Assert.Equal([KeyValuePair.Create("macro_speed", "0")], pairs);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public void Serialize_WithStatusPlaySpeedOutOfRange_ThrowsArgumentOutOfRangeException(int statusPlaySpeed)
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage2).Settings;
            var settings = new KeyboardSettings
            {
                StatusPlaySpeed = statusPlaySpeed,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardSettingsSerializer.Serialize(capability, settings));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        public void Serialize_WithStartupProfileNumberOutOfRange_ThrowsArgumentOutOfRangeException(int profileNumber)
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage360).Settings;
            var settings = new KeyboardSettings
            {
                StartupProfileNumber = profileNumber,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => KeyboardSettingsSerializer.Serialize(capability, settings));
        }

        [Fact]
        public void Serialize_Always_NeverEmitsReservedKeys()
        {
            var settings = new KeyboardSettings
            {
                ThumbMode = "mac",
                IsMacroDisabled = true,
                IsPowerUser = true,
                Country = "us",
                IsProgramKeyLockEnabled = true,
                IsProfileSyncModeEnabled = true,
            };

            foreach (var device in DeviceCatalog.All)
            {
                var pairs = KeyboardSettingsSerializer.Serialize(device.Settings, settings);

                Assert.Empty(pairs);
            }
        }

        [Fact]
        public void Serialize_WithNullCapability_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => KeyboardSettingsSerializer.Serialize(null!, KeyboardSettings.Empty));
        }

        [Fact]
        public void Serialize_WithNullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => KeyboardSettingsSerializer.Serialize(SettingsCapability.None, null!));
        }
    }
}
