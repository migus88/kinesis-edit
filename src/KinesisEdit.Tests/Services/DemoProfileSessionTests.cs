using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The whole point, end to end: a profile session loaded over the demo file service comes back
    /// populated, and nothing underneath it is touched.
    /// <para>
    /// The inner service is the recording fake, not a stub that answers: if a single read escaped
    /// the demo root it would either throw or show up in <c>ReadCount</c>, so "no disk access" is
    /// asserted rather than assumed. Core learns nothing from any of this — it is handed a file
    /// service and cannot tell a fixture-backed one from a drive-backed one
    /// (docs/app/profiles.md, "The service seam").
    /// </para>
    /// </summary>
    public class DemoProfileSessionTests
    {
        private readonly FakeVDriveFileService _inner = new();

        [Fact]
        public void Load_ProducesAPopulatedLayoutAndLightingModel()
        {
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);
            var location = new DemoDeviceProvider().TryCreateLocation(device)!;
            var factory = new ProfileSessionFactory(new DemoVDriveFileService(_inner));

            var session = factory.Load(location, device.Id, device.LayoutScheme.FirstProfileNumber);

            Assert.NotEqual(0, session.Layout.ModifiedKeyCount);
            Assert.NotEqual(0, session.Layout.MacroCount);
            Assert.Equal(1, session.Layout.TapAndHoldCount);
            Assert.Empty(session.InvalidLines);

            var lighting = Assert.IsType<LightingModel>(session.Lighting);

            Assert.Equal(LightingMode.Breathe, lighting.TopLayer.Mode);
            Assert.NotEmpty(lighting.TopLayer.KeyColors);

            // Nothing was written and nothing was read off the real service; a session freshly
            // loaded also has nothing to save.
            Assert.False(session.IsDirty);
            Assert.Equal(0, _inner.ReadCount);
            Assert.Empty(_inner.WrittenPaths);
        }

        [Fact]
        public void Load_FindsTheKeyboardSettingsFile()
        {
            // LoadKeyboardSettings throws FileNotFoundException for a missing file, and
            // ProfileSession.Load reads the settings snapshot on every load — so a fixture set
            // without kbd_settings.txt would turn every demo open into an error dialog. The load
            // above already depends on it; this states the dependency, and the values, outright.
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);
            var location = new DemoDeviceProvider().TryCreateLocation(device)!;
            var settingsService = new SettingsService(new DemoVDriveFileService(_inner));

            var settings = settingsService.LoadKeyboardSettings(location);

            Assert.Equal(1, settings.StartupProfileNumber);
            Assert.Equal(5, settings.MacroSpeed);
            Assert.Equal(0, _inner.ReadCount);
        }

        [Fact]
        public void Load_ReadsTheAppSettingsSwatches()
        {
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);
            var location = new DemoDeviceProvider().TryCreateLocation(device)!;
            var settingsService = new SettingsService(new DemoVDriveFileService(_inner));

            var settings = settingsService.LoadAppSettings(location);

            Assert.Contains(settings.CustomColors, color => color is not null);
            Assert.Equal(0, _inner.ReadCount);
        }
    }
}
