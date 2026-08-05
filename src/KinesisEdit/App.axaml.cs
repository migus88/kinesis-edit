using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.SavantElite;
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
    /// window. Polling is started last, once the window exists, because the message-box presenter
    /// needs it as a modal owner. The <c>--keystroke-spike</c> argument replaces that whole graph
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

        /// <summary>Loads the application XAML.</summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>Builds the object graph, shows the shell, and arms the detection loop.</summary>
        public override void OnFrameworkInitializationCompleted()
        {
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

            _deviceMonitor = new DeviceMonitorService(monitor, fileService, new AvaloniaUiDispatcher());

            var sessions = new DeviceSessionManager(fileService);

            // The owner is the topmost window rather than the shell, so a message box raised from
            // inside a modal dialog is owned by that dialog instead of by the window it already
            // blocks.
            var presenter = new MessageBoxPresenter(() => FindOwnerWindow(desktop));
            var notifications = new NotificationService(presenter, sessions);
            var ejectNotifier = new VDriveEjectNotifier(
                new DeviceEjectService(VDriveEject.CreateForCurrentPlatform()),
                notifications);

            var urlLauncher = new ProcessUrlLauncher();

            // The Savant Elite2 editor reads active/pedals.txt through the same file service the
            // rest of the app uses (docs/app/savant-elite.md); it is stateless, so one instance
            // serves every session the shell opens.
            var pedalFiles = new PedalFileService(fileService);

            var editorFactory = new EditorViewModelFactory(
                new ProfileSessionFactory(),
                () => ResolveCaptureService(desktop),
                notifications,
                pedalFiles);

            _dashboard = new DashboardViewModel(_deviceMonitor, ejectNotifier, urlLauncher);
            _shell = new MainWindowViewModel(_dashboard, _deviceMonitor, sessions, notifications, ejectNotifier, editorFactory);

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
