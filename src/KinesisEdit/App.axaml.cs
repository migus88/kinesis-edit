using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Core.VDrive.Eject;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Input;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit
{
    /// <summary>
    /// The composition root. Everything is wired by hand — the app deliberately has no DI
    /// container — in the order the service layer expects: discovery, then the detection loop,
    /// then the session and notification services, and only then the view models and the shell
    /// window. Polling is started last, once the window exists, because the surfaces a scan can
    /// raise — the message box on the shell's overlay, the file pickers on its storage provider —
    /// are all reached through it. The <c>--keystroke-spike</c> argument replaces that whole graph
    /// with the self-contained <see cref="KeystrokeCaptureSpikeWindow"/>.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Command-line switch that opens the capture harness instead of the shell.</summary>
        private const string KeystrokeSpikeArgument = "--keystroke-spike";

        private DeviceMonitorService? _deviceMonitor;
        private AvaloniaKeystrokeCaptureService? _captureService;
        private DashboardViewModel? _dashboard;
        private MainWindowViewModel? _shell;
        private IMotionSettings? _motionSettings;

        /// <summary>Loads the application XAML.</summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>Builds the object graph, shows the shell, and arms the detection loop.</summary>
        public override void OnFrameworkInitializationCompleted()
        {
            // Before any window exists, because the alias resources it writes are what the
            // views' transitions bind to. Resolved once here and never re-read: the OS
            // accessibility setting is a launch-time question (docs/app/design-system.md).
            _motionSettings = MotionSettings.CreateForCurrentPlatform();
            MotionResourceBinder.Apply(this, _motionSettings);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.Args?.Contains(KeystrokeSpikeArgument) == true)
                {
                    // `dotnet run --project src/KinesisEdit -- --keystroke-spike` opens the issue-#12
                    // capture harness instead of the shell (docs/app/keystroke-capture.md). It owns
                    // its own capture service and needs none of the services below, so nothing is
                    // wired and the detection loop never runs.
                    desktop.MainWindow = new KeystrokeCaptureSpikeWindow();
                }
                else
                {
                    var notifications = BuildServices(desktop);

                    desktop.MainWindow = new MainWindow(_shell!, notifications);
                    desktop.Exit += OnExit;

                    // The shell never starts or stops polling itself; the composition root owns the
                    // loop's lifetime and it runs until exit, because an open editor needs the same
                    // per-tick version-file re-check as the dashboard (specs/10-apps-and-ui.md).
                    // This first pass is synchronous on purpose: DashboardViewModel is built before
                    // the window, so the cards must exist before the first frame or the troubleshoot
                    // empty state would flash on every launch.
                    _deviceMonitor!.Start();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private INotificationService BuildServices(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var fileService = new VDriveFileService();
            var monitor = new VDriveMonitor(new VDriveScanner(PlatformVolumeEnumerator.Create()));

            // One clock and one dispatcher for the whole shell: the detection loop stamps its
            // completed passes with the clock and the app bar's "refreshed Ns ago" readout ages
            // against the same one, and both marshal onto the UI thread the same way.
            var clock = new SystemClock();
            var dispatcher = new AvaloniaUiDispatcher();

            _deviceMonitor = new DeviceMonitorService(monitor, fileService, dispatcher, clock);

            // One settings service for the whole app: app_settings.txt must have a single reader
            // and a single writer, or the notification-suppression flags and the lighting pickers'
            // custom colors clobber each other (specs/08-settings.md §3).
            var settings = new SettingsServiceAdapter(new SettingsService(fileService));
            var sessions = new DeviceSessionManager(settings);

            // The message box is drawn inside the shell window, over the notification overlay's
            // scrim, so what the presenter needs is that overlay rather than an owner window - and
            // the overlay does not exist yet either. Same deferred shape as the pickers below.
            //
            // The capture service goes with it, deferred the same way and for a sharper reason: the
            // box now shares the shell's TopLevel, where a live capture swallows the dialog's own
            // Enter and Escape and assigns them instead. The field is read rather than
            // ResolveCaptureService called - a box raised before any editor has opened has nothing
            // to suspend, and building a capture service to answer a dialog would be backwards.
            var presenter = new MessageBoxPresenter(
                () => FindMessageBoxHost(desktop),
                () => _captureService);
            var notifications = new NotificationService(presenter, sessions);
            var ejectNotifier = new VDriveEjectNotifier(
                new DeviceEjectService(VDriveEject.CreateForCurrentPlatform()),
                notifications);

            var urlLauncher = new ProcessUrlLauncher();

            // The Savant Elite2 editor reads active/pedals.txt through the same file service the
            // rest of the app uses (docs/app/savant-elite.md); it is stateless, so one instance
            // serves every session the shell opens.
            var pedalFiles = new PedalFileService(fileService);

            // Both pickers defer their owner window exactly as the message-box presenter does:
            // the storage provider hangs off a TopLevel, and no window exists yet at this point.
            var editorFactory = new EditorViewModelFactory(
                new ProfileSessionFactory(),
                settings,
                () => ResolveCaptureService(desktop),
                notifications,
                pedalFiles,
                new AvaloniaFolderPickerService(() => FindOwnerWindow(desktop)),
                new AvaloniaFilePickerService(() => FindOwnerWindow(desktop)),
                fileService,
                urlLauncher,
                sessions);

            _dashboard = new DashboardViewModel(_deviceMonitor, ejectNotifier, urlLauncher);
            _shell = new MainWindowViewModel(
                _dashboard,
                _deviceMonitor,
                sessions,
                notifications,
                editorFactory,
                clock,
                dispatcher);

            return notifications;
        }

        /// <summary>
        /// The app-wide keystroke capture service, built over the shell window the first time an
        /// editor asks for one. It previews the key events of a single <c>TopLevel</c>
        /// (docs/app/keystroke-capture.md) and the window does not exist while the graph above is
        /// wired — the same ordering problem the message-box presenter solves with its
        /// <c>Func&lt;Window?&gt;</c> owner. By the time a device is opened the window is on screen.
        /// </summary>
        private IKeystrokeCaptureService ResolveCaptureService(IClassicDesktopStyleApplicationLifetime desktop)
        {
            return _captureService ??= new AvaloniaKeystrokeCaptureService(
                desktop.MainWindow ?? throw new InvalidOperationException(
                    "The shell window must exist before keystroke capture can attach to it."));
        }

        /// <summary>
        /// The surface a message box is drawn on: the shell window's notification overlay. It is
        /// resolved at the moment a box is asked for rather than captured, because the presenter
        /// that calls this is built before the window exists. Null until then — and
        /// <see cref="MessageBoxPresenter"/> answers such a request rather than hanging on it.
        /// </summary>
        private static IMessageBoxPresenter? FindMessageBoxHost(IClassicDesktopStyleApplicationLifetime desktop)
        {
            return (desktop.MainWindow as MainWindow)?.MessageBoxHost;
        }

        /// <summary>
        /// The window a dialog raised right now belongs to: the topmost one. Windows are searched
        /// newest first, because <c>desktop.Windows</c> is in open order and the most recently
        /// opened window is the one on top — which is also the one blocking everything under it
        /// while it is modal. Activation alone is not enough: nothing reports
        /// <see cref="Window.IsActive"/> while the user is in another app, and a dialog raised in
        /// that state must still be owned by the open modal dialog rather than by the shell it
        /// already blocks.
        /// </summary>
        private static Window? FindOwnerWindow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var windows = desktop.Windows;

            for (var index = windows.Count - 1; index >= 0; index--)
            {
                if (windows[index].IsActive)
                {
                    return windows[index];
                }
            }

            return windows.Count > 0 ? windows[^1] : desktop.MainWindow;
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            _shell?.Dispose();
            _dashboard?.Dispose();
            _deviceMonitor?.Dispose();

            // After the shell, which closes the open editor first: the editor stops capture, this
            // detaches it from the window for good.
            _captureService?.Dispose();
        }
    }
}
