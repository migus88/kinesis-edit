using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Views
{
    /// <summary>
    /// The shell window's half of the host preferences: its size, its position and whether it was
    /// maximised (docs/app/host-preferences.md). Everything here is about a real
    /// <see cref="MainWindow"/>, because none of it is reachable from a view model — reading a live
    /// window's geometry is Avalonia's business (app-shell.md invariant 8).
    /// <para>
    /// The restore runs in the window's constructor, so most of these tests never show anything:
    /// what matters is that the numbers are in place <b>before</b> the first frame.
    /// </para>
    /// </summary>
    public sealed class MainWindowGeometryTests : IDisposable
    {
        private static readonly TimeSpan _neverPolls = TimeSpan.FromHours(1);

        private readonly FakeVDriveScanner _scanner = new();
        private readonly FakeVDriveFileService _fileService = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeHostPreferencesStore _preferences = new();
        private readonly FakeEditorViewModelFactory _editors = new();
        private readonly ISettingsService _settings;
        private readonly DeviceSessionManager _sessions;
        private readonly DeviceMonitorService _monitor;
        private readonly DashboardViewModel _dashboard;
        private readonly MainWindowViewModel _shell;

        public MainWindowGeometryTests()
        {
            _settings = TestDevices.CreateSettingsService(_fileService);
            _sessions = new DeviceSessionManager(_settings);
            _monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _fileService,
                new FakeUiDispatcher());

            _dashboard = new DashboardViewModel(
                _monitor,
                new VDriveEjectNotifier(new FakeDeviceEjectService(), _notifications),
                new FakeUrlLauncher());

            _shell = new MainWindowViewModel(
                _dashboard,
                _monitor,
                _sessions,
                _notifications,
                _editors,
                new SettingsScreenViewModel(_preferences, _ => { }, _ => { }),
                new HelpScreenViewModel(new FakeUrlLauncher()));
        }

        [AvaloniaFact]
        public void AWindowWithNoStore_Always_KeepsTheSizeItsXamlDeclares()
        {
            // Every headless scene builds the window this way, and the app did too before there
            // were preferences: no store means nothing is restored and nothing is written.
            var window = new MainWindow(_shell, _notifications);

            Assert.Equal(ThemedHost.DefaultWidth, window.Width);
            Assert.Equal(ThemedHost.DefaultHeight, window.Height);
            Assert.Equal(WindowState.Normal, window.WindowState);
        }

        [AvaloniaFact]
        public void AStoredGeometry_OnConstruction_IsBackBeforeTheWindowIsEverShown()
        {
            var position = OnScreenPosition();

            Store(new WindowGeometry(900, 620, position.X, position.Y, IsMaximized: false));

            var window = new MainWindow(_shell, _notifications, _preferences);

            Assert.Equal(900, window.Width);
            Assert.Equal(620, window.Height);
            Assert.Equal(position, window.Position);
            Assert.Equal(WindowState.Normal, window.WindowState);
        }

        [AvaloniaFact]
        public void AStoredSizeBelowTheWindowsMinimum_Always_IsClampedUpToIt()
        {
            // WindowGeometry's own floor is 1 — it rejects garbage rather than restating a minimum
            // that belongs to the window. So a 200x100 window is a *storable* geometry and it is
            // this restore that has to refuse to shrink below 720x480.
            Store(new WindowGeometry(200, 100, X: null, Y: null, IsMaximized: false));

            var window = new MainWindow(_shell, _notifications, _preferences);

            Assert.Equal(window.MinWidth, window.Width);
            Assert.Equal(window.MinHeight, window.Height);
        }

        [AvaloniaFact]
        public void AStoredPositionOnNoConnectedScreen_Always_IsDroppedWhileTheSizeSurvives()
        {
            // The monitor the window was last closed on is gone. Obeying the file would open the
            // window somewhere the user cannot reach it, so the placement goes back to the platform
            // and only the size is honoured.
            var offScreen = OffScreenPosition();

            Store(new WindowGeometry(880, 600, offScreen.X, offScreen.Y, IsMaximized: false));

            var window = new MainWindow(_shell, _notifications, _preferences);

            Assert.Equal(880, window.Width);
            Assert.Equal(600, window.Height);
            Assert.NotEqual(offScreen, window.Position);
            Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
        }

        [AvaloniaFact]
        public void AStoredMaximizedWindow_Always_ComesBackMaximizedAndKeepsARestoreSize()
        {
            Store(new WindowGeometry(1040, 700, X: null, Y: null, IsMaximized: true));

            var window = new MainWindow(_shell, _notifications, _preferences);

            Assert.Equal(WindowState.Maximized, window.WindowState);

            // The size is still applied, so un-maximising lands on a real window rather than on
            // whatever the platform had before.
            Assert.Equal(1040, window.Width);
            Assert.Equal(700, window.Height);
        }

        [AvaloniaFact]
        public void ARestore_Never_PutsTheWindowIntoAMinimizedState()
        {
            // Nothing stores a minimised flag, and the only state the restore ever sets is
            // Maximized — so however the app was last closed, it opens visible.
            foreach (var maximized in new[] { true, false })
            {
                Store(new WindowGeometry(820, 540, X: null, Y: null, maximized));

                var window = new MainWindow(_shell, _notifications, _preferences);

                Assert.NotEqual(WindowState.Minimized, window.WindowState);
            }
        }

        [AvaloniaFact]
        public async Task AnApprovedClose_Always_PersistsTheGeometryAndItRoundTrips()
        {
            var window = new MainWindow(_shell, _notifications, _preferences);

            using (ThemedHost.Show(window, ThemeVariant.Dark, width: 940, height: 610))
            {
                window.Close();

                await SettleClosingAsync();

                Assert.False(window.IsVisible);
            }

            var stored = _preferences.Current.Window;

            Assert.NotNull(stored);
            Assert.Equal(940, stored!.Width);
            Assert.Equal(610, stored.Height);
            Assert.False(stored.IsMaximized);

            // The whole point of storing it: a window built from what was written comes back the
            // same size.
            var reopened = new MainWindow(_shell, _notifications, _preferences);

            Assert.Equal(940, reopened.Width);
            Assert.Equal(610, reopened.Height);
        }

        [AvaloniaFact]
        public async Task ARefusedClose_Never_PersistsAnything()
        {
            // This is why the save is not in OnClosing: that override cancels *every* first-pass
            // close and re-issues it only after the question is answered, so saving there would
            // record geometry for closes the user then aborts.
            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseResult = false
            };

            _editors.EditorToReturn = editor;

            await _shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var window = new MainWindow(_shell, _notifications, _preferences);

            using var host = ThemedHost.Show(window, ThemeVariant.Dark, width: 940, height: 610);

            window.Close();

            await SettleClosingAsync();

            Assert.True(window.IsVisible, "A refused close still shut the window.");
            Assert.Null(_preferences.Current.Window);
            Assert.Equal(0, _preferences.UpdateCount);
        }

        [AvaloniaFact]
        public async Task AStoreThatThrows_Never_StopsTheWindowClosing()
        {
            // A preference is not worth a window that refuses to shut.
            var window = new MainWindow(_shell, _notifications, new ThrowingHostPreferencesStore());

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            window.Close();

            await SettleClosingAsync();

            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public async Task AGeometryWrite_Never_ClobbersTheOtherPreferences()
        {
            // Both writers go through Update(Func<…>), so the window's close cannot overwrite a
            // theme the Settings screen wrote a moment earlier.
            _preferences.Update(current => current with { Theme = AppThemePreference.Dark });

            var window = new MainWindow(_shell, _notifications, _preferences);

            using (ThemedHost.Show(window, ThemeVariant.Dark))
            {
                window.Close();

                await SettleClosingAsync();
            }

            Assert.Equal(AppThemePreference.Dark, _preferences.Current.Theme);
            Assert.NotNull(_preferences.Current.Window);
        }

        private static async Task SettleClosingAsync()
        {
            for (var pass = 0; pass < 5; pass++)
            {
                Dispatcher.UIThread.RunJobs();

                await Task.Yield();
            }

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>A point the headless platform's one screen really contains.</summary>
        private static PixelPoint OnScreenPosition()
        {
            var screen = new Window().Screens?.Primary
                ?? throw new InvalidOperationException("The headless platform reported no screen.");

            return screen.Bounds.Position + new PixelPoint(40, 30);
        }

        /// <summary>A point no screen contains, i.e. the monitor that was unplugged.</summary>
        private static PixelPoint OffScreenPosition()
        {
            return new PixelPoint(90000, 90000);
        }

        private void Store(WindowGeometry geometry)
        {
            _preferences.Update(current => current with { Window = geometry });
        }

        public void Dispose()
        {
            _shell.Dispose();
            _dashboard.Dispose();
            _monitor.Dispose();
        }

        /// <summary>
        /// A store whose every write fails. The real one swallows its own I/O failures, so this is
        /// the harsher case the window has to survive on its own.
        /// </summary>
        private sealed class ThrowingHostPreferencesStore : IHostPreferencesStore
        {
            public HostPreferences Current => HostPreferences.Default;

            public event Action? Changed
            {
                add { }
                remove { }
            }

            public void Update(Func<HostPreferences, HostPreferences> mutate)
            {
                throw new InvalidOperationException("The preferences store is unavailable.");
            }
        }
    }
}
