using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// The pre-save validation gate (specs/04-layout-file-format.md §5.3's "validate macro
    /// capacity first"; this issue's "Pre-save validation" scope bullet extends the same gate to
    /// every reported limit): <see cref="ProfileSession.Save"/> stops and writes nothing when
    /// <see cref="KeyboardLayout.Validate"/> reports a breach.
    /// </summary>
    public sealed class ProfileSessionValidationTests
    {
        [Fact]
        public void Save_WhenLayoutBreachesADeviceLimit_StopsBeforeWritingAnyFile()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var originalBytes = File.ReadAllBytes(drive.GetLayoutFilePath(DeviceId.FreestyleEdge, 1));
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            // FS Edge does not support multi-modifiers (Advantage360 only, 11 §11.2); the
            // tolerant load path stores the code anyway (04 §4.2), which Validate() then reports.
            var aKey = session.Layout.Layers[0].FindByPositionKeyCode(
                KeyRegistry.FindByToken("a", TokenDialect.Gen1)!.Code)!;
            Assert.True(aKey.ApplyMultiModifiers("cxxs"));

            var result = session.Save();

            Assert.False(result.Success);
            Assert.Contains(result.Violations, violation => violation.Kind == ModelViolationKind.MultiModifiersNotSupported);
            Assert.Null(result.PostSaveMessage);
            Assert.False(result.Ejected);
            Assert.Equal(originalBytes, File.ReadAllBytes(drive.GetLayoutFilePath(DeviceId.FreestyleEdge, 1)));
        }
    }
}
