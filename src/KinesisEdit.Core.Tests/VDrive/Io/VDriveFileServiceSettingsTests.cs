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

        [Fact]
        public void UpdateSettingsFile_WithRemovedKey_DeletesThatLineAndKeepsEveryOther()
        {
            // The merge's only deletion. It exists for values that have no text form: a cleared
            // custom-color slot is never written `cust_color_1=` (spec 08 §3), so nothing but a
            // removal can take it off the drive.
            var path = CreateSettingsFile(
                "future_key=whatever",
                "cust_color_1=[255][0][128]",
                "",
                "some unparseable garbage",
                "save_msg=on");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { "cust_color_1" });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(
                new[]
                {
                    "future_key=whatever",
                    "",
                    "some unparseable garbage",
                    "save_msg=on",
                },
                lines);
        }

        [Theory]
        [InlineData("cust_color_1", "cust_color_10=[1][2][3]")]
        [InlineData("v_drive", "v_drive_open_on_startup=ON")]
        public void UpdateSettingsFile_RemovingShorterKeyOfCollisionPair_LeavesTheLongerKeyLine(
            string removedKey,
            string longerKeyLine)
        {
            // A bare StartsWith deletion would take cust_color_10 with cust_color_1. Removal uses
            // the same '='-separator rule as every other key match in this module.
            var path = CreateSettingsFile(removedKey + "=old", longerKeyLine);

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { removedKey });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { longerKeyLine }, lines);
        }

        [Theory]
        [InlineData("cust_color_10", "cust_color_1=[1][2][3]")]
        [InlineData("v_drive_open_on_startup", "v_drive=auto")]
        public void UpdateSettingsFile_RemovingLongerKeyOfCollisionPair_LeavesTheShorterKeyLine(
            string removedKey,
            string shorterKeyLine)
        {
            var path = CreateSettingsFile(shorterKeyLine, removedKey + "=old");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { removedKey });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { shorterKeyLine }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithRemovedKeyNotInFile_ChangesNothing()
        {
            var path = CreateSettingsFile("power_user=true", "country=us");

            _service.UpdateSettingsFile(
                path,
                Array.Empty<KeyValuePair<string, string>>(),
                new[] { "cust_color_4", "cust_color_5" });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "power_user=true", "country=us" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithRemovedKeyOccurringTwice_DeletesEveryOccurrence()
        {
            // Mirrors the update side, which rewrites every occurrence: leaving one behind would
            // let the last line win on the next load and resurrect the value.
            var path = CreateSettingsFile("cust_color_2=[1][1][1]", "power_user=true", "cust_color_2=[2][2][2]");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { "cust_color_2" });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "power_user=true" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithDifferentlyCasedRemovedKey_DeletesTheLine()
        {
            var path = CreateSettingsFile("CUST_COLOR_3=[1][2][3]");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { "cust_color_3" });

            Assert.Empty(_service.ReadAllLines(path));
        }

        [Fact]
        public void UpdateSettingsFile_WithRemovedKeyPrefixLineLackingSeparator_TreatsLineAsUnmanaged()
        {
            var path = CreateSettingsFile("game_mode", "game_modes=ON");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { "game_mode" });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "game_mode", "game_modes=ON" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithSameKeyRemovedAndValued_WritesTheValue()
        {
            // A caller error either way; pinned so it degrades to "written", never to "lost".
            var path = CreateSettingsFile("macro_speed=1");

            _service.UpdateSettingsFile(
                path,
                new[] { KeyValuePair.Create("macro_speed", "7") },
                new[] { "macro_speed" });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "macro_speed=7" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithRemovalsAndValuesTogether_AppliesBoth()
        {
            var path = CreateSettingsFile("cust_color_1=[1][1][1]", "future_key=keep", "save_msg=off");

            _service.UpdateSettingsFile(
                path,
                new[] { KeyValuePair.Create("save_msg", "on") },
                new[] { "cust_color_1" });

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "future_key=keep", "save_msg=on" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithNoRemovedKeys_LeavesLinesUnchanged()
        {
            var path = CreateSettingsFile("power_user=true", "cust_color_1=[1][1][1]");

            _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), Array.Empty<string>());

            var lines = _service.ReadAllLines(path);
            Assert.Equal(new[] { "power_user=true", "cust_color_1=[1][1][1]" }, lines);
        }

        [Fact]
        public void UpdateSettingsFile_WithEmptyRemovedKey_ThrowsArgumentException()
        {
            var path = CreateSettingsFile("power_user=true");

            Assert.Throws<ArgumentException>(
                () => _service.UpdateSettingsFile(path, Array.Empty<KeyValuePair<string, string>>(), new[] { "" }));
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
