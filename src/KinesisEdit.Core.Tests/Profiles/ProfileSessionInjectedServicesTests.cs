using System.Reflection;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// <see cref="ProfileSession.Load"/>'s optional file service — the module's only substitution
    /// seam. Every test here runs against a v-Drive root that <b>does not exist</b>: if any read
    /// or write escaped the injected service and reached the platform, the session would throw
    /// rather than pass. Nothing here injects an ejector, because a save never ejects (see
    /// <see cref="Save_Always_WritesTheProfileFilesAndEjectsNothing"/>).
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

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService);
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

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService);
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

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService);
            var result = session.SaveAs(5, setAsStartup: true);

            Assert.True(result.Success);
            Assert.Equal(new[] { location.SettingsFilePath }, fileService.MergedSettingsPaths);
            Assert.Equal(
                new[] { "macro_speed=5", "startup_file=layout5.txt", "led_mode=led5.txt" },
                fileService.GetFile(location.SettingsFilePath));
            Assert.False(Directory.Exists(location.RootPath));
        }

        /// <summary>
        /// The inverse of the two tests that used to live here. Until issue #131,
        /// <c>ExecuteSave</c>'s last step was <c>IVDriveEjector.Eject(location.RootPath)</c>, so
        /// every <see cref="ProfileSession.Save"/> unmounted the volume behind the user's back —
        /// against docs/design/README.md's "Nothing ejects implicitly… Eject is its own deliberate
        /// action on the device card". A save now writes its files and stops.
        /// <para>
        /// It is pinned structurally as well as behaviourally, because there is deliberately no
        /// ejector left to inject and watch: nothing <see cref="ProfileSession"/> holds, takes or
        /// answers with can release a volume, so re-adding the step cannot pass unnoticed. The
        /// eject module itself stays — <c>DeviceEjectService</c>/<c>VDriveEjectNotifier</c> and
        /// the dashboard card's button are its real consumers.
        /// </para>
        /// </summary>
        [Fact]
        public void Save_Always_WritesTheProfileFilesAndEjectsNothing()
        {
            var location = CreateAbsentDriveLocation(DeviceId.FreestyleEdgeRgb);
            var fileService = CreateSeededFileService(location, profileNumber: 1, startupProfileNumber: 1);

            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1, fileService);
            var result = session.Save();

            Assert.True(result.Success);
            Assert.Equal(
                new[] { GetLayoutPath(location, 1), GetLightingPath(location, 1) },
                fileService.WrittenPaths);
            Assert.Empty(FindEjectDependencies());
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

        /// <summary>
        /// Everything about <see cref="ProfileSession"/> and <see cref="ProfileSaveResult"/> that
        /// names a type from the eject module, described for a failure message. Empty is the
        /// contract: a session cannot eject if it cannot reach an ejector.
        /// </summary>
        private static IReadOnlyList<string> FindEjectDependencies()
        {
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.Public | BindingFlags.NonPublic;

            var ejectNamespace = typeof(IVDriveEjector).Namespace;
            var found = new List<string>();

            foreach (var type in new[] { typeof(ProfileSession), typeof(ProfileSaveResult) })
            {
                foreach (var field in type.GetFields(All))
                {
                    if (field.FieldType.Namespace == ejectNamespace)
                    {
                        found.Add($"{type.Name}.{field.Name} is a {field.FieldType.Name}");
                    }
                }

                foreach (var property in type.GetProperties(All))
                {
                    if (property.PropertyType.Namespace == ejectNamespace)
                    {
                        found.Add($"{type.Name}.{property.Name} is a {property.PropertyType.Name}");
                    }
                }

                var parameters = type.GetConstructors(All)
                    .SelectMany(constructor => constructor.GetParameters())
                    .Concat(type.GetMethods(All).SelectMany(method => method.GetParameters()));

                foreach (var parameter in parameters)
                {
                    if (parameter.ParameterType.Namespace == ejectNamespace)
                    {
                        found.Add($"{type.Name} takes a {parameter.ParameterType.Name} parameter");
                    }
                }
            }

            return found;
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
