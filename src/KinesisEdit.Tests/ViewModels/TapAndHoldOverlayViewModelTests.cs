using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Assign Tap and Hold Action overlay of specs/11-feature-dialogs.md §11.1: its labels
    /// and note, the two capture fields, the capability-supplied delay and its clamp, the three
    /// validation messages, the four pre-dialog checks, and the firmware gate of §11.1 / 09 §2
    /// with its <c>'Update Firmware'</c> button.
    /// </summary>
    public sealed class TapAndHoldOverlayViewModelTests
    {
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Strings_MatchTheSpecVerbatim()
        {
            Assert.Equal("Assign Tap and Hold Action", TapAndHoldOverlayViewModel.OverlayTitle);
            Assert.Equal("Tap Action", TapAndHoldOverlayViewModel.TapActionLabel);
            Assert.Equal("Hold Action", TapAndHoldOverlayViewModel.HoldActionLabel);
            Assert.Equal("Delay (1-999ms)", TapAndHoldOverlayViewModel.DelayLabel);
            Assert.Equal("Search for tokens", TapAndHoldOverlayViewModel.SearchHint);
            Assert.Equal("Tap action is not sent until key is released.", TapAndHoldOverlayViewModel.NoteText);
            Assert.Equal(
                "Designate the action sent when the key is tapped and released faster than the delay",
                TapAndHoldOverlayViewModel.TapActionHint);
            Assert.Equal(
                "Designate the action sent when the key is held longer than the delay",
                TapAndHoldOverlayViewModel.HoldActionHint);
            Assert.Equal(
                "Designate the time interval used to differentiate between the Tap and Hold actions",
                TapAndHoldOverlayViewModel.DelayHint);
            Assert.Equal(
                "Please select a timing delay between 1ms and 999ms.",
                TapAndHoldOverlayViewModel.InvalidDelayMessage);
            Assert.Equal("Please select a Tap Action", TapAndHoldOverlayViewModel.MissingTapActionMessage);
            Assert.Equal("Please select a Hold Action", TapAndHoldOverlayViewModel.MissingHoldActionMessage);
        }

        [Fact]
        public void TryCreate_OnAKeyThatPassesEveryCheck_ReturnsTheOverlay()
        {
            var layout = TestLayouts.CreateLayout("esc", "a");
            var layer = layout.Layers[0];

            var result = TapAndHoldOverlayViewModel.TryCreate(layout, layer, layer.Keys[0]);

            Assert.True(result.IsAllowed);
            Assert.NotNull(result.Overlay);
            Assert.Null(result.RefusalMessage);
            Assert.Equal(TapAndHoldOverlayViewModel.OverlayTitle, result.Overlay!.Title);
        }

        [Fact]
        public void TryCreate_OnAnAlphanumericTopLayerKey_ReturnsTheSpecRefusal()
        {
            var layout = TestLayouts.CreateLayout("esc", "a");
            var layer = layout.Layers[0];

            var result = TapAndHoldOverlayViewModel.TryCreate(layout, layer, layer.Keys[1]);

            Assert.False(result.IsAllowed);
            Assert.Null(result.Overlay);
            Assert.Equal(
                "You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.",
                result.RefusalMessage);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, 250)]
        [InlineData(DeviceId.Advantage360, 150)]
        public void Constructor_OnAnySupportingDevice_StartsAtTheCapabilitysDefaultDelay(DeviceId deviceId, int expected)
        {
            var capability = DeviceCatalog.GetById(deviceId).TapAndHold;

            var overlay = new TapAndHoldOverlayViewModel(CreateKey(), capability, TokenDialect.Gen1);

            Assert.Equal(expected, overlay.DelayMilliseconds);
            Assert.Equal(1, overlay.MinimumDelayMilliseconds);
            Assert.Equal(999, overlay.MaximumDelayMilliseconds);
        }

        [Fact]
        public void Constructor_OnAKeyThatAlreadyHasATapAndHold_StartsFromItsAssignment()
        {
            var key = CreateKey();
            key.SetTapAndHold(Gen1("a"), Gen1("lctrl"), 300);

            var overlay = Create(key);

            Assert.Same(key.TapAction, overlay.TapAction);
            Assert.Same(key.HoldAction, overlay.HoldAction);
            Assert.Equal(300, overlay.DelayMilliseconds);
        }

        [Fact]
        public void WantsKeystrokes_WithNoArmedField_IsFalse()
        {
            var overlay = Create();

            Assert.False(overlay.WantsKeystrokes);
            Assert.Equal(TapAndHoldField.None, overlay.ArmedField);
        }

        [Fact]
        public void ReceiveKeystroke_WhileTheTapFieldIsArmed_FillsItAndDisarms()
        {
            var overlay = Create();

            overlay.ArmTapActionCommand.Execute(null);

            Assert.True(overlay.WantsKeystrokes);

            overlay.ReceiveKeystroke(Keystroke("a"));

            Assert.Same(Gen1("a"), overlay.TapAction);
            Assert.Null(overlay.HoldAction);
            Assert.False(overlay.WantsKeystrokes);
            Assert.NotEmpty(overlay.TapActionText);
        }

        [Fact]
        public void ReceiveKeystroke_WhileTheHoldFieldIsArmed_FillsOnlyThatField()
        {
            var overlay = Create();

            overlay.ArmHoldActionCommand.Execute(null);
            overlay.ReceiveKeystroke(Keystroke("lctrl"));

            Assert.Same(Gen1("lctrl"), overlay.HoldAction);
            Assert.Null(overlay.TapAction);
        }

        [Fact]
        public void ReceiveKeystroke_WithNothingArmed_IsIgnored()
        {
            var overlay = Create();

            overlay.ReceiveKeystroke(Keystroke("a"));

            Assert.Null(overlay.TapAction);
            Assert.Null(overlay.HoldAction);
        }

        [Fact]
        public void ReceiveKeystroke_AfterTheOverlayClosed_IsIgnored()
        {
            var overlay = Create();

            overlay.ArmTapActionCommand.Execute(null);
            overlay.CancelCommand.Execute(null);
            overlay.ReceiveKeystroke(Keystroke("a"));

            Assert.False(overlay.WantsKeystrokes);
            Assert.Null(overlay.TapAction);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void Accept_WithADelayOutsideTheRange_ReportsTheSpecMessageAndStaysOpen(int delay)
        {
            var overlay = Create();

            overlay.DelayMilliseconds = delay;
            overlay.AcceptCommand.Execute(null);

            Assert.Equal(TapAndHoldOverlayViewModel.InvalidDelayMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public void Accept_WithNoTapAction_ReportsTheSpecMessage()
        {
            var overlay = Create();

            overlay.AcceptCommand.Execute(null);

            Assert.Equal(TapAndHoldOverlayViewModel.MissingTapActionMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public void Accept_WithNoHoldAction_ReportsTheSpecMessage()
        {
            var overlay = Create();

            overlay.ArmTapActionCommand.Execute(null);
            overlay.ReceiveKeystroke(Keystroke("a"));
            overlay.AcceptCommand.Execute(null);

            Assert.Equal(TapAndHoldOverlayViewModel.MissingHoldActionMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public void Accept_WithBothActions_AssignsTheTapAndHoldToTheKeyAndCloses()
        {
            var key = CreateKey();
            var overlay = Create(key);

            overlay.ArmTapActionCommand.Execute(null);
            overlay.ReceiveKeystroke(Keystroke("a"));
            overlay.ArmHoldActionCommand.Execute(null);
            overlay.ReceiveKeystroke(Keystroke("lctrl"));
            overlay.DelayMilliseconds = 300;
            overlay.AcceptCommand.Execute(null);

            Assert.True(key.IsTapAndHold);
            Assert.Same(Gen1("a"), key.TapAction);
            Assert.Same(Gen1("lctrl"), key.HoldAction);
            Assert.Equal(300, key.TimingDelay);
            Assert.True(overlay.WasAccepted);
            Assert.True(overlay.IsClosed);
        }

        [Fact]
        public void Cancel_AfterFillingBothFields_LeavesTheKeyUntouched()
        {
            var key = CreateKey();
            var overlay = Create(key);

            overlay.ArmTapActionCommand.Execute(null);
            overlay.ReceiveKeystroke(Keystroke("a"));
            overlay.CancelCommand.Execute(null);

            Assert.False(key.IsTapAndHold);
            Assert.False(overlay.WasAccepted);
        }

        [Fact]
        public void IncreaseDelayCommand_AtTheMaximum_StaysClamped()
        {
            var overlay = Create();

            overlay.DelayMilliseconds = 999;
            overlay.IncreaseDelayCommand.Execute(null);

            Assert.Equal(999, overlay.DelayMilliseconds);
        }

        [Fact]
        public void DecreaseDelayCommand_AtTheMinimum_StaysClamped()
        {
            var overlay = Create();

            overlay.DelayMilliseconds = 1;
            overlay.DecreaseDelayCommand.Execute(null);

            Assert.Equal(1, overlay.DelayMilliseconds);
        }

        [Fact]
        public void DecreaseDelayCommand_FromAnOutOfRangeValue_ClampsBackIntoTheRange()
        {
            var overlay = Create();

            overlay.DelayMilliseconds = 5000;
            overlay.DecreaseDelayCommand.Execute(null);

            Assert.Equal(999, overlay.DelayMilliseconds);
        }

        [Fact]
        public void SearchTapActionCommand_RaisesTheTapActionPickerAndDisarmsCapture()
        {
            var overlay = Create();
            SearchKeysOverlayViewModel? picker = null;

            overlay.SearchRequested += requested => picker = requested;
            overlay.ArmTapActionCommand.Execute(null);
            overlay.SearchTapActionCommand.Execute(null);

            Assert.NotNull(picker);
            Assert.Equal(SearchKeysOverlayViewModel.TapActionTitle, picker!.Title);
            Assert.False(overlay.WantsKeystrokes);
        }

        [Fact]
        public void SearchHoldActionCommand_WhenThePickerAccepts_WritesTheActionIntoTheHoldField()
        {
            var overlay = Create();
            SearchKeysOverlayViewModel? picker = null;

            overlay.SearchRequested += requested => picker = requested;
            overlay.SearchHoldActionCommand.Execute(null);

            Assert.NotNull(picker);
            Assert.Equal(SearchKeysOverlayViewModel.HoldActionTitle, picker!.Title);

            picker.SelectedEntry = picker.Results.First(entry => entry.FileToken == "lctrl");
            picker.AcceptCommand.Execute(null);

            Assert.Same(Gen1("lctrl"), overlay.HoldAction);
            Assert.Null(overlay.TapAction);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WithFirmwareAtTheGate_ShowsNothingAndAllows()
        {
            var allowed = await TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 1),
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_InDemoMode_BypassesTheGate()
        {
            var allowed = await TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                new FirmwareState { IsDemoMode = true },
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_BelowTheGate_RefusesWithTheSpecMessageAndTheUpdateButton()
        {
            var allowed = await TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            var request = Assert.Single(_notifications.MessageBoxes);
            var button = Assert.Single(request.CustomButtons);

            Assert.False(allowed);
            Assert.Equal(TapAndHoldOverlayViewModel.OverlayTitle, request.Title);
            Assert.Equal(TapAndHoldOverlayViewModel.FirmwareRefusalMessage, request.Message);
            Assert.Equal(FirmwareFeatureGate.UpdateFirmwareButtonCaption, button.Caption);
            Assert.Empty(_urlLauncher.OpenedUrls);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WhenUpdateFirmwareIsPressed_OpensTheDevicesSupportPage()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = MessageBoxResult.Custom,
                CustomButtonId = FirmwareFeatureGate.UpdateFirmwareButtonId
            };

            var allowed = await TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            Assert.False(allowed);
            Assert.Equal(
                FirmwareSupportUrls.FindUrl(DeviceId.FreestyleEdgeRgb),
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WhenTheDialogIsDismissed_OpensNothing()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Ok };

            await TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            Assert.Empty(_urlLauncher.OpenedUrls);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_OnAFreestyleBoard_UsesTheGatesOwnMessage()
        {
            await TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdge,
                Firmware(1, 0, 479),
                _notifications,
                _urlLauncher);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold)!.Message,
                request.Message);
        }

        /// <summary>
        /// Drift guard: the fallback used for the gate rows that carry no message (Advantage2,
        /// Edge RGB) must stay the wording the Freestyle row stores, since §11.1 quotes one
        /// refusal for every app.
        /// </summary>
        [Fact]
        public void FirmwareRefusalMessage_MatchesTheGateRowThatStoresIt()
        {
            Assert.Equal(
                TapAndHoldOverlayViewModel.FirmwareRefusalMessage,
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold)!.Message);
        }

        /// <summary>
        /// <c>KeyboardKey.SetTapAndHold</c> refuses a position that can never be remapped
        /// (specs/05-key-model.md §5.3), and §11.1's accept has to honour that answer: a panel that
        /// closed reporting success would leave the caller refreshing a cap that never changed.
        /// </summary>
        [Fact]
        public void TryAccept_WhenTheKeyRefusesTheAssignment_StaysOpenAndReportsIt()
        {
            var locked = TestLayouts.CreateLockedKeyLayout().Layers[0].Keys[1];
            var overlay = Create(locked);

            Assert.False(locked.CanEdit);

            Arm(overlay, TapAndHoldField.Tap, "a");
            Arm(overlay, TapAndHoldField.Hold, "lctrl");

            overlay.AcceptCommand.Execute(null);

            Assert.False(overlay.WasAccepted);
            Assert.False(overlay.IsClosed);
            Assert.Equal(TapAndHoldOverlayViewModel.LockedKeyMessage, overlay.ErrorMessage);
            Assert.False(locked.IsTapAndHold);
        }

        private static void Arm(TapAndHoldOverlayViewModel overlay, TapAndHoldField field, string token)
        {
            if (field == TapAndHoldField.Hold)
            {
                overlay.ArmHoldActionCommand.Execute(null);
            }
            else
            {
                overlay.ArmTapActionCommand.Execute(null);
            }

            overlay.ReceiveKeystroke(Keystroke(token));
        }

        private static TapAndHoldOverlayViewModel Create(KeyboardKey? key = null)
        {
            return new TapAndHoldOverlayViewModel(
                key ?? CreateKey(),
                DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).TapAndHold,
                TokenDialect.Gen1);
        }

        private static KeyboardKey CreateKey()
        {
            return TestLayouts.CreateLayout("esc", "a").Layers[0].Keys[0];
        }

        private static KeyDefinition Gen1(string token)
        {
            return TestLayouts.Gen1Key(token);
        }

        private static CapturedKeystroke Keystroke(string token)
        {
            return new CapturedKeystroke
            {
                Key = Gen1(token),
                PhysicalKey = PhysicalKeyCode.None
            };
        }

        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }
    }
}
