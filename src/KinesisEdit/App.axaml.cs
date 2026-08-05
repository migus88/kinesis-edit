using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Core.VDrive.Eject;
using KinesisEdit.Core.VDrive.Io;
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
            var presenter = new MessageBoxPresenter(() => desktop.MainWindow);
            var notifications = new NotificationService(presenter, sessions);
            var ejectNotifier = new VDriveEjectNotifier(
                new DeviceEjectService(VDriveEject.CreateForCurrentPlatform()),
                notifications);

            _dashboard = new DashboardViewModel(_deviceMonitor, ejectNotifier, new ProcessUrlLauncher());
            _shell = new MainWindowViewModel(_dashboard, _deviceMonitor, sessions, notifications, ejectNotifier);

            return notifications;
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            _shell?.Dispose();
            _dashboard?.Dispose();
            _deviceMonitor?.Dispose();
        }
    }
}
