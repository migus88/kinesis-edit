using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// <see cref="ProfileSession.Load"/>'s optional file service and ejector — the module's only
    /// substitution seam. Every test here runs against a v-Drive root that <b>does not exist</b>:
    /// if any read, write or eject escaped the injected services and reached the platform, the
    /// session would throw (or the eject would report failure) rather than pass.
    /// </summary>
    public sealed class ProfileSessionInjectedServicesTests
    {
        private const string LayoutLine = "[F1]>[a]";

        private const string LightingLine = "[mono]>[10][20][30]";

        [Fact]
        public void Load_WithAnInjectedFileService_ReadsLayoutLightingAndSettingsThroughItAndTouchesNoDisk()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber: 1);

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService);

            Assert.Equal(
                new[]
                {
                    GetLayoutPath(location, 1),
                    GetLightingPath(location, 1),
                    location.SettingsFilePath
                },
                fileService.ReadPaths);

            Assert.NotNull(session.Lighting);
            Assert.False(session.IsDirty);
            Assert.False(Directory.Exists(location.RootPath));
        }

        /// <summary>
        /// The trap this seam exists to close: a session whose layout comes from an injected file
        /// service but whose <c>SettingsService</c> is the shared static one would read its
        /// settings off the real disk. The settings snapshot is observable through
        /// <c>PostSaveMessage</c>, which branches on whether the saved profile is the startup
        /// profile — so a session that ignored the injected service for settings could not produce
        /// both wordings from two in-memory settings files.
        /// </summary>
        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void Save_TakesItsStartupProfileFromSettingsReadThroughTheInjectedFileService(
            int startupProfileNumber,
            bool expectsStartupWording)
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber);
            var ejector = new RecordingVDriveEjector();

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService, ejector);
            var result = session.Save();

            Assert.Contains(location.SettingsFilePath, fileService.ReadPaths);
            Assert.True(result.Success);
            Assert.Equal(
                ProfileSaveMessageCatalog.GetMessage(DeviceId.FreestyleEdgeRgb, 1, expectsStartupWording),
                result.PostSaveMessage);
            Assert.False(Directory.Exists(location.RootPath));
        }

        [Fact]
        public void Save_WithAnInjectedFileService_WritesLayoutAndLightingOnlyThroughIt()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber: 1);
            var ejector = new RecordingVDriveEjector();

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService, ejector);
            var bKey = session.Layout.Layers[0].FindByPositionKeyCode(Key("b").Code)!;
            Assert.True(bKey.Remap(Key("c")));

            var expectedLayoutLines = LayoutFileSerializer.Serialize(session.Layout, session.InvalidLines);
            var result = session.Save();

            Assert.True(result.Success);
            Assert.Equal(
                new[] { GetLayoutPath(location, 1), GetLightingPath(location, 1) },
                fileService.WrittenPaths);
            Assert.All(fileService.WriteAllowCreateFlags, allowCreate => Assert.True(allowCreate));
            Assert.Equal(expectedLayoutLines, fileService.GetFile(GetLayoutPath(location, 1)));
            Assert.False(Directory.Exists(location.RootPath));
        }

        [Fact]
        public void SaveAs_WithSetAsStartup_MergesTheSettingsFileThroughTheInjectedFileService()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber: 1);
            var ejector = new RecordingVDriveEjector();

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService, ejector);
            var result = session.SaveAs(5, setAsStartup: true);

            Assert.True(result.Success);
            Assert.Equal(new[] { location.SettingsFilePath }, fileService.MergedSettingsPaths);
            Assert.Equal(
                new[] { "macro_speed=5", "startup_file=layout5.txt", "led_mode=led5.txt" },
                fileService.GetFile(location.SettingsFilePath));
            Assert.False(Directory.Exists(location.RootPath));
        }

        [Fact]
        public void Save_WithAnInjectedEjector_EjectsTheDriveRootThroughItAndReportsItsResult()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber: 1);
            var ejector = new RecordingVDriveEjector();

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService, ejector);
            var result = session.Save();

            // The platform ejector could not have produced this on any platform: Windows/Linux
            // report unsupported, and macOS would have run "diskutil unmount" on a path that does
            // not exist. A true Ejected is proof the injected ejector ran instead.
            Assert.Equal(new[] { location.RootPath }, ejector.EjectedPaths);
            Assert.True(result.Ejected);
        }

        [Fact]
        public void Save_WithAnInjectedEjectorThatFails_ReportsNotEjectedWithoutFailingTheSave()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber: 1);
            var ejector = new RecordingVDriveEjector
            {
                Result = new VDriveEjectResult { Succeeded = false, Message = "busy" }
            };

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService, ejector);
            var result = session.Save();

            Assert.True(result.Success);
            Assert.False(result.Ejected);
            Assert.Single(ejector.EjectedPaths);
        }

        /// <summary>
        /// A device with no profile-orchestrated led file still routes everything it does read
        /// through the injected service — nothing falls back to the shared default per file kind.
        /// </summary>
        [Fact]
        public void Load_WithAnInjectedFileServiceOnADeviceWithoutLighting_ReadsOnlyLayoutAndSettings()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestylePro);
            var fileService = new RecordingVDriveFileService();
            fileService.AddFile(GetLayoutPath(location, 1), LayoutLine);
            fileService.AddFile(location.SettingsFilePath, "macro_speed=5");

            var session = ProfileSession.Load(location, DeviceId.FreestylePro, 1, fileService);

            Assert.Null(session.Lighting);
            Assert.Equal(
                new[] { GetLayoutPath(location, 1), location.SettingsFilePath },
                fileService.ReadPaths);
            Assert.False(Directory.Exists(location.RootPath));
        }

        private static RecordingVDriveFileService CreateSeededFileService(
            VDriveLocation location,
            int profileNumber,
            int startupProfileNumber)
        {
            var fileService = new RecordingVDriveFileService();

            fileService.AddFile(GetLayoutPath(location, profileNumber), LayoutLine);
            fileService.AddFile(GetLightingPath(location, profileNumber), LightingLine);
            fileService.AddFile(
                location.SettingsFilePath,
                "macro_speed=5",
                $"startup_file=layout{startupProfileNumber}.txt",
                $"led_mode=led{startupProfileNumber}.txt");

            return fileService;
        }

        private static VDriveLocation CreateAbsentDriveLocation(DeviceId deviceId)
        {
            return new VDriveLocation
            {
                Device = DeviceCatalog.GetById(deviceId),
                RootPath = Path.Combine(Path.GetTempPath(), "KinesisEditNoSuchDrive-" + Guid.NewGuid().ToString("N")),
                IsWritable = true
            };
        }

        private static string GetLayoutPath(VDriveLocation location, int profileNumber)
        {
            return Path.Combine(location.LayoutsFolderPath!, $"layout{profileNumber}.txt");
        }

        private static string GetLightingPath(VDriveLocation location, int profileNumber)
        {
            return Path.Combine(location.LightingFolderPath!, $"led{profileNumber}.txt");
        }

        private static KeyDefinition Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in Gen1.");
        }
    }
}
