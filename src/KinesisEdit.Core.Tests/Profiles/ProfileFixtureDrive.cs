using System.Text;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// A real temporary v-Drive folder tree for <see cref="Profiles.ProfileSession"/> tests,
    /// following the fixture-drive pattern of <c>SettingsServiceTests</c>: a real temp directory,
    /// the real file service underneath <see cref="Profiles.ProfileSession"/>, byte-exact file
    /// assertions.
    /// </summary>
    internal sealed class ProfileFixtureDrive : IDisposable
    {
        public string RootPath { get; }

        public ProfileFixtureDrive()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public VDriveLocation CreateLocation(DeviceId deviceId)
        {
            var device = DeviceCatalog.GetById(deviceId);

            return new VDriveLocation
            {
                Device = device,
                RootPath = RootPath,
                IsWritable = true
            };
        }

        public void WriteLayoutFile(DeviceId deviceId, int profileNumber, params string[] lines)
        {
            var location = CreateLocation(deviceId);

            WriteFile(Path.Combine(location.LayoutsFolderPath!, $"layout{profileNumber}.txt"), lines);
        }

        public void WriteLightingFile(DeviceId deviceId, int profileNumber, params string[] lines)
        {
            var location = CreateLocation(deviceId);

            WriteFile(Path.Combine(location.LightingFolderPath!, $"led{profileNumber}.txt"), lines);
        }

        public void WriteSettingsFile(DeviceId deviceId, params string[] lines)
        {
            var location = CreateLocation(deviceId);

            WriteFile(location.SettingsFilePath, lines);
        }

        public string GetLayoutFilePath(DeviceId deviceId, int profileNumber)
        {
            return Path.Combine(CreateLocation(deviceId).LayoutsFolderPath!, $"layout{profileNumber}.txt");
        }

        public string GetLightingFilePath(DeviceId deviceId, int profileNumber)
        {
            return Path.Combine(CreateLocation(deviceId).LightingFolderPath!, $"led{profileNumber}.txt");
        }

        public string GetSettingsFilePath(DeviceId deviceId)
        {
            return CreateLocation(deviceId).SettingsFilePath;
        }

        public static void WriteFile(string path, params string[] lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, BuildFileBytes(lines));
        }

        public static byte[] BuildFileBytes(params string[] lines)
        {
            var content = new StringBuilder();

            foreach (var line in lines)
            {
                content.Append(line);
                content.Append(Environment.NewLine);
            }

            return Encoding.Latin1.GetBytes(content.ToString());
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
