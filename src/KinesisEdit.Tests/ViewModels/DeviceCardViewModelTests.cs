using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The device card of specs/10-apps-and-ui.md as the redesign draws it
    /// (docs/design/mockups.md §1b, §2e): the four card states, the catalog-composed meta line,
    /// the mono mount path, the per-state explanation and the per-state actions.
    /// </summary>
    public class DeviceCardViewModelTests
    {
        private const string ScanCaption = "Scan for v-Drive";

        [Theory]
        [InlineData(VDriveConnectionStatus.Connected, DeviceCardState.Connected, "Connected", StatusSeverity.Ok)]
        [InlineData(VDriveConnectionStatus.NotDetected, DeviceCardState.Resting, "Not Detected", StatusSeverity.Unknown)]
        [InlineData(VDriveConnectionStatus.CannotAccess, DeviceCardState.CannotAccess, "Cannot Access", StatusSeverity.Error)]
        public void State_PerConnectionState_UsesTheSpecWordingAndItsOwnSeverity(
            VDriveConnectionStatus status,
            DeviceCardState expectedState,
            string expectedText,
            StatusSeverity expectedSeverity)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, status), out _, out _);

            Assert.Equal(expectedState, card.State);
            Assert.Equal(expectedText, card.StatusText);
            Assert.Equal(expectedSeverity, card.StatusSeverity);
        }

        /// <summary>
        /// The defect this card refresh names: a known device whose drive is simply not mounted
        /// used to paint red. It is the resting state, not an error, and the status chip has no
        /// <c>.unknown</c> face — the absence of a colour is the treatment.
        /// </summary>
        [Fact]
        public void StatusSeverity_WhenResting_IsUnknownAndNeverError()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Advantage2, VDriveConnectionStatus.NotDetected),
                out _,
                out _);

            Assert.Equal(StatusSeverity.Unknown, card.StatusSeverity);
            Assert.NotEqual(StatusSeverity.Error, card.StatusSeverity);
            Assert.NotEqual(StatusSeverity.Warning, card.StatusSeverity);
        }

        [Fact]
        public void State_WhileAPassIsInFlight_IsScanningWhateverTheDriveSays()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            card.IsScanning = true;

            Assert.Equal(DeviceCardState.Scanning, card.State);
            Assert.Equal("Scanning for v-Drive…", card.StatusText);
            Assert.Equal(StatusSeverity.Unknown, card.StatusSeverity);

            card.IsScanning = false;

            Assert.Equal(DeviceCardState.Connected, card.State);
        }

        /// <summary>The meta line is composed in Core; the card only surfaces it.</summary>
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.SavantElite2)]
        public void MetaLine_ForEveryDevice_IsWhatTheCatalogComposes(DeviceId deviceId)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(deviceId), out _, out _);

            Assert.Equal(DeviceMetaLine.Describe(DeviceCatalog.GetById(deviceId)), card.MetaLine);
            Assert.NotEmpty(card.MetaLine);
        }

        [Theory]
        [InlineData(VDriveConnectionStatus.Connected)]
        [InlineData(VDriveConnectionStatus.CannotAccess)]
        public void MountPath_WhenADriveIsMounted_IsTheVolumeRoot(VDriveConnectionStatus status)
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko, status);
            var card = CreateCard(snapshot, out _, out _);

            Assert.True(card.HasMountPath);
            Assert.Equal(snapshot.Location!.RootPath, card.MountPath);
        }

        [Fact]
        public void MountPath_WhenResting_HasNothingToShow()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.NotDetected),
                out _,
                out _);

            Assert.False(card.HasMountPath);
            Assert.Null(card.MountPath);
        }

        [Fact]
        public void ExplanationText_WhenResting_SaysItIsRestingRatherThanBroken()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Advantage2, VDriveConnectionStatus.NotDetected),
                out _,
                out _);

            Assert.True(card.HasExplanation);
            Assert.Equal(
                "Known device, no drive mounted. Idle and quiet — no red, no spinner. This is the resting state, not an error.",
                card.ExplanationText);
        }

        /// <summary>
        /// The drive name is templated, not the mockup's literal "TKO": the sentence names the
        /// volume the user is looking at, and every device mounts a different one.
        /// </summary>
        [Fact]
        public void ExplanationText_WhenTheDriveIsUnwritable_NamesThatDrive()
        {
            var tko = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);
            var advantage360 = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Advantage360, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            Assert.Equal("TKO", tko.DriveName);
            Assert.Equal(
                "Drive TKO is visible but not writable. Another app may have a file open, or the volume mounted read-only.",
                tko.ExplanationText);
            Assert.Equal(
                "Drive ADV360 is visible but not writable. Another app may have a file open, or the volume mounted read-only.",
                advantage360.ExplanationText);
        }

        [Theory]
        [InlineData(VDriveConnectionStatus.Connected)]
        [InlineData(VDriveConnectionStatus.CannotAccess)]
        public void ExplanationText_WhereTheStatusLineSaysEverything_IsEmptyForConnected(VDriveConnectionStatus status)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, status), out _, out _);

            Assert.Equal(status == VDriveConnectionStatus.CannotAccess, card.HasExplanation);
        }

        [Fact]
        public void ExplanationText_WhileScanning_IsEmpty()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.NotDetected),
                out _,
                out _);

            card.IsScanning = true;

            Assert.False(card.HasExplanation);
            Assert.Equal(string.Empty, card.ExplanationText);
        }

        [Fact]
        public void Actions_WhenConnected_AreConfigureScanAndEject()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.Equal(ScanCaption, card.SecondaryActionCaption);
            Assert.True(card.CanRunSecondaryAction);
            Assert.True(card.ShowsEject);
            Assert.True(card.CanEject);
            Assert.False(card.IsDemoMode);
        }

        [Fact]
        public void Actions_WhenResting_AreDemoModeAndScanWithNothingToEject()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.NotDetected),
                out _,
                out _);

            Assert.Equal("Demo Mode", card.PrimaryActionCaption);
            Assert.Equal(ScanCaption, card.SecondaryActionCaption);
            Assert.False(card.ShowsEject);
            Assert.False(card.CanEject);
            Assert.True(card.IsDemoMode);
        }

        /// <summary>
        /// An unwritable drive is still a drive: the card offers Configure — the editor enters demo
        /// mode by itself (03 §3.5) — and its secondary button becomes 'Retry access'.
        /// </summary>
        [Fact]
        public void Actions_WhenTheDriveIsUnwritable_AreConfigureRetryAccessAndEject()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.Equal("Retry access", card.SecondaryActionCaption);
            Assert.True(card.ShowsEject);
            Assert.True(card.IsDemoMode);
        }

        /// <summary>
        /// Nothing cancels a running pass — <c>DeviceMonitorService.Refresh()</c> has no
        /// cancellation token — so the Scanning card renders no Cancel button. The secondary button
        /// holds its position and reads 'Scanning' instead, and every other action keeps the place
        /// the drive gave it, so a 2 s refresh cannot shift the layout under the cursor.
        /// </summary>
        [Fact]
        public void Actions_WhileScanning_OfferNoCancelAndHoldTheirPositions()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            card.IsScanning = true;

            Assert.Equal("Scanning", card.SecondaryActionCaption);
            Assert.DoesNotContain("Cancel", card.SecondaryActionCaption, StringComparison.OrdinalIgnoreCase);
            Assert.False(card.CanRunSecondaryAction);
            Assert.False(card.SecondaryActionCommand.CanExecute(null));

            // Unchanged by the scan: the buttons are chosen from the drive, not from the overlay.
            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.True(card.ShowsEject);
        }

        [Fact]
        public void CanEject_WhenEjectionIsUnsupported_IsFalse()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out var ejectService, out _);
            ejectService.IsSupported = false;

            Assert.False(card.CanEject);
            Assert.False(card.EjectCommand.CanExecute(null));
        }

        [Fact]
        public void CanEject_WhenConnectedAndSupported_IsTrue()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            Assert.True(card.CanEject);
            Assert.True(card.EjectCommand.CanExecute(null));
        }

        /// <summary>
        /// A <c>Cannot Access</c> drive is mounted, and unmounting it is exactly the remedy the
        /// card's own explanation points at ("another app may have a file open"), so Eject is live
        /// there — mockup §1b puts the button on that card.
        /// </summary>
        [Fact]
        public void CanEject_WhenTheDriveIsMountedButUnwritable_IsStillTrue()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            Assert.True(card.ShowsEject);
            Assert.True(card.CanEject);
        }

        [Fact]
        public async Task EjectCommand_WhenExecuted_EjectsTheCardsDrive()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var card = CreateCard(snapshot, out var ejectService, out _);

            await card.EjectCommand.ExecuteAsync(null);

            Assert.Equal(snapshot.Location!.RootPath, Assert.Single(ejectService.EjectedPaths));
        }

        [Fact]
        public void ConfigureCommand_WhenExecuted_RequestsTheCardsSnapshot()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var requested = new List<DeviceSnapshot>();
            var card = new DeviceCardViewModel(
                snapshot,
                new VDriveEjectNotifier(new FakeDeviceEjectService(), new FakeNotificationService()),
                requested.Add,
                () => Task.CompletedTask);

            card.ConfigureCommand.Execute(null);

            Assert.Same(snapshot, Assert.Single(requested));
        }

        [Fact]
        public void Key_IsTheScannedSlotAndNotTheResolvedDevice()
        {
            // Freestyle resolution is many-to-one, so keying on the resolved id would collapse two
            // attached drives onto one card (docs/app/app-shell.md, invariant 4).
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.FreestylePro), out _, out _);

            Assert.Equal(DeviceId.FreestylePro.ToString(), card.Key);
            Assert.Equal(card.ScannedDeviceId.ToString(), card.Key);
        }

        /// <summary>
        /// The secondary button rescans in every device and drive state, including the connected
        /// Freestyle Edge RGB that used to swap to 'Check for Updates'.
        /// </summary>
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess)]
        [InlineData(DeviceId.Tko, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.Advantage360, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.FreestyleEdge, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.FreestylePro, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.Advantage2, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.SavantElite2, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.Tko, VDriveConnectionStatus.CannotAccess)]
        [InlineData(DeviceId.Tko, VDriveConnectionStatus.NotDetected)]
        public async Task SecondaryAction_ForEveryDeviceAndStatus_ScansForTheVDrive(
            DeviceId deviceId,
            VDriveConnectionStatus status)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(deviceId, status), out _, out var scans);

            Assert.Equal(
                status == VDriveConnectionStatus.CannotAccess ? "Retry access" : ScanCaption,
                card.SecondaryActionCaption);

            await card.SecondaryActionCommand.ExecuteAsync(null);

            Assert.Single(scans);
        }

        [Fact]
        public void Update_WithNewStatus_NotifiesEveryDerivedValue()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);
            var changed = new List<string?>();
            card.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            card.Update(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess));

            Assert.Equal("Cannot Access", card.StatusText);
            Assert.Equal("Retry access", card.SecondaryActionCaption);
            Assert.True(card.HasExplanation);

            foreach (var propertyName in new[]
            {
                nameof(DeviceCardViewModel.State),
                nameof(DeviceCardViewModel.StatusText),
                nameof(DeviceCardViewModel.StatusSeverity),
                nameof(DeviceCardViewModel.MetaLine),
                nameof(DeviceCardViewModel.MountPath),
                nameof(DeviceCardViewModel.HasMountPath),
                nameof(DeviceCardViewModel.ExplanationText),
                nameof(DeviceCardViewModel.HasExplanation),
                nameof(DeviceCardViewModel.PrimaryActionCaption),
                nameof(DeviceCardViewModel.SecondaryActionCaption),
                nameof(DeviceCardViewModel.ShowsEject),
                nameof(DeviceCardViewModel.CanEject)
            })
            {
                Assert.Contains(propertyName, changed);
            }
        }

        [Fact]
        public void Update_WithTheSameSnapshot_NotifiesNothing()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var card = CreateCard(snapshot, out _, out _);
            var changed = new List<string?>();
            card.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            card.Update(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Empty(changed);
        }

        [Fact]
        public void IsScanning_WhenItFlips_NotifiesEveryValueItChanges()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);
            var changed = new List<string?>();
            card.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            card.IsScanning = true;

            Assert.Contains(nameof(DeviceCardViewModel.State), changed);
            Assert.Contains(nameof(DeviceCardViewModel.StatusText), changed);
            Assert.Contains(nameof(DeviceCardViewModel.SecondaryActionCaption), changed);
            Assert.Contains(nameof(DeviceCardViewModel.CanRunSecondaryAction), changed);

            changed.Clear();
            card.IsScanning = true;

            Assert.Empty(changed);
        }

        private static DeviceCardViewModel CreateCard(
            DeviceSnapshot snapshot,
            out FakeDeviceEjectService ejectService,
            out List<int> scans)
        {
            ejectService = new FakeDeviceEjectService();

            var scanCalls = new List<int>();
            scans = scanCalls;

            return new DeviceCardViewModel(
                snapshot,
                new VDriveEjectNotifier(ejectService, new FakeNotificationService()),
                _ => { },
                () =>
                {
                    scanCalls.Add(scanCalls.Count);

                    return Task.CompletedTask;
                });
        }
    }
}
