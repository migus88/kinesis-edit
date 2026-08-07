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
        /// The notification service every scene shares, for a test that hosts <c>MainWindow</c>
        /// itself (its second constructor takes one).
        /// </summary>
        public INotificationService Notifications => _notifications;

        /// <summary>
        /// The staged profile session the shared editor loaded, for a test that has to see a write
        /// actually land — ⌘S, most of all. Null until an editor has loaded.
        /// </summary>
        public FakeProfileSession? Session => _profiles.SessionToReturn;

        /// <summary>
        /// The capture service every editor of this scene is wired to. It attaches no window
        /// handler of its own, so a test that needs the <b>real</b> ordering — the service
        /// previewing a key on the window <em>above</em> the editor view — pushes keystrokes in
        /// through it from a tunnel handler of its own.
        /// </summary>
        public FakeKeystrokeCaptureService Capture => _capture;

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

            // The one window that takes its view model through the constructor gets it there: the
            // wiring it does in it (attaching the notification overlay) is part of what the scene
            // has to exercise. The message box is no longer a window — it is a card
            // NotificationOverlay hosts — so it is built like every other view, from its
            // DataContext.
            if (viewType == typeof(MainWindow))
            {
                return new MainWindow(CreateShell(), _notifications);
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

            // A version file, so the firmware gates of spec 09 §2 are MET. Without one the key
            // inspector's Tap & hold panel renders its polite refusal instead of its two fields —
            // a real state, but not the one the mockups are about, and every frame captured off
            // this scene would be of the wrong face.
            var device = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdgeRgb));

            // A settings file the panel can actually read. Without one the Settings tab's rows load
            // nothing, HasLoadedSettings stays false, every row and its Save are disabled and the
            // tab renders a read-failure note — a real state, but not the one mockup 1j is about,
            // and every frame captured off this scene would be of the wrong screen.
            _files.SetFile(
                device.Location!.SettingsFilePath,
                "startup_file=layout2.txt",
                "macro_speed=8",
                "status_play_speed=5",
                "v_drive=auto",
                "game_mode=on");

            var editor = new KeyboardEditorViewModel(
                device,
                _profiles,
                TestDevices.CreateSettingsService(_files),
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher,
                OpenSessionFor(device));

            _disposables.Add(editor);

            // The editor draws its own toolbar, whose Home button and status chip come from the
            // shell as data (IShellChrome). Without this the chrome renders blank in every frame
            // this factory feeds — which is exactly the failure the interface exists to avoid.
            editor.Shell = CreateShell();

            await editor.LoadAsync().ConfigureAwait(true);

            _editor = editor;

            return editor;
        }

        /// <summary>
        /// The shared editor with a duplicate-key advisory on the shown layer, so a scene can draw
        /// the amber strip. It is produced through the app's own remap path — select a cap, start
        /// listening, push a keystroke in from the capture fake — rather than by writing to the
        /// model behind the editor's back, because the rebuild hook (<c>RefreshCounters</c>) is
        /// half of what these scenes are meant to exercise.
        /// </summary>
        public async Task<KeyboardEditorViewModel> CreateEditorWithAdvisoriesAsync()
        {
            var editor = await CreateEditorAsync().ConfigureAwait(true);
            var layer = editor.SelectedLayer
                ?? throw new InvalidOperationException("The editor scene rendered no layer.");

            // The board already carries this token on its first position, so one remap puts it on
            // two — which is exactly what DuplicateKeyScan reports.
            Remap(editor, layer, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            if (editor.AdvisoryStrip.AdvisoryCount == 0)
            {
                throw new InvalidOperationException("The duplicate remap produced no advisory.");
            }

            return editor;
        }

        /// <summary>
        /// A lighting panel whose <c>Fn</c> layer sits <b>below</b> the LED 1.0.44 firmware gate
        /// (07 §3), so the panel refuses it. The shared editor is deliberately firmware-complete —
        /// its version file is what lets the key inspector draw its Tap &amp; hold fields rather
        /// than a refusal — so a scene that wants a gate to be <em>unmet</em> has to build its own
        /// editor and say why.
        /// </summary>
        public async Task<LightingTabViewModel> CreateGatedLightingAsync()
        {
            var editor = CreateEditorFor(DeviceId.FreestyleEdgeRgb);

            await editor.LoadAsync().ConfigureAwait(true);

            return editor.Lighting;
        }

        /// <summary>
        /// A second keyboard editor, for <paramref name="deviceId"/> rather than for the Freestyle
        /// Edge RGB the shared one uses. It is what a scene needs to render the chrome of a board
        /// with a different capability set — the TKO, whose led file this app cannot edit yet, so
        /// its strip carries no Lighting tab at all.
        /// </summary>
        public KeyboardEditorViewModel CreateEditorFor(DeviceId deviceId)
        {
            var device = TestDevices.CreateSnapshot(deviceId);
            var editor = new KeyboardEditorViewModel(
                device,
                _profiles,
                TestDevices.CreateSettingsService(_files),
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher,
                OpenSessionFor(device));

            _disposables.Add(editor);

            editor.Shell = CreateShell();

            return editor;
        }

        /// <summary>
        /// Makes the staged profile session report itself dirty, so a scene can drive the amber
        /// Save through the very binding the app uses. The session is built lazily by the factory,
        /// so this is only meaningful after an editor has loaded.
        /// </summary>
        public void MarkSessionDirty()
        {
            if (_profiles.SessionToReturn is null)
            {
                throw new InvalidOperationException("No profile session has been loaded yet.");
            }

            _profiles.SessionToReturn.IsDirty = true;
        }

        /// <summary>
        /// The shell view model. With <paramref name="withDevices"/> the dashboard shows the two
        /// detected devices; without, a completed scan found nothing and the status chip reads
        /// v-Drive Error, which is what mockup 1d asks for. <b>It is not demo mode</b> — that is
        /// what an open demo <em>session</em> reports, so a scene that wants the purple chip opens
        /// one (see <c>ControlRenderTests.CreateDemoShell</c>).
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

            // The readout ticker is parked and the clock stands still: a scene renders one frame,
            // and a ticker firing behind it would be a property change from a thread-pool thread
            // for a string that cannot have changed.
            var shell = new MainWindowViewModel(
                dashboard,
                monitor,
                new DeviceSessionManager(settings),
                _notifications,
                CreateEjectNotifier(),
                editors,
                new FakeSystemClock(),
                new FakeUiDispatcher(),
                _neverPolls);

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
            // A non-default pick on purpose: the "default" tag belongs to the Advantage2 whatever
            // the screen currently shows, and a scene that started on it could not tell the two
            // apart. The baseline is 0 and the count below it is what a screen that has been open
            // for a few passes reads.
            var emptyState = new NoDeviceViewModel(
                _urlLauncher,
                _ => { },
                () => Task.CompletedTask,
                completedRefreshBaseline: 0,
                initialDevice: DeviceId.FreestyleEdgeRgb);

            emptyState.SetRefreshActivity(isRefreshing: false, completedRefreshCount: 8);

            return emptyState;
        }

        /// <summary>A card for one connected Freestyle Edge RGB.</summary>
        public DeviceCardViewModel CreateDeviceCard()
        {
            return CreateDeviceCard(DeviceCardState.Connected);
        }

        /// <summary>
        /// A Freestyle Edge RGB card wearing <paramref name="state"/>. Every one of the four faces
        /// is buildable, because the design's claim about them is a claim about the set — they are
        /// one fixed height and differ only in their rail, their status line and their buttons — and
        /// a state no scene can build is a state nobody ever looks at.
        /// <para>
        /// <see cref="DeviceCardState.Scanning"/> is not a drive fact but the card's own
        /// <c>IsScanning</c> laid over one, which is exactly how the dashboard drives it.
        /// </para>
        /// </summary>
        public DeviceCardViewModel CreateDeviceCard(DeviceCardState state)
        {
            var status = state switch
            {
                DeviceCardState.Resting => VDriveConnectionStatus.NotDetected,
                DeviceCardState.CannotAccess => VDriveConnectionStatus.CannotAccess,
                _ => VDriveConnectionStatus.Connected
            };

            return new DeviceCardViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, status),
                CreateEjectNotifier(),
                _ => { },
                () => Task.CompletedTask)
            {
                IsScanning = state == DeviceCardState.Scanning
            };
        }

        /// <summary>
        /// The card for the one board the app does not edit — the Advantage 360 Professional, which
        /// is configured in Kinesis' web tool. Taken from the catalog rather than named, on the same
        /// rule the view model itself follows.
        /// </summary>
        public WebToolCardViewModel CreateWebToolCard()
        {
            var device = WebToolCardViewModel.WebToolDevices().FirstOrDefault()
                ?? throw new InvalidOperationException("The catalog holds no web-configured device.");

            return new WebToolCardViewModel(device, _urlLauncher);
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
                var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess);
                var editor = new KeyboardEditorViewModel(
                    device,
                    _profiles,
                    TestDevices.CreateSettingsService(_files),
                    _capture,
                    _notifications,
                    _folderPicker,
                    _filePicker,
                    _files,
                    _urlLauncher,
                    OpenSessionFor(device));

                _disposables.Add(editor);

                // Demo mode still draws the editor's own bar, so it still needs the shell — and
                // this scene is the one the Demo Mode bar's "Connect a device" action binds through.
                editor.Shell = CreateShell(withDevices: false);

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
        /// The shared editor with its key inspector rail <b>open on a remappable position</b>. Every
        /// rail scene goes through here rather than building a panel by hand: the rail is only ever
        /// on screen because a cap was clicked, and a scene that skipped the click would be
        /// rendering a state the app cannot reach.
        /// <para>
        /// The position is the board's Escape key rather than a letter or a digit, because §11.1
        /// refuses A-Z and 0-9 on the top layer — so the Tap &amp; hold panel of the very same rail
        /// would draw its refusal instead of its fields.
        /// </para>
        /// </summary>
        public async Task<KeyboardEditorViewModel> CreateEditorWithInspectorAsync()
        {
            var editor = await CreateEditorAsync().ConfigureAwait(true);
            var layer = editor.SelectedLayer
                ?? throw new InvalidOperationException("The editor scene rendered no layer.");

            editor.SelectKeyCommand.Execute(layer.Keys[0]);

            if (!editor.Inspector.IsOpen)
            {
                throw new InvalidOperationException("Selecting a cap did not open the key inspector.");
            }

            return editor;
        }

        /// <summary>
        /// A dialog request with every part of the message box present: mockup 1k's own shape —
        /// YesNoCancel, outcome-named affirmatives, and the suppression opt-out.
        /// </summary>
        public MessageBoxViewModel CreateMessageBox()
        {
            return new MessageBoxViewModel(new MessageBoxRequest
            {
                Title = "Copy macros too?",
                Message = "You're copying F3 onto F8. F3 carries 3 macro slots.",
                Icon = MessageBoxIcon.Confirmation,
                Buttons = MessageBoxButtons.YesNoCancel,
                YesCaption = "Include macros",
                NoCaption = "Key data only",
                SuppressionKey = NotificationKeys.Save
            });
        }

        /// <summary>A corner toast, the app's ordinary notice — mockup 1k's success variant.</summary>
        public ToastViewModel CreateToast()
        {
            return new ToastViewModel(new ToastRequest
            {
                Title = "Saved",
                Message = "Written to the v-Drive. Eject from the dashboard when you want the keyboard to reload them."
            });
        }

        /// <summary>The other toast mockup 1k draws: the same card, on the amber ramp.</summary>
        public ToastViewModel CreateAdvisoryToast()
        {
            return new ToastViewModel(new ToastRequest
            {
                Title = "Saved with 3 advisories",
                Message = "Everything was written. Review the warnings.",
                Severity = ToastSeverity.Advisory
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

            // The status chip binds to an IShellChrome, which is what the shell view model is —
            // the same object the editor reaches it through.
            if (viewType == typeof(StatusChipView))
            {
                return CreateShell();
            }

            if (viewType == typeof(WebToolCardView))
            {
                return CreateWebToolCard();
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

            // The strip binds to its own view model, and it is hidden when the section has nothing
            // to say — so its scene is the strip of an editor that actually has something to say.
            if (viewType == typeof(AdvisoryStripView))
            {
                return (await CreateEditorWithAdvisoriesAsync().ConfigureAwait(true)).AdvisoryStrip;
            }

            if (viewType == typeof(KeyboardSettingsView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).Settings;
            }

            // The Settings tab's second section. It is a section of that tab rather than a screen
            // of its own, so its scene is the same editor's — the tab hosts this very object.
            if (viewType == typeof(AppPreferencesView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).Settings.Preferences;
            }

            if (viewType == typeof(LightingTabView))
            {
                return (await CreateEditorAsync().ConfigureAwait(true)).Lighting;
            }

            if (viewType == typeof(SavantElitePedalView))
            {
                return await CreatePedalEditorAsync().ConfigureAwait(true);
            }

            // The legend row is the *editor's*, not the board's: its counts are pushed in by
            // RefreshCounters. The advisories scene is what gives it something to count — a
            // duplicate remap moves Remapped and Advisory together, so a row of zeros never
            // passes for a working one.
            if (viewType == typeof(BoardLegendView))
            {
                return (await CreateEditorWithAdvisoriesAsync().ConfigureAwait(true)).BoardLegend;
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

            if (viewType == typeof(MessageBoxView))
            {
                return CreateMessageBox();
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

            if (viewType == typeof(TokenPickerOverlayView))
            {
                return new TokenPickerOverlayViewModel(TokenPickerOverlayViewModel.MacroTitle, TokenDialect.Gen1);
            }

            // ===== The key inspector rail and its panels ========================================
            // All five come off ONE loaded editor with the rail open on a real position, never off a
            // hand-built view model: the rail's state is pushed in by the editor's refresh funnel, so
            // a scene that constructed a panel directly would render a shape the app never produces.
            if (viewType == typeof(KeyInspectorView))
            {
                return (await CreateEditorWithInspectorAsync().ConfigureAwait(true)).Inspector;
            }

            if (viewType == typeof(RemapPanelView))
            {
                return await FindInspectorPanelAsync<RemapPanelViewModel>().ConfigureAwait(true);
            }

            if (viewType == typeof(TapAndHoldPanelView))
            {
                return await FindInspectorPanelAsync<TapAndHoldPanelViewModel>().ConfigureAwait(true);
            }

            // The picker itself, hosted by the Remap panel. It is the same instance the panel draws,
            // so the scene shows a picker over a real catalog rather than an empty one.
            if (viewType == typeof(TokenPickerView))
            {
                return (await FindInspectorPanelAsync<RemapPanelViewModel>().ConfigureAwait(true)).Picker;
            }

            // The locked-key panel is UNREACHABLE on the only board with a picture — the Edge RGB's
            // index 0 is an ordinary hotkey, and Locked() appears only in the Advantage2/TKO/Adv360
            // geometries (issues #40/#41). The rail builds it either way, so the scene is the rail's
            // own instance, refreshed against the loaded profile.
            if (viewType == typeof(LockedKeyPanelView))
            {
                return (await CreateEditorWithInspectorAsync().ConfigureAwait(true)).Inspector.LockedPanel;
            }

            return null;
        }

        /// <summary>
        /// The rail's panel of type <typeparamref name="TPanel"/>, off an editor whose inspector is
        /// open. The rail exposes only the <em>showing</em> panel, so each mode is put on screen and
        /// then read back — which also proves the tab actually reaches it.
        /// </summary>
        private async Task<TPanel> FindInspectorPanelAsync<TPanel>()
            where TPanel : KeyInspectorPanelViewModel
        {
            var inspector = (await CreateEditorWithInspectorAsync().ConfigureAwait(true)).Inspector;

            foreach (var tab in inspector.Tabs)
            {
                inspector.SelectModeCommand.Execute(tab);

                if (inspector.ActivePanel is TPanel panel)
                {
                    return panel;
                }
            }

            throw new InvalidOperationException($"The key inspector hosts no {typeof(TPanel).Name}.");
        }

        /// <summary>
        /// Remaps one position through the editor's own workflow: select, listen, receive. The
        /// capture fake pushes the keystroke in exactly as the real service would.
        /// </summary>
        private void Remap(
            KeyboardEditorViewModel editor,
            KeyboardLayerViewModel layer,
            int keyIndex,
            KeyDefinition assignment)
        {
            var key = layer.FindByIndex(keyIndex)
                ?? throw new InvalidOperationException($"The layer has no position {keyIndex}.");

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            _capture.RaiseKeystroke(assignment);
        }

        /// <summary>
        /// A session manager with <paramref name="device"/> already open, which is the state the
        /// shell is always in by the time it builds an editor (<c>MainWindowViewModel.OpenDevice</c>
        /// calls <c>Begin</c> and only then asks the factory for one). Without it every editor
        /// scene would fall back to <see cref="NullAppPreferencesStore"/> and the Settings tab's
        /// preferences section would render its read-only face — a state the app only ever shows
        /// for a board with no drive, so the frames would be of the wrong screen.
        /// <para>
        /// Each editor gets its own manager rather than sharing the shells': a shell ends the
        /// session it holds when its navigation unwinds, and a scene's editor must not lose its
        /// store to another scene's shell.
        /// </para>
        /// </summary>
        private DeviceSessionManager OpenSessionFor(DeviceSnapshot device)
        {
            var sessions = new DeviceSessionManager(TestDevices.CreateSettingsService(_files));

            sessions.Begin(device);

            return sessions;
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
                new FakeSystemClock(),
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
