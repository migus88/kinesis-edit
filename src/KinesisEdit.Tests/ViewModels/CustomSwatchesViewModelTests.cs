using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The settings screen's custom-swatch strip: the twelve <c>cust_color_N</c> slots of
    /// specs/08-settings.md §3, shown, replaceable and clearable.
    /// <para>
    /// The load-bearing case here is the <b>cross-consumer</b> one. Before issue #95 the colour
    /// picker loaded and saved <c>app_settings.txt</c> for itself, which was safe only while it was
    /// the only consumer; this strip is the second, and two independent readers show each other
    /// stale slots the moment either writes. So the tests below put both view models on one store
    /// and assert each sees the other's write <i>with no reload</i>, in both directions.
    /// </para>
    /// </summary>
    public sealed class CustomSwatchesViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _filePath;
        private readonly VDriveLocation _location;
        private readonly VDriveFileService _fileService = new();
        private readonly ISettingsService _settings;

        public CustomSwatchesViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);

            _location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb) with { RootPath = _tempDirectory };
            _filePath = VDriveAppPreferencesStore.GetFilePath(_location);
            _settings = TestDevices.CreateSettingsService(_fileService);

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        [Fact]
        public void Slots_AreTheTwelveOfTheSpec_AndOpenOnTheFirst()
        {
            var strip = CreateStrip();

            Assert.Equal(AppSettings.CustomColorCount, strip.Slots.Count);
            Assert.Equal(new[] { 1, 12 }, new[] { strip.Slots[0].SlotNumber, strip.Slots[^1].SlotNumber });
            Assert.All(strip.Slots, slot => Assert.False(slot.HasColor));

            Assert.Same(strip.Slots[0], strip.SelectedSlot);
            Assert.True(strip.Slots[0].IsSelected);
            Assert.False(strip.Slots[1].IsSelected);
        }

        [Fact]
        public void Refresh_ReadsTheStoredSlots_AndLeavesUnsetOnesEmpty()
        {
            CreateFile("cust_color_3=[20][30][40]");

            var strip = CreateStrip();

            strip.Refresh();

            Assert.Equal("#141E28", strip.Slots[2].ColorHex);

            // An unset slot is "no swatch stored", not black.
            Assert.False(strip.Slots[0].HasColor);
        }

        [Fact]
        public void SelectSlotCommand_MovesTheRing_AndLoadsAFilledSlotsColourIntoTheBox()
        {
            CreateFile("cust_color_5=[10][20][30]");

            var strip = CreateStrip();

            strip.Refresh();
            strip.SelectSlotCommand.Execute(strip.Slots[4]);

            Assert.Same(strip.Slots[4], strip.SelectedSlot);
            Assert.False(strip.Slots[0].IsSelected);
            Assert.True(strip.Slots[4].IsSelected);
            Assert.Equal("#0A141E", strip.ColorHex);
            Assert.Equal(CustomSwatchesViewModel.FormatSelection(strip.Slots[4].Caption), strip.SelectionCaption);

            // An empty slot leaves whatever the user was about to store alone.
            strip.SelectSlotCommand.Execute(strip.Slots[7]);

            Assert.Equal("#0A141E", strip.ColorHex);
        }

        [Fact]
        public void ReplaceCommand_WritesTheTypedColourIntoTheSelectedSlot()
        {
            var strip = CreateStrip();

            strip.SelectSlotCommand.Execute(strip.Slots[3]);
            strip.ColorHex = "#0080FF";
            strip.ReplaceCommand.Execute(null);

            Assert.Equal("#0080FF", strip.Slots[3].ColorHex);
            Assert.Contains("cust_color_4=[0][128][255]", File.ReadAllLines(_filePath), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReplaceCommand_WithAColourThatDoesNotParse_IsUnavailableAndWritesNothing()
        {
            var strip = CreateStrip();

            strip.ColorHex = "not a colour";

            Assert.False(strip.ReplaceCommand.CanExecute(null));

            strip.ReplaceCommand.Execute(null);

            Assert.False(strip.Slots[0].HasColor);
            Assert.False(File.Exists(_filePath));
        }

        [Fact]
        public void ClearCommand_UnsetsTheSlotRatherThanPaintingItBlack()
        {
            CreateFile("cust_color_1=[255][0][128]", "save_msg=on");

            var strip = CreateStrip();

            strip.Refresh();

            Assert.True(strip.ClearCommand.CanExecute(null));

            strip.ClearCommand.Execute(null);

            // Unset, not black: an empty slot is "no swatch stored" (spec 08 §3).
            Assert.False(strip.Slots[0].HasColor);
            Assert.False(strip.ClearCommand.CanExecute(null));

            // Spec 08 §3: an unset slot's key is never written as `cust_color_1=`.
            Assert.DoesNotContain(
                File.ReadAllLines(_filePath),
                line => line.Equals("cust_color_1=", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ClearCommand_TakesTheLineOffTheDrive_AndTheSwatchDoesNotComeBack()
        {
            // The clear-does-not-survive-a-restart defect of issue #95, inverted. Saving an
            // app-settings model is a read-modify-write (specs/08-settings.md §1) and the
            // serializer skips an unset slot rather than emitting `cust_color_1=` (invariant 5),
            // so a merge alone could only add or replace a swatch — the old line survived on disk
            // and came back on the next launch. Core now expresses a cleared slot as a *removal*,
            // and this test is what says so.
            //
            // cust_color_10 is seeded on purpose: cust_color_1 is a prefix of it, so a StartsWith
            // deletion would take both.
            CreateFile("cust_color_1=[255][0][128]", "cust_color_10=[100][100][100]", "save_msg=on");

            var strip = CreateStrip();

            strip.Refresh();
            strip.ClearCommand.Execute(null);

            var lines = File.ReadAllLines(_filePath);

            Assert.DoesNotContain(
                lines,
                line => line.StartsWith("cust_color_1=", StringComparison.OrdinalIgnoreCase));

            // Surgical: the colliding slot and the foreign flag line are untouched.
            Assert.Contains("cust_color_10=[100][100][100]", lines, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("save_msg=on", lines, StringComparer.OrdinalIgnoreCase);

            // The next launch — a store that reads the file afresh — sees an empty slot 1.
            var relaunched = CreateStrip();

            relaunched.Refresh();

            Assert.False(relaunched.Slots[0].HasColor);
            Assert.Equal("#646464", relaunched.Slots[9].ColorHex);
        }

        [Fact]
        public void ClearCommand_ForAnEmptySlot_IsUnavailable()
        {
            var strip = CreateStrip();

            Assert.False(strip.ClearCommand.CanExecute(null));
        }

        [Fact]
        public void InDemoMode_TheStripReadsTheDriveButCannotEdit()
        {
            // Spec 08 §3 bans saving app settings in demo mode, not loading them — and the strip
            // matches ColorPickerViewModel.CanStoreCustomColors, whose "Add to Custom Colors" is
            // already unavailable there.
            CreateFile("cust_color_2=[1][2][3]");

            var strip = new CustomSwatchesViewModel(new ReadOnlyAppPreferencesStore(CreateStore()));

            strip.Refresh();

            Assert.False(strip.CanEdit);
            Assert.Equal("#010203", strip.Slots[1].ColorHex);
            Assert.False(strip.ReplaceCommand.CanExecute(null));
            Assert.False(strip.ClearCommand.CanExecute(null));

            strip.ReplaceCommand.Execute(null);
            strip.ClearCommand.Execute(null);

            Assert.Equal(new[] { "cust_color_2=[1][2][3]" }, File.ReadAllLines(_filePath));
        }

        [Fact]
        public async Task WhenTheColourPickerStoresASwatch_TheStripSeesItWithNoReload()
        {
            // The stale-slot bug this store exists to kill, in the direction it used to happen:
            // the picker wrote the file, and nothing else in the app knew.
            var store = CreateStore();
            var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var picker = new ColorPickerViewModel(device, store);
            var strip = new CustomSwatchesViewModel(store);

            await picker.LoadAsync();
            strip.Refresh();

            Assert.False(strip.Slots[0].HasColor);

            picker.Color = new LedColor(9, 8, 7);
            picker.StoreCustomColorCommand.Execute(null);

            Assert.Equal("#090807", strip.Slots[0].ColorHex);
        }

        [Fact]
        public async Task WhenTheStripReplacesASwatch_TheColourPickerSeesItWithNoReload()
        {
            var store = CreateStore();
            var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var picker = new ColorPickerViewModel(device, store);
            var strip = new CustomSwatchesViewModel(store);

            await picker.LoadAsync();
            strip.Refresh();

            strip.SelectSlotCommand.Execute(strip.Slots[6]);
            strip.ColorHex = "#FF8000";
            strip.ReplaceCommand.Execute(null);

            Assert.Equal("#FF8000", picker.CustomColors[6].ColorHex);

            strip.ClearCommand.Execute(null);

            Assert.False(picker.CustomColors[6].HasColor);
        }

        [Fact]
        public void Constructor_WithoutAStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomSwatchesViewModel(null!));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private CustomSwatchesViewModel CreateStrip()
        {
            return new CustomSwatchesViewModel(CreateStore());
        }

        private VDriveAppPreferencesStore CreateStore()
        {
            return new VDriveAppPreferencesStore(_settings, _location);
        }

        private void CreateFile(params string[] lines)
        {
            File.WriteAllLines(_filePath, lines);
        }
    }
}
