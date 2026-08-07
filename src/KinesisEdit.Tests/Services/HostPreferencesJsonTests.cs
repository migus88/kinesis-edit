using System.Text.Json;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The tolerant read and the canonical write. Every file engine in this repo reads tolerantly
    /// and writes the current form; this one is no different, and the point of the suite is that
    /// <see cref="HostPreferencesParser.Parse"/> has <b>no</b> input that makes it throw.
    /// </summary>
    public class HostPreferencesJsonTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_YieldsDefaults_WhenThereIsNoContent(string? json)
        {
            Assert.Equal(HostPreferences.Default, HostPreferencesParser.Parse(json));
        }

        [Theory]
        [InlineData("{ this is not json")]
        [InlineData("{\"theme\":}")]
        [InlineData("}{")]
        [InlineData("\0\0\0")]
        [InlineData("{\"theme\": \"Dark\",}")]
        public void Parse_YieldsDefaults_WhenTheJsonIsCorrupt(string json)
        {
            var exception = Record.Exception(() => HostPreferencesParser.Parse(json));

            Assert.Null(exception);
            Assert.Equal(HostPreferences.Default, HostPreferencesParser.Parse(json));
        }

        [Theory]
        [InlineData("[1, 2, 3]")]
        [InlineData("\"a string\"")]
        [InlineData("42")]
        [InlineData("true")]
        [InlineData("null")]
        public void Parse_YieldsDefaults_WhenTheRootIsNotAnObject(string json)
        {
            Assert.Equal(HostPreferences.Default, HostPreferencesParser.Parse(json));
        }

        [Fact]
        public void Parse_IgnoresUnknownKeys_AndKeepsTheOnesItModels()
        {
            // A file written by a newer version, or hand-edited, must not cost the user the
            // preferences this version does understand.
            var json = """
                {
                  "theme": "Dark",
                  "motion": "AlwaysReduce",
                  "somethingFromTheFuture": { "nested": [1, 2, 3] },
                  "anotherOne": 42
                }
                """;

            var preferences = HostPreferencesParser.Parse(json);

            Assert.Equal(AppThemePreference.Dark, preferences.Theme);
            Assert.Equal(MotionPreference.AlwaysReduce, preferences.Motion);
        }

        [Fact]
        public void Parse_YieldsDefaults_ForWrongTypedValues()
        {
            var json = """
                {
                  "theme": 3,
                  "motion": true,
                  "window": "not an object"
                }
                """;

            var preferences = HostPreferencesParser.Parse(json);

            Assert.Equal(HostPreferences.Default, preferences);
        }

        [Fact]
        public void Parse_FallsBackPerField_NotPerFile()
        {
            // The whole reason this reads field by field instead of deserializing: one bad line
            // must not cost the user everything else in the file.
            var json = """
                {
                  "theme": [],
                  "motion": "NeverReduce"
                }
                """;

            var preferences = HostPreferencesParser.Parse(json);

            Assert.Equal(AppThemePreference.FollowSystem, preferences.Theme);
            Assert.Equal(MotionPreference.NeverReduce, preferences.Motion);
        }

        [Theory]
        [InlineData("\"Chartreuse\"")]
        [InlineData("\"\"")]
        [InlineData("\"7\"")]
        [InlineData("\"-1\"")]
        public void Parse_YieldsTheDefaultTheme_ForAnUnknownName(string themeValue)
        {
            var preferences = HostPreferencesParser.Parse($"{{\"theme\": {themeValue}}}");

            Assert.Equal(AppThemePreference.FollowSystem, preferences.Theme);
        }

        [Theory]
        [InlineData("dark", AppThemePreference.Dark)]
        [InlineData("LIGHT", AppThemePreference.Light)]
        [InlineData("FollowSystem", AppThemePreference.FollowSystem)]
        public void Parse_MatchesEnumNames_CaseInsensitively(string stored, AppThemePreference expected)
        {
            Assert.Equal(expected, HostPreferencesParser.Parse($"{{\"theme\": \"{stored}\"}}").Theme);
        }

        [Fact]
        public void Parse_MatchesPropertyNames_CaseInsensitively()
        {
            var preferences = HostPreferencesParser.Parse("""
                { "Theme": "Dark", "MOTION": "AlwaysReduce", "Window": { "Width": 900, "HEIGHT": 600 } }
                """);

            Assert.Equal(AppThemePreference.Dark, preferences.Theme);
            Assert.Equal(MotionPreference.AlwaysReduce, preferences.Motion);
            Assert.Equal(900, preferences.Window?.Width);
            Assert.Equal(600, preferences.Window?.Height);
        }

        [Theory]
        [InlineData("{\"window\": {\"width\": 0, \"height\": 600}}")]
        [InlineData("{\"window\": {\"width\": -1, \"height\": 600}}")]
        [InlineData("{\"window\": {\"width\": 900, \"height\": -50}}")]
        [InlineData("{\"window\": {\"width\": 1e30, \"height\": 600}}")]
        [InlineData("{\"window\": {\"width\": 900}}")]
        [InlineData("{\"window\": {\"height\": 600}}")]
        [InlineData("{\"window\": {\"width\": \"900\", \"height\": \"600\"}}")]
        [InlineData("{\"window\": {}}")]
        [InlineData("{\"window\": null}")]
        public void Parse_StoresNoGeometry_WhenTheWindowIsNotAWindow(string json)
        {
            Assert.Null(HostPreferencesParser.Parse(json).Window);
        }

        [Fact]
        public void Parse_DropsACoordinate_RatherThanTheWholeGeometry()
        {
            // A bad position is worth forgetting; a good size is not worth throwing away with it.
            var preferences = HostPreferencesParser.Parse("""
                { "window": { "width": 900, "height": 600, "x": "left", "y": 40 } }
                """);

            Assert.NotNull(preferences.Window);
            Assert.Null(preferences.Window.X);
            Assert.Equal(40, preferences.Window.Y);
        }

        [Fact]
        public void Parse_TreatsANonBooleanMaximized_AsNotMaximized()
        {
            var preferences = HostPreferencesParser.Parse("""
                { "window": { "width": 900, "height": 600, "maximized": "yes" } }
                """);

            Assert.NotNull(preferences.Window);
            Assert.False(preferences.Window.IsMaximized);
        }

        [Fact]
        public void RoundTrip_PreservesEveryField()
        {
            var preferences = new HostPreferences
            {
                Theme = AppThemePreference.Light,
                Motion = MotionPreference.NeverReduce,
                Window = new WindowGeometry(1234.5, 678.25, -900, 42, true)
            };

            var restored = HostPreferencesParser.Parse(HostPreferencesSerializer.Serialize(preferences));

            Assert.Equal(preferences, restored);
        }

        [Theory]
        [InlineData(AppThemePreference.FollowSystem)]
        [InlineData(AppThemePreference.Light)]
        [InlineData(AppThemePreference.Dark)]
        public void RoundTrip_PreservesEveryTheme(AppThemePreference theme)
        {
            var preferences = new HostPreferences { Theme = theme };

            Assert.Equal(theme, HostPreferencesParser.Parse(HostPreferencesSerializer.Serialize(preferences)).Theme);
        }

        [Theory]
        [InlineData(MotionPreference.FollowSystem)]
        [InlineData(MotionPreference.AlwaysReduce)]
        [InlineData(MotionPreference.NeverReduce)]
        public void RoundTrip_PreservesEveryMotionPreference(MotionPreference motion)
        {
            var preferences = new HostPreferences { Motion = motion };

            Assert.Equal(motion, HostPreferencesParser.Parse(HostPreferencesSerializer.Serialize(preferences)).Motion);
        }

        [Fact]
        public void RoundTrip_PreservesAGeometryWithNoPosition()
        {
            var preferences = new HostPreferences { Window = new WindowGeometry(720, 480, null, null, false) };

            var restored = HostPreferencesParser.Parse(HostPreferencesSerializer.Serialize(preferences));

            Assert.Equal(preferences, restored);
        }

        [Fact]
        public void Serialize_WritesEnumNames_NotNumbers()
        {
            // Numbers would tie the file to the enum's declaration order forever.
            var json = HostPreferencesSerializer.Serialize(new HostPreferences
            {
                Theme = AppThemePreference.Dark,
                Motion = MotionPreference.AlwaysReduce
            });

            Assert.Contains("\"Dark\"", json, StringComparison.Ordinal);
            Assert.Contains("\"AlwaysReduce\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Serialize_OmitsTheWindow_WhenNothingIsStored()
        {
            var json = HostPreferencesSerializer.Serialize(HostPreferences.Default);

            Assert.DoesNotContain("window", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Serialize_OmitsAGeometryThatIsNotAWindow()
        {
            // Built by hand rather than through TryCreate: Utf8JsonWriter throws on NaN, and a
            // preference save is not allowed to throw.
            var preferences = new HostPreferences { Window = new WindowGeometry(double.NaN, 600, null, null, false) };

            var json = HostPreferencesSerializer.Serialize(preferences);

            Assert.DoesNotContain("window", json, StringComparison.OrdinalIgnoreCase);
            Assert.Null(HostPreferencesParser.Parse(json).Window);
        }

        [Fact]
        public void Serialize_ProducesValidJson()
        {
            var json = HostPreferencesSerializer.Serialize(new HostPreferences
            {
                Theme = AppThemePreference.Light,
                Window = new WindowGeometry(1000, 680, 0, 0, false)
            });

            using var document = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }

        [Fact]
        public void Serialize_Rejects_Null()
        {
            Assert.Throws<ArgumentNullException>(() => HostPreferencesSerializer.Serialize(null!));
        }
    }
}
