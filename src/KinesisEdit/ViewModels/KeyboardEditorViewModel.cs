using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor of a programmable keyboard: the keyboard picture of one profile plus the remap
    /// workflow of specs/10-apps-and-ui.md ("click an on-screen key — the key enters 'listening'
    /// state; the next physical keypress captured by the app becomes the new assignment").
    /// <para>
    /// It coordinates and owns no rules of its own: the profile is loaded and saved by
    /// <see cref="IProfileSessionFactory"/> (docs/app/profiles.md), keystrokes arrive from
    /// <see cref="IKeystrokeCaptureService"/> (docs/app/keystroke-capture.md), the model is Core's
    /// (docs/app/keyboard-model.md), and per-key/per-layer presentation lives in
    /// <see cref="KeyboardKeyViewModel"/>/<see cref="KeyboardLayerViewModel"/>.
    /// </para>
    /// </summary>
    public sealed class KeyboardEditorViewModel : DeviceEditorViewModel, IDisposable
    {
        /// <summary>Prefix of the profile caption; the loaded profile number follows it.</summary>
        public const string ProfileCaptionPrefix = "Profile ";

        /// <summary>Caption of the loading indicator during a save. Not a spec string.</summary>
        public const string SavingCaption = "Saving...";

        /// <summary>Title of the dialog raised when the profile cannot be read from the drive.</summary>
        public const string LoadFailureTitle = "Load Profile";

        /// <summary>Message prefix of that dialog; the exception's message follows it.</summary>
        public const string LoadFailureMessagePrefix = "The profile could not be loaded from the v-Drive: ";

        /// <summary>Title of everything a save raises — its failure dialogs and its post-save toast.</summary>
        public const string SaveTitle = "Save Profile";

        /// <summary>Heading of the violation list shown when validation stopped the save (04 §5.3).</summary>
        public const string SaveRejectedMessage = "The profile was not saved because it exceeds the device's limits:";

        /// <summary>Message prefix when the save threw; the exception's message follows it.</summary>
        public const string SaveErrorMessagePrefix = "The profile could not be saved: ";

        /// <summary>Builds the caption of the profile indicator ("Profile 1").</summary>
        public static string BuildProfileCaption(int profileNumber)
        {
            return ProfileCaptionPrefix + profileNumber.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Prefix of the "Macro (n)" counter of specs/10-apps-and-ui.md.</summary>
        public const string MacroCounterPrefix = "Macro";

        /// <summary>Builds the "Remap (n)" counter of specs/10-apps-and-ui.md.</summary>
        public static string BuildRemapCounterCaption(int modifiedKeyCount)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Remap ({modifiedKeyCount})");
        }

        /// <summary>Builds the "Macro (n)" counter of specs/10-apps-and-ui.md.</summary>
        public static string BuildMacroCounterCaption(int macroCount)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{MacroCounterPrefix} ({macroCount})");
        }

        /// <summary>Renders one unapplied layout line for the invalid-line list (04 §5.2).</summary>
        public static string BuildInvalidLineMessage(LayoutInvalidLine line)
        {
            ArgumentNullException.ThrowIfNull(line);

            return string.Create(CultureInfo.InvariantCulture, $"Line {line.LineNumber}: {line.Text}");
        }

        /// <summary>Whether the profile is still being read; the picture is empty until it is not.</summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>Whether a save is in flight.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>The loaded model, or null while loading (and after a load that failed outright).</summary>
        public KeyboardLayout? Layout
        {
            get => _layout;
            private set => SetProperty(ref _layout, value);
        }

        /// <summary>"Profile n" for a loaded profile; empty in demo mode, which reads no file.</summary>
        public string ProfileCaption
        {
            get => _profileCaption;
            private set => SetProperty(ref _profileCaption, value);
        }

        /// <summary>Remapped positions across every layer (<see cref="KeyboardLayout.ModifiedKeyCount"/>).</summary>
        public int ModifiedKeyCount
        {
            get => _modifiedKeyCount;
            private set
            {
                if (SetProperty(ref _modifiedKeyCount, value))
                {
                    OnPropertyChanged(nameof(RemapCounterCaption));
                }
            }
        }

        /// <summary>The "Remap (n)" counter of specs/10-apps-and-ui.md.</summary>
        public string RemapCounterCaption => BuildRemapCounterCaption(ModifiedKeyCount);

        /// <summary>Macros across the whole profile (<see cref="KeyboardLayout.MacroCount"/>).</summary>
        public int MacroCount
        {
            get => _macroCount;
            private set
            {
                if (SetProperty(ref _macroCount, value))
                {
                    OnPropertyChanged(nameof(MacroCounterCaption));
                }
            }
        }

        /// <summary>The "Macro (n)" counter of specs/10-apps-and-ui.md.</summary>
        public string MacroCounterCaption => BuildMacroCounterCaption(MacroCount);

        /// <summary>Lines of the loaded file that could not be applied (04 §5); shown, never dropped silently.</summary>
        public IReadOnlyList<string> InvalidLineMessages
        {
            get => _invalidLineMessages;
            private set
            {
                if (SetProperty(ref _invalidLineMessages, value))
                {
                    OnPropertyChanged(nameof(HasInvalidLines));
                }
            }
        }

        /// <summary>Whether the loaded file carried lines the parser could not apply.</summary>
        public bool HasInvalidLines => InvalidLineMessages.Count > 0;

        /// <summary>Board width in key units; the view picks a pixel scale and multiplies.</summary>
        public double BoardWidth => _visual?.Width ?? 0;

        /// <summary>Board height in key units.</summary>
        public double BoardHeight => _visual?.Height ?? 0;

        /// <summary>
        /// The editor's sections. <see cref="EditorTab.Lighting"/> and
        /// <see cref="EditorTab.Settings"/> are disabled placeholders (issue #16).
        /// </summary>
        public IReadOnlyList<EditorTabViewModel> Tabs { get; }

        /// <summary>The open section.</summary>
        public EditorTab SelectedTab
        {
            get => _selectedTab;
            set => SelectTab(value);
        }

        /// <summary>
        /// The macro editor of specs/10-apps-and-ui.md, built once the profile is loaded; null
        /// while loading and after a load that produced no model at all.
        /// </summary>
        public MacroPanelViewModel? MacroPanel
        {
            get => _macroPanel;
            private set
            {
                if (SetProperty(ref _macroPanel, value))
                {
                    OnPropertyChanged(nameof(IsMacroPanelVisible));
                }
            }
        }

        /// <summary>Whether the macro panel is the open section. The Keys tab hides it again.</summary>
        public bool IsMacroPanelVisible => _selectedTab == EditorTab.Macros && _macroPanel is not null;

        /// <summary>
        /// The feature panel rendered over the editor — Tap and Hold, Macro Timing Delays, Search
        /// Keys, Export (spec 11) — or null when none is open. Opened through
        /// <see cref="ShowOverlay"/> and held by <see cref="EditorOverlayHost"/>, which drops it
        /// again when it raises <see cref="EditorOverlayViewModel.Closed"/>.
        /// </summary>
        public EditorOverlayViewModel? ActiveOverlay => _overlays.Active;

        /// <summary>Whether a feature panel is open over the editor.</summary>
        public bool HasActiveOverlay => _overlays.Active is not null;

        /// <summary>
        /// Whether the open feature panel is itself waiting for the next physical keystroke — a
        /// Tap and Hold field armed by its <em>Press Key</em> button. That is the one case in which
        /// Escape belongs to the panel's field rather than to dismissing the panel, so the view's
        /// Escape route reads this instead of guessing from <c>KeyEventArgs.Handled</c>: capture may
        /// have swallowed the key for something else entirely.
        /// </summary>
        public bool IsOverlayAwaitingKeystroke => _overlays.Active is IKeystrokeSink { WantsKeystrokes: true };

        /// <summary>The device's layers, in model order.</summary>
        public IReadOnlyList<KeyboardLayerViewModel> Layers
        {
            get => _layers;
            private set => SetProperty(ref _layers, value);
        }

        /// <summary>The layer the picture is showing.</summary>
        public KeyboardLayerViewModel? SelectedLayer
        {
            get => _selectedLayer;
            private set
            {
                if (SetProperty(ref _selectedLayer, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>The key every key-scoped action applies to, or null when nothing is selected.</summary>
        public KeyboardKeyViewModel? SelectedKey
        {
            get => _selectedKey;
            private set
            {
                if (SetProperty(ref _selectedKey, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>The key waiting for the next physical keypress, or null when nothing is listening.</summary>
        public KeyboardKeyViewModel? ListeningKey
        {
            get => _listeningKey;
            private set
            {
                if (SetProperty(ref _listeningKey, value))
                {
                    OnPropertyChanged(nameof(IsListening));

                    NotifyCommands();
                }
            }
        }

        /// <summary>Whether a key is currently listening for its new assignment.</summary>
        public bool IsListening => ListeningKey is not null;

        /// <summary>Opens a section of the editor; disabled tabs are refused.</summary>
        public IRelayCommand<EditorTabViewModel> SelectTabCommand { get; }

        /// <summary>Switches the picture to another layer, cancelling anything in progress.</summary>
        public IRelayCommand<KeyboardLayerViewModel> SelectLayerCommand { get; }

        /// <summary>
        /// What a click on a key cap runs: it selects the key, and a second click on the key that
        /// is already selected starts listening (specs/10-apps-and-ui.md, "Remap workflow").
        /// </summary>
        public IRelayCommand<KeyboardKeyViewModel> SelectKeyCommand { get; }

        /// <summary>Puts the selected key into listening state.</summary>
        public IRelayCommand BeginRemapCommand { get; }

        /// <summary>Leaves listening state without changing anything (the view binds Escape to it).</summary>
        public IRelayCommand CancelRemapCommand { get; }

        /// <summary>Drops the selected key's remap (specs/10-apps-and-ui.md, "Reset Key").</summary>
        public IRelayCommand ResetKeyCommand { get; }

        /// <summary>Resets every key of the shown layer.</summary>
        public IRelayCommand ResetLayerCommand { get; }

        /// <summary>Resets every key of every layer.</summary>
        public IRelayCommand ResetLayoutCommand { get; }

        /// <summary>Writes the profile back to the v-Drive; never available in demo mode (03 §3.5).</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// Opens the Assign Tap and Hold Action panel for the selected key (11 §11.1), after the
        /// firmware gate and the four pre-dialog checks; a refusal from either is shown instead.
        /// </summary>
        public IAsyncRelayCommand TapAndHoldCommand { get; }

        /// <summary>Opens the Macro Timing Delays panel and inserts its delay into the macro (11 §11.3).</summary>
        public IAsyncRelayCommand InsertDelayCommand { get; }

        /// <summary>Opens Search Keys over the macro and inserts the picked action (11 §11.6).</summary>
        public IRelayCommand InsertSpecialActionCommand { get; }

        /// <summary>Opens the Export files panel (11 §11.5); never available in demo mode (03 §3.5).</summary>
        public IRelayCommand ExportCommand { get; }

        /// <summary>
        /// Imports an external <c>.txt</c> over this profile's layout or lighting
        /// (specs/10-apps-and-ui.md, 07 §1.4); never available in demo mode (03 §3.5).
        /// </summary>
        public IAsyncRelayCommand ImportCommand { get; }

        /// <summary>Dismisses the open feature panel; the overlay's own Cancel path runs first.</summary>
        public IRelayCommand CloseOverlayCommand { get; }

        private readonly IProfileSessionFactory _profileSessions;
        private readonly IKeystrokeCaptureService _capture;
        private readonly INotificationService _notifications;
        private readonly IFolderPickerService _folderPicker;
        private readonly IVDriveFileService _files;
        private readonly IUrlLauncher _urlLauncher;
        private readonly ProfileImporter _importer;
        private readonly EditorOverlayHost _overlays;
        private readonly KeyboardVisual? _visual;
        private readonly Action<CapturedKeystroke> _keystrokeCapturedHandler;
        private readonly Action<SearchKeysOverlayViewModel> _searchRequestedHandler;
        private readonly EventHandler _activeOverlayChangedHandler;
        private readonly EventHandler _tapAndHoldClosedHandler;
        private readonly EventHandler _macroRecordingChangedHandler;
        private readonly EventHandler _macrosChangedHandler;
        private readonly PropertyChangedEventHandler _macroPanelPropertyChangedHandler;
        private IProfileSession? _session;
        private TapAndHoldOverlayViewModel? _tapAndHoldOverlay;
        private KeyboardKeyViewModel? _tapAndHoldKey;
        private IReadOnlyList<KeyboardLayerViewModel> _layers = [];
        private IReadOnlyList<string> _invalidLineMessages = [];
        private KeyboardLayerViewModel? _selectedLayer;
        private KeyboardKeyViewModel? _selectedKey;
        private KeyboardKeyViewModel? _listeningKey;
        private KeyboardLayout? _layout;
        private MacroPanelViewModel? _macroPanel;
        private EditorTab _selectedTab = EditorTab.Keys;
        private string _profileCaption = string.Empty;
        private int _modifiedKeyCount;
        private int _macroCount;
        private bool _isLoading = true;
        private bool _isBusy;
        private bool _hasLoadStarted;
        private bool _isDisposed;

        /// <summary>
        /// Creates the editor for <paramref name="device"/>. Construction is deliberately cheap —
        /// no file is touched here — so the shell can swap the view in immediately and let
        /// <see cref="LoadAsync"/> do the reading.
        /// </summary>
        public KeyboardEditorViewModel(
            DeviceSnapshot device,
            IProfileSessionFactory profileSessions,
            IKeystrokeCaptureService capture,
            INotificationService notifications,
            IFolderPickerService folderPicker,
            IFilePickerService filePicker,
            IVDriveFileService files,
            IUrlLauncher urlLauncher) : base(device)
        {
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _importer = new ProfileImporter(filePicker);
            _overlays = new EditorOverlayHost(_capture);

            // The board picture belongs to the device, not to the profile, so it is resolved once
            // and shared by every layer (docs/app/domain-data.md, "Visual geometry").
            _visual = VisualCatalog.TryGet(device.DeviceId, out var visual) ? visual : null;

            Tabs = EditorTabViewModel.CreateAll();

            SelectTabCommand = new RelayCommand<EditorTabViewModel>(OnSelectTab, tab => tab?.IsEnabled == true);
            SelectLayerCommand = new RelayCommand<KeyboardLayerViewModel>(SelectLayer);
            SelectKeyCommand = new RelayCommand<KeyboardKeyViewModel>(SelectKey);
            BeginRemapCommand = new RelayCommand(BeginRemap, () => CanBeginRemap());
            CancelRemapCommand = new RelayCommand(CancelRemap, () => IsListening);
            // The !IsLoading && !IsBusy guard matches CanBeginRemap/CanSave: a save serializes the
            // model on a background thread, so mutating it from here mid-save would race it.
            ResetKeyCommand = new RelayCommand(ResetKey, () => SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy);
            ResetLayerCommand = new RelayCommand(ResetLayer, () => SelectedLayer is not null && !IsLoading && !IsBusy);
            ResetLayoutCommand = new RelayCommand(ResetLayout, () => Layout is not null && !IsLoading && !IsBusy);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave());
            TapAndHoldCommand = new AsyncRelayCommand(OpenTapAndHoldAsync, () => CanOpenTapAndHold());
            InsertDelayCommand = new AsyncRelayCommand(InsertDelayAsync, () => CanInsertIntoMacro());
            InsertSpecialActionCommand = new RelayCommand(InsertSpecialAction, () => CanInsertIntoMacro());
            ExportCommand = new RelayCommand(OpenExport, () => CanExport());
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => CanImport());
            CloseOverlayCommand = new RelayCommand(_overlays.Dismiss, () => ActiveOverlay is not null);

            SelectTab(EditorTab.Keys);

            _activeOverlayChangedHandler = (_, _) => OnActiveOverlayChanged();
            _tapAndHoldClosedHandler = (_, _) => OnTapAndHoldClosed();
            _searchRequestedHandler = OnSearchRequested;
            _macroRecordingChangedHandler = (_, _) => OnMacroRecordingChanged();
            _macrosChangedHandler = (_, _) => RefreshCounters();
            _macroPanelPropertyChangedHandler = (_, e) => OnMacroPanelPropertyChanged(e);

            _overlays.ActiveChanged += _activeOverlayChangedHandler;

            _keystrokeCapturedHandler = OnKeystrokeCaptured;
            _capture.KeystrokeCaptured += _keystrokeCapturedHandler;
        }

        /// <summary>
        /// Reads the profile off the UI thread and fills the picture in. Total: a drive that
        /// vanished, an unreadable file, an unsupported profile — every failure is reported through
        /// the notification service and degrades to the factory-default layout with saving
        /// disabled, because the shell fires this and forgets it. A second call is a no-op.
        /// </summary>
        public override async Task LoadAsync()
        {
            if (_isDisposed || _hasLoadStarted)
            {
                return;
            }

            _hasLoadStarted = true;
            IsLoading = true;

            LoadOutcome outcome;

            try
            {
                outcome = await Task.Run(LoadProfile).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                outcome = new LoadOutcome { Error = exception };
            }

            try
            {
                Apply(outcome);
            }
            finally
            {
                IsLoading = false;
            }

            if (outcome.Error is not null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = LoadFailureTitle,
                    Message = LoadFailureMessagePrefix + outcome.Error.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Opens <paramref name="overlay"/> over the editor. Which command opens which panel is
        /// the feature's own business; the hosting itself — the swap, the capture suspension a
        /// text-entry panel needs, and the teardown on
        /// <see cref="EditorOverlayViewModel.Closed"/> — is <see cref="EditorOverlayHost"/>'s.
        /// A listening key <b>and any macro recording</b> are ended first: an inline panel is
        /// modal, and either of those would otherwise keep eating the keystrokes the panel is
        /// there for — spec 10 routes a captured key to the Tap and Hold dialog "if that dialog is
        /// open", not "if a field is armed".
        /// </summary>
        public void ShowOverlay(EditorOverlayViewModel overlay)
        {
            ArgumentNullException.ThrowIfNull(overlay);

            if (_isDisposed)
            {
                return;
            }

            // Reaching the host directly ends whatever the editor's own feature commands had
            // going: the panel opened here becomes the entire overlay state, so a half-finished
            // Tap and Hold can never write back into an editor that has moved on.
            DetachTapAndHold();
            CancelRemap();

            // A recording underneath owns the capture service and would swallow every key aimed at
            // the panel, Escape included; stopping it hands the service back before the panel asks
            // the host for it.
            _macroPanel?.StopRecording();

            _overlays.Show(overlay);
        }

        private LoadOutcome LoadProfile()
        {
            try
            {
                // Demo mode never touches the drive (03 §3.5), and a device with no location has
                // no file to read either: both edit a factory-default model in memory.
                if (IsDemoMode || Device.Location is null)
                {
                    return new LoadOutcome { Layout = KeyboardLayout.Create(Device.DeviceId) };
                }

                var session = _profileSessions.Load(
                    Device.Location,
                    Device.DeviceId,
                    Device.Device.LayoutScheme.FirstProfileNumber);

                return new LoadOutcome { Session = session, Layout = session.Layout };
            }
            catch (Exception exception)
            {
                return new LoadOutcome { Layout = TryCreateFallbackLayout(), Error = exception };
            }
        }

        private KeyboardLayout? TryCreateFallbackLayout()
        {
            try
            {
                return KeyboardLayout.Create(Device.DeviceId);
            }
            catch (Exception)
            {
                // A device with no geometry has nothing to fall back to; the editor then shows the
                // failure and an empty picture rather than crashing the shell.
                return null;
            }
        }

        private void Apply(LoadOutcome outcome)
        {
            _session = outcome.Session;
            Layout = outcome.Layout;
            ProfileCaption = outcome.Session is null ? string.Empty : BuildProfileCaption(outcome.Session.ProfileNumber);
            InvalidLineMessages = BuildInvalidLineMessages(outcome.Session);

            Layers = outcome.Layout is not null && _visual is not null
                ? KeyboardLayerViewModel.BuildAll(outcome.Layout, _visual, outcome.Session?.Lighting)
                : [];

            AttachMacroPanel(outcome.Layout);

            SelectLayer(Layers.Count > 0 ? Layers[0] : null);
            RefreshCounters();
        }

        private void AttachMacroPanel(KeyboardLayout? layout)
        {
            DetachMacroPanel();

            if (layout is null)
            {
                return;
            }

            var panel = new MacroPanelViewModel(Device, layout);

            panel.RecordingChanged += _macroRecordingChangedHandler;
            panel.MacrosChanged += _macrosChangedHandler;
            panel.PropertyChanged += _macroPanelPropertyChangedHandler;

            MacroPanel = panel;
        }

        private void DetachMacroPanel()
        {
            if (_macroPanel is null)
            {
                return;
            }

            _macroPanel.StopRecording();
            _macroPanel.RecordingChanged -= _macroRecordingChangedHandler;
            _macroPanel.MacrosChanged -= _macrosChangedHandler;
            _macroPanel.PropertyChanged -= _macroPanelPropertyChangedHandler;

            MacroPanel = null;
        }

        /// <summary>
        /// Which macro the panel has open decides whether the two insertion commands of §11.3 and
        /// §11.6 are available, and the panel moves it on its own — a slot click, a list-row
        /// click — not only when the editor changes the trigger.
        /// </summary>
        private void OnMacroPanelPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MacroPanelViewModel.EditedMacro))
            {
                NotifyCommands();
            }
        }

        /// <summary>
        /// Capture belongs to the editor, not to the panel (docs/app/keyboard-editor.md,
        /// invariant 4), so the panel only announces that it started or stopped recording and the
        /// editor turns the service on and off around it. A key cannot be listening for a remap
        /// while a macro records — the two consume the same keystrokes — so entering recording
        /// cancels the listen.
        /// </summary>
        private void OnMacroRecordingChanged()
        {
            if (_macroPanel is null)
            {
                return;
            }

            if (_macroPanel.IsRecording)
            {
                CancelRemap();

                _capture.Start();
            }
            else
            {
                _capture.Stop();
            }

            NotifyCommands();
        }

        private static IReadOnlyList<string> BuildInvalidLineMessages(IProfileSession? session)
        {
            if (session is null || session.InvalidLines.Count == 0)
            {
                return [];
            }

            var messages = new List<string>(session.InvalidLines.Count);

            foreach (var line in session.InvalidLines)
            {
                messages.Add(BuildInvalidLineMessage(line));
            }

            return messages;
        }

        private void SelectTab(EditorTab tab)
        {
            // A tab with nothing behind it stays shut whichever way it is asked for, so a two-way
            // binding cannot open what the command refuses.
            if (FindTab(tab) is { IsEnabled: false })
            {
                return;
            }

            // Recording belongs to the macro panel; leaving it must not keep swallowing keys.
            if (tab != EditorTab.Macros)
            {
                _macroPanel?.StopRecording();
            }

            // The property name is passed explicitly: the caller-member default would name this
            // method rather than the property the view is bound to.
            SetProperty(ref _selectedTab, tab, nameof(SelectedTab));

            OnPropertyChanged(nameof(IsMacroPanelVisible));

            foreach (var entry in Tabs)
            {
                entry.IsSelected = entry.Tab == _selectedTab;
            }

            // The two macro-insertion commands are only available while the macro panel is on
            // screen, so switching sections has to re-evaluate them.
            NotifyCommands();
        }

        private EditorTabViewModel? FindTab(EditorTab tab)
        {
            foreach (var entry in Tabs)
            {
                if (entry.Tab == tab)
                {
                    return entry;
                }
            }

            return null;
        }

        private void OnSelectTab(EditorTabViewModel? tab)
        {
            if (tab is null || !tab.IsEnabled)
            {
                return;
            }

            SelectTab(tab.Tab);
        }

        /// <summary>
        /// Switches layers. Anything half-done belongs to the layer it was started on — a key
        /// listening for its new assignment most of all — so the switch cancels it and the whole
        /// key collection is swapped.
        /// </summary>
        private void SelectLayer(KeyboardLayerViewModel? layer)
        {
            CancelRemap();
            _macroPanel?.StopRecording();
            ClearSelectedKey();

            foreach (var entry in Layers)
            {
                entry.IsSelected = ReferenceEquals(entry, layer);
            }

            SelectedLayer = layer;

            UpdateMacroTrigger();
        }

        /// <summary>Both tabs share one board and one selection: the selected key is the trigger.</summary>
        private void UpdateMacroTrigger()
        {
            _macroPanel?.SetTrigger(SelectedKey, SelectedLayer?.Layer);
        }

        /// <summary>
        /// The click contract of specs/10-apps-and-ui.md: the first click selects, a second click
        /// on the same key starts listening, and a click on the listening key cancels it again.
        /// Selecting a different key always cancels listening first.
        /// </summary>
        private void SelectKey(KeyboardKeyViewModel? key)
        {
            if (key is null)
            {
                CancelRemap();
                ClearSelectedKey();
                UpdateMacroTrigger();

                return;
            }

            if (ReferenceEquals(key, SelectedKey))
            {
                if (IsListening)
                {
                    CancelRemap();
                }
                else
                {
                    BeginRemap();
                }

                return;
            }

            CancelRemap();
            ClearSelectedKey();

            key.IsSelected = true;
            SelectedKey = key;

            UpdateMacroTrigger();
        }

        private void ClearSelectedKey()
        {
            if (SelectedKey is not null)
            {
                SelectedKey.IsSelected = false;
            }

            SelectedKey = null;
        }

        private bool CanBeginRemap()
        {
            // A macro recording and a listening key would fight over the same keystrokes, and an
            // open feature panel owns them outright, so neither may start a remap.
            return SelectedKey is not null
                   && SelectedKey.CanEdit
                   && !IsLoading
                   && !IsBusy
                   && _macroPanel?.IsRecording != true
                   && ActiveOverlay is null;
        }

        /// <summary>
        /// Enters listening state: from here the next captured keystroke becomes the assignment.
        /// Only one key ever listens, so an in-flight listen is cancelled first.
        /// </summary>
        private void BeginRemap()
        {
            if (!CanBeginRemap())
            {
                return;
            }

            CancelRemap();

            var key = SelectedKey!;

            key.IsListening = true;
            ListeningKey = key;

            _capture.Start();
        }

        /// <summary>Leaves listening state and stops capture; the model is untouched.</summary>
        private void CancelRemap()
        {
            if (ListeningKey is null)
            {
                return;
            }

            StopListening();
        }

        private void StopListening()
        {
            if (ListeningKey is not null)
            {
                ListeningKey.IsListening = false;
                ListeningKey = null;
            }

            // Capture is never left running: it swallows keystrokes from the rest of the app while
            // it is on (docs/app/keystroke-capture.md).
            _capture.Stop();
        }

        /// <summary>
        /// The routing rule of specs/10-apps-and-ui.md: "a captured key is forwarded to the Tap
        /// and Hold dialog <b>if that dialog is open</b>; otherwise it is applied as a remap, or
        /// appended to the active macro when the macro-entry box is focused". One keystroke reaches
        /// exactly one target, in that order — an open sink panel first, then a recording macro,
        /// then a listening key. The editor owns the single subscription, so the order lives in one
        /// place.
        /// <para>
        /// The sink takes precedence on being <em>open</em>, not on being armed: a panel with no
        /// field armed swallows the keystroke and discards it, which is what keeps anything under
        /// a modal panel from quietly consuming keys aimed at it.
        /// </para>
        /// </summary>
        private void OnKeystrokeCaptured(CapturedKeystroke keystroke)
        {
            if (keystroke is null)
            {
                return;
            }

            if (ActiveOverlay is IKeystrokeSink overlaySink)
            {
                overlaySink.ReceiveKeystroke(keystroke);

                return;
            }

            if (_macroPanel is { WantsKeystrokes: true } panel)
            {
                panel.ReceiveKeystroke(keystroke);

                return;
            }

            ApplyRemap(keystroke);
        }

        private void ApplyRemap(CapturedKeystroke keystroke)
        {
            var key = ListeningKey;

            if (key is null)
            {
                return;
            }

            // Remap(originalKey) is how "assign the key its own action" clears the remap
            // (04 §2.1) — the capture path deliberately goes through the editor path so that
            // pressing a key's own default un-does it, exactly like the legacy apps.
            key.Key.Remap(keystroke.Key);

            StopListening();

            key.RefreshFromModel();
            RefreshCounters();
        }

        /// <summary>
        /// The single place the editor reacts to the open panel changing: the two bindable
        /// projections of <see cref="EditorOverlayHost.Active"/>, and the commands an open panel
        /// stands every other action down for.
        /// </summary>
        private void OnActiveOverlayChanged()
        {
            OnPropertyChanged(nameof(ActiveOverlay));
            OnPropertyChanged(nameof(HasActiveOverlay));
            OnPropertyChanged(nameof(IsOverlayAwaitingKeystroke));

            CloseOverlayCommand.NotifyCanExecuteChanged();
            NotifyCommands();
        }

        /// <summary>
        /// A Search button of the open Tap and Hold panel (§11.1). The picker arrives fully
        /// configured — its title is the field's, and the picked action is written back into the
        /// panel by the panel itself — so the editor only has to show it over its parent.
        /// </summary>
        private void OnSearchRequested(SearchKeysOverlayViewModel search)
        {
            if (search is null || _tapAndHoldOverlay is not { } parent)
            {
                return;
            }

            _overlays.ShowNested(search, parent);
        }

        private void ShowTapAndHold(TapAndHoldOverlayViewModel overlay, KeyboardKeyViewModel key)
        {
            ShowOverlay(overlay);

            if (!ReferenceEquals(ActiveOverlay, overlay))
            {
                // Refused (disposed, or already closed): nothing is up, so nothing may be hooked.
                return;
            }

            _tapAndHoldOverlay = overlay;
            _tapAndHoldKey = key;

            overlay.SearchRequested += _searchRequestedHandler;
            overlay.Closed += _tapAndHoldClosedHandler;
        }

        /// <summary>
        /// The panel is gone: drop its hooks first — so a late event can never reach an editor
        /// that has moved on — and only then apply what an accepted assignment changed. Core
        /// announces nothing (invariant 3), so the cap has to be re-read by hand.
        /// </summary>
        private void OnTapAndHoldClosed()
        {
            var wasAccepted = _tapAndHoldOverlay?.WasAccepted == true;
            var key = _tapAndHoldKey;

            DetachTapAndHold();

            if (!wasAccepted || key is null)
            {
                return;
            }

            key.RefreshFromModel();

            RefreshCounters();
        }

        private void DetachTapAndHold()
        {
            if (_tapAndHoldOverlay is not null)
            {
                _tapAndHoldOverlay.SearchRequested -= _searchRequestedHandler;
                _tapAndHoldOverlay.Closed -= _tapAndHoldClosedHandler;
                _tapAndHoldOverlay = null;
            }

            _tapAndHoldKey = null;
        }

        /// <summary>
        /// Shows a one-shot panel whose single result is a keystroke to append to the macro under
        /// edit — the Macro Timing Delays and Search Keys panels of §11.3/§11.6. Both hooks come
        /// off again the moment the panel closes, however it closed, so a dismissed panel can
        /// never write into the macro afterwards.
        /// </summary>
        private void ShowMacroInsertOverlay(
            EditorOverlayViewModel overlay,
            Action<Action<KeyDefinition>> subscribe,
            Action<Action<KeyDefinition>> unsubscribe)
        {
            ShowOverlay(overlay);

            if (!ReferenceEquals(ActiveOverlay, overlay))
            {
                return;
            }

            // A lambda, not a method group: InsertKeystroke reports "no macro is being edited"
            // with a bool this path has nothing to do with, and the panel may be gone by then.
            var insert = new Action<KeyDefinition>(key => _macroPanel?.InsertKeystroke(key));

            EventHandler? closed = null;

            closed = (_, _) =>
            {
                unsubscribe(insert);

                overlay.Closed -= closed;
            };

            subscribe(insert);

            overlay.Closed += closed;
        }

        /// <summary>
        /// §11.1's order: the firmware gate first, then the four pre-dialog checks, then the
        /// panel. Both gates report themselves and simply refuse, so nothing here decides what a
        /// refusal says. The state is re-checked after the gate's dialog, which is awaited and
        /// during which the user may have selected another key.
        /// </summary>
        private async Task OpenTapAndHoldAsync()
        {
            if (!CanOpenTapAndHold())
            {
                return;
            }

            var key = SelectedKey!;

            var isAvailable = await TapAndHoldOverlayViewModel
                .EnsureFirmwareAvailableAsync(Device.DeviceId, Device.Firmware, _notifications, _urlLauncher)
                .ConfigureAwait(true);

            if (!isAvailable || !CanOpenTapAndHold() || !ReferenceEquals(SelectedKey, key))
            {
                return;
            }

            var result = TapAndHoldOverlayViewModel.TryCreate(Layout!, SelectedLayer!.Layer, key.Key);

            if (!result.IsAllowed)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = TapAndHoldOverlayViewModel.OverlayTitle,
                    Message = result.RefusalMessage!,
                    Icon = MessageBoxIcon.Warning
                }).ConfigureAwait(true);

                return;
            }

            ShowTapAndHold(result.Overlay!, key);
        }

        /// <summary>
        /// §11.1's own precondition, ahead of the firmware gate and the four pre-dialog checks:
        /// the device has to <em>have</em> the feature. Without this guard a board that does not
        /// (<see cref="Core.Devices.TapAndHoldCapability.IsSupported"/> false — it also states no
        /// delay range, so the panel would open at 0 ms) could be given an assignment that
        /// <see cref="KeyboardLayout.Validate"/> then reports as
        /// <see cref="ModelViolationKind.TapAndHoldNotSupported"/>, blocking the whole save.
        /// </summary>
        private bool CanOpenTapAndHold()
        {
            return Layout is { } layout
                   && layout.Device.TapAndHold.IsSupported
                   && SelectedLayer is not null
                   && SelectedKey is not null
                   && SelectedKey.CanEdit
                   && !IsLoading
                   && !IsBusy
                   && ActiveOverlay is null;
        }

        private async Task InsertDelayAsync()
        {
            if (!CanInsertIntoMacro())
            {
                return;
            }

            var isAvailable = await MacroDelayOverlayViewModel
                .EnsureFirmwareAvailableAsync(Device.DeviceId, Device.Firmware, _notifications, _urlLauncher)
                .ConfigureAwait(true);

            if (!isAvailable || !CanInsertIntoMacro())
            {
                return;
            }

            var overlay = new MacroDelayOverlayViewModel(Layout!.Dialect);

            ShowMacroInsertOverlay(
                overlay,
                handler => overlay.Accepted += handler,
                handler => overlay.Accepted -= handler);
        }

        private void InsertSpecialAction()
        {
            if (!CanInsertIntoMacro())
            {
                return;
            }

            var overlay = new SearchKeysOverlayViewModel(SearchKeysOverlayViewModel.MacroTitle, Layout!.Dialect);

            ShowMacroInsertOverlay(
                overlay,
                handler => overlay.Selected += handler,
                handler => overlay.Selected -= handler);
        }

        /// <summary>
        /// Both insertion panels target the macro the macro panel currently has open, so two
        /// things have to be true: there is one — <see cref="MacroPanelViewModel.EditedMacro"/> is
        /// null on a device without macros, with nothing selected, and on a position that cannot
        /// carry one (05 §5.3) — and that panel is <b>on screen</b>. Selecting any macro-capable
        /// key opens an unassigned draft, so without the second test the two buttons stay live on
        /// the Keys tab and the picked token is appended to a macro the user cannot see and never
        /// assigns.
        /// </summary>
        private bool CanInsertIntoMacro()
        {
            return Layout is not null
                   && IsMacroPanelVisible
                   && _macroPanel?.EditedMacro is not null
                   && !IsLoading
                   && !IsBusy
                   && ActiveOverlay is null;
        }

        private void OpenExport()
        {
            if (!CanExport())
            {
                return;
            }

            ShowOverlay(new ExportOverlayViewModel(_session, _folderPicker, _files, _notifications));
        }

        private bool CanExport()
        {
            // Demo mode reads no profile at all, so there is nothing to serialize (03 §3.5).
            return _session is not null && !IsDemoMode && !IsLoading && !IsBusy && ActiveOverlay is null;
        }

        /// <summary>
        /// Runs the import and re-renders whatever it replaced. The refresh goes through the same
        /// <see cref="Apply"/> the load uses — the imported file built a brand-new model, exactly
        /// as a load would have — so layers, the board, the macro panel, the counters and the
        /// invalid-line list all come from one place.
        /// </summary>
        private async Task ImportAsync()
        {
            var session = _session;

            if (session is null || !CanImport())
            {
                return;
            }

            CancelRemap();
            _macroPanel?.StopRecording();

            var outcome = await _importer.ImportAsync(session, Device.DeviceId).ConfigureAwait(true);

            if (_isDisposed)
            {
                return;
            }

            if (outcome.WasApplied)
            {
                Apply(new LoadOutcome { Session = session, Layout = session.Layout });
            }

            if (outcome.FailureMessage is not null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = ProfileImporter.DialogTitle,
                    Message = outcome.FailureMessage,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return;
            }

            if (outcome.SuccessMessage is not null)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Title = ProfileImporter.DialogTitle,
                    Message = outcome.SuccessMessage
                });
            }
        }

        private bool CanImport()
        {
            // Demo mode holds no session to import into (03 §3.5), and the Advantage 360's
            // factory profile disables Import with Save (specs/02-devices.md, "Profiles 0-9") —
            // which is exactly what CanSave answers.
            return _session is { CanSave: true } && !IsDemoMode && !IsLoading && !IsBusy && ActiveOverlay is null;
        }

        /// <summary>
        /// Drops the selected key's remap. This is <see cref="KeyboardKey.ClearRemap"/> and not
        /// <c>Remap(OriginalKey)</c> on purpose: the latter also clears the position's tap-and-hold
        /// and multi-modifier assignment as a side effect (docs/app/keyboard-model.md,
        /// "Watch out"), which is not what "reset this key's remap" means.
        /// </summary>
        private void ResetKey()
        {
            var key = SelectedKey;

            if (key is null || !key.CanEdit)
            {
                return;
            }

            CancelRemap();

            key.Key.ClearRemap();
            key.RefreshFromModel();

            RefreshCounters();
        }

        private void RefreshCounters()
        {
            ModifiedKeyCount = Layout?.ModifiedKeyCount ?? 0;
            MacroCount = Layout?.MacroCount ?? 0;
        }

        private void ResetLayer()
        {
            var layer = SelectedLayer;

            if (layer is null)
            {
                return;
            }

            CancelRemap();
            _macroPanel?.StopRecording();

            layer.Layer.Reset();
            layer.RefreshFromModel();

            // KeyboardLayer.Reset clears every rule including the macro slots, so the panel is
            // sitting on macros that no longer exist.
            _macroPanel?.RefreshFromModel();

            RefreshCounters();
        }

        private void ResetLayout()
        {
            var layout = Layout;

            if (layout is null)
            {
                return;
            }

            CancelRemap();
            _macroPanel?.StopRecording();

            layout.Reset();

            foreach (var layer in Layers)
            {
                layer.RefreshFromModel();
            }

            _macroPanel?.RefreshFromModel();

            RefreshCounters();
        }

        private bool CanSave()
        {
            // Demo mode never writes (03 §3.5), and the Advantage 360's factory profile is
            // read-only (CanSave on the session).
            return _session is not null && _session.CanSave && !IsDemoMode && !IsLoading && !IsBusy;
        }

        /// <summary>
        /// Runs the save sequence off the UI thread between the loading indicator's show and hide,
        /// then reports its outcome: the violations that stopped it (04 §5.3), or the device's
        /// post-save refresh wording as a toast.
        /// </summary>
        private async Task SaveAsync()
        {
            var session = _session;

            if (session is null || !CanSave())
            {
                return;
            }

            CancelRemap();
            _macroPanel?.StopRecording();

            ProfileSaveResult? result = null;
            Exception? error = null;

            IsBusy = true;
            _notifications.ShowLoading(SavingCaption);

            try
            {
                result = await Task.Run(session.Save).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                _notifications.HideLoading();

                IsBusy = false;
            }

            if (error is not null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = SaveTitle,
                    Message = SaveErrorMessagePrefix + error.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return;
            }

            if (!result!.Success)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = SaveTitle,
                    Message = BuildViolationMessage(result.Violations),
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return;
            }

            if (result.PostSaveMessage is not null)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Title = SaveTitle,
                    Message = result.PostSaveMessage
                });
            }
        }

        private static string BuildViolationMessage(IReadOnlyList<ModelViolation> violations)
        {
            var lines = new List<string>(violations.Count + 1) { SaveRejectedMessage };

            foreach (var violation in violations)
            {
                lines.Add(violation.Message);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private async Task TryShowMessageBoxAsync(MessageBoxRequest request)
        {
            try
            {
                await _notifications.ShowMessageBoxAsync(request).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // A box that cannot be put on screen (the window is already gone) must not bring
                // the app down; the editor state already carries the outcome.
            }
        }

        private void NotifyCommands()
        {
            BeginRemapCommand.NotifyCanExecuteChanged();
            CancelRemapCommand.NotifyCanExecuteChanged();
            ResetKeyCommand.NotifyCanExecuteChanged();
            ResetLayerCommand.NotifyCanExecuteChanged();
            ResetLayoutCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            TapAndHoldCommand.NotifyCanExecuteChanged();
            InsertDelayCommand.NotifyCanExecuteChanged();
            InsertSpecialActionCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            ImportCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Stops capture and detaches from it, and tears down every overlay and macro-panel
        /// subscription with it. The capture service is app-wide and outlives the editor, so
        /// leaving it started would swallow every keystroke of the dashboard behind us. Safe to
        /// call multiple times.
        /// <para>
        /// <see cref="EditorOverlayHost.Close"/> <em>cancels</em> the open panel rather than
        /// merely dropping it: the one-shot hooks of <see cref="ShowMacroInsertOverlay"/> come off
        /// on the panel's own <see cref="EditorOverlayViewModel.Closed"/>, so cancelling is what
        /// runs them. The Tap and Hold hooks are dropped first, which is why a half-finished
        /// assignment cannot write back into a disposed editor on the way out.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _capture.KeystrokeCaptured -= _keystrokeCapturedHandler;

            DetachTapAndHold();

            _overlays.Close();
            _overlays.ActiveChanged -= _activeOverlayChangedHandler;

            DetachMacroPanel();
            StopListening();
        }

        /// <summary>What one load attempt produced: a session (or none, in demo mode), a model, and a failure.</summary>
        private sealed record LoadOutcome
        {
            public IProfileSession? Session { get; init; }

            public KeyboardLayout? Layout { get; init; }

            public Exception? Error { get; init; }
        }
    }
}
