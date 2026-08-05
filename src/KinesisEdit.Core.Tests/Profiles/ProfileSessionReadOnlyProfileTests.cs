using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// The Advantage 360 profile 0 guard (specs/02-devices.md "Profiles 0-9": profile 0 is
    /// factory/non-programmable with no on-disk file): <see cref="ProfileSession.Load"/> and
    /// <see cref="ProfileSession.SaveAs"/> both refuse profile 0 with
    /// <see cref="ProfileReadOnlyException"/>, and a normal profile's <see cref="ProfileSession.CanSave"/> is true.
    /// </summary>
    public sealed class ProfileSessionReadOnlyProfileTests
    {
        [Fact]
        public void Load_ForAdvantage360ProfileZero_ThrowsProfileReadOnlyException()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteSettingsFile(DeviceId.Advantage360, "model=Adv360", "profile=3");
            var location = drive.CreateLocation(DeviceId.Advantage360);

            var exception = Assert.Throws<ProfileReadOnlyException>(
                () => ProfileSession.Load(location, DeviceId.Advantage360, 0));

            Assert.Equal("Profile 0 is non-programmable so you must use the Save As Button...", exception.Message);
        }

        [Fact]
        public void CanSave_ForANormalAdvantage360Profile_IsTrue()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.Advantage360, 3, "<base>");
            drive.WriteLightingFile(DeviceId.Advantage360, 3, "[ind1]>[caps][255][0][0]");
            drive.WriteSettingsFile(DeviceId.Advantage360, "model=Adv360", "profile=3");

            var location = drive.CreateLocation(DeviceId.Advantage360);
            var session = ProfileSession.Load(location, DeviceId.Advantage360, 3);

            Assert.True(session.CanSave);
        }

        [Fact]
        public void SaveAs_TargetingProfileZero_ThrowsProfileReadOnlyExceptionRegardlessOfSetAsStartup()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.Advantage360, 3, "<base>");
            drive.WriteLightingFile(DeviceId.Advantage360, 3, "[ind1]>[caps][255][0][0]");
            drive.WriteSettingsFile(DeviceId.Advantage360, "model=Adv360", "profile=3");

            var location = drive.CreateLocation(DeviceId.Advantage360);
            var session = ProfileSession.Load(location, DeviceId.Advantage360, 3);

            Assert.Throws<ProfileReadOnlyException>(() => session.SaveAs(0, setAsStartup: false));
            Assert.Throws<ProfileReadOnlyException>(() => session.SaveAs(0, setAsStartup: true));
        }
    }
}
