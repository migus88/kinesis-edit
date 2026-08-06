using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The shared colour picker of specs/07-lighting.md §4: the premixed row, and the twelve
    /// custom slots that live in <c>app_settings.txt</c> (specs/08-settings.md §3). The parse and
    /// serialize rules are Core's; what this layer has to get right is which slot it writes and
    /// whether it writes at all.
    /// </summary>
    public sealed class ColorPickerViewModelTests
    {
        private readonly FakeSettingsService _settings = new();

        [Fact]
        public void PremixedColors_AreTheTenSwatchesOfTheSpec()
        {
            Assert.Equal(
                new[] { "White", "Yellow", "Red", "Lime", "Blue", "Fuchsia", "Orange", "Azure", "Aqua", "Black" },
                ColorPickerViewModel.PremixedColors.Select(swatch => swatch.Name));
            Assert.Equal("#FF8000", ColorPickerViewModel.PremixedColors[6].ColorHex);
            Assert.Equal("#0080FF", ColorPickerViewModel.PremixedColors[7].ColorHex);
        }

        [Fact]
        public void CustomColors_AreTwelveEmptySlotsBeforeAnythingIsRead()
        {
            var picker = CreatePicker();

            Assert.Equal(AppSettings.CustomColorCount, picker.CustomColors.Count);
            Assert.All(picker.CustomColors, slot => Assert.False(slot.HasColor));
            Assert.Equal(new[] { 1, 12 }, new[] { picker.CustomColors[0].SlotNumber, picker.CustomColors[^1].SlotNumber });
        }

        [Fact]
        public async Task LoadAsync_ReadsTheStoredSlotsAndLeavesUnsetOnesEmpty()
        {
            _settings.AppSettingsToReturn = AppSettings.Empty
                .WithCustomColor(1, new SettingsColor(255, 0, 128))
                .WithCustomColor(12, new SettingsColor(0, 0, 0));

            var picker = CreatePicker();

            await picker.LoadAsync();

            Assert.Equal("#FF0080", picker.CustomColors[0].ColorHex);

            // An unset slot is "no swatch stored", not black — and a stored black stays black.
            Assert.True(picker.CustomColors[^1].HasColor);
            Assert.Equal("#000000", picker.CustomColors[^1].ColorHex);
            Assert.False(picker.CustomColors[5].HasColor);
        }

        [Fact]
        public async Task LoadAsync_WithoutADrive_ReadsNothing()
        {
            var picker = CreatePicker(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            await picker.LoadAsync();

            Assert.All(picker.CustomColors, slot => Assert.False(slot.HasColor));
        }

        [Fact]
        public async Task LoadAsync_WhenTheFileCannotBeRead_LeavesTheSlotsEmpty()
        {
            _settings.LoadAppExceptionToThrow = new IOException("the v-Drive went away");

            var picker = CreatePicker();

            await picker.LoadAsync();

            Assert.All(picker.CustomColors, slot => Assert.False(slot.HasColor));
        }

        [Fact]
        public async Task StoreCustomColorCommand_WritesTheFirstEmptySlotThrough()
        {
            var picker = CreatePicker();

            picker.Color = new LedColor(1, 2, 3);

            await picker.StoreCustomColorCommand.ExecuteAsync(null);

            Assert.Equal("#010203", picker.CustomColors[0].ColorHex);

            var saved = Assert.Single(_settings.AppSettingsSaves);

            Assert.Equal(new SettingsColor(1, 2, 3), saved.CustomColors[0]);
            Assert.Null(saved.CustomColors[1]);
        }

        [Fact]
        public async Task StoreCustomColorCommand_ReReadsBeforeWriting_SoOtherFlagsSurvive()
        {
            // app_settings.txt has one reader and one writer; a whole-model write built from a
            // stale snapshot would drop the notification flags another feature just set.
            _settings.AppSettingsToReturn = AppSettings.Empty with { IsSaveMessageHidden = true };

            var picker = CreatePicker();

            picker.Color = new LedColor(9, 9, 9);

            await picker.StoreCustomColorCommand.ExecuteAsync(null);

            var saved = Assert.Single(_settings.AppSettingsSaves);

            Assert.True(saved.IsSaveMessageHidden);
            Assert.Equal(new SettingsColor(9, 9, 9), saved.CustomColors[0]);
        }

        [Fact]
        public async Task StoreCustomColorCommand_WhenEverySlotIsFull_RotatesThroughThem()
        {
            var settings = AppSettings.Empty;

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                settings = settings.WithCustomColor(slotNumber, new SettingsColor(1, 1, 1));
            }

            _settings.AppSettingsToReturn = settings;

            var picker = CreatePicker();

            await picker.LoadAsync();

            picker.Color = new LedColor(2, 2, 2);
            await picker.StoreCustomColorCommand.ExecuteAsync(null);

            picker.Color = new LedColor(3, 3, 3);
            await picker.StoreCustomColorCommand.ExecuteAsync(null);

            Assert.Equal("#020202", picker.CustomColors[0].ColorHex);
            Assert.Equal("#030303", picker.CustomColors[1].ColorHex);
            Assert.Equal(2, _settings.AppSettingsSaves.Count);
        }

        [Fact]
        public async Task StoreCustomColorCommand_InDemoMode_IsUnavailableAndWritesNothing()
        {
            // Spec 08 §3: app settings are never saved in demo mode.
            var picker = CreatePicker(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            Assert.False(picker.CanStoreCustomColors);
            Assert.False(picker.StoreCustomColorCommand.CanExecute(null));

            await picker.StoreCustomColorCommand.ExecuteAsync(null);

            Assert.Empty(_settings.AppSettingsSaves);
            Assert.All(picker.CustomColors, slot => Assert.False(slot.HasColor));
        }

        [Fact]
        public async Task StoreCustomColorCommand_WhenTheWriteThrows_KeepsTheSlotForTheSession()
        {
            _settings.SaveAppExceptionToThrow = new IOException("the v-Drive went away");

            var picker = CreatePicker();

            picker.Color = new LedColor(4, 5, 6);

            await picker.StoreCustomColorCommand.ExecuteAsync(null);

            Assert.Equal("#040506", picker.CustomColors[0].ColorHex);
        }

        [Fact]
        public void SelectSwatchCommand_SetsTheCurrentColourAndRaisesTheEvent()
        {
            var picker = CreatePicker();
            var observed = new List<LedColor>();

            picker.ColorChanged += observed.Add;
            picker.SelectSwatchCommand.Execute(ColorPickerViewModel.PremixedColors[2]);

            Assert.Equal(new LedColor(255, 0, 0), picker.Color);
            Assert.Equal("#FF0000", picker.ColorHex);
            Assert.Equal(new LedColor(255, 0, 0), Assert.Single(observed));
        }

        [Fact]
        public async Task SelectCustomColorCommand_ForAFilledSlot_SetsTheColourAndForAnEmptyOneDoesNothing()
        {
            _settings.AppSettingsToReturn = AppSettings.Empty.WithCustomColor(3, new SettingsColor(20, 30, 40));

            var picker = CreatePicker();

            await picker.LoadAsync();

            picker.SelectCustomColorCommand.Execute(picker.CustomColors[2]);

            Assert.Equal(new LedColor(20, 30, 40), picker.Color);

            picker.SelectCustomColorCommand.Execute(picker.CustomColors[5]);

            Assert.Equal(new LedColor(20, 30, 40), picker.Color);
        }

        [Fact]
        public void ColorHex_WithAnUnparseableValue_LeavesTheColourAlone()
        {
            var picker = CreatePicker();

            picker.Color = new LedColor(1, 2, 3);
            picker.ColorHex = "not a colour";

            Assert.Equal(new LedColor(1, 2, 3), picker.Color);

            picker.ColorHex = "#0080FF";

            Assert.Equal(new LedColor(0, 128, 255), picker.Color);
        }

        private ColorPickerViewModel CreatePicker(DeviceSnapshot? snapshot = null)
        {
            return new ColorPickerViewModel(
                snapshot ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _settings);
        }
    }
}
