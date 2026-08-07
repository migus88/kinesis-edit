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
using KinesisEdit.ViewModels.Advisories;

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
    public sealed partial class KeyboardEditorViewModel : DeviceEditorViewModel, IDisposable
    {
        /// <summary>Prefix of the profile caption; the loaded profile number follows it.</summary>
        public const string ProfileCaptionPrefix = "Profile ";

        /// <summary>Caption of the loading indicator during a save. Not a spec string.</summary>
        public const string SavingCaption = "Saving…";

        /// <summary>
        /// The Demo Mode bar's copy, verbatim from mockup 1f. Purple, never amber: demo mode is its
        /// own state in the four-status vocabulary and amber is reserved for advisories.
        /// </summary>
        public const string DemoModeBarMessage =
            "Demo Mode — no keyboard attached. Nothing you change here is written anywhere.";

        /// <summary>
        /// The Demo Mode bar's one action (mockup 1f), verbatim; it runs the shell's Home. The
        /// mockup's other action — <c>Export layout to file…</c> — is deliberately not rendered:
        /// demo mode opens no session, so <c>CanExport</c> is false for exactly as long as the bar
        /// is on screen, and a control that can never become live is not shown at all.
        /// </summary>
        public const string DemoModeConnectCaption = "Connect a device";

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

        /// <summary>Title of the confirmation raised before a layer is erased. Not a spec string.</summary>
        public const string ResetLayerTitle = "Reset Layer";

        /// <summary>
        /// The layer-reset prompt. It says what is erased and — because nothing this app does is
        /// written behind the user's back — that the drive is untouched until Save.
        /// </summary>
        public const string ResetLayerConfirmation =
            "Do you want to clear every remap and macro on this layer? Nothing is written to the keyboard until you save.";

        /// <summary>The affirmative, named after what it does rather than "Yes" (mockup 1k). It still answers <c>Yes</c>.</summary>
        public const string ResetLayerConfirmCaption = "Clear layer";

        /// <summary>Title of the confirmation raised before every layer is erased.</summary>
        public const string ResetLayoutTitle = "Reset Layout";

        /// <summary>
        /// The whole-profile prompt. Same shape as the layer's, and deliberately different words:
        /// the two scopes share one suppression key, so the sentence is the only thing that tells
        /// the user which of them is about to run.
        /// </summary>
        public const string ResetLayoutConfirmation =
            "Do you want to clear every remap and macro on every layer of this profile? Nothing is written to the keyboard until you save.";

        /// <summary>The affirmative of the whole-profile prompt. It still answers <c>Yes</c>.</summary>
        public const string ResetLayoutConfirmCaption = "Clear all layers";

        /// <summary>The way out of either reset prompt. It still answers <c>No</c>.</summary>
        public const string ResetDeclineCaption = "Cancel";

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

        /// <summary>
        /// This editor draws the whole 46 px bar itself, so the shell hides its own
        /// (<see cref="MainWindowViewModel.IsShellChromeVisible"/>).
        /// </summary>
        public override bool ProvidesOwnChrome => true;

        /// <summary>
        /// The open drive's mount path, shown in mono beside the device name because it is a value
        /// that exists verbatim on the machine. Empty in demo mode and on a device with no
        /// location, which is what <see cref="HasMountPath"/> hides.
        /// </summary>
        public string MountPath => Device.Location?.RootPath ?? string.Empty;

        /// <summary>Whether there is a mount path worth printing.</summary>
        public bool HasMountPath => !IsDemoMode && MountPath.Length > 0;

        /// <summary>
        /// Whether the open profile re-serializes to something different from what was loaded —
        /// the toolbar's Save turns amber for exactly this ("any edit marks the session dirty →
        /// Save turns amber", docs/design/handoff.md § Interactions).
        /// <para>
        /// It is a <b>pull</b> from <see cref="IProfileSession.IsDirty"/>, which compares serialized
        /// lines: Core's model announces nothing, so nothing pushes a notification when a key, a
        /// macro or a lighting state is mutated. <see cref="RefreshDirtyState"/> is therefore called
        /// from every path that can move the layout or the lighting model — see its remarks for the
        /// list, which is the single most fragile thing about this property: a path that forgets it
        /// leaves Save grey while the user has unsaved work.
        /// </para>
        /// <para>
        /// Always false in demo mode and after a load that produced no session: there is nothing to
        /// write, and Save is unavailable there anyway (03 §3.5).
        /// </para>
        /// </summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
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

        /// <summary>
        /// What the app has noticed about the open profile and is reporting without having changed
        /// anything (<see cref="EditorAdvisories"/>). Rebuilt by <see cref="RefreshCounters"/>, so
        /// every path that can move the layout ends in a fresh set; replaced whole rather than
        /// mutated, because Core announces nothing.
        /// <para>
        /// <b>Nothing here gates anything.</b> No command's <c>CanExecute</c> reads it, a save with
        /// advisories succeeds, and an over-budget layout is written as it stands — the board
        /// truncates. That is the design law ("advisories never block"), and it is the reason this
        /// is a read-out and not a validator.
        /// </para>
        /// </summary>
        public EditorAdvisories Advisories
        {
            get => _advisories;
            private set => SetProperty(ref _advisories, value);
        }

        /// <summary>
        /// The summary strip's own view model: the <b>open section's</b> count and sentence, and
        /// the <c>Review N</c> walk. Built once here and handed the set by
        /// <see cref="RefreshAdvisorySummary"/>; <c>AdvisoryStripView</c> binds to it directly, so
        /// nothing about the strip is a property of this class.
        /// </summary>
        public AdvisoryStripViewModel AdvisoryStrip { get; }

        /// <summary>Board width in key units; the view picks a pixel scale and multiplies.</summary>
        public double BoardWidth => _visual?.Width ?? 0;

        /// <summary>Board height in key units.</summary>
        public double BoardHeight => _visual?.Height ?? 0;

        /// <summary>
        /// The editor's sections, filtered by what the device carries
        /// (<see cref="EditorTabViewModel.CreateAll"/>): Layout and Macros always, Settings where
        /// the board has a settings file, Lighting only where its led file is the model
        /// <see cref="LightingTabViewModel"/> edits. Every entry opens a working section — a
        /// feature the board lacks is not rendered at all rather than disabled.
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

        /// <summary>
        /// Whether the next physical keystroke already belongs to somebody: a key waiting for its
        /// new assignment, a macro being recorded, an armed field of a modal panel, or an armed
        /// record button in the key inspector rail. These are the consumers of "one keystroke, one
        /// target" (invariant 5), and while any of them is live the editor's keyboard grammar is
        /// <b>off entirely</b> — a user assigning ⌘S to a key must get <c>s</c>-with-Meta recorded,
        /// not a save.
        /// <para>
        /// <b>The rail's flag is not optional here.</b> The rail is not modal, so gate 2 of the
        /// view's key handler (an open overlay owns the keyboard) never fires for it — without this
        /// term ⌘S would start serializing the model while a hold action was being recorded.
        /// </para>
        /// <para>
        /// It is read on demand by <see cref="Views.KeyboardEditorView"/>'s key handler and is
        /// deliberately not observable: nothing binds it, and its sources already raise their own
        /// notifications.
        /// </para>
        /// </summary>
        public bool IsCaptureActive =>
            IsListening
            || IsOverlayAwaitingKeystroke
            || _macroPanel?.IsRecording == true
            || Inspector.IsRecording;

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

        /// <summary>Opens a section of the editor; a section this strip does not carry is refused.</summary>
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

        /// <summary>
        /// Resets every key of the shown layer, after the confirmation of
        /// <see cref="NotificationKeys.ResetLayer"/> — which the user can switch off for good on
        /// the Settings tab ("Confirm before resetting a layer", mockup 1j).
        /// </summary>
        public IRelayCommand ResetLayerCommand { get; }

        /// <summary>
        /// Resets every key of every layer, after the same confirmation under the same key; only
        /// the wording differs (see <see cref="ResetLayoutConfirmation"/>).
        /// </summary>
        public IRelayCommand ResetLayoutCommand { get; }

        /// <summary>Writes the profile back to the v-Drive; never available in demo mode (03 §3.5).</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>Opens the Macro Timing Delays panel and inserts its delay into the macro (11 §11.3).</summary>
        public IAsyncRelayCommand InsertDelayCommand { get; }

        /// <summary>Opens Search Keys over the macro and inserts the picked action (11 §11.6).</summary>
        public IRelayCommand InsertSpecialActionCommand { get; }

        /// <summary>
        /// ⌘F, the grammar's "focus the token search from anywhere in the editor"
        /// (docs/design/mockups.md <c>2b</c>).
        /// <para>
        /// It has somewhere to write now. On the Layout tab it puts the caret in the <b>key
        /// inspector's</b> own search field, where ↵ assigns the picked action to the selected
        /// position — which is what the accelerator was always meant to do, and could not before the
        /// rail existed. With a macro open on the Macros tab it <b>is</b> the insertion picker
        /// (<see cref="InsertSpecialActionCommand"/>): finding a token there means inserting it.
        /// </para>
        /// </summary>
        public IRelayCommand OpenSearchCommand { get; }

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
        private readonly IAppPreferencesStore _preferences;
        private readonly ProfileImporter _importer;
        private readonly EditorOverlayHost _overlays;
        private readonly KeyboardVisual? _visual;
        private readonly Action<CapturedKeystroke> _keystrokeCapturedHandler;
        private readonly EventHandler _activeOverlayChangedHandler;
        private readonly EventHandler _macroRecordingChangedHandler;
        private readonly EventHandler _macrosChangedHandler;
        private readonly EventHandler _lightingChangedHandler;
        private readonly PropertyChangedEventHandler _macroPanelPropertyChangedHandler;
        private IProfileSession? _session;
        private IReadOnlyList<KeyboardLayerViewModel> _layers = [];
        private IReadOnlyList<string> _invalidLineMessages = [];
        private KeyboardLayerViewModel? _selectedLayer;
        private KeyboardKeyViewModel? _selectedKey;
        private KeyboardKeyViewModel? _listeningKey;
        private KeyboardLayout? _layout;
        private MacroPanelViewModel? _macroPanel;
        private EditorAdvisories _advisories = EditorAdvisories.Empty;
        private EditorTab _selectedTab = EditorTab.Keys;
        private string _profileCaption = string.Empty;
        private int _modifiedKeyCount;
        private int _macroCount;
        private bool _isLoading = true;
        private bool _isBusy;
        private bool _isDirty;
        private bool _hasLoadStarted;
        private bool _isDisposed;

        /// <summary>
        /// Set by <see cref="OnKeystrokeCaptured"/> whenever a keystroke went to <b>any</b> sink —
        /// the open modal's, or an armed key-inspector panel's — and read-and-cleared by the view on
        /// the very key event that produced it. See <see cref="TryTakeOverlayKeystroke"/>.
        /// <para>
        /// It is latched by the <em>router</em> rather than announced by the panel, so there is one
        /// answer to "did a sink take this one" no matter which sink took it.
        /// </para>
        /// </summary>
        private bool _hasOverlayTakenKeystroke;

        /// <summary>
        /// Creates the editor for <paramref name="device"/>. Construction is deliberately cheap —
        /// no file is touched here — so the shell can swap the view in immediately and let
        /// <see cref="LoadAsync"/> do the reading.
        /// <para>
        /// <paramref name="sessions"/> is how the editor reaches this device's
        /// <c>app_settings.txt</c> (<see cref="IAppPreferencesStore"/>): the shell opens the
        /// session <i>before</i> it builds an editor, so the active session is already the one
        /// being edited. It is optional because an editor built with no session at all — most of
        /// the unit tests — must still work, and then every preference sits at its default. That
        /// is a fallback, not a shape to copy: an editor over <see cref="NullAppPreferencesStore"/>
        /// draws its Settings tab's preferences section in its read-only face, which is the face
        /// the app only shows for a board with no drive.
        /// </para>
        /// </summary>
        public KeyboardEditorViewModel(
            DeviceSnapshot device,
            IProfileSessionFactory profileSessions,
            ISettingsService settings,
            IKeystrokeCaptureService capture,
            INotificationService notifications,
            IFolderPickerService folderPicker,
            IFilePickerService filePicker,
            IVDriveFileService files,
            IUrlLauncher urlLauncher,
            IDeviceSessionAccessor? sessions = null) : base(device)
        {
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _preferences = sessions?.Active?.Preferences ?? NullAppPreferencesStore.Instance;
            _importer = new ProfileImporter(filePicker);
            _overlays = new EditorOverlayHost(_capture);

            // The board picture belongs to the device, not to the profile, so it is resolved once
            // and shared by every layer (docs/app/domain-data.md, "Visual geometry").
            _visual = VisualCatalog.TryGet(device.DeviceId, out var visual) ? visual : null;

            // The two tabs that read app_settings.txt are handed the session's store rather than
            // loading the file for themselves: one reader and one writer, or the colour picker's
            // swatches and the settings screen's preferences show each other stale state
            // (docs/app/settings.md). Neither parameter has a default, so an editor that forgot to
            // thread the store is a compile error rather than a screen that quietly reads nothing.
            Settings = new KeyboardSettingsViewModel(device, settings, notifications, _preferences, urlLauncher);
            Lighting = new LightingTabViewModel(device, notifications, _preferences);

            // The strip owns the projection and the Review walk; selecting what a note is about is
            // this class's, because the board and the macro panel are. Built before SelectTab
            // below, which projects onto it. It is handed the session's preferences because one of
            // them — `advisory_detail` — decides whether its sentence is trimmed or shown whole,
            // and it follows that store for as long as the editor is open.
            AdvisoryStrip = new AdvisoryStripViewModel(SelectAnchoredKey, SelectAnchoredMacro, _preferences);

            // The Lighting tab is rendered only for a board whose led file is the two-layer key
            // backlight model the panel edits — absent, never disabled, on every other board and on
            // one with no lighting hardware at all. The question is device-level on purpose: this
            // constructor runs before any profile has been read, and demo mode never reads one.
            Tabs = EditorTabViewModel.CreateAll(device.Device, Lighting.IsAvailable);

            SelectTabCommand = new RelayCommand<EditorTabViewModel>(OnSelectTab, tab => tab is not null);
            SelectLayerCommand = new RelayCommand<KeyboardLayerViewModel>(SelectLayer);
            SelectKeyCommand = new RelayCommand<KeyboardKeyViewModel>(SelectKey);
            BeginRemapCommand = new RelayCommand(BeginRemap, () => CanBeginRemap());
            CancelRemapCommand = new RelayCommand(CancelRemap, () => IsListening);
            // The !IsLoading && !IsBusy guard matches CanBeginRemap/CanSave: a save serializes the
            // model on a background thread, so mutating it from here mid-save would race it.
            ResetKeyCommand = new RelayCommand(ResetKey, () => SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy);
            // Both resets ask first, so both are async; Reset Key does not, because it drops one
            // position's remap and the spec's own reset confirmation covers macros, not remaps.
            ResetLayerCommand = new AsyncRelayCommand(ResetLayerAsync, () => SelectedLayer is not null && !IsLoading && !IsBusy);
            ResetLayoutCommand = new AsyncRelayCommand(ResetLayoutAsync, () => Layout is not null && !IsLoading && !IsBusy);
            CopyKeyCommand = new RelayCommand(ArmCopyKey, () => CanCopyKey());
            CancelCopyKeyCommand = new RelayCommand(CancelCopyKey, () => IsCopyArmed);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave());
            InsertDelayCommand = new AsyncRelayCommand(InsertDelayAsync, () => CanInsertIntoMacro());
            InsertSpecialActionCommand = new RelayCommand(InsertSpecialAction, () => CanInsertIntoMacro());
            OpenSearchCommand = new RelayCommand(OpenSearch, () => CanOpenSearch());
            ExportCommand = new RelayCommand(OpenExport, () => CanExport());
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => CanImport());
            CloseOverlayCommand = new RelayCommand(_overlays.Dismiss, () => ActiveOverlay is not null);

            // The legend row is a projection, not a decision: it holds the shown layer's five
            // counts and runs two of this class's commands. Built here so it exists before the
            // first SelectTab/SelectLayer, both of which refresh it.
            BoardLegend = new BoardLegendViewModel(CopyKeyCommand, ResetLayerCommand);

            // The key inspector rail and its two mode panels (KeyboardEditorViewModel.Inspector.cs).
            // Built here for the same reason: RefreshLegend pushes state into it, and the first
            // SelectLayer below already runs that.
            Inspector = CreateInspector();

            SelectTab(EditorTab.Keys);

            _activeOverlayChangedHandler = (_, _) => OnActiveOverlayChanged();
            _macroRecordingChangedHandler = (_, _) => OnMacroRecordingChanged();
            _macrosChangedHandler = (_, _) => RefreshCounters();
            _macroPanelPropertyChangedHandler = (_, e) => OnMacroPanelPropertyChanged(e);

            // A lighting edit moves no counter, but it does move the session: ProfileSession.Save
            // writes led<n>.txt from the very model the panel mutates, so the amber Save is the
            // only thing that reports it.
            _lightingChangedHandler = (_, _) => RefreshDirtyState();

            Lighting.ModelChanged += _lightingChangedHandler;

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

        /// <summary>
        /// Opens <paramref name="overlay"/> over the editor. Which command opens which panel is
        /// the feature's own business; the hosting itself — the swap, the capture suspension a
        /// text-entry panel needs, and the teardown on
        /// <see cref="EditorOverlayViewModel.Closed"/> — is <see cref="EditorOverlayHost"/>'s.
        /// A listening key, any macro recording <b>and any armed rail panel</b> are ended first: an
        /// inline panel is modal, and any of those would otherwise keep eating the keystrokes the
        /// panel is there for — spec 10 routes a captured key to a modal "if that dialog is open",
        /// not "if a field is armed".
        /// </summary>
        public void ShowOverlay(EditorOverlayViewModel overlay)
        {
            ArgumentNullException.ThrowIfNull(overlay);

            if (_isDisposed)
            {
                return;
            }

            CancelRemap();

            // The scrim swallows every click aimed at the board, so an armed copy could never be
            // finished while a panel is up.
            CancelCopyKey();

            // A recording underneath owns the capture service and would swallow every key aimed at
            // the panel, Escape included; stopping it hands the service back before the panel asks
            // the host for it. The rail is under the same scrim, so its armed record button stands
            // down for the same reason — the rail itself stays open behind the panel.
            _macroPanel?.StopRecording();

            DeactivateInspector();

            _overlays.Show(overlay);
        }

        /// <summary>
        /// Moves the key selection one step across the <b>physical</b> board — the grammar's
        /// "↑↓←→ move key selection across the physical grid, not tab order"
        /// (docs/design/mockups.md <c>2b</c>). Returns whether the selection actually moved, which
        /// is what tells the view there is a new cap to put the focus ring on.
        /// <para>
        /// The geometry is <see cref="KeyAdjacency"/>'s and lives in this class only because the
        /// board picture does: <c>_visual</c> is the editor's, never the view's. With nothing
        /// selected yet the first cap of the shown layer is where an arrow lands, so the grammar
        /// has an entry point that does not need a click first.
        /// </para>
        /// <para>
        /// It lands through <see cref="SelectKeyDirectly"/> and <b>never</b> through
        /// <see cref="SelectKeyCommand"/>: the latter promotes a second hit on the already-selected
        /// cap into listening, and arrowing onto a key must never start capture.
        /// </para>
        /// </summary>
        public bool MoveSelection(NavigationDirection direction)
        {
            if (direction is NavigationDirection.None || _visual is null || IsLoading || IsBusy)
            {
                return false;
            }

            if (SelectedLayer is not { } layer || layer.Keys.Count == 0)
            {
                return false;
            }

            if (SelectedKey is null)
            {
                SelectKeyDirectly(layer.Keys[0]);

                return true;
            }

            if (KeyAdjacency.Next(_visual, SelectedKey.Index, direction) is not { } target
                || layer.FindByIndex(target.Index) is not { } cap)
            {
                return false;
            }

            SelectKeyDirectly(cap);

            return true;
        }

        /// <summary>
        /// Whether the keystroke being handled right now was already taken by the open panel's own
        /// keystroke sink — and clears the record as it answers, so one keystroke is reported once.
        /// <para>
        /// It exists because <see cref="IsOverlayAwaitingKeystroke"/> cannot answer the question.
        /// The capture service previews the window's key events in the <b>tunnel</b> phase, above
        /// this editor's view, so by the time the view's own handler runs the sink has already
        /// received the key <em>and disarmed</em> — leaving the panel looking idle. Escape read
        /// that as "nothing is waiting" and closed the whole panel in the same keypress it was
        /// meant only to fill an armed field with.
        /// </para>
        /// <para>
        /// The view reads it on <b>every</b> key down it sees, not only on Escape, so a latch set
        /// by an ordinary keystroke cannot survive into a later Escape.
        /// </para>
        /// </summary>
        public bool TryTakeOverlayKeystroke()
        {
            var taken = _hasOverlayTakenKeystroke;

            _hasOverlayTakenKeystroke = false;

            return taken;
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

            // The lighting panel edits the very model the session hands out, so mutating it is
            // all a lighting save takes (ProfileSession.Save writes led<n>.txt whenever Lighting
            // is non-null). It shares these layer view models, so a recoloured key repaints
            // without the picture being rebuilt — on the lighting board, which is the only
            // picture that draws an LED strip (KeyboardView.ShowsLedStrips).
            Lighting.Attach(outcome.Session?.Lighting, Layers);
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
            // A section this strip does not carry stays shut whichever way it is asked for, so a
            // two-way binding cannot open what the command refuses. There is no longer a second
            // case: a tab that exists always works, because a feature the board lacks is not
            // rendered at all (EditorTabViewModel).
            if (FindTab(tab) is null)
            {
                return;
            }

            // Both consumers of the app-wide capture service are ended here, or it keeps
            // swallowing keystrokes behind the section the user moved to: listening belongs to the
            // keyboard picture, and recording to the macro panel — which the Macros tab is allowed
            // to keep, because that is the section it belongs to.
            CancelRemap();

            // An armed copy is finished with a click on the board; a section that does not draw
            // the board could never finish it, so it ends here too.
            CancelCopyKey();

            // The rail is drawn on the Layout tab only, so a record button armed in it must not go
            // on capturing behind a section that does not show it. The rail is stood down, not
            // closed: coming back to the Layout tab must find it as it was left.
            DeactivateInspector();

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

            // The strip is the *open* section's, so the sentence and the count move with the tab.
            // The set itself does not: a tab switch writes nothing.
            RefreshAdvisorySummary();

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
            if (tab is null)
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
            CancelCopyKey();
            _macroPanel?.StopRecording();
            DeactivateInspector();
            ClearSelectedKey();

            foreach (var entry in Layers)
            {
                entry.IsSelected = ReferenceEquals(entry, layer);
            }

            SelectedLayer = layer;

            // The Layout tab's strip is about the layer on screen, so it follows the switch. So is
            // the legend row: its five counts are the shown layer's, and a switch writes nothing,
            // so neither goes through RefreshCounters.
            RefreshAdvisorySummary();
            RefreshLegend();

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
        /// <para>
        /// An armed <c>Copy key…</c> takes the click ahead of all of that: while a copy is waiting
        /// for its target, the next cap clicked <em>is</em> the target and nothing else
        /// (<see cref="CompleteCopyKey"/>). Without the interception the second half of the pick
        /// would be read as the click contract's "a second hit on the selected cap" and start a
        /// remap on the very key the user meant to copy from.
        /// </para>
        /// </summary>
        private void SelectKey(KeyboardKeyViewModel? key)
        {
            // Clicking a cap is a request for the inspector, whichever branch the click then takes —
            // including a second click on the cap that is already selected, which must reopen a rail
            // the user pressed Escape on rather than only starting a remap. It is why the rail needs
            // Open() at all: Refresh alone cannot tell "the user asked again" from "somebody else's
            // edit went through the funnel".
            if (key is not null)
            {
                Inspector.Open();
            }

            if (IsCopyArmed)
            {
                if (key is not null)
                {
                    CompleteCopyKey(key);

                    return;
                }

                // A click that selects nothing is not a target; it ends the pick and then falls
                // through to the ordinary "nothing is selected" branch below.
                CancelCopyKey();
            }

            if (key is null)
            {
                CancelRemap();
                ClearSelectedKey();
                UpdateMacroTrigger();

                // Nothing is selected, so the rail has nothing to be about: it collapses and its
                // Auto column measures zero. A selection change writes nothing, so it never reaches
                // RefreshCounters — hence the explicit push here and in SelectKeyDirectly.
                RefreshInspector();

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

            SelectKeyDirectly(key);
        }

        /// <summary>
        /// Moves the selection to <paramref name="key"/> and nothing else — no second-click
        /// promotion to listening. It is what the click contract's "a different key" branch does,
        /// and what <c>Review</c> needs: reviewing an advisory must not arm the keyboard.
        /// </summary>
        private void SelectKeyDirectly(KeyboardKeyViewModel key)
        {
            if (ReferenceEquals(key, SelectedKey))
            {
                return;
            }

            CancelRemap();
            ClearSelectedKey();

            key.IsSelected = true;
            SelectedKey = key;

            UpdateMacroTrigger();

            RefreshInspector();
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

            // A listening key and an armed copy are two different answers to "what does the next
            // input mean", so only one of them is ever live. Arming the copy ends a listen for the
            // same reason (ArmCopyKey), which is what makes the Escape order below unambiguous.
            CancelCopyKey();

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
        /// A <b>modal</b> sink takes precedence on being <em>open</em>, not on being armed: a panel
        /// with no field armed swallows the keystroke and discards it, which is what keeps anything
        /// under a scrim from quietly consuming keys aimed at the panel. The <b>rail</b> is the
        /// opposite, and deliberately so — see <see cref="TryRouteToInspector"/>.
        /// </para>
        /// </summary>
        private void OnKeystrokeCaptured(CapturedKeystroke keystroke)
        {
            if (keystroke is null)
            {
                return;
            }

            // Ahead of everything, because an armed record button in the rail is the most specific
            // claim on the next keystroke there is. It is also the only branch here that tests
            // WantsKeystrokes rather than mere existence.
            if (TryRouteToInspector(keystroke))
            {
                return;
            }

            if (ActiveOverlay is IKeystrokeSink overlaySink)
            {
                // Latched for the view, which is still to see this same key event: the sink may
                // disarm as it takes the key, and "no field is armed any more" must not read as
                // "the panel was idle, close it" (see TryTakeOverlayKeystroke).
                _hasOverlayTakenKeystroke = true;

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
            // The latch belongs to the panel that set it; a swap or a dismiss ends its claim.
            _hasOverlayTakenKeystroke = false;

            OnPropertyChanged(nameof(ActiveOverlay));
            OnPropertyChanged(nameof(HasActiveOverlay));
            OnPropertyChanged(nameof(IsOverlayAwaitingKeystroke));

            CloseOverlayCommand.NotifyCanExecuteChanged();
            NotifyCommands();
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

        /// <summary>
        /// §11.6's <c>Search Keys (Macro)</c>: the same picker the key inspector hosts, wrapped in a
        /// modal because an insertion is a question with one answer that has to come back here. The
        /// session's <c>Recent</c> history is shared, so an action inserted into a macro is offered
        /// by the rail's own chip afterwards.
        /// </summary>
        private void InsertSpecialAction()
        {
            if (!CanInsertIntoMacro())
            {
                return;
            }

            var overlay = new TokenPickerOverlayViewModel(
                TokenPickerOverlayViewModel.MacroTitle,
                Layout!.Dialect,
                _recentTokens);

            ShowMacroInsertOverlay(
                overlay,
                handler => overlay.Selected += handler,
                handler => overlay.Selected -= handler);
        }

        /// <summary>
        /// ⌘F. With a macro open on the Macros tab this <em>is</em> the insertion picker, so the
        /// token the user searches for is inserted where they were working. Everywhere else it puts
        /// the caret in the <b>key inspector's</b> search field — the rail is not modal, so nothing
        /// is opened over anything: the picker is already on screen beside the board, and ↵ on a row
        /// assigns it to the selected position.
        /// <para>
        /// A modal panel that is already up keeps the keyboard: ⌘F never replaces one feature panel
        /// with another, and it never reaches past a scrim into the rail underneath it.
        /// </para>
        /// </summary>
        private void OpenSearch()
        {
            if (!CanOpenSearch())
            {
                return;
            }

            if (ActiveOverlay is TokenPickerOverlayViewModel picker)
            {
                picker.FocusSearch();

                return;
            }

            if (CanInsertIntoMacro())
            {
                InsertSpecialAction();

                return;
            }

            if (ActiveOverlay is not null)
            {
                return;
            }

            // The rail's own Remap panel. It refuses politely on a locked position and while nothing
            // is selected — the panel decides, not this class.
            Inspector.Open();

            _remapPanel.FocusSearch();
        }

        private bool CanOpenSearch()
        {
            return Layout is not null && !IsLoading && !IsBusy;
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
            CancelCopyKey();
            _macroPanel?.StopRecording();
            DeactivateInspector();

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

        /// <summary>
        /// Re-reads everything the chrome says about the model: the two spec-10 counters, the
        /// legend row's five layer-scoped counts, and the dirty flag behind the amber Save.
        /// <b>Every path that can write to the layout ends here</b> — a captured remap, the three
        /// resets, a completed key copy, an accepted tap-and-hold, every
        /// <see cref="MacroPanelViewModel.MacrosChanged"/>, and <see cref="Apply"/> after a load or
        /// an import. Core announces nothing, so a path that skips it leaves every readout stale.
        /// </summary>
        private void RefreshCounters()
        {
            ModifiedKeyCount = Layout?.ModifiedKeyCount ?? 0;
            MacroCount = Layout?.MacroCount ?? 0;

            // Order matters by one step: RebuildAdvisories pushes each layer's advisory count in,
            // and the legend row reads it back off the layer.
            RebuildAdvisories();
            RefreshLegend();
            RefreshDirtyState();
        }

        /// <summary>
        /// Re-reads what the app has to say about the layout and pushes it everywhere it is shown:
        /// the per-key flag, the macro rows, and the strip's own count and sentence.
        /// <para>
        /// Hooked to <see cref="RefreshCounters"/> and deliberately <b>not</b> to
        /// <see cref="RefreshDirtyState"/>. The set is derived from the
        /// <see cref="KeyboardLayout"/> alone, and the only path that calls the dirty refresh on its
        /// own is a lighting write, which cannot move a layout advisory —
        /// <see cref="KeyboardLayout.Validate"/> walks every key of every layer, and paying for that
        /// on each click of the colour picker would be felt for a set that cannot have changed.
        /// </para>
        /// </summary>
        private void RebuildAdvisories()
        {
            Advisories = EditorAdvisories.Build(Layout);

            foreach (var layer in Layers)
            {
                foreach (var key in layer.Keys)
                {
                    key.HasAdvisory = _advisories.HasAdvisoryForKey(layer.Index, key.Index);
                }

                // The one count the layer cannot derive from its own caps: the fact lives out here,
                // in EditorAdvisories, so it is pushed in exactly like the per-cap flag above. It is
                // an aggregate of what the strip already says, not a third place an advisory is
                // reported (invariant 21).
                layer.AdvisoryCount = _advisories.CountForLayer(layer.Index);
            }

            if (_macroPanel is not null)
            {
                _macroPanel.Advisories = _advisories;
            }

            RefreshAdvisorySummary();
        }

        /// <summary>
        /// Hands the strip the current set narrowed to the open section, without rebuilding it: the
        /// tab and the layer decide which advisories the strip is about, and neither changes the
        /// model.
        /// </summary>
        private void RefreshAdvisorySummary()
        {
            AdvisoryStrip.Project(_advisories, _selectedTab, SelectedLayer?.Index);
        }

        /// <summary>
        /// <c>Review N</c>'s key half: puts the board's selection on the anchored cap. Handed to
        /// <see cref="AdvisoryStripViewModel"/> as a callback, because the board is this class's.
        /// <para>
        /// It lands through <see cref="SelectKeyDirectly"/>, <b>never</b>
        /// <see cref="SelectKeyCommand"/>: the click contract promotes a second hit on the
        /// already-selected cap into listening, and reviewing is reading.
        /// </para>
        /// </summary>
        private void SelectAnchoredKey(AdvisoryAnchor anchor)
        {
            if (anchor.KeyIndex is not int keyIndex || SelectedLayer?.FindByIndex(keyIndex) is not { } key)
            {
                return;
            }

            SelectKeyDirectly(key);
        }

        /// <summary>
        /// <c>Review N</c>'s macro half: opens the anchored row in the macro panel. The strip's
        /// other callback, for the same reason — the panel is this class's.
        /// </summary>
        private void SelectAnchoredMacro(AdvisoryAnchor anchor)
        {
            if (_macroPanel is null)
            {
                return;
            }

            foreach (var row in _macroPanel.Macros)
            {
                if (row.Layer?.Index == anchor.LayerIndex
                    && row.Key?.Index == anchor.KeyIndex
                    && row.Slot == anchor.MacroIndex)
                {
                    _macroPanel.SelectMacroCommand.Execute(row);

                    return;
                }
            }
        }

        /// <summary>
        /// Re-asks the session whether it still serializes to what was loaded. Split from
        /// <see cref="RefreshCounters"/> because the lighting tab moves the session without moving
        /// a counter, and calling the counter refresh from there would be a lie about what changed.
        /// <para>
        /// Not called after a successful save: Core's baseline is captured at load and
        /// <c>ProfileSession.Save</c> does not move it (docs/app/profiles.md), so the session goes
        /// on reporting itself dirty once it has been written. <see cref="SaveAsync"/> therefore
        /// clears the flag outright, and the next real edit re-asks and gets true again.
        /// </para>
        /// </summary>
        private void RefreshDirtyState()
        {
            IsDirty = _session?.IsDirty == true;
        }

        /// <summary>
        /// Erases the shown layer, after the confirmation of <see cref="NotificationKeys.ResetLayer"/>.
        /// <para>
        /// <b>The question is asked through the notification service and nowhere else.</b> Whether
        /// the user switched it off lives in <c>app_settings.txt</c>, and
        /// <see cref="NotificationService"/> already short-circuits a hidden key to
        /// <see cref="MessageBoxOutcome.ForSuppressed"/>; reading <c>IsHidden</c> here would be a
        /// second policy for one decision. Hence <see cref="MessageBoxRequest.SuppressedResult"/>
        /// is <c>Yes</c>: a suppressed confirmation means "go ahead", not "do nothing".
        /// </para>
        /// </summary>
        private async Task ResetLayerAsync()
        {
            var layer = SelectedLayer;

            if (layer is null)
            {
                return;
            }

            if (!await ConfirmResetAsync(ResetLayerTitle, ResetLayerConfirmation, ResetLayerConfirmCaption).ConfigureAwait(true))
            {
                return;
            }

            // Re-read: the confirmation is modal but the layer selection is not frozen behind it,
            // and erasing a layer the user is no longer looking at would be the worst kind of bug.
            layer = SelectedLayer;

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

        /// <summary>
        /// Erases every layer, after a confirmation that <b>shares</b> the layer reset's
        /// suppression key.
        /// <para>
        /// One preference, not two: the catalog models a single reset confirmation
        /// ("Confirm before resetting a layer", mockup 1j), and a user who switched it off has
        /// answered the question "should a reset stop and ask me?" for both scopes. Leaving the
        /// wider scope unsuppressible would put two policies behind one checkbox and leave a prompt
        /// the settings screen cannot re-enable. What differs is the wording — the sentence names
        /// every layer — and the stakes are bounded the same way: a reset changes memory only, and
        /// nothing reaches the drive until Save.
        /// </para>
        /// </summary>
        private async Task ResetLayoutAsync()
        {
            if (Layout is null)
            {
                return;
            }

            if (!await ConfirmResetAsync(ResetLayoutTitle, ResetLayoutConfirmation, ResetLayoutConfirmCaption).ConfigureAwait(true))
            {
                return;
            }

            // Re-read after the box: a load or an import may have replaced the model behind it.
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

        /// <summary>
        /// Puts one of the two reset confirmations on screen and reports whether the erase may go
        /// ahead. False for every other answer, including a box that could not be shown at all —
        /// a confirmation that failed must not erase anything, and must not bring the app down
        /// either (the same rule the lighting tab's Reset All follows).
        /// </summary>
        private async Task<bool> ConfirmResetAsync(string title, string message, string confirmCaption)
        {
            MessageBoxOutcome outcome;

            try
            {
                outcome = await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = title,
                    Message = message,
                    Icon = MessageBoxIcon.Confirmation,
                    Buttons = MessageBoxButtons.YesNo,
                    YesCaption = confirmCaption,
                    NoCaption = ResetDeclineCaption,
                    SuppressionKey = NotificationKeys.ResetLayer,
                    SuppressedResult = MessageBoxResult.Yes
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                return false;
            }

            return outcome.Result == MessageBoxResult.Yes;
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
            CancelCopyKey();
            _macroPanel?.StopRecording();
            DeactivateInspector();

            ProfileSaveResult? result = null;
            Exception? error = null;

            IsBusy = true;

            try
            {
                // Shown inside the try, and hidden after IsBusy is cleared below: both calls fan
                // out to the overlay, and a failure in either must not leave the flag stuck — it
                // disables Save and every editing command for as long as the editor is open.
                _notifications.ShowLoading(SavingCaption);

                result = await Task.Run(session.Save).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                IsBusy = false;

                _notifications.HideLoading();
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

            // The profile is on the drive: Save goes back to accent. Set rather than re-read,
            // because the session's dirty baseline is the one captured at load and a save does not
            // move it (see RefreshDirtyState).
            IsDirty = false;

            var message = AdvisoryStripViewModel.BuildPostSaveMessage(result.PostSaveMessage, Advisories.Total);

            if (message is not null)
            {
                // A message that counts advisories must not arrive on the success face: the amber
                // variant of mockup 1k exists for exactly this toast, and BuildPostSaveMessage has
                // already folded "saved with N advisories" into the text above. Everything was
                // still written — an advisory is a remark, never a failure — so this stays a toast
                // rather than becoming a message box (docs/design/README.md: advisories never block).
                _notifications.ShowToast(new ToastRequest
                {
                    Title = SaveTitle,
                    Message = message,
                    Severity = Advisories.Total > 0 ? ToastSeverity.Advisory : ToastSeverity.Success
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
            // SelectTabCommand is deliberately absent: the strip is built once from device-level
            // facts (EditorTabViewModel), every tab in it works, and the command's predicate reads
            // nothing but whether it was handed one — so no state here could change its answer.
            BeginRemapCommand.NotifyCanExecuteChanged();
            CancelRemapCommand.NotifyCanExecuteChanged();
            ResetKeyCommand.NotifyCanExecuteChanged();
            ResetLayerCommand.NotifyCanExecuteChanged();
            ResetLayoutCommand.NotifyCanExecuteChanged();
            CopyKeyCommand.NotifyCanExecuteChanged();
            CancelCopyKeyCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            InsertDelayCommand.NotifyCanExecuteChanged();
            InsertSpecialActionCommand.NotifyCanExecuteChanged();
            OpenSearchCommand.NotifyCanExecuteChanged();
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
        /// runs them. The rail's hooks are dropped first, which is why a half-finished assignment
        /// cannot write back into a disposed editor on the way out.
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

            Lighting.ModelChanged -= _lightingChangedHandler;

            // The preferences store belongs to the device session, which outlives this editor, so
            // the strip has to come off it here or a closed editor keeps being re-read.
            AdvisoryStrip.Dispose();

            DetachInspector();

            _overlays.Close();
            _overlays.ActiveChanged -= _activeOverlayChangedHandler;

            DetachMacroPanel();
            StopListening();
            CancelCopyKey();
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
