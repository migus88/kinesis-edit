using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
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

        /// <summary>Builds the "Remap (n)" counter of specs/10-apps-and-ui.md.</summary>
        public static string BuildRemapCounterCaption(int modifiedKeyCount)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Remap ({modifiedKeyCount})");
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
        /// The editor's sections, filtered by what the device carries
        /// (<see cref="EditorTabViewModel.CreateAll"/>). Macros is still a disabled placeholder
        /// (issue #15).
        /// </summary>
        public IReadOnlyList<EditorTabViewModel> Tabs { get; }

        /// <summary>
        /// The Settings tab's panel (docs/app/settings.md). Always built — it is cheap and reads
        /// nothing on its own — but only reachable when <see cref="Tabs"/> carries
        /// <see cref="EditorTab.Settings"/>, i.e. when the device has an app-managed settings file.
        /// </summary>
        public KeyboardSettingsViewModel Settings { get; }

        /// <summary>
        /// The Lighting tab's panel (docs/app/lighting.md). Always built, like
        /// <see cref="Settings"/>, and pointed at the profile's lighting model by
        /// <see cref="LoadAsync"/>; only reachable on a device
        /// <see cref="LightingTabViewModel.IsSupported"/> accepts.
        /// </summary>
        public LightingTabViewModel Lighting { get; }

        /// <summary>The open section.</summary>
        public EditorTab SelectedTab
        {
            get => _selectedTab;
            set => SelectTab(value);
        }

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

        private readonly IProfileSessionFactory _profileSessions;
        private readonly IKeystrokeCaptureService _capture;
        private readonly INotificationService _notifications;
        private readonly KeyboardVisual? _visual;
        private readonly Action<CapturedKeystroke> _keystrokeCapturedHandler;
        private IProfileSession? _session;
        private IReadOnlyList<KeyboardLayerViewModel> _layers = [];
        private IReadOnlyList<string> _invalidLineMessages = [];
        private KeyboardLayerViewModel? _selectedLayer;
        private KeyboardKeyViewModel? _selectedKey;
        private KeyboardKeyViewModel? _listeningKey;
        private KeyboardLayout? _layout;
        private EditorTab _selectedTab = EditorTab.Keys;
        private string _profileCaption = string.Empty;
        private int _modifiedKeyCount;
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
            ISettingsService settings,
            IKeystrokeCaptureService capture,
            INotificationService notifications) : base(device)
        {
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

            // The board picture belongs to the device, not to the profile, so it is resolved once
            // and shared by every layer (docs/app/domain-data.md, "Visual geometry").
            _visual = VisualCatalog.TryGet(device.DeviceId, out var visual) ? visual : null;

            Settings = new KeyboardSettingsViewModel(device, settings, notifications);
            Lighting = new LightingTabViewModel(device, settings, notifications);

            // The Lighting tab is enabled for the boards whose led file is the two-layer key
            // backlight model the panel edits; the tab is already absent on a device without
            // lighting hardware. The question is device-level on purpose: this constructor runs
            // before any profile has been read, and demo mode never reads one at all.
            Tabs = EditorTabViewModel.CreateAll(device.Device, Lighting.IsAvailable);

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

            SelectTab(EditorTab.Keys);

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

            // The Settings tab reads its own file, and reports its own failures inline, so a
            // settings read can never stop the picture from appearing. The Lighting tab only
            // reads the picker's stored swatches; its model came with the profile above.
            await Settings.LoadAsync().ConfigureAwait(true);
            await Lighting.LoadAsync().ConfigureAwait(true);

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

            SelectLayer(Layers.Count > 0 ? Layers[0] : null);
            RefreshCounters();

            // The lighting panel edits the very model the session hands out, so mutating it is
            // all a lighting save takes (ProfileSession.Save writes led<n>.txt whenever Lighting
            // is non-null). It shares these layer view models, which is how a recoloured key
            // repaints on the Keys tab too.
            Lighting.Attach(outcome.Session?.Lighting, Layers);
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
            // A tab with nothing behind it — disabled, or absent because the device does not carry
            // that section at all — stays shut whichever way it is asked for, so a two-way binding
            // cannot open what the command refuses.
            if (FindTab(tab) is not { IsEnabled: true })
            {
                return;
            }

            // Listening belongs to the keyboard picture: leaving the tab must stop capture, or the
            // app-wide service keeps swallowing keystrokes behind the section the user moved to.
            CancelRemap();

            // The property name is passed explicitly: the caller-member default would name this
            // method rather than the property the view is bound to.
            SetProperty(ref _selectedTab, tab, nameof(SelectedTab));

            foreach (var entry in Tabs)
            {
                entry.IsSelected = entry.Tab == _selectedTab;
            }
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
            ClearSelectedKey();

            foreach (var entry in Layers)
            {
                entry.IsSelected = ReferenceEquals(entry, layer);
            }

            SelectedLayer = layer;
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
            return SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy;
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

        private void OnKeystrokeCaptured(CapturedKeystroke keystroke)
        {
            var key = ListeningKey;

            if (key is null || keystroke is null)
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

        private void ResetLayer()
        {
            var layer = SelectedLayer;

            if (layer is null)
            {
                return;
            }

            CancelRemap();

            layer.Layer.Reset();
            layer.RefreshFromModel();

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

            layout.Reset();

            foreach (var layer in Layers)
            {
                layer.RefreshFromModel();
            }

            RefreshCounters();
        }

        private void RefreshCounters()
        {
            ModifiedKeyCount = Layout?.ModifiedKeyCount ?? 0;
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
            // SelectTabCommand is deliberately absent: a tab's IsEnabled is fixed at construction
            // (EditorTabViewModel builds the strip from device-level facts only, including the
            // lighting one) and the command's predicate reads nothing but its parameter, so there
            // is no state here that could change its answer.
            BeginRemapCommand.NotifyCanExecuteChanged();
            CancelRemapCommand.NotifyCanExecuteChanged();
            ResetKeyCommand.NotifyCanExecuteChanged();
            ResetLayerCommand.NotifyCanExecuteChanged();
            ResetLayoutCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Stops capture and detaches from it. The capture service is app-wide and outlives the
        /// editor, so leaving it started would swallow every keystroke of the dashboard behind us.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _capture.KeystrokeCaptured -= _keystrokeCapturedHandler;

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
