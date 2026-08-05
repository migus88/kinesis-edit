using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The "Check for Updates" dialog of specs/09-firmware.md §3, run as its seven steps: start
    /// every row on "Checking for update...", take the local versions from the snapshot the
    /// detection loop already produced (step 1), add the app's own version (step 2), GET the
    /// published-versions endpoint (step 3), compare (steps 4-5), and turn any failure into
    /// "Check connection" plus a dialog (step 6). Comparison itself belongs to
    /// <see cref="UpdateCheckService"/>; this view model owns the flow and the wording only, and
    /// nothing here transfers firmware — an "Update Now" row opens the support site (step 7).
    /// </summary>
    public sealed class FirmwareUpdateViewModel : ViewModelBase, IDisposable
    {
        /// <summary>Window title (§3).</summary>
        public const string WindowTitle = "Check for Updates";

        /// <summary>Caption of the dialog's dismiss button. Not a spec string — the legacy form was closed from its title bar.</summary>
        public const string CloseCaption = "Close";

        /// <summary>Message prefix of the §3 step 6 failure dialog; the exception's message follows it.</summary>
        public const string ConnectionErrorMessagePrefix = "Error accessing internet or firmware website: ";

        /// <summary>Title of the raw-JSON dump shown in firmware-debug mode (§3 step 3); the spec gives none.</summary>
        public const string FirmwareDebugTitle = "Firmware Debug";

        /// <summary>The §3 step 6 dialog message for <paramref name="exceptionMessage"/>.</summary>
        public static string BuildConnectionErrorMessage(string? exceptionMessage)
        {
            return ConnectionErrorMessagePrefix + exceptionMessage;
        }

        /// <summary>Window title.</summary>
        public string Title => WindowTitle;

        /// <summary>The device being checked.</summary>
        public DeviceSnapshot Device { get; }

        /// <summary>
        /// The dialog's rows, in §3 order: keyboard, lighting, app — with the lighting row absent
        /// (not merely hidden) for the Advantage 360, which is what shrinks the window.
        /// </summary>
        public ObservableCollection<FirmwareUpdateRowViewModel> Rows { get; } = [];

        /// <summary>Whether a check is currently in flight.</summary>
        public bool IsChecking
        {
            get => _isChecking;
            private set => SetProperty(ref _isChecking, value);
        }

        /// <summary>Runs the check; the window fires it once, when it is shown.</summary>
        public IAsyncRelayCommand CheckCommand { get; }

        private readonly IVersionManifestClient _manifestClient;
        private readonly IAppVersionProvider _appVersionProvider;
        private readonly INotificationService _notifications;
        private readonly IUrlLauncher _urlLauncher;
        private readonly UpdateCheckPlatform _platform;
        private readonly CancellationTokenSource _cancellation = new();
        private bool _isChecking;
        private bool _isDisposed;

        /// <summary>Creates the dialog's view model for <paramref name="device"/>.</summary>
        public FirmwareUpdateViewModel(
            DeviceSnapshot device,
            IVersionManifestClient manifestClient,
            IAppVersionProvider appVersionProvider,
            INotificationService notifications,
            IUrlLauncher urlLauncher,
            UpdateCheckPlatform platform)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            _manifestClient = manifestClient ?? throw new ArgumentNullException(nameof(manifestClient));
            _appVersionProvider = appVersionProvider ?? throw new ArgumentNullException(nameof(appVersionProvider));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _platform = platform;

            CheckCommand = new AsyncRelayCommand(CheckAsync, () => !IsChecking);

            SetRows(UpdateCheckService.BuildCheckingRows(device.DeviceId));
        }

        /// <summary>
        /// Runs the §3 flow once, and never throws. A second call while one is in flight is
        /// ignored, so a double click cannot fire two requests; a cancelled run (the window closed
        /// mid-fetch) leaves the rows untouched and shows nothing; every other failure becomes the
        /// step 6 outcome. Totality is load-bearing: the window runs this through
        /// <see cref="IAsyncRelayCommand"/>, which rethrows on the UI thread, where an escaped
        /// exception is an unhandled crash rather than a reported error.
        /// </summary>
        public async Task CheckAsync()
        {
            if (IsChecking || _isDisposed)
            {
                return;
            }

            IsChecking = true;
            CheckCommand.NotifyCanExecuteChanged();

            try
            {
                await RunCheckAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                await ReportFailureAsync(exception).ConfigureAwait(true);
            }
            finally
            {
                IsChecking = false;
                CheckCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task RunCheckAsync()
        {
            var deviceId = Device.DeviceId;

            SetRows(UpdateCheckService.BuildCheckingRows(deviceId));

            if (Rows.Count == 0)
            {
                // A device with no update dialog at all (§4: the FS Edge/Pro, Advantage2 and
                // Savant Elite 2 apps have none) has no rows to fill, so there is nothing to
                // compare and no reason to spend a request. FirmwareUpdatePresenter refuses to
                // open the window for one; this is the same rule one layer down.
                return;
            }

            // Step 1: the local versions were re-read by the detection loop on its last pass, so
            // the dialog compares what the shell already knows instead of touching the drive again.
            // "Comes back empty" is the whole rule — a present but unparseable value is compared
            // as 0.0.0 (§1.1) — which is why the condition is Core's rather than a null check here.
            if (!UpdateCheckService.HasReadableKeyboardVersion(Device.VersionFile))
            {
                SetRows(UpdateCheckService.BuildUnreadableRows(deviceId));

                return;
            }

            var endpointUrl = VersionEndpoints.FindUrl(deviceId);

            if (endpointUrl is null)
            {
                // Unreachable for the three devices that offer the dialog — all of them are served
                // by a master app — but a missing endpoint is a failure to reach it all the same.
                SetRows(UpdateCheckService.BuildConnectionErrorRows(deviceId));

                return;
            }

            var manifest = await _manifestClient.FetchAsync(endpointUrl, _cancellation.Token).ConfigureAwait(true);

            if (_cancellation.IsCancellationRequested)
            {
                return;
            }

            SetRows(UpdateCheckService.BuildRows(new UpdateCheckRequest
            {
                Device = deviceId,
                LocalVersions = Device.VersionFile,
                Manifest = manifest,
                AppVersionText = _appVersionProvider.Version,
                Platform = _platform
            }));

            // The step 3 dump comes after the rows on purpose: the message box blocks until it is
            // dismissed, and the rows behind it must already show the result rather than "Checking
            // for update...".
            await ShowFirmwareDebugAsync(manifest).ConfigureAwait(true);
        }

        /// <summary>
        /// Step 6: no internet, an HTTP error, a malformed payload — and anything else that escaped
        /// the flow — are one outcome, every row on "Check connection" plus a dialog quoting the
        /// message. Both halves are best effort, because reporting a failure must never be the
        /// thing that brings the app down.
        /// </summary>
        private async Task ReportFailureAsync(Exception exception)
        {
            if (_isDisposed || _cancellation.IsCancellationRequested)
            {
                // The run was cancelled deliberately — the window closed mid-fetch — so no rows are
                // rewritten and no dialog is raised behind a dialog that is already gone.
                return;
            }

            try
            {
                SetRows(UpdateCheckService.BuildConnectionErrorRows(Device.DeviceId));
            }
            catch (Exception)
            {
                // Rebuilding the rows runs the bound view's collection handlers; if they throw, the
                // dialog below is still the other, user-visible half of step 6.
            }

            await TryShowMessageBoxAsync(new MessageBoxRequest
            {
                Title = WindowTitle,
                Message = BuildConnectionErrorMessage(exception.Message),
                Icon = MessageBoxIcon.Error
            }).ConfigureAwait(true);
        }

        private Task ShowFirmwareDebugAsync(VersionManifest manifest)
        {
            // Firmware-debug mode is an empty debug_firm.on marker at the drive root
            // (specs/03-vdrive-and-files.md §3.4); §3 step 3 dumps the raw response under it.
            if (Device.Location?.DebugFlags.HasFlag(VDriveDebugFlags.FirmwareDebug) != true)
            {
                return Task.CompletedTask;
            }

            return TryShowMessageBoxAsync(new MessageBoxRequest
            {
                Title = FirmwareDebugTitle,
                Message = manifest.RawJson
            });
        }

        /// <summary>
        /// Shows <paramref name="request"/> and swallows whatever it throws. A check outlives its
        /// window easily enough — quitting the app mid-fetch is one way — and a modal over a closed
        /// owner throws instead of opening; the rows already carry the outcome, so a box that
        /// cannot be shown degrades quietly rather than crashing the process.
        /// </summary>
        private async Task TryShowMessageBoxAsync(MessageBoxRequest request)
        {
            try
            {
                await _notifications.ShowMessageBoxAsync(request).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Nowhere to show it; see the summary above.
            }
        }

        private void SetRows(IReadOnlyList<UpdateCheckRow> rows)
        {
            Rows.Clear();

            foreach (var row in rows)
            {
                Rows.Add(new FirmwareUpdateRowViewModel(row, _urlLauncher));
            }
        }

        /// <summary>Cancels an in-flight request; the window disposes the view model when it closes. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
