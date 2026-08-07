using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The fixture v-Drive itself: that it is complete, and that every file in it parses through
    /// the <b>production</b> parsers.
    /// <para>
    /// "It loaded" is not the assertion. The layout parser <i>collects</i> lines it cannot apply
    /// rather than failing (specs/04-layout-file-format.md §5), so a fixture with a typo would load
    /// fine, show nothing on the board and drop the bad line on the first save. Zero invalid lines
    /// is the assertion — and, for the two files that have a canonical form, that a save would
    /// rewrite them byte for byte, which is what says a fixture is a file this app could have
    /// written rather than something merely tolerated.
    /// </para>
    /// </summary>
    public class DemoVDriveFixturesTests
    {
        private const string LayoutFileName = "layout1.txt";
        private const string LedFileName = "led1.txt";
        private const int DelayMilliseconds = 500;

        private readonly DemoVDriveFixtures _fixtures = DemoVDriveFixtures.Default;
        private readonly VDriveLocation _location =
            DemoVDrive.CreateLocation(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb));

        [Fact]
        public void Fixtures_CoverTheFreestyleEdgeRgbAndNothingElse()
        {
            Assert.Equal([DeviceId.FreestyleEdgeRgb], _fixtures.Devices);
        }

        [Fact]
        public void Fixtures_AnswerEveryPathTheRgbDriveNeeds()
        {
            // Every expectation here is derived, not spelled: three are VDriveLocation's own
            // computed properties and the fourth is Core's app-settings path, so the fixture set is
            // filed against the catalog's folder names rather than against a copy of them.
            string[] expected =
            [
                LayoutPath(),
                LedPath(),
                SettingsService.GetAppSettingsFilePath(_location),
                _location.SettingsFilePath,
                _location.VersionFilePath
            ];

            Assert.Equal(
                expected.OrderBy(path => path, StringComparer.Ordinal),
                _fixtures.Paths.OrderBy(path => path, StringComparer.Ordinal));
        }

        [Fact]
        public void Layout_ParsesWithNoInvalidLines()
        {
            var result = ParseLayout();

            Assert.Empty(result.InvalidLines);
        }

        [Fact]
        public void Layout_ReportsNoLimitViolations()
        {
            // Validate() reports rather than enforces (docs/app/keyboard-model.md), so a violation
            // would not stop the load — it would put an amber advisory on a demo board that exists
            // to show the app at its best.
            Assert.Empty(ParseLayout().Layout.Validate());
        }

        [Fact]
        public void Layout_IsAlreadyWhatASaveWouldWrite()
        {
            var result = ParseLayout();

            Assert.Equal(
                _fixtures.ReadLines(LayoutPath()),
                LayoutFileSerializer.Serialize(result.Layout, result.InvalidLines));
        }

        [Fact]
        public void Layout_CarriesRemapsOnBothLayers()
        {
            var layout = ParseLayout().Layout;

            Assert.Contains(layout.FindLayer(0)!.Keys, key => key.IsModified);
            Assert.Contains(layout.FindLayer(1)!.Keys, key => key.IsModified);
            Assert.Equal(1, layout.TapAndHoldCount);
        }

        [Fact]
        public void Layout_CarriesMacrosWithCoTriggersAndDelays()
        {
            var layout = ParseLayout().Layout;
            var macros = layout.EnumerateMacros().ToArray();
            var delayToken = MacroDelayTokens.BuildCustomToken(DelayMilliseconds);

            Assert.Equal(3, macros.Length);
            Assert.Contains(macros, macro => macro.CoTriggers.Count > 1);

            // By token, not by code: on the RGB family the parser reads d125/d500 back as the
            // generated 10085 + N keys while MacroDelayTokens.ResolveCustom answers the legacy
            // 10007/10008 rows (docs/app/layout-files.md, "Parsing"). The token is what the file
            // carries and what both spellings agree on.
            Assert.Contains(
                macros,
                macro => macro.Keystrokes.Any(
                    keystroke => keystroke.Key.GetToken(TokenDialect.Gen1) == delayToken));
        }

        [Fact]
        public void Led_OpensOnAnAnimatedModeWithPerKeyColorsAndAnFnBaseColor()
        {
            var lighting = LedFileParser.ParseRgb(_fixtures.ReadLines(LedPath()));

            Assert.Equal(LightingMode.Breathe, lighting.TopLayer.Mode);
            Assert.NotEmpty(lighting.TopLayer.KeyColors);
            Assert.Equal(LightingMode.Rain, lighting.FnLayer.Mode);
            Assert.False(lighting.FnLayer.BaseColor.IsBlack);
            Assert.False(lighting.FnLayer.EffectColor.IsBlack);
        }

        [Fact]
        public void Led_IsAlreadyWhatASaveWouldWrite()
        {
            var lines = _fixtures.ReadLines(LedPath());

            Assert.Equal(lines, LedFileSerializer.SerializeRgb(LedFileParser.ParseRgb(lines)));
        }

        [Fact]
        public void KeyboardSettings_AreFullAndRealistic()
        {
            // The mandatory file: SettingsService.LoadKeyboardSettings throws FileNotFoundException
            // when it is missing, which aborts the whole profile load into an error dialog.
            var settings = KeyboardSettingsParser.Parse(_fixtures.ReadLines(_location.SettingsFilePath));

            Assert.Equal(1, settings.StartupProfileNumber);
            Assert.Equal(LedFileName, settings.LedMode);
            Assert.Equal(5, settings.MacroSpeed);
            Assert.Equal(2, settings.StatusPlaySpeed);
            Assert.True(settings.IsVDriveAutoMountEnabled);
            Assert.False(settings.IsGameModeEnabled);
        }

        [Fact]
        public void AppSettings_PopulateSomeOfTheColorPickersSlots()
        {
            var settings = AppSettingsParser.Parse(
                _fixtures.ReadLines(SettingsService.GetAppSettingsFilePath(_location)));

            var populated = settings.CustomColors.Count(color => color is not null);

            // Some, not all: the picker's empty and filled slots both need to be on screen.
            Assert.InRange(populated, 1, AppSettings.CustomColorCount - 1);
            Assert.NotNull(settings.IsAppIntroMessageHidden);
        }

        [Fact]
        public void VersionFile_ParsesAndOpensTheFirmwareGates()
        {
            var versionFile = VersionFileParser.Parse(
                DeviceId.FreestyleEdgeRgb,
                _fixtures.ReadLines(_location.VersionFilePath));

            // A missing or unparseable "KBD Firmware:" line is exactly what DeviceMonitorService
            // reports as VDriveHealth.Error, so the demo session would open wearing a v-Drive Error.
            Assert.NotNull(versionFile.KeyboardFirmware);
            Assert.NotNull(versionFile.LedFirmware);
            Assert.Equal("FS Edge RGB", versionFile.ModelName);

            // isDemoMode false on purpose: every gate passes in demo mode (spec 09 §2), so asking
            // with the demo flag set would assert nothing about the fixture's own versions.
            var firmware = FirmwareState.FromVersionFile(versionFile, isDemoMode: false);

            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(
                DeviceId.FreestyleEdgeRgb,
                LightingMode.Fireball,
                firmware));
            Assert.True(LightingAvailability.IsFnLayerLightingAvailable(DeviceId.FreestyleEdgeRgb, firmware));
        }

        [Fact]
        public void ReadLines_ReportsAMissingFixtureAsAMissingFile()
        {
            Assert.Throws<FileNotFoundException>(
                () => _fixtures.ReadLines(Path.Combine(_location.LayoutsFolderPath!, "layout9.txt")));
        }

        private string LayoutPath()
        {
            return Path.Combine(_location.LayoutsFolderPath!, LayoutFileName);
        }

        private string LedPath()
        {
            return Path.Combine(_location.LightingFolderPath!, LedFileName);
        }

        private LayoutParseResult ParseLayout()
        {
            return new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(_fixtures.ReadLines(LayoutPath()));
        }
    }
}
