using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// Invalid-line keep/drop (specs/04-layout-file-format.md §5.2): every unparseable line
    /// loads with <see cref="Layouts.LayoutInvalidLine.Keep"/> defaulting to false, and only the
    /// lines a caller flips to true survive the next <see cref="ProfileSession.Save"/>.
    /// </summary>
    public sealed class ProfileSessionInvalidLineTests
    {
        [Fact]
        public void Load_WithUnparseableLines_TracksThemWithKeepDefaultingToFalse()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]", "fn [ZZZ]>[a]", "fn [YYY]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            Assert.Equal(2, session.InvalidLines.Count);
            Assert.All(session.InvalidLines, line => Assert.False(line.Keep));
        }

        [Fact]
        public void Save_WithOneKeptInvalidLine_ReproducesItVerbatimAndDropsTheOther()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]", "fn [ZZZ]>[a]", "fn [YYY]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            session.InvalidLines[0].Keep = true;

            var result = session.Save();

            Assert.True(result.Success);

            var savedLines = File.ReadAllLines(drive.GetLayoutFilePath(DeviceId.FreestyleEdge, 1));
            Assert.Contains("fn [ZZZ]>[a]", savedLines);
            Assert.DoesNotContain("fn [YYY]>[a]", savedLines);
        }
    }
}
