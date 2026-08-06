using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Tests.Profiles;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Core.Tests.Transfer
{
    /// <summary>
    /// The export content of specs/11-feature-dialogs.md §11.5: the layout and/or the lighting
    /// configuration serialized under the current files' base names (<c>layout1.txt</c>,
    /// <c>led1.txt</c>), one file per checked box, and no led file at all where the profile has
    /// no lighting.
    /// </summary>
    public sealed class ProfileExportPlannerTests
    {
        [Fact]
        public void Plan_WithLayoutAndLighting_ReturnsBothFilesUnderTheProfileBaseNames()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadRgbSession(drive, profileNumber: 1);

            var files = ProfileExportPlanner.Plan(session, ProfileExportSelection.LayoutAndLighting);

            Assert.Equal(2, files.Count);
            Assert.Equal("layout1.txt", files[0].FileName);
            Assert.Equal("led1.txt", files[1].FileName);
            Assert.Equal(LayoutFileSerializer.Serialize(session.Layout, session.InvalidLines), files[0].Lines);
            Assert.Equal(LedFileSerializer.SerializeRgb((LightingModel)session.Lighting!), files[1].Lines);
        }

        [Fact]
        public void Plan_WithLayoutOnly_ReturnsOnlyTheLayoutFile()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadRgbSession(drive, profileNumber: 1);

            var files = ProfileExportPlanner.Plan(session, ProfileExportSelection.LayoutOnly);

            Assert.Single(files);
            Assert.Equal("layout1.txt", files[0].FileName);
        }

        [Fact]
        public void Plan_WithLightingOnly_ReturnsOnlyTheLedFile()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadRgbSession(drive, profileNumber: 1);

            var files = ProfileExportPlanner.Plan(session, ProfileExportSelection.LightingOnly);

            Assert.Single(files);
            Assert.Equal("led1.txt", files[0].FileName);
            Assert.Equal(LedFileSerializer.SerializeRgb((LightingModel)session.Lighting!), files[0].Lines);
        }

        [Fact]
        public void Plan_UsesTheSessionProfileNumberInBothFileNames()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadRgbSession(drive, profileNumber: 7);

            var files = ProfileExportPlanner.Plan(session, ProfileExportSelection.LayoutAndLighting);

            Assert.Equal("layout7.txt", files[0].FileName);
            Assert.Equal("led7.txt", files[1].FileName);
        }

        [Fact]
        public void Plan_WithLightingOnlyOnADeviceWithoutLighting_ReturnsNoFiles()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadFreestyleEdgeSession(drive);

            Assert.Empty(ProfileExportPlanner.Plan(session, ProfileExportSelection.LightingOnly));
        }

        [Fact]
        public void Plan_WithLayoutAndLightingOnADeviceWithoutLighting_ReturnsOnlyTheLayoutFile()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadFreestyleEdgeSession(drive);

            var files = ProfileExportPlanner.Plan(session, ProfileExportSelection.LayoutAndLighting);

            Assert.Single(files);
            Assert.Equal("layout1.txt", files[0].FileName);
        }

        [Fact]
        public void Plan_ReflectsEditsAndKeptInvalidLines()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, "[F1]>[a]", "[nosuchkey]>[b]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdgeRgb);

            var session = ProfileSession.Load(
                drive.CreateLocation(DeviceId.FreestyleEdgeRgb), DeviceId.FreestyleEdgeRgb, 1);

            var invalidLine = Assert.Single(session.InvalidLines);
            invalidLine.Keep = true;

            var files = ProfileExportPlanner.Plan(session, ProfileExportSelection.LayoutOnly);

            Assert.Contains("[F1]>[a]", files[0].Lines);
            Assert.Contains("[nosuchkey]>[b]", files[0].Lines);
        }

        [Fact]
        public void Plan_WithAnUndefinedSelection_Throws()
        {
            using var drive = new ProfileFixtureDrive();
            var session = LoadRgbSession(drive, profileNumber: 1);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ProfileExportPlanner.Plan(session, (ProfileExportSelection)42));
        }

        [Fact]
        public void Plan_WithANullSession_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => ProfileExportPlanner.Plan(null!, ProfileExportSelection.LayoutAndLighting));
        }

        private static ProfileSession LoadRgbSession(ProfileFixtureDrive drive, int profileNumber)
        {
            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, profileNumber, "[F1]>[a]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, profileNumber, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdgeRgb);

            return ProfileSession.Load(
                drive.CreateLocation(DeviceId.FreestyleEdgeRgb), DeviceId.FreestyleEdgeRgb, profileNumber);
        }

        private static ProfileSession LoadFreestyleEdgeSession(ProfileFixtureDrive drive)
        {
            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            return ProfileSession.Load(drive.CreateLocation(DeviceId.FreestyleEdge), DeviceId.FreestyleEdge, 1);
        }
    }
}
