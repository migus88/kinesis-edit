using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the published-versions payload of specs/09-firmware.md §3 step 3 — the sample response
    /// verbatim, the documented key set, and the tolerant-reading rules (unknown keys kept, empty
    /// values treated as absent, malformed bodies rejected).
    /// </summary>
    public class VersionManifestTests
    {
        private const string SpecSampleJson = """{"keyboard_ver":"1.0.0","lighting_ver":"1.0.0","app_ver":"2.0.20", "mac_app_ver":"2.1.3"}""";

        private const string AllKeysJson = """
            {
              "keyboard_ver": "1.0.1",
              "lighting_ver": "1.0.2",
              "app_ver": "2.0.20",
              "pc_app_version": "2.0.21",
              "mac_app_ver": "2.1.3",
              "mac_app_version": "2.1.4",
              "tko_keyboard_version": "1.0.5",
              "tko_lighting_version": "1.0.6",
              "kb360_version": "1.0.7"
            }
            """;

        [Fact]
        public void TryParse_WithSpecSample_ReadsTheDocumentedKeys()
        {
            var isParsed = VersionManifest.TryParse(SpecSampleJson, out var manifest);

            Assert.True(isParsed);
            Assert.Equal("1.0.0", manifest.KeyboardVersion);
            Assert.Equal("1.0.0", manifest.LightingVersion);
            Assert.Equal("2.0.20", manifest.WindowsGamingAppVersion);
            Assert.Equal("2.1.3", manifest.MacGamingAppVersion);
        }

        [Fact]
        public void TryParse_WithSpecSample_LeavesUnlistedKeysNull()
        {
            VersionManifest.TryParse(SpecSampleJson, out var manifest);

            Assert.Null(manifest.WindowsOfficeAppVersion);
            Assert.Null(manifest.MacOfficeAppVersion);
            Assert.Null(manifest.TkoKeyboardVersion);
            Assert.Null(manifest.TkoLightingVersion);
            Assert.Null(manifest.Advantage360Version);
        }

        [Fact]
        public void TryParse_WithSpecSample_PreservesTheRawResponseText()
        {
            VersionManifest.TryParse(SpecSampleJson, out var manifest);

            Assert.Equal(SpecSampleJson, manifest.RawJson);
        }

        [Fact]
        public void TryParse_WithEveryDocumentedKey_MapsEachToItsTypedAccessor()
        {
            var isParsed = VersionManifest.TryParse(AllKeysJson, out var manifest);

            Assert.True(isParsed);
            Assert.Equal("1.0.1", manifest.KeyboardVersion);
            Assert.Equal("1.0.2", manifest.LightingVersion);
            Assert.Equal("2.0.20", manifest.WindowsGamingAppVersion);
            Assert.Equal("2.0.21", manifest.WindowsOfficeAppVersion);
            Assert.Equal("2.1.3", manifest.MacGamingAppVersion);
            Assert.Equal("2.1.4", manifest.MacOfficeAppVersion);
            Assert.Equal("1.0.5", manifest.TkoKeyboardVersion);
            Assert.Equal("1.0.6", manifest.TkoLightingVersion);
            Assert.Equal("1.0.7", manifest.Advantage360Version);
        }

        [Fact]
        public void TryParse_WithUnknownKeys_KeepsThemAndStillReadsTheKnownOnes()
        {
            var json = """{"keyboard_ver":"1.0.0","future_device_version":"9.9.9"}""";

            var isParsed = VersionManifest.TryParse(json, out var manifest);

            Assert.True(isParsed);
            Assert.Equal("1.0.0", manifest.KeyboardVersion);
            Assert.Equal("9.9.9", manifest.GetValue("future_device_version"));
            Assert.Equal(2, manifest.Values.Count);
        }

        [Fact]
        public void GetValue_WithAbsentKey_ReturnsNull()
        {
            VersionManifest.TryParse(SpecSampleJson, out var manifest);

            Assert.Null(manifest.GetValue("kb360_version"));
        }

        [Theory]
        [InlineData("""{"keyboard_ver":""}""")]
        [InlineData("""{"keyboard_ver":"   "}""")]
        [InlineData("""{"keyboard_ver":null}""")]
        [InlineData("""{"keyboard_ver":{"nested":"1.0.0"}}""")]
        [InlineData("""{"keyboard_ver":["1.0.0"]}""")]
        public void TryParse_WithUnusableValue_TreatsTheKeyAsAbsent(string json)
        {
            var isParsed = VersionManifest.TryParse(json, out var manifest);

            Assert.True(isParsed);
            Assert.Null(manifest.KeyboardVersion);
            Assert.Empty(manifest.Values);
        }

        [Fact]
        public void TryParse_WithNonStringScalarValue_ReadsItsRawText()
        {
            var json = """{"keyboard_ver":1}""";

            VersionManifest.TryParse(json, out var manifest);

            Assert.Equal("1", manifest.KeyboardVersion);
        }

        [Fact]
        public void GetValue_WithDifferentKeyCasing_MatchesCaseInsensitively()
        {
            var json = """{"KEYBOARD_VER":"1.0.0"}""";

            VersionManifest.TryParse(json, out var manifest);

            Assert.Equal("1.0.0", manifest.KeyboardVersion);
            Assert.Equal("1.0.0", manifest.GetValue("Keyboard_Ver"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetValue_WithBlankKey_ReturnsNull(string? key)
        {
            VersionManifest.TryParse(SpecSampleJson, out var manifest);

            Assert.Null(manifest.GetValue(key));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("""{"keyboard_ver":""")]
        public void TryParse_WithMalformedOrMissingBody_ReturnsFalseAndTheEmptyManifest(string? json)
        {
            var isParsed = VersionManifest.TryParse(json, out var manifest);

            Assert.False(isParsed);
            Assert.Same(VersionManifest.Empty, manifest);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("""["1.0.0"]""")]
        [InlineData("\"1.0.0\"")]
        [InlineData("5")]
        [InlineData("null")]
        public void TryParse_WithNonObjectRoot_ReturnsFalse(string json)
        {
            var isParsed = VersionManifest.TryParse(json, out var manifest);

            Assert.False(isParsed);
            Assert.Empty(manifest.Values);
        }

        [Fact]
        public void TryParse_WithTrailingCommaOrComment_ReadsTolerantly()
        {
            var json = """
                {
                  // published by the endpoint
                  "keyboard_ver": "1.0.0",
                }
                """;

            var isParsed = VersionManifest.TryParse(json, out var manifest);

            Assert.True(isParsed);
            Assert.Equal("1.0.0", manifest.KeyboardVersion);
        }

        [Fact]
        public void Empty_Always_CarriesNoValuesAndNoRawJson()
        {
            Assert.Empty(VersionManifest.Empty.Values);
            Assert.Equal(string.Empty, VersionManifest.Empty.RawJson);
            Assert.Null(VersionManifest.Empty.KeyboardVersion);
        }
    }
}
