using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    public sealed class VDriveScannerTests : IDisposable
    {
        private readonly string _tempRoot;

        public VDriveScannerTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "kinesis-edit-scanner-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_tempRoot);
        }

        [Theory]
        [InlineData("ADVANTAGE2")]
        [InlineData("KINESIS KB")]
        [InlineData("ADV2")]
        public void Scan_WithAdvantage2VolumeLabel_FindsAdvantage2(string label)
        {
            var volumeRoot = CreateVolume("vol", "active/version.txt");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, label));

            var location = Assert.Single(scanner.Scan());

            Assert.Equal(DeviceId.Advantage2, location.Device.Id);
            Assert.Equal(volumeRoot, location.RootPath);
            Assert.True(location.IsWritable);
            Assert.Equal(VDriveDebugFlags.None, location.DebugFlags);
        }

        [Fact]
        public void Scan_WithUnknownVolumeLabel_ReturnsNothing()
        {
            var volumeRoot = CreateVolume("vol", "firmware/version.txt");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "MY BACKUP DISK"));

            Assert.Empty(scanner.Scan());
        }

        [Fact]
        public void Scan_WithMatchingLabelButMissingMarkerFolder_ReturnsNothing()
        {
            var volumeRoot = CreateVolume("vol");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "ADV360"));

            Assert.Empty(scanner.Scan());
        }

        [Fact]
        public void Scan_WithMarkerFolderButMissingMarkerFile_ReturnsNothing()
        {
            var volumeRoot = CreateVolume("vol");
            Directory.CreateDirectory(Path.Combine(volumeRoot, "firmware"));
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "TKO"));

            Assert.Empty(scanner.Scan());
        }

        [Fact]
        public void Scan_WithDifferentlyCasedMarkerNames_FindsDevice()
        {
            var volumeRoot = CreateVolume("vol", "FIRMWARE/VERSION.TXT");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "TKO"));

            var location = Assert.Single(scanner.Scan());

            Assert.Equal(DeviceId.Tko, location.Device.Id);
            Assert.True(location.IsWritable);
        }

        [Fact]
        public void Scan_WithDuplicateMountSuffixLabel_FindsDeviceByBaseLabel()
        {
            var volumeRoot = CreateVolume("ADV360 1", "settings/settings.txt");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "ADV360 1"));

            var location = Assert.Single(scanner.Scan());

            Assert.Equal(DeviceId.Advantage360, location.Device.Id);
            Assert.Equal(volumeRoot, location.RootPath);
        }

        [Fact]
        public void Scan_WithCrossfireKeypadLabel_ReturnsNothing()
        {
            var volumeRoot = CreateVolume("vol", "active/version.txt", "firmware/version.txt");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "CROSSFIRE KEYPAD"));

            Assert.Empty(scanner.Scan());
        }

        [Theory]
        [InlineData("debug.on", VDriveDebugFlags.Debug)]
        [InlineData("DEBUG.ON", VDriveDebugFlags.Debug)]
        [InlineData("debug_firm.on", VDriveDebugFlags.FirmwareDebug)]
        [InlineData("devmode.on", VDriveDebugFlags.DevMode)]
        public void Scan_WithDebugFlagFileAtRoot_ReportsThatFlag(string flagFileName, VDriveDebugFlags expectedFlags)
        {
            var volumeRoot = CreateVolume("vol", "firmware/version.txt", flagFileName);
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "FS EDGE RGB"));

            var location = Assert.Single(scanner.Scan());

            Assert.Equal(expectedFlags, location.DebugFlags);
        }

        [Fact]
        public void Scan_WithAllDebugFlagFilesAtRoot_ReportsCombinedFlags()
        {
            var volumeRoot = CreateVolume("vol", "firmware/version.txt", "debug.on", "debug_firm.on", "devmode.on");
            var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "FS EDGE RGB"));

            var location = Assert.Single(scanner.Scan());

            var expectedFlags = VDriveDebugFlags.Debug | VDriveDebugFlags.FirmwareDebug | VDriveDebugFlags.DevMode;
            Assert.Equal(expectedFlags, location.DebugFlags);
        }

        [Fact]
        public void Scan_WithReadOnlyVersionFile_ReportsNotWritable()
        {
            var volumeRoot = CreateVolume("vol", "firmware/version.txt");
            var versionFilePath = Path.Combine(volumeRoot, "firmware", "version.txt");

            File.SetAttributes(versionFilePath, FileAttributes.ReadOnly);

            try
            {
                var scanner = CreateScanner(new VolumeCandidate(volumeRoot, "TKO"));

                var location = Assert.Single(scanner.Scan());

                Assert.Equal(DeviceId.Tko, location.Device.Id);

                if (Environment.IsPrivilegedProcess)
                {
                    // A privileged (root) process bypasses Unix write-permission checks, so the
                    // read-only attribute cannot make the open-for-write probe fail there; the
                    // device-found half of the test above stays meaningful.
                    return;
                }

                Assert.False(location.IsWritable);
            }
            finally
            {
                File.SetAttributes(versionFilePath, FileAttributes.Normal);
            }
        }

        [Fact]
        public void Scan_WithMultipleDeviceVolumesMounted_ReturnsOneLocationPerDevice()
        {
            var advantage2Root = CreateVolume("vol-adv2", "active/version.txt");
            var tkoRoot = CreateVolume("vol-tko", "firmware/version.txt");
            var advantage360Root = CreateVolume("vol-adv360", "settings/settings.txt");
            var pedalRoot = CreateVolume("vol-se2", "active/version.txt");

            var scanner = CreateScanner(
                new VolumeCandidate(advantage2Root, "ADVANTAGE2"),
                new VolumeCandidate(tkoRoot, "TKO"),
                new VolumeCandidate(advantage360Root, "ADV360"),
                new VolumeCandidate(pedalRoot, "SE2"));

            var locations = scanner.Scan();

            Assert.Equal(4, locations.Count);

            var expectedDeviceIds = new[] { DeviceId.Advantage2, DeviceId.Tko, DeviceId.Advantage360, DeviceId.SavantElite2 };
            Assert.Equal(expectedDeviceIds, locations.Select(location => location.Device.Id));
        }

        [Fact]
        public void Scan_WithTwoVolumesForSameDevice_ReturnsFirstMatchOnly()
        {
            var firstRoot = CreateVolume("vol-first", "active/version.txt");
            var secondRoot = CreateVolume("vol-second", "active/version.txt");

            var scanner = CreateScanner(
                new VolumeCandidate(firstRoot, "ADVANTAGE2"),
                new VolumeCandidate(secondRoot, "ADV2"));

            var location = Assert.Single(scanner.Scan());

            Assert.Equal(firstRoot, location.RootPath);
        }

        [Fact]
        public void Scan_WithMissingVolumeRoot_SkipsCandidate()
        {
            var missingRoot = Path.Combine(_tempRoot, "not-mounted");
            var scanner = CreateScanner(new VolumeCandidate(missingRoot, "ADVANTAGE2"));

            Assert.Empty(scanner.Scan());
        }

        private static VDriveScanner CreateScanner(params VolumeCandidate[] candidates)
        {
            return new VDriveScanner(new FakeVolumeEnumerator(candidates));
        }

        private string CreateVolume(string directoryName, params string[] relativeFilePaths)
        {
            var volumeRoot = Path.Combine(_tempRoot, directoryName);

            Directory.CreateDirectory(volumeRoot);

            foreach (var relativeFilePath in relativeFilePaths)
            {
                var fullPath = Path.Combine(volumeRoot, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, string.Empty);
            }

            return volumeRoot;
        }

        public void Dispose()
        {
            if (!Directory.Exists(_tempRoot))
            {
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(_tempRoot, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
