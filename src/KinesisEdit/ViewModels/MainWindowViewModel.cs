using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The single-window shell: it hosts the dashboard and swaps in an editor when a device is
    /// opened, exactly like the legacy dashboards embedded an editor form in their content panel
    /// (specs/10-apps-and-ui.md, "Opening a device" and "Home"). It also owns the app bar's
    /// chrome — the window title, the nav-pill selection, the status indicator (green
    /// 'v-Drive OK', red 'v-Drive Error', or 'Demo Mode') and the mono "refreshed 0.4s ago"
    /// readout of docs/design/mockups.md §1b.
    /// <para>
    /// It is also the app's <see cref="IShellChrome"/>: an editor that draws its own 46 px bar
    /// reaches Home and the status chip through that interface rather than up the visual tree, and
    /// the shell hides its own bar for as long as such an editor is open
    /// (<see cref="IsShellChromeVisible"/>).
    /// </para>
    /// </summary>
    public sealed class MainWindowViewModel : ViewModelBase, IShellChrome, IDisposable
    {
        /// <summary>The app's own name; the window title on the dashboard, and the tail of every other title.</summary>
        public const string AppTitle = "KinesisEdit";

        /// <summary>Indicator text while the version file re-check succeeds.</summary>
        public const string VDriveOkIndicator = "v-Drive OK";

        /// <summary>Indicator text while the version file cannot be read.</summary>
        public const string VDriveErrorIndicator = "v-Drive Error";

        /// <summary>Indicator text while editing without a connected, writable drive (03 §3.5).</summary>
        public const string DemoModeIndicator = "Demo Mode";

        /// <summary>Title of the box raised when a device could not be opened at all.</summary>
        public const string OpenFailureTitle = "Open Device";

        /// <summary>Message prefix of that box; the exception's message follows it.</summary>
        public const string OpenFailureMessagePrefix = "The device could not be opened: ";

        /// <summary>Title of the box raised when the app could not ask whether it may close.</summary>
        public const string CloseFailureTitle = "Close KinesisEdit";

        /// <summary>Message prefix of that box; the exception's message follows it.</summary>
        public const string CloseFailureMessagePrefix =
            "The open editor could not be asked about unsaved changes, so the app stayed open: ";

        // Window title shapes: "KinesisEdit", "TKO — KinesisEdit", "TKO (Demo) — KinesisEdit"
        // (docs/design/mockups.md §2g and the editor screens).
        private const string WindowTitleSeparator = " — ";
        private const string DemoModeTitleSuffix = " (Demo)";

        // "refreshed 0.4s ago" / "refreshed 1.2s ago" (docs/design/mockups.md §1b, §1c) — one
        // decimal, invariant, and rendered in mono so the width never reflows the app bar.
        private const string LastRefreshedTemplate = "refreshed {0:0.0}s ago";

        /// <summary>
        /// How often the "refreshed Ns ago" readout is re-evaluated. 200 ms, not the 100 ms the
        /// one-decimal format could justify: the readout's job is to show the loop is alive, and at
        /// 5 Hz the tenths digit steps by an even 2 each tick — visibly smooth, at half the
        /// property-change traffic. A change notification is raised only when the formatted text
        /// actually differs, so a still readout costs nothing at all.
        /// </summary>
        private static readonly TimeSpan _defaultLastRefreshedTickInterval = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// The window title for a device name and demo flag: <c>KinesisEdit</c> with no device,
        /// <c>‹Device› — KinesisEdit</c> while editing, <c>‹Device› (Demo) — KinesisEdit</c> in
        /// demo mode.
        /// </summary>
        public static string BuildWindowTitle(string? deviceName, bool isDemoMode)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return AppTitle;
            }

            var suffix = isDemoMode ? DemoModeTitleSuffix : string.Empty;

            return deviceName + suffix + WindowTitleSeparator + AppTitle;
        }

        /// <summary>
        /// The "refreshed Ns ago" readout for a completed-pass timestamp and the current time.
        /// Empty while no pass has completed — there is nothing to age, and the app bar shows
        /// nothing rather than a zero that would claim a scan that never happened. A timestamp in
        /// the future (a clock that stepped back) reads as 0.0s rather than negative.
        /// </summary>
        public static string FormatLastRefreshed(DateTimeOffset? lastRefreshedUtc, DateTimeOffset now)
        {
            if (lastRefreshedUtc is null)
            {
                return string.Empty;
            }

            var elapsedSeconds = (now - lastRefreshedUtc.Value).TotalSeconds;

            if (elapsedSeconds < 0)
            {
                elapsedSeconds = 0;
            }

            return string.Format(CultureInfo.InvariantCulture, LastRefreshedTemplate, elapsedSeconds);
        }

        /// <summary>The dashboard; also the view shown whenever no editor is open.</summary>
        public DashboardViewModel Dashboard { get; }

        /// <summary>The view currently filling the window: the dashboard or the open editor.</summary>
        public ViewModelBase CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        /// <summary>
        /// The open editor, or null while the dashboard is showing. Which editor it is depends on
        /// the device and is decided by <see cref="IEditorViewModelFactory"/>; the shell only ever
        /// uses what <see cref="DeviceEditorViewModel"/> declares.
        /// </summary>
        public DeviceEditorViewModel? Editor
        {
            get => _editor;
            private set
            {
                if (SetProperty(ref _editor, value))
                {
                    OnPropertyChanged(nameof(IsEditorOpen));
                    OnPropertyChanged(nameof(IsHomeSelected));
                    OnPropertyChanged(nameof(WindowTitle));

                    // Both directions: an editor with its own chrome opening hides the shell bar,
                    // and the same editor closing brings it back. Raised here rather than at the
                    // two call sites so no navigation path can forget one of them.
                    OnPropertyChanged(nameof(IsShellChromeVisible));
                }
            }
        }

        /// <summary>Whether an editor is open — which is when Home has somewhere to go.</summary>
        public bool IsEditorOpen => _editor is not null;

        /// <summary>
        /// Whether the window draws its own top bar. False exactly while an editor that
        /// <see cref="DeviceEditorViewModel.ProvidesOwnChrome"/> is open: the mockups draw one
        /// 46 px bar while editing, carrying the same Home / device / Save / status pill this bar
        /// carries, so keeping both would stack 92 px of chrome and put two status pills on screen.
        /// </summary>
        public bool IsShellChromeVisible => _editor?.ProvidesOwnChrome != true;

        /// <summary>
        /// Whether the Home nav pill reads as the current location. Settings and Help are never
        /// selected — they are their own issue and stay unavailable — so the view needs no
        /// equivalent for them; it styles them as inactive nav rather than broken buttons.
        /// <para>
        /// This is the exact complement of <see cref="IsEditorOpen"/>, which is why
        /// <see cref="HomeCommand"/> may <b>not</b> be gated on an editor being open: the nav pill
        /// takes its selected face from a style qualified <c>.selected:not(:disabled)</c>
        /// (Themes/ControlThemes/Pills.axaml), so a Home that is disabled exactly when it is
        /// selected can never wear that face and renders as a third dead pill on the dashboard.
        /// </para>
        /// </summary>
        public bool IsHomeSelected => !IsEditorOpen;

        /// <summary>The window title: 'KinesisEdit', '‹Device› — KinesisEdit', or '‹Device› (Demo) — KinesisEdit'.</summary>
        public string WindowTitle => BuildWindowTitle(_editor?.DeviceName, _isDemoMode);

        /// <summary>The mono "refreshed 0.4s ago" readout; empty until the first pass completes.</summary>
        public string LastRefreshedText => FormatLastRefreshed(_monitor.LastRefreshedUtc, _clock.UtcNow);

        /// <summary>Whether a completed detection pass exists to age against.</summary>
        public bool HasLastRefreshed => _monitor.LastRefreshedUtc is not null;

        /// <summary>Whether the open session runs in demo mode.</summary>
        public bool IsDemoMode
        {
            get => _isDemoMode;
            private set
            {
                if (SetProperty(ref _isDemoMode, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        /// <summary>
        /// Whether a navigation — or the window's close question — is in flight. Each of the three
        /// may put a modal question on screen and none of them blocks the top bar, so without this
        /// a Configure would open a second session underneath the answer to the first one, and a
        /// second close attempt would stack a second prompt.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    HomeCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Status indicator caption.</summary>
        public string StatusIndicatorText
        {
            get => _statusIndicatorText;
            private set => SetProperty(ref _statusIndicatorText, value);
        }

        /// <summary>Severity the view maps to the indicator's color.</summary>
        public StatusSeverity StatusIndicatorSeverity
        {
            get => _statusIndicatorSeverity;
            private set => SetProperty(ref _statusIndicatorSeverity, value);
        }

        /// <summary>
        /// Navigates to the dashboard: asks the open editor whether it may be closed, then closes
        /// it. <b>It never ejects</b> — docs/design/mockups.md §1l: "Home just goes home … ejecting
        /// is its own deliberate action on the dashboard card, so nothing is released behind the
        /// user's back." It is a <em>navigation</em>, so it stays runnable while the dashboard is
        /// already showing and simply does nothing — see <see cref="IsHomeSelected"/> for why the
        /// alternative, gating it on <see cref="IsEditorOpen"/>, cannot work.
        /// </summary>
        public IAsyncRelayCommand HomeCommand { get; }

        /// <summary>
        /// The top bar's Settings button (specs/10-apps-and-ui.md). Permanently disabled until
        /// the app-settings dialog exists: nothing consumes <see cref="SettingsRequested"/> yet,
        /// and a button that silently does nothing is worse than a visibly unavailable one.
        /// </summary>
        public IRelayCommand SettingsCommand { get; }

        /// <summary>The top bar's Help button; disabled on the same terms as <see cref="SettingsCommand"/>.</summary>
        public IRelayCommand HelpCommand { get; }

        /// <summary>Raised by <see cref="SettingsCommand"/> until the settings dialog exists.</summary>
        public event Action? SettingsRequested;

        /// <summary>Raised by <see cref="HelpCommand"/> until the help dialog exists.</summary>
        public event Action? HelpRequested;

        private readonly DeviceMonitorService _monitor;
        private readonly DeviceSessionManager _sessions;
        private readonly INotificationService _notifications;
        private readonly IEditorViewModelFactory _editors;
        private readonly ISystemClock _clock;
        private readonly IUiDispatcher _dispatcher;
        private readonly Timer _lastRefreshedTicker;
        private ViewModelBase _currentView;
        private DeviceEditorViewModel? _editor;
        private string _statusIndicatorText = DemoModeIndicator;
        private StatusSeverity _statusIndicatorSeverity = StatusSeverity.Demo;
        private string _lastRefreshedText = string.Empty;
        private bool _isDemoMode;
        private bool _isBusy;
        private bool _isDisposed;

        /// <summary>
        /// Creates the shell over the dashboard, the detection loop, and the session services.
        /// <paramref name="clock"/> ages the "refreshed Ns ago" readout and
        /// <paramref name="dispatcher"/> marshals its ticker onto the UI thread — a
        /// <c>DispatcherTimer</c> would put Avalonia inside a view model
        /// (docs/app/app-shell.md, invariant 8). <paramref name="lastRefreshedTickInterval"/>
        /// overrides the 200 ms default; tests park it.
        /// <para>
        /// No eject notifier: since Home stopped ejecting (docs/design/mockups.md §1l) the shell
        /// has nothing to eject, and the only eject left in the app is the dashboard card's, which
        /// holds its own notifier.
        /// </para>
        /// </summary>
        public MainWindowViewModel(
            DashboardViewModel dashboard,
            DeviceMonitorService monitor,
            DeviceSessionManager sessions,
            INotificationService notifications,
            IEditorViewModelFactory editors,
            ISystemClock clock,
            IUiDispatcher dispatcher,
            TimeSpan? lastRefreshedTickInterval = null)
        {
            Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _editors = editors ?? throw new ArgumentNullException(nameof(editors));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _currentView = dashboard;

            HomeCommand = new AsyncRelayCommand(GoHomeAsync, () => !IsBusy);
            SettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(), () => false);
            HelpCommand = new RelayCommand(() => HelpRequested?.Invoke(), () => false);

            Dashboard.ConfigureRequested += OpenDevice;
            _monitor.Updated += OnMonitorUpdated;
            _monitor.RefreshActivityChanged += OnRefreshActivityChanged;

            UpdateStatusIndicator();

            var tickInterval = lastRefreshedTickInterval ?? _defaultLastRefreshedTickInterval;

            _lastRefreshedTicker = new Timer(OnLastRefreshedTick, null, tickInterval, tickInterval);
        }

        /// <summary>
        /// Opens <paramref name="device"/> in the editor and forgets the task — the form the
        /// dashboard's <c>ConfigureRequested</c> event needs, since it hands out no place to await
        /// one. Everything the navigation does is inside <see cref="OpenDeviceAsync"/>, which is
        /// total.
        /// </summary>
        public void OpenDevice(DeviceSnapshot device)
        {
            _ = OpenDeviceAsync(device);
        }

        /// <summary>
        /// Opens <paramref name="device"/> in the editor, in the order specs/10-apps-and-ui.md
        /// prescribes for Configure: ask the open editor whether it may be closed, set demo mode
        /// from the device's connected/writable state, record the active device, show the loading
        /// splash, swap the view in, initialize. Which editor is built is
        /// <see cref="IEditorViewModelFactory"/>'s decision, so the shell stays device-agnostic
        /// however many editors exist.
        /// <para>
        /// <b>Total.</b> <see cref="OpenDevice"/> forgets the task it returns, so a failure anywhere
        /// in the navigation — the session manager, an editor's constructor — would otherwise become
        /// an unobserved exception leaving the shell on the dashboard with a session open, demo mode
        /// already flipped and nothing said. Instead the half-done navigation is undone and the
        /// failure is shown.
        /// </para>
        /// </summary>
        public async Task OpenDeviceAsync(DeviceSnapshot device)
        {
            if (IsBusy)
            {
                return;
            }

            DeviceEditorViewModel? editor = null;

            // The confirmation may put a modal question on screen, so the navigation is busy from
            // here: without it a second Configure could open a session underneath the answer.
            IsBusy = true;

            try
            {
                // Deliberately outside the guarded region below: a confirmation that fails must
                // abort the navigation, not abandon the editor it was asking about — dropping that
                // editor would discard the very unsaved changes the question is there to protect.
                if (!await ConfirmOpenAsync(device).ConfigureAwait(true))
                {
                    return;
                }

                try
                {
                    IsDemoMode = device.IsDemoMode;

                    _sessions.Begin(device);

                    // Creating an editor may still fail, so the splash is hidden in a finally: an
                    // unexpected failure must not leave the shell wedged behind a loading overlay
                    // with no editor and a disabled Home button.
                    try
                    {
                        _notifications.ShowLoading(LoadingCaptions.ForDevice(device.DisplayName));

                        CloseEditor();

                        editor = CreateEditor(device);

                        // The editor gets the shell as data before it is on screen, so an editor
                        // that draws its own bar has its Home command and its status chip bound by
                        // the time the first frame is measured (IShellChrome).
                        editor.Shell = this;

                        Editor = editor;
                        CurrentView = editor;

                        // The detection loop keeps running: specs/10-apps-and-ui.md requires the
                        // open editor to "re-verify the device's version file every tick" to drive
                        // the v-Drive OK / v-Drive Error indicator, and here one loop serves both
                        // the dashboard and the editor.
                        UpdateStatusIndicator();
                    }
                    finally
                    {
                        _notifications.HideLoading();
                    }
                }
                catch (Exception exception)
                {
                    editor = null;

                    AbandonOpenDevice();

                    await ReportOpenFailureAsync(exception).ConfigureAwait(true);
                }
            }
            finally
            {
                IsBusy = false;
            }

            // What the editor still has to do — every editor reads its files here — happens after
            // the view is on screen, against the editor's own loading state: the shell's splash
            // covers the swap, not the drive read. LoadAsync never throws, which is what makes
            // forgetting the task safe.
            if (editor is not null)
            {
                _ = editor.LoadAsync();
            }
        }

        /// <summary>
        /// Whether the app may close — the answer the window's <c>Closing</c> guard acts on. True
        /// at once when no editor is open; otherwise the open editor is asked the same question
        /// Home and Configure ask it (<see cref="DeviceEditorViewModel.ConfirmCloseAsync"/>), so
        /// all three ways out of a session raise one prompt and none of them can drop unsaved work
        /// silently (docs/design/mockups.md §1l, "Unsaved changes").
        /// <para>
        /// <b>Total, and its safe answer is "no".</b> It is awaited from the window's closing
        /// handler, so a throw would escape onto the UI thread with nobody to catch it; and every
        /// way this can fail — a confirmation that throws, a box that could not be presented,
        /// another navigation already asking — ends with the window staying open. Losing work
        /// because the *question* failed is the exact outcome this guard exists to prevent.
        /// </para>
        /// <para>
        /// It takes part in <see cref="IsBusy"/> for the same reason both navigations do: a
        /// Configure must not start underneath the close prompt, and a second close attempt while
        /// the prompt is up must not stack a second prompt behind it.
        /// </para>
        /// <para>
        /// It only answers. Tearing the editor down stays with <see cref="Dispose"/>, which the
        /// composition root runs from <c>desktop.Exit</c> — so a close that is approved and then
        /// abandoned by the platform leaves the session exactly as it was.
        /// </para>
        /// </summary>
        public async Task<bool> ConfirmShutdownAsync()
        {
            if (!IsEditorOpen)
            {
                return true;
            }

            // Something is already asking — a navigation, or an earlier close attempt whose prompt
            // is still up. Refusing rather than queueing keeps it to one question: the user answers
            // the box on screen and closes again if that is still what they want.
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;

            try
            {
                return await ConfirmEditorCloseAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                await ReportFailureAsync(
                    CloseFailureTitle,
                    CloseFailureMessagePrefix + exception.Message).ConfigureAwait(true);

                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private DeviceEditorViewModel CreateEditor(DeviceSnapshot device)
        {
            return _editors.Create(device);
        }

        /// <summary>
        /// Whether the navigation may go ahead: there is a device, and the open editor agreed to be
        /// closed. A confirmation that throws is reported and answered with "no", which leaves the
        /// open editor on screen with everything it was holding — the alternative, treating it as a
        /// failed open, would discard its unsaved changes without asking.
        /// </summary>
        private async Task<bool> ConfirmOpenAsync(DeviceSnapshot device)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(device);

                return await ConfirmEditorCloseAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                await ReportOpenFailureAsync(exception).ConfigureAwait(true);

                return false;
            }
        }

        /// <summary>
        /// Puts the shell back where a failed open found it: no session, no editor, the dashboard on
        /// screen and the indicator recomputed. Every step is idempotent, so it is safe whether the
        /// navigation failed before or after the session was recorded.
        /// <para>
        /// Every step is also guarded on its own. This runs inside the <c>catch</c> of a method whose
        /// contract is that it never throws, with the error box still to come — and the steps can
        /// throw for real: <see cref="CloseEditor"/> disposes the editor, which for the pedal editor
        /// stops the app-wide keystroke capture service. One failing step must neither swallow the
        /// report nor stop the rest from putting the shell back together.
        /// </para>
        /// </summary>
        private void AbandonOpenDevice()
        {
            RunGuarded(() => _sessions.End());
            RunGuarded(CloseEditor);
            RunGuarded(() =>
            {
                CurrentView = Dashboard;
                IsDemoMode = false;
            });
            RunGuarded(UpdateStatusIndicator);
        }

        /// <summary>Runs one undo step, swallowing whatever it throws (see <see cref="AbandonOpenDevice"/>).</summary>
        private static void RunGuarded(Action step)
        {
            try
            {
                step();
            }
            catch (Exception)
            {
                // Deliberately ignored: the failure being reported is the one that matters.
            }
        }

        /// <summary>Tells the user why the device did not open (see <see cref="ReportFailureAsync"/>).</summary>
        private Task ReportOpenFailureAsync(Exception exception)
        {
            return ReportFailureAsync(OpenFailureTitle, OpenFailureMessagePrefix + exception.Message);
        }

        /// <summary>
        /// Tells the user that something the shell was doing failed. Swallows a box that cannot be
        /// shown — the window may already be gone, which is precisely the case while the app is
        /// closing, and a failure to report a failure must not escape a method whose whole point is
        /// that it never throws.
        /// </summary>
        private async Task ReportFailureAsync(string title, string message)
        {
            try
            {
                await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = title,
                    Message = message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Nothing left to try: the failure is already on its way to being invisible.
            }
        }

        /// <summary>
        /// Returns to the dashboard. <b>Nothing is ejected here</b>: docs/design/mockups.md §1l
        /// says "Home just goes home — it never ejects. Ejecting is its own deliberate action on
        /// the dashboard card, so nothing is released behind the user's back", and a drive released
        /// as a side effect of a navigation is exactly what that forbids. The dashboard card's
        /// <c>Eject</c> button is the only eject in the app.
        /// </summary>
        private async Task GoHomeAsync()
        {
            // Home is already where we are: nothing to confirm and nothing to close. The command
            // stays runnable there so the pill can wear its selected face, so this is the one place
            // that has to say the navigation is a no-op.
            if (!IsEditorOpen || IsBusy)
            {
                return;
            }

            IsBusy = true;

            try
            {
                if (!await ConfirmEditorCloseAsync().ConfigureAwait(true))
                {
                    return;
                }

                _sessions.End();

                CloseEditor();

                CurrentView = Dashboard;
                IsDemoMode = false;
            }
            finally
            {
                IsBusy = false;
            }

            UpdateStatusIndicator();
        }

        /// <summary>
        /// Asks the open editor whether it may be closed (unsaved work, an edit in progress). The
        /// shell knows nothing about what is at stake — only whether the navigation may go ahead.
        /// </summary>
        private Task<bool> ConfirmEditorCloseAsync()
        {
            return Editor?.ConfirmCloseAsync() ?? Task.FromResult(true);
        }

        /// <summary>
        /// Drops the open editor, disposing it first. An editor may hold the app-wide keystroke
        /// capture service, which would otherwise keep swallowing keystrokes from the dashboard
        /// behind it (docs/app/keystroke-capture.md). The reference is dropped even when the
        /// disposal fails: an editor that could not be torn down cleanly is still gone from the
        /// screen, and keeping it here would leave Home pointing at it.
        /// </summary>
        private void CloseEditor()
        {
            var editor = Editor;

            try
            {
                if (editor is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            finally
            {
                // Dropped even when the disposal failed, and before the reference goes: an editor
                // still holding the shell would keep a Home button live on a surface that is no
                // longer on screen.
                if (editor is not null)
                {
                    editor.Shell = null;
                }

                Editor = null;
            }
        }

        private void UpdateStatusIndicator()
        {
            var session = _sessions.Active;

            if (session is not null)
            {
                SetStatusIndicator(session.Health);

                return;
            }

            SetStatusIndicator(GetDashboardHealth());
        }

        private VDriveHealth GetDashboardHealth()
        {
            // Nothing has been looked at yet. The chip keeps the Demo Mode face its field
            // initialisers give it rather than announcing a failure no scan has established —
            // "gone" is a finding, and before the first pass there is none. In the shipped app
            // this state is never on screen: the composition root runs one synchronous pass in
            // Start() before the first frame.
            if (_monitor.LastRefreshedUtc is null)
            {
                return VDriveHealth.Unknown;
            }

            foreach (var snapshot in _monitor.Snapshots)
            {
                if (snapshot.Status == VDriveConnectionStatus.Connected && snapshot.Health == VDriveHealth.Ok)
                {
                    return VDriveHealth.Ok;
                }
            }

            // Everything else a completed scan can report is "v-Drive Error", which mockup 1a
            // defines as "gone · unwritable" — a drive that is present but unreadable is
            // unwritable, and no drive at all is gone. Mockup 1d says so outright: the chip reads
            // v-Drive Error while nothing is present. Demo Mode is not this state; it means
            // "nothing is written", which is true of an open demo session and is what the session
            // branch above reports.
            return VDriveHealth.Error;
        }

        private void SetStatusIndicator(VDriveHealth health)
        {
            switch (health)
            {
                case VDriveHealth.Ok:
                    StatusIndicatorText = VDriveOkIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Ok;
                    break;
                case VDriveHealth.Error:
                    StatusIndicatorText = VDriveErrorIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Error;
                    break;
                default:
                    // Demo mode, not an advisory: no drive is being written to, which the design
                    // gives its own colour rather than folding into amber. Two things reach here —
                    // an open demo session, and the instant before the first scan completes; on
                    // the dashboard a *completed* scan that found nothing reads v-Drive Error
                    // instead, per mockup 1d.
                    StatusIndicatorText = DemoModeIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Demo;
                    break;
            }
        }

        private void OnMonitorUpdated(DeviceMonitorUpdate update)
        {
            // The open session follows the loop, so a drive that disappears or stops answering
            // mid-session flips the indicator to 'v-Drive Error' instead of freezing on the state
            // it had when the editor opened. The deferred "Keyboard Connection Lost" dialog that
            // update.HasConnectionLoss also feeds is save/load-time behavior owned by a later issue.
            _sessions.UpdateActive(update.Snapshots);

            UpdateStatusIndicator();
        }

        /// <summary>
        /// A pass started or finished. Arrives already marshaled onto the UI thread
        /// (docs/app/app-shell.md, invariant 7), and re-reads the readout immediately so a
        /// completed pass snaps to "refreshed 0.0s ago" instead of waiting for the next tick.
        /// </summary>
        private void OnRefreshActivityChanged()
        {
            PublishLastRefreshedText();
        }

        /// <summary>
        /// Ages the readout. A plain <see cref="Timer"/> marshaled through the
        /// <see cref="IUiDispatcher"/>, not a <c>DispatcherTimer</c>: view models carry no Avalonia
        /// types (docs/app/app-shell.md, invariant 8).
        /// </summary>
        private void OnLastRefreshedTick(object? state)
        {
            if (_isDisposed)
            {
                return;
            }

            _dispatcher.Post(PublishLastRefreshedText);
        }

        /// <summary>
        /// Raises the readout's change notification, and only when the rendered text really
        /// changed — a still readout must not cost a property change per tick.
        /// </summary>
        private void PublishLastRefreshedText()
        {
            if (_isDisposed)
            {
                return;
            }

            var text = LastRefreshedText;

            if (string.Equals(text, _lastRefreshedText, StringComparison.Ordinal))
            {
                return;
            }

            _lastRefreshedText = text;

            OnPropertyChanged(nameof(LastRefreshedText));
            OnPropertyChanged(nameof(HasLastRefreshed));
        }

        /// <summary>
        /// Unsubscribes from the dashboard and the detection loop, stops the readout ticker, and
        /// closes any open editor. Safe to call multiple times.
        /// <para>
        /// It still closes the editor even though <see cref="ConfirmShutdownAsync"/> now runs
        /// first, and deliberately <em>asks nothing</em>: <see cref="IDisposable"/> cannot await,
        /// and this must stay correct for anything that tears the shell down without going through
        /// the window's closing guard. The guard is what makes the question happen while there is
        /// still a window to ask it in; this is the teardown that follows.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            Dashboard.ConfigureRequested -= OpenDevice;
            _monitor.Updated -= OnMonitorUpdated;
            _monitor.RefreshActivityChanged -= OnRefreshActivityChanged;

            _lastRefreshedTicker.Dispose();

            CloseEditor();
        }
    }
}
