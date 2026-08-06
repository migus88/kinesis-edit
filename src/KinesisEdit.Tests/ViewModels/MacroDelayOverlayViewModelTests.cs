using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Macro Timing Delays overlay of specs/11-feature-dialogs.md §11.3: the two captions,
    /// the exclusivity of the random radio and the custom field, the 1-999 clamp and validation,
    /// the resulting <c>dran</c>/<c>d001</c>..<c>d999</c> keys, and the firmware gate of 09 §2.
    /// The round-trip cases pin what §11.3's result does once it is written to a layout file
    /// (specs/06-macros.md §2.2).
    /// </summary>
    public sealed class MacroDelayOverlayViewModelTests
    {
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Strings_MatchTheSpecVerbatim()
        {
            Assert.Equal("Macro Timing Delays", MacroDelayOverlayViewModel.OverlayTitle);
            Assert.Equal("Random Delay (1-150ms)", MacroDelayOverlayViewModel.RandomDelayCaption);
            Assert.Equal("Custom Delay (1-999ms)", MacroDelayOverlayViewModel.CustomDelayCaption);
            Assert.Equal(
                "Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.",
                MacroDelayOverlayViewModel.InvalidDelayMessage);
        }

        [Fact]
        public void Constructor_Always_PreselectsNeitherControl()
        {
            var overlay = Create();

            Assert.Equal(MacroDelayOverlayViewModel.OverlayTitle, overlay.Title);
            Assert.False(overlay.IsRandomDelay);
            Assert.Equal(0, overlay.CustomDelayMilliseconds);
            Assert.Equal(1, overlay.MinimumDelayMilliseconds);
            Assert.Equal(999, overlay.MaximumDelayMilliseconds);
        }

        [Fact]
        public void CustomDelayMilliseconds_WhenTyped_UnchecksTheRandomRadio()
        {
            var overlay = Create();

            overlay.IsRandomDelay = true;
            overlay.CustomDelayMilliseconds = 250;

            Assert.False(overlay.IsRandomDelay);
        }

        [Fact]
        public void Accept_WithNothingSelected_ReportsTheSpecMessageAndStaysOpen()
        {
            var overlay = Create();

            overlay.AcceptCommand.Execute(null);

            Assert.Equal(MacroDelayOverlayViewModel.InvalidDelayMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
            Assert.Null(overlay.SelectedDelay);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void Accept_WithACustomDelayOutsideTheRange_ReportsTheSpecMessage(int delay)
        {
            var overlay = Create();

            overlay.CustomDelayMilliseconds = delay;
            overlay.AcceptCommand.Execute(null);

            Assert.Equal(MacroDelayOverlayViewModel.InvalidDelayMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public void Accept_WithTheRandomRadio_YieldsTheRandomDelayKey()
        {
            var overlay = Create();
            KeyDefinition? accepted = null;

            overlay.Accepted += definition => accepted = definition;
            overlay.IsRandomDelay = true;
            overlay.AcceptCommand.Execute(null);

            Assert.NotNull(accepted);
            Assert.Equal(MacroDelayTokens.RandomToken, accepted!.GetToken(TokenDialect.Gen1));
            Assert.Same(accepted, overlay.SelectedDelay);
            Assert.True(overlay.WasAccepted);
            Assert.True(overlay.IsClosed);
        }

        [Theory]
        [InlineData(1, "d001")]
        [InlineData(50, "d050")]
        [InlineData(250, "d250")]
        [InlineData(999, "d999")]
        public void Accept_WithACustomDelay_YieldsTheZeroPaddedDelayKey(int delay, string token)
        {
            var overlay = Create();
            KeyDefinition? accepted = null;

            overlay.Accepted += definition => accepted = definition;
            overlay.CustomDelayMilliseconds = delay;
            overlay.AcceptCommand.Execute(null);

            Assert.NotNull(accepted);
            Assert.Equal(token, accepted!.GetToken(TokenDialect.Gen1));
        }

        [Fact]
        public void Accept_WithTheRandomRadioOverAStaleCustomValue_StillYieldsTheRandomKey()
        {
            var overlay = Create();

            overlay.CustomDelayMilliseconds = 250;
            overlay.IsRandomDelay = true;
            overlay.AcceptCommand.Execute(null);

            Assert.Equal(MacroDelayTokens.RandomToken, overlay.SelectedDelay!.GetToken(TokenDialect.Gen1));
        }

        [Fact]
        public void IncreaseDelayCommand_AtTheMaximum_StaysClamped()
        {
            var overlay = Create();

            overlay.CustomDelayMilliseconds = 999;
            overlay.IncreaseDelayCommand.Execute(null);

            Assert.Equal(999, overlay.CustomDelayMilliseconds);
        }

        [Fact]
        public void DecreaseDelayCommand_FromAnEmptyField_LandsOnTheMinimum()
        {
            var overlay = Create();

            overlay.DecreaseDelayCommand.Execute(null);

            Assert.Equal(1, overlay.CustomDelayMilliseconds);
        }

        [Fact]
        public void Cancel_AfterPickingADelay_ReportsNothing()
        {
            var overlay = Create();
            var raised = 0;

            overlay.Accepted += _ => raised++;
            overlay.IsRandomDelay = true;
            overlay.CancelCommand.Execute(null);

            Assert.Equal(0, raised);
            Assert.Null(overlay.SelectedDelay);
            Assert.True(overlay.IsClosed);
        }

        /// <summary>
        /// §11.3's own contract: a delay the dialog produced must survive being written to the
        /// layout file and read back (06 §2.2), both as text and as model content.
        /// </summary>
        [Fact]
        public void AcceptedDelays_AppendedToAMacroAndRoundTripped_SurviveUnchanged()
        {
            var parsed = ParseRgb("{q}>{a}");
            var macro = parsed.Layout.EnumerateMacros().Single();

            macro.AddKeystroke(new Keystroke(Delay(250)));
            macro.AddKeystroke(new Keystroke(RandomDelay()));

            var lines = LayoutFileSerializer.Serialize(parsed.Layout, parsed.InvalidLines);
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
            var roundTripped = reparsed.Layout.EnumerateMacros().Single();

            Assert.Equal(["{q}>{s5}{x1}{a}{d250}{dran}"], lines);
            Assert.True(macro.IsEquivalentTo(roundTripped));
            Assert.Equal(lines, LayoutFileSerializer.Serialize(reparsed.Layout, reparsed.InvalidLines));
        }

        /// <summary>
        /// The known 125 ms / 500 ms asymmetry of 05 §3.12 and 06 §2.2, pinned rather than
        /// hidden: <see cref="MacroDelayTokens.ResolveCustom"/> resolves those two by token, and
        /// the legacy rows 10007/10008 shadow their generated twins, while the RGB/TKO parser
        /// reads the same text back as the generated codes 10085 + N. The <b>file text</b> is
        /// identical in both directions — which is the interoperability contract — but the
        /// key-table identity is not, so the macro does not compare equivalent.
        /// </summary>
        [Theory]
        [InlineData(125)]
        [InlineData(500)]
        public void AcceptedDelay_AtALegacyFixedValueOnTheRgb_KeepsItsTextButNotItsKeyIdentity(int delay)
        {
            var parsed = ParseRgb("{q}>{a}");
            var macro = parsed.Layout.EnumerateMacros().Single();
            var picked = Delay(delay);

            macro.AddKeystroke(new Keystroke(picked));

            var lines = LayoutFileSerializer.Serialize(parsed.Layout, parsed.InvalidLines);
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
            var roundTripped = reparsed.Layout.EnumerateMacros().Single();

            Assert.Equal([$"{{q}}>{{s5}}{{x1}}{{a}}{{d{delay}}}"], lines);
            Assert.Equal(lines, LayoutFileSerializer.Serialize(reparsed.Layout, reparsed.InvalidLines));
            Assert.NotEqual(picked.Code, roundTripped.Keystrokes[^1].Key.Code);
            Assert.Equal(10085 + delay, roundTripped.Keystrokes[^1].Key.Code);
            Assert.False(macro.IsEquivalentTo(roundTripped));
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_OnAnUngatedBoard_ShowsNothingAndAllows()
        {
            var allowed = await MacroDelayOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_OnAFreestyleAtTheGate_Allows()
        {
            var allowed = await MacroDelayOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdge,
                Firmware(1, 0, 340),
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_InDemoMode_BypassesTheGate()
        {
            var allowed = await MacroDelayOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdge,
                new FirmwareState { IsDemoMode = true },
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_OnAFreestyleBelowTheGate_RefusesWithTheSpecMessage()
        {
            var allowed = await MacroDelayOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdge,
                Firmware(1, 0, 339),
                _notifications,
                _urlLauncher);

            var request = Assert.Single(_notifications.MessageBoxes);
            var button = Assert.Single(request.CustomButtons);

            Assert.False(allowed);
            Assert.Equal(MacroDelayOverlayViewModel.OverlayTitle, request.Title);
            Assert.Equal(MacroDelayOverlayViewModel.FirmwareRefusalMessage, request.Message);
            Assert.Equal(FirmwareFeatureGate.UpdateFirmwareButtonCaption, button.Caption);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WhenUpdateFirmwareIsPressed_OpensTheDevicesSupportPage()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = MessageBoxResult.Custom,
                CustomButtonId = FirmwareFeatureGate.UpdateFirmwareButtonId
            };

            await MacroDelayOverlayViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdge,
                Firmware(1, 0, 339),
                _notifications,
                _urlLauncher);

            Assert.Equal(
                FirmwareSupportUrls.FindUrl(DeviceId.FreestyleEdge),
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        /// <summary>Drift guard against the gate row that stores the same refusal (09 §2).</summary>
        [Fact]
        public void FirmwareRefusalMessage_MatchesTheGateRowThatStoresIt()
        {
            Assert.Equal(
                MacroDelayOverlayViewModel.FirmwareRefusalMessage,
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.CustomMacroDelays)!.Message);
        }

        private static MacroDelayOverlayViewModel Create()
        {
            return new MacroDelayOverlayViewModel(TokenDialect.Gen1);
        }

        /// <summary>The key the overlay produces for a custom delay of <paramref name="delay"/> ms.</summary>
        private static KeyDefinition Delay(int delay)
        {
            var overlay = Create();

            overlay.CustomDelayMilliseconds = delay;
            overlay.AcceptCommand.Execute(null);

            return overlay.SelectedDelay!;
        }

        /// <summary>The key the overlay produces for the random delay.</summary>
        private static KeyDefinition RandomDelay()
        {
            var overlay = Create();

            overlay.IsRandomDelay = true;
            overlay.AcceptCommand.Execute(null);

            return overlay.SelectedDelay!;
        }

        private static LayoutParseResult ParseRgb(params string[] lines)
        {
            return new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
        }

        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }
    }
}
