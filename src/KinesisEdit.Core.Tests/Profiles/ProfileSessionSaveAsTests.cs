using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// <see cref="ProfileSession.SaveAs"/>'s startup-profile pairing (specs/07-lighting.md §1.2
    /// "Save As to a profile number switches both the current layout file and the current led
    /// file at once"): with <c>setAsStartup: true</c> the settings file gains the new
    /// <c>startup_file</c>/<c>led_mode</c> pair; with <c>setAsStartup: false</c> it is untouched.
    /// </summary>
    public sealed class ProfileSessionSaveAsTests
    {
        [Fact]
        public void SaveAs_WithSetAsStartupTrue_UpdatesStartupFileAndLedModeInPlace()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, "[F1]>[a]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(
                DeviceId.FreestyleEdgeRgb,
                "macro_speed=5",
                "startup_file=layout1.txt",
                "led_mode=led1.txt");

            var location = drive.CreateLocation(DeviceId.FreestyleEdgeRgb);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1);

            var result = session.SaveAs(5, setAsStartup: true);

            Assert.True(result.Success);
            Assert.Equal(
                new[] { "macro_speed=5", "startup_file=layout5.txt", "led_mode=led5.txt" },
                File.ReadAllLines(drive.GetSettingsFilePath(DeviceId.FreestyleEdgeRgb)));
            Assert.True(File.Exists(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 5)));
            Assert.True(File.Exists(drive.GetLightingFilePath(DeviceId.FreestyleEdgeRgb, 5)));

            // The profile this session was loaded from is untouched by a SaveAs to a different slot.
            Assert.Equal(
                new[] { "[F1]>[a]" },
                File.ReadAllLines(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1)));
        }

        [Fact]
        public void SaveAs_WithSetAsStartupFalse_LeavesSettingsFileUntouched()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, "[F1]>[a]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(
                DeviceId.FreestyleEdgeRgb,
                "macro_speed=5",
                "startup_file=layout1.txt",
                "led_mode=led1.txt");

            var location = drive.CreateLocation(DeviceId.FreestyleEdgeRgb);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1);
            var originalSettingsBytes = File.ReadAllBytes(drive.GetSettingsFilePath(DeviceId.FreestyleEdgeRgb));

            var result = session.SaveAs(5, setAsStartup: false);

            Assert.True(result.Success);
            Assert.Equal(originalSettingsBytes, File.ReadAllBytes(drive.GetSettingsFilePath(DeviceId.FreestyleEdgeRgb)));
            Assert.True(File.Exists(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 5)));
        }
    }
}
