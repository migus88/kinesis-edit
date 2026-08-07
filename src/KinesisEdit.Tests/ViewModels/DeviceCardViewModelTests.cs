using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Core.VDrive.Eject;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The device card of specs/10-apps-and-ui.md as the redesign draws it
    /// (docs/design/mockups.md §1b, §2e): the three card states, the catalog-composed meta line,
    /// the mono mount path, the per-state explanation and the per-state actions.
    /// <para>
    /// A card exists only while its drive does — the dashboard's roster is what the last scan
    /// found — so every state here is built over a <b>mounted</b> drive: Connected or
    /// CannotAccess, with Scanning laid over either.
    /// </para>
    /// </summary>
    public class DeviceCardViewModelTests
    {
        [Theory]
        [InlineData(VDriveConnectionStatus.Connected, DeviceCardState.Connected, "Connected", StatusSeverity.Ok)]
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

        /// <summary>
        /// A scan in flight is transient, gets no colour, and its indeterminate bar is what reads as
        /// motion — emphatically not red. The status chip has no <c>.unknown</c> face on purpose:
        /// the absence of a chip colour <em>is</em> that treatment.
        /// </summary>
        [Fact]
        public void StatusSeverity_WhileScanning_IsUnknownAndNeverError()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Advantage2), out _, out _);

            card.IsScanning = true;

            Assert.Equal(StatusSeverity.Unknown, card.StatusSeverity);
            Assert.NotEqual(StatusSeverity.Error, card.StatusSeverity);
            Assert.NotEqual(StatusSeverity.Warning, card.StatusSeverity);
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
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            card.IsScanning = true;

            Assert.False(card.HasExplanation);
            Assert.Equal(string.Empty, card.ExplanationText);
        }

        /// <summary>
        /// Issue #118, item 6: a connected card offers <c>Configure</c> and <c>Eject</c> and
        /// nothing else. Re-scanning a drive the app is already talking to changes nothing, so the
        /// secondary button is not drawn at all rather than drawn and disabled.
        /// </summary>
        [Fact]
        public void Actions_WhenConnected_AreExactlyConfigureAndEject()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.False(card.ShowsSecondaryAction);
            Assert.True(card.ShowsEject);
            Assert.True(card.CanEject);
            Assert.False(card.IsDemoMode);
        }

        /// <summary>
        /// An unwritable drive is still a drive: the card offers Configure — the editor enters demo
        /// mode by itself (03 §3.5) — and its secondary button becomes 'Retry access', the one
        /// thing that can help it.
        /// </summary>
        [Fact]
        public void Actions_WhenTheDriveIsUnwritable_AreConfigureRetryAccessAndEject()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.True(card.ShowsSecondaryAction);
            Assert.Equal("Retry access", card.SecondaryActionCaption);
            Assert.True(card.ShowsEject);
            Assert.True(card.IsDemoMode);
        }

        /// <summary>
        /// Issue #118: <see cref="DeviceCardViewModel.ShowsSecondaryAction"/> is gated on the
        /// <b>drive</b>, never on <see cref="DeviceCardViewModel.State"/>. Every mounted drive is
        /// walked, scanning and not, because <c>GetState</c> reports <c>Scanning</c> for all of them
        /// while a pass is in flight — a state-based test would flash the button onto a connected
        /// card mid-scan and shift its layout under the cursor.
        /// </summary>
        [Theory]
        [InlineData(VDriveConnectionStatus.Connected, false, false)]
        [InlineData(VDriveConnectionStatus.Connected, true, false)]
        [InlineData(VDriveConnectionStatus.CannotAccess, false, true)]
        [InlineData(VDriveConnectionStatus.CannotAccess, true, true)]
        public void ShowsSecondaryAction_FollowsTheDriveAndNotTheCardState(
            VDriveConnectionStatus status,
            bool isScanning,
            bool expected)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, status), out _, out _);

            card.IsScanning = isScanning;

            Assert.Equal(expected, card.ShowsSecondaryAction);

            // ...and the card really is in the Scanning face, so the case above is not vacuous.
            Assert.Equal(isScanning, card.State == DeviceCardState.Scanning);
        }

        /// <summary>
        /// Nothing cancels a running pass — <c>DeviceMonitorService.Refresh()</c> has no
        /// cancellation token — so the Scanning card renders no Cancel button. The secondary button
        /// holds its position and reads 'Scanning' instead, and every other action keeps the place
        /// the drive gave it.
        /// </summary>
        [Fact]
        public void Actions_WhileScanning_OfferNoCancelAndHoldTheirPositions()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            card.IsScanning = true;

            Assert.Equal("Scanning", card.SecondaryActionCaption);
            Assert.DoesNotContain("Cancel", card.SecondaryActionCaption, StringComparison.OrdinalIgnoreCase);
            Assert.False(card.CanRunSecondaryAction);
            Assert.False(card.SecondaryActionCommand.CanExecute(null));

            // Unchanged by the scan: the buttons are chosen from the drive, not from the overlay.
            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.True(card.ShowsSecondaryAction);
            Assert.True(card.ShowsEject);
        }

        /// <summary>
        /// The secondary button has exactly two captions, because it is only ever rendered over a
        /// mounted-but-unwritable drive. 'Scan for v-Drive' — the caption a resting card used to
        /// carry — no longer exists anywhere on a card.
        /// </summary>
        [Fact]
        public void SecondaryActionCaption_HasOnlyTheTwoCaptionsAnUnwritableDriveCanShow()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out _);

            Assert.Equal(DeviceCardViewModel.RetryAccessActionCaption, card.SecondaryActionCaption);

            card.IsScanning = true;

            Assert.Equal(DeviceCardViewModel.ScanningActionCaption, card.SecondaryActionCaption);
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

        /// <summary>
        /// Issue #123, bug 1: the volume is gone the instant the unmount returns, and a card outlives
        /// no drive. Without a scan of its own the card would go on describing an unmounted volume
        /// until <c>DeviceLivenessWatcher</c>'s next tick — so the eject asks for the same pass the
        /// watcher would have, straight away.
        /// </summary>
        [Fact]
        public async Task EjectCommand_WhenTheEjectSucceeds_RescansSoTheCardGoesWithTheDrive()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out var scans);

            await card.EjectCommand.ExecuteAsync(null);

            Assert.Single(scans);
        }

        /// <summary>
        /// A failed eject changed nothing about the drive. Scanning anyway would put every card into
        /// its Scanning face just to re-confirm the state the user is already looking at, on top of
        /// the failure dialog they have just been shown.
        /// </summary>
        [Fact]
        public async Task EjectCommand_WhenTheEjectFails_RescansNothing()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out var ejectService, out var scans);
            ejectService.ResultToReturn = new VDriveEjectResult
            {
                Succeeded = false,
                Message = "busy"
            };

            await card.EjectCommand.ExecuteAsync(null);

            Assert.Single(ejectService.EjectedPaths);
            Assert.Empty(scans);
        }

        /// <summary>
        /// Nor does a platform that cannot eject: nothing was released, so there is nothing to look
        /// at again.
        /// </summary>
        [Fact]
        public async Task EjectCommand_WhereEjectionIsUnsupported_RescansNothing()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out var ejectService, out var scans);
            ejectService.IsSupported = false;

            await card.EjectCommand.ExecuteAsync(null);

            Assert.Empty(ejectService.EjectedPaths);
            Assert.Empty(scans);
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
        /// The secondary button re-runs detection for every board whose drive is unwritable,
        /// including the Freestyle Edge RGB that used to swap to 'Check for Updates'.
        /// </summary>
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.SavantElite2)]
        public async Task SecondaryAction_ForEveryUnwritableDrive_RetriesTheScan(DeviceId deviceId)
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(deviceId, VDriveConnectionStatus.CannotAccess),
                out _,
                out var scans);

            Assert.True(card.ShowsSecondaryAction);
            Assert.Equal("Retry access", card.SecondaryActionCaption);

            await card.SecondaryActionCommand.ExecuteAsync(null);

            Assert.Single(scans);
        }

        [Fact]
        public void HasBeenPresented_UntilAViewHostsIt_IsFalse()
        {
            // The design animates a card in when its device is newly detected; a card view is also
            // rebuilt every time the shell swaps the dashboard back in, and an existing card being
            // re-hosted is not an insertion (docs/app/app-shell.md, invariant 8).
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            Assert.False(card.HasBeenPresented);

            card.MarkPresented();

            Assert.True(card.HasBeenPresented);
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
                nameof(DeviceCardViewModel.ShowsSecondaryAction),
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
            Assert.Contains(nameof(DeviceCardViewModel.ShowsSecondaryAction), changed);
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
