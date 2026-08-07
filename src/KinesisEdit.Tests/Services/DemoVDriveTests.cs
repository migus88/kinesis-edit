using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The demo drive's root and the path test that scopes every demo behaviour. The load-bearing
    /// property is the last one: a demo root that could ever equal a mount point would put the
    /// fixture reader in front of somebody's real drive and the write refusal in front of their
    /// real files.
    /// </summary>
    public class DemoVDriveTests
    {
        /// <summary>
        /// Roots the discovery layer actually produces: <c>/Volumes/&lt;label&gt;</c> on macOS, a
        /// drive root on Windows, <c>/media|/run/media/&lt;user&gt;/&lt;label&gt;</c> on Linux
        /// (docs/app/vdrive.md, "Discovery") — plus a plausible export target, since an export
        /// writes through the same file service.
        /// </summary>
        public static TheoryData<string> RealPaths =>
        [
            "/Volumes/FS EDGE RGB",
            "/Volumes/FS EDGE RGB/layouts/layout1.txt",
            "/Volumes/TKO",
            "/Volumes/ADV360 1",
            "E:\\",
            "E:\\layouts\\layout1.txt",
            "C:\\Users\\someone\\Documents\\layout1.txt",
            "/media/someone/FS EDGE RGB",
            "/run/media/someone/FS EDGE RGB",
            "/Users/someone/Documents/KinesisEdit/layout1.txt",
            "/"
        ];

        [Fact]
        public void GetRootPath_NamesTheDevice()
        {
            Assert.Equal(
                DemoVDrive.RootPrefix + nameof(DeviceId.FreestyleEdgeRgb),
                DemoVDrive.GetRootPath(DeviceId.FreestyleEdgeRgb));

            Assert.NotEqual(
                DemoVDrive.GetRootPath(DeviceId.FreestyleEdgeRgb),
                DemoVDrive.GetRootPath(DeviceId.Tko));
        }

        [Fact]
        public void CreateLocation_IsNeverWritable()
        {
            var location = DemoVDrive.CreateLocation(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb));

            // Demo mode is "not connected or no read/write access" (03 §3.5), and it is this flag
            // the app already reads to route preferences to ReadOnlyAppPreferencesStore and to
            // keep app_settings.txt unwritten (spec 08 §3).
            Assert.False(location.IsWritable);
            Assert.Equal(DemoVDrive.GetRootPath(DeviceId.FreestyleEdgeRgb), location.RootPath);
        }

        [Fact]
        public void CreateLocation_DerivesEveryWorkingPathFromTheCatalog()
        {
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);
            var location = DemoVDrive.CreateLocation(device);

            // Not asserted against literals: the point is that the synthetic location resolves the
            // same catalog-driven folders a discovered one does (03 §3.4), which is what lets the
            // fixtures be addressed by real paths.
            Assert.Equal(
                Path.Combine(location.RootPath, device.VersionFolder!, device.VersionFile!),
                location.VersionFilePath);
            Assert.Equal(
                Path.Combine(location.RootPath, device.SettingsFolder!, device.SettingsFile!),
                location.SettingsFilePath);
            Assert.Equal(
                Path.Combine(location.RootPath, device.LayoutScheme.LayoutFolder!),
                location.LayoutsFolderPath);
            Assert.Equal(
                Path.Combine(location.RootPath, device.LayoutScheme.LightingFolder!),
                location.LightingFolderPath);
        }

        [Fact]
        public void IsUnderRoot_AcceptsEveryPathTheLocationDerives()
        {
            var location = DemoVDrive.CreateLocation(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb));

            Assert.True(DemoVDrive.IsUnderRoot(location.RootPath));
            Assert.True(DemoVDrive.IsUnderRoot(location.VersionFilePath));
            Assert.True(DemoVDrive.IsUnderRoot(location.SettingsFilePath));
            Assert.True(DemoVDrive.IsUnderRoot(SettingsService.GetAppSettingsFilePath(location)));
            Assert.True(DemoVDrive.IsUnderRoot(Path.Combine(location.LayoutsFolderPath!, "layout1.txt")));
            Assert.True(DemoVDrive.IsUnderRoot(Path.Combine(location.LightingFolderPath!, "led1.txt")));
        }

        [Fact]
        public void IsUnderRoot_RefusesNothingness()
        {
            Assert.False(DemoVDrive.IsUnderRoot(null));
            Assert.False(DemoVDrive.IsUnderRoot(string.Empty));
        }

        [Theory]
        [MemberData(nameof(RealPaths))]
        public void IsUnderRoot_RejectsRealPaths(string path)
        {
            Assert.False(DemoVDrive.IsUnderRoot(path));
        }

        [Fact]
        public void DemoPaths_AreNeverFullyQualifiedFilesystemPaths()
        {
            // The general form of the theory above, and the reason the root is scheme-shaped: a
            // mount root is by definition a fully qualified path on its own platform (/Volumes/…,
            // E:\, /media/…), and no demo path ever is on any platform — so no demo path can equal
            // one, for any volume label anyone ever ships. It also means a demo path that somehow
            // escaped the demo file service resolves against nothing rather than against a file.
            var location = DemoVDrive.CreateLocation(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb));

            Assert.False(Path.IsPathFullyQualified(location.RootPath));
            Assert.False(Path.IsPathFullyQualified(location.VersionFilePath));
            Assert.False(Path.IsPathFullyQualified(location.SettingsFilePath));
            Assert.False(Path.IsPathFullyQualified(location.LayoutsFolderPath!));
            Assert.False(Path.IsPathFullyQualified(location.LightingFolderPath!));
        }
    }
}
