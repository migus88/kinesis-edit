using System.Text;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.VDrive.Io
{
    public sealed class VDriveFileServiceSettingsTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly VDriveFileService _service = new();

        public VDriveFileServiceSettingsTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void UpdateSettingsFile_WithMissingFile_ThrowsFileNotFoundException()
        {
            var path = Path.Combine(_tempDirectory, "missing.txt");
            var values = new Dictionary<string, string> { ["game_mode"] = "ON" };

            Assert.Throws<FileNotFoundException>(() => _service.UpdateSettingsFile(path, values));
        }

        [Fact]
        public void UpdateSettingsFile_WithExistingManagedKey_ReplacesLineInPlacePreservingOrder()
        {
            var path = CreateSettingsFile("game_mode=OFF", "macro_speed=5", "status_play_speed=3");

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create("macro_speed", "9") });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "game_mode=OFF", "macro_speed=9", "status_play_speed=3" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithUnknownAndReservedLines_PreservesThemVerbatim()
        {
            var path = CreateSettingsFile(
                "power_user=true",
                "country=us",
                "thumb_mode=mac",
                "program_key_lock=OFF",
                "profile_sync_mode=ON",
                "",
                "some unparseable garbage",
                "macro_speed=5");

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create("macro_speed", "0") });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(
                new[]
                {
                    "power_user=true",
                    "country=us",
                    "thumb_mode=mac",
                    "program_key_lock=OFF",
                    "profile_sync_mode=ON",
                    "",
                    "some unparseable garbage",
                    "macro_speed=0",
                },
                lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithMissingKeys_AppendsThemInCallerOrder()
        {
            var path = CreateSettingsFile("power_user=true");

            _service.UpdateSettingsFile(
                path,
                new[]
                {
                    KeyValuePair.Create("game_mode", "ON"),
                    KeyValuePair.Create("macro_speed", "5"),
                });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "power_user=true", "game_mode=ON", "macro_speed=5" }, lines);
        }

        [Theory]
        [InlineData("v_drive", "manual", "v_drive_open_on_startup=ON")]
        [InlineData("cust_color_1", "[9][9][9]", "cust_color_10=[1][2][3]")]
        public void UpdateSettingsFile_WithPrefixCollisionPairsFromSpec_UpdatesOnlyTheExactKey(
            string key,
            string value,
            string longerKeyLine)
        {
            var shorterKeyLine = key + "=old";
            var path = CreateSettingsFile(shorterKeyLine, longerKeyLine);

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create(key, value) });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { $"{key}={value}", longerKeyLine }, lines);
        }

        [Theory]
        [InlineData("v_drive_open_on_startup", "off", "v_drive=auto")]
        [InlineData("cust_color_10", "[7][7][7]", "cust_color_1=[1][2][3]")]
        public void UpdateSettingsFile_WithLongerKeyOfCollisionPair_LeavesShorterKeyUntouched(
            string key,
            string value,
            string shorterKeyLine)
        {
            var longerKeyLine = key + "=old";
            var path = CreateSettingsFile(shorterKeyLine, longerKeyLine);

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create(key, value) });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { shorterKeyLine, $"{key}={value}" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithDifferentlyCasedKeyInFile_MatchesCaseInsensitivelyAndWritesCallerCasing()
        {
            var path = CreateSettingsFile("GAME_MODE=OFF");

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create("game_mode", "ON") });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "game_mode=ON" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithMultipleOccurrencesOfManagedKey_UpdatesAllOccurrences()
        {
            var path = CreateSettingsFile("macro_speed=1", "power_user=true", "macro_speed=2");

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create("macro_speed", "7") });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "macro_speed=7", "power_user=true", "macro_speed=7" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithKeyPrefixLineLackingSeparator_TreatsLineAsUnmanaged()
        {
            var path = CreateSettingsFile("game_mode", "game_modes=ON");

            _service.UpdateSettingsFile(path, new[] { KeyValuePair.Create("game_mode", "OFF") });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "game_mode", "game_modes=ON", "game_mode=OFF" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithNoValues_LeavesLinesUnchanged()
        {
            var path = CreateSettingsFile("power_user=true", "country=us");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>());

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "power_user=true", "country=us" }, lines);
        }

        private string CreateSettingsFile(params string[] lines)
        {
            var path = Path.Combine(_tempDirectory, "settings.txt");
            var content = new StringBuilder();

            foreach (var line in lines)
            {
                content.Append(line);
                content.Append('\n');
            }

            File.WriteAllText(path, content.ToString(), Encoding.Latin1);

            return path;
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
