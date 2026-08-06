using Avalonia.Controls;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// Builds every view the app declares, over a view model realistic enough that its bindings
    /// produce content — a view whose <c>DataContext</c> is null renders an empty shell and proves
    /// nothing about the styles that would have painted it.
    /// <para>
    /// The collaborators are the shell tests' own fakes, so no scene touches a drive, a settings
    /// file or the OS. One instance serves one test: it owns the view models it hands out and
    /// disposes them with itself.
    /// </para>
    /// </summary>
    internal sealed class ViewSceneFactory : IDisposable
    {
        private static readonly TimeSpan _neverPolls = TimeSpan.FromHours(1);

        private readonly FakeVDriveScanner _scanner = new();
        private readonly FakeVDriveFileService _files = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly FakeFolderPickerService _folderPicker = new();
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeDeviceEjectService _ejectService = new();
        private readonly List<IDisposable> _disposables = [];

        private KeyboardEditorViewModel? _editor;

        /// <summary>
        /// Every <b>screen</b> the app declares: a whole surface the app hosts and hands a view
        /// model, which in this app is always a <see cref="Window"/> or a
        /// <see cref="UserControl"/> — the views under <c>Views/</c> plus the composite controls
        /// under <c>Controls/</c> that are authored the same way (the board, one key cap, the
        /// colour picker).
        /// <para>
        /// It is deliberately not "every <see cref="Control"/> that is not a
        /// <see cref="Panel"/>". <c>Controls/</c> also holds <b>leaf controls</b> drawn in code —
        /// <see cref="KeyboardPanel"/>, <see cref="Icon"/> — which are not screens: they have no
        /// view model, a scene for them would be a fabrication rather than a realistic one, and
        /// <see cref="ViewRenderSmokeTests.EveryView_GetsADataContext_OrDeclaresItNeedsNone"/>
        /// would have to grow a permanent exemption per leaf. They are covered by their own
        /// targeted render tests instead (<c>Controls/KeyboardPanelTests</c>,
        /// <c>Design/IconRenderTests</c>), which can drive the properties a leaf actually has.
        /// </para>
        /// <para>
        /// The narrowing drops nothing that was discovered before: every type the old filter found
        /// is a <see cref="Window"/> or a <see cref="UserControl"/>, and
        /// <see cref="ViewRenderSmokeTests.TheViewCatalog_CoversEveryScreenTheAppCanShow"/> holds
        /// the count and the named screens to that.
        /// </para>
        /// </summary>
        public static IReadOnlyList<Type> ViewTypes()
        {
            return typeof(App).Assembly
                .GetTypes()
                .Where(type => type.IsClass
                    && !type.IsAbstract
                    && (typeof(Window).IsAssignableFrom(type) || typeof(UserControl).IsAssignableFrom(type)))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>The view named by <paramref name="viewTypeName"/>, ready to be hosted.</summary>
        public async Task<Control> CreateAsync(string viewTypeName)
        {
            ArgumentNullException.ThrowIfNull(viewTypeName);

            var viewType = typeof(App).Assembly.GetType(viewTypeName, throwOnError: true)!;

            // The two windows that take their view model through the constructor get it there: the
            // wiring they do in it (the notification overlay, the Enter/Escape contract) is part of
            // what the scene has to exercise.
            if (viewType == typeof(MainWindow))
            {
                return new MainWindow(CreateShell(), _notifications);
            }

            if (viewType == typeof(MessageBoxWindow))
            {
                return new MessageBoxWindow(CreateMessageBox());
            }

            var view = (Control)Activator.CreateInstance(viewType)!;

            if (view is NotificationOverlay overlay)
            {
                overlay.Attach(_notifications);
                _disposables.Add(new Detacher(overlay));
            }

            var dataContext = await CreateDataContextAsync(viewType).ConfigureAwait(true);

            if (dataContext is not null)
            {
                view.DataContext = dataContext;
            }

            return view;
        }

        /// <summary>The Freestyle Edge RGB editor, loaded, shared by every scene that needs it.</summary>
        public async Task<KeyboardEditorViewModel> CreateEditorAsync()
        {
            if (_editor is not null)
            {
                return _editor;
            }

            var editor = new KeyboardEditorViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                TestDevices.CreateSettingsService(_files),
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

            _disposables.Add(editor);

            await editor.LoadAsync().ConfigureAwait(true);

            _editor = editor;

            return editor;
        }

        /// <summary>
        /// The shell view model. With <paramref name="withDevices"/> the dashboard shows the two
        /// detected devices; without, nothing is detected at all and the shell settles into demo
        /// mode — the state the purple status chip renders in.
        /// </summary>
        public MainWindowViewModel CreateShell(bool withDevices = true)
        {
            var dashboard = CreateDashboard(withDevices);
            var monitor = CreateMonitor(withDevices);
            var settings = TestDevices.CreateSettingsService(_files);
            var editors = new EditorViewModelFactory(
                _profiles,
                settings,
                () => _capture,
                _notifications,
                new PedalFileService(_files),
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

            var shell = new MainWindowViewModel(
                dashboard,
                monitor,
                new DeviceSessionManager(settings),
                _notifications,
                CreateEjectNotifier(),
                editors);

            _disposables.Add(shell);

            monitor.Refresh();

            return shell;
        }

        /// <summary>The dashboard, populated with the two devices the frame captures show.</summary>
        public DashboardViewModel CreateDashboard(bool withDevices = true)
        {
            var monitor = CreateMonitor(withDevices);
            var dashboard = new DashboardViewModel(monitor, CreateEjectNotifier(), _urlLauncher);

            _disposables.Add(dashboard);

            monitor.Refresh();

            return dashboard;
        }

        /// <summary>The troubleshoot panel the dashboard shows while nothing is detected.</summary>
        public NoDeviceViewModel CreateNoDevice()
        {
            return new NoDeviceViewModel(_urlLauncher, _ => { }, () => Task.CompletedTask, DeviceId.FreestyleEdgeRgb);
        }

        /// <summary>A card for one connected Freestyle Edge RGB.</summary>
        public DeviceCardViewModel CreateDeviceCard()
        {
            return new DeviceCardViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                CreateEjectNotifier(),
                _ => { },
                () => Task.CompletedTask);
        }

        /// <summary>
        /// The three editors in demo mode — an unwritable drive, so nothing is written and each
        /// view shows its DEMO MODE pill. Keyed by view type, because the pill is authored once per
        /// view and the guard over it has to reach all three.
        /// </summary>
        public async Task<object> CreateDemoModeContextAsync(Type viewType)
        {
            ArgumentNullException.ThrowIfNull(viewType);

            if (viewType == typeof(EditorPlaceholderView))
            {
                return new EditorPlaceholderViewModel(
                    TestDevices.CreateSnapshot(DeviceId.Advantage2, VDriveConnectionStatus.CannotAccess));
            }

            if (viewType == typeof(SavantElitePedalView))
            {
                return await CreatePedalEditorAsync(VDriveConnectionStatus.CannotAccess).ConfigureAwait(true);
            }

            if (viewType == typeof(KeyboardEditorView))
            {
                var editor = new KeyboardEditorViewModel(
                    TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess),
                    _profiles,
                    TestDevices.CreateSettingsService(_files),
                    _capture,
                    _notifications,
                    _folderPicker,
                    _filePicker,
                    _files,
                    _urlLauncher);

                _disposables.Add(editor);

                await editor.LoadAsync().ConfigureAwait(true);

                return editor;
            }

            throw new ArgumentException($"{viewType.Name} has no demo-mode scene.", nameof(viewType));
        }

        /// <summary>The Savant Elite2 pedal editor, loaded off the fake drive.</summary>
        public async Task<SavantElitePedalViewModel> CreatePedalEditorAsync(
            VDriveConnectionStatus status = VDriveConnectionStatus.Connected)
        {
            var editor = new SavantElitePedalViewModel(
                TestDevices.CreateSnapshot(DeviceId.SavantElite2, status),
                new PedalFileService(_files),
                _capture,
                _notifications);

            _disposables.Add(editor);

            await editor.LoadAsync().ConfigureAwait(true);

            return editor;
        }

        /// <summary>
        /// The Tap and Hold panel of spec 11 §11.1, over the board's Escape key — §11.1 refuses
        /// A-Z and 0-9 on the top layer, so the scene cannot use one of those.
        /// </summary>
        public async Task<TapAndHoldOverlayViewModel> CreateTapAndHoldOverlayAsync()
        {
            var editor = await CreateEditorAsync().ConfigureAwait(true);
            var layout = editor.Layout!;
            var layer = layout.Layers[0];
            var result = TapAndHoldOverlayViewModel.TryCreate(layout, layer, layer.Keys[0]);

            return result.Overlay
                ?? throw new InvalidOperationException($"Tap and Hold refused the scene: {result.RefusalMessage}");
        }

        /// <summary>A dialog request with every part of the message box present.</summary>
        public MessageBoxViewModel CreateMessageBox()
        {
            return new MessageBoxViewModel(new MessageBoxRequest
            {
                Title = "Save this profile?",
                Message = "The profile has unsaved changes. Saving rewrites layout1.txt on the v-Drive.",
                Icon = MessageBoxIcon.Confirmation,
                Buttons = MessageBoxButtons.YesNo,
                SuppressionKey = "save_msg"
            });
        }

        /// <summary>A corner toast, the app's ordinary notice.</summary>
        public ToastViewModel CreateToast()
        {
            return new ToastViewModel(new ToastRequest
            {
                Title = "Saved",
                Message = "Profile 1 written to /Volumes/FS_EDGE."
            });
        }

        public void Dispose()
        {
            for (var index = _disposables.Count - 1; index >= 0; index--)
            {
                _disposables[index].Dispose();
            }

            _disposables.Clear();
        }

        /// <summary>
        /// The view model a given view binds against, or null where the view supplies its own —
        /// <see cref="NotificationOverlay"/> binds to itself, and the layout primitives and the
        /// capture spike window carry no view model at all.
        /// </summary>
        private async Task<object?> CreateDataContextAsync(Type viewType)
        {
            if (viewType == typeof(DashboardView))
            {
                return CreateDashboard();
            }

            if (viewType == typeof(DeviceCardView))
            {
                return CreateDeviceCard();
            }

            if (viewType == typeof(NoDeviceView))
            {
                return CreateNoDevice();
            }

            if (viewType == typeof(EditorPlaceholderView))
            {
                return new EditorPlaceholderViewModel(TestDevices.CreateSnapshot(DeviceId.Advantage2));
            }

            if (viewType == typeof(KeyboardEditorView))
            {
                return await CreateEditorAsync().ConfigureAwait(true);
            }

            if (viewType == typeof(KeyboardSettingsView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).Settings;
            }

            if (viewType == typeof(LightingTabView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).Lighting;
            }

            if (viewType == typeof(SavantElitePedalView))
            {
                return await CreatePedalEditorAsync().ConfigureAwait(true);
            }

            if (viewType == typeof(KeyboardView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).SelectedLayer;
            }

            if (viewType == typeof(KeyCapView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];
            }

            if (viewType == typeof(ColorPickerView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).Lighting.Picker;
            }

            if (viewType == typeof(ToastView))
            {
                return CreateToast();
            }

            if (viewType == typeof(LoadingView))
            {
                return new LoadingViewModel();
            }

            if (viewType == typeof(ExportOverlayView))
            {
                return new ExportOverlayViewModel(null, _folderPicker, _files, _notifications);
            }

            if (viewType == typeof(MacroDelayOverlayView))
            {
                return new MacroDelayOverlayViewModel(TokenDialect.Gen1);
            }

            if (viewType == typeof(SearchKeysOverlayView))
            {
                return new SearchKeysOverlayViewModel(SearchKeysOverlayViewModel.DefaultTitle, TokenDialect.Gen1);
            }

            if (viewType == typeof(TapAndHoldOverlayView))
            {
                return await CreateTapAndHoldOverlayAsync().ConfigureAwait(true);
            }

            return null;
        }

        private DeviceMonitorService CreateMonitor(bool withDevices = true)
        {
            if (withDevices)
            {
                _scanner.SetResult(
                    TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb),
                    TestDevices.CreateLocation(DeviceId.Advantage2, isWritable: false));
            }
            else
            {
                _scanner.SetResult();
            }

            var monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _files,
                new FakeUiDispatcher(),
                _neverPolls);

            _disposables.Add(monitor);

            return monitor;
        }

        private VDriveEjectNotifier CreateEjectNotifier()
        {
            return new VDriveEjectNotifier(_ejectService, _notifications);
        }

        /// <summary>Unhooks an overlay from the notification service when the scene is torn down.</summary>
        private sealed class Detacher : IDisposable
        {
            private readonly NotificationOverlay _overlay;

            public Detacher(NotificationOverlay overlay)
            {
                _overlay = overlay;
            }

            public void Dispose()
            {
                _overlay.Detach();
            }
        }
    }
}
