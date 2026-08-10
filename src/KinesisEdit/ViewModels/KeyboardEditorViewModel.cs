using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
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
    public sealed partial class KeyboardEditorViewModel
        : DeviceEditorViewModel,
            IDisposable,
            IResetScopeHost,
            IMacroInsertionHost,
            IEditorKeystrokeHost,
            IEditorSelectionHost
    {
        /// <summary>Prefix of the profile caption; the loaded profile number follows it.</summary>
        public const string ProfileCaptionPrefix = "Profile ";

        /// <summary>Caption of the loading indicator during a save. Not a spec string.</summary>
        public const string SavingCaption = "Saving…";

        /// <summary>
        /// The rail width macro editing is entitled to (<c>WidthInspectorRailWide</c>, 480 px —
        /// docs/design/handoff.md § Geometry says 300, raised by issue #146). A <b>floor</b> on
        /// <see cref="EffectiveInspectorRailWidth"/>, never a replacement — see
        /// <see cref="InspectorRailWidthViewModel.EffectiveWidth"/>.
        /// <para>
        /// The number itself moved to <see cref="InspectorRailWidthViewModel"/> with the rest of the
        /// width state (issue #124); this stays as the name the editor's own callers and tests know
        /// it by, so the rehoming changed no public contract of this class.
        /// </para>
        /// </summary>
        public const double MacroInspectorRailWidth = InspectorRailWidthViewModel.MacroRailWidth;

        /// <summary>
        /// The Demo Mode bar's copy, verbatim from mockup 1f. Purple, never amber: demo mode is its
        /// own state in the four-status vocabulary and amber is reserved for advisories.
        /// </summary>
        public const string DemoModeBarMessage =
            "Demo Mode — no keyboard attached. Nothing you change here is written anywhere.";

        /// <summary>
        /// The Demo Mode bar's first action (mockup 1f), verbatim; it opens the §11.5 export panel.
        /// <para>
        /// It is rendered — and live. Demo mode now opens a real session over the fixture drive, and
        /// an export writes to a folder the user picked rather than to the v-Drive, so nothing about
        /// 03 §3.5 stands in its way. This is where a user with no hardware gets something out of
        /// the app, which is exactly why the mockup draws it here.
        /// </para>
        /// </summary>
        public const string DemoModeExportCaption = "Export layout to file…";

        /// <summary>The Demo Mode bar's second action (mockup 1f), verbatim; it runs the shell's Home.</summary>
        public const string DemoModeConnectCaption = "Connect a device";

        /// <summary>Title of the dialog raised when the profile cannot be read from the drive.</summary>
        public const string LoadFailureTitle = "Load Profile";

        /// <summary>Message prefix of that dialog; the exception's message follows it.</summary>
        public const string LoadFailureMessagePrefix = "The profile could not be loaded from the v-Drive: ";

        /// <summary>Title of everything a save raises — its failure dialogs and its post-save toast.</summary>
        public const string SaveTitle = "Save Profile";

        /// <summary>Heading of the violation list shown when validation stopped the save (04 §5.3).</summary>
        public const string SaveRejectedMessage = "The profile was not saved because it exceeds the device's limits:";

        /// <summary>
        /// The same heading when the press was writing <b>several</b> profiles: it says "nothing",
        /// because the pre-pass means nothing was written, and each violation line below it is
        /// prefixed with the profile it came from.
        /// </summary>
        public const string SaveRejectedProfilesMessage =
            "Nothing was saved because these profiles exceed the device's limits:";

        /// <summary>Opening of the aggregated post-save sentence; the profile numbers follow it.</summary>
        public const string SavedProfilesPrefix = "Saved profiles ";

        /// <summary>What joins the last two numbers of that sentence.</summary>
        public const string SavedProfilesConjunction = " and ";

        /// <summary>Message prefix when the save threw; the exception's message follows it.</summary>
        public const string SaveErrorMessagePrefix = "The profile could not be saved: ";

        /// <summary>
        /// What a press of <c>Save</c> with nothing unsaved anywhere says. It writes no file — a
        /// v-Drive is flash, and rewriting bytes that are already there is the one thing the user's
        /// own request singled out — so it reports that rather than doing nothing visible.
        /// </summary>
        public const string NothingToSaveMessage = "No unsaved changes. Nothing was written to the keyboard.";

        /// <summary>
        /// The reset prompts' wording, all six strings of it. It lives on
        /// <see cref="ResetScopeCoordinator"/> now, beside the commands it guards; these stay as the
        /// names this class's callers and tests know it by, so the rehoming changed no public
        /// contract of this class — the same forwarding the <see cref="MacroInspectorRailWidth"/>
        /// const above does for the rail's width.
        /// </summary>
        public const string ResetLayerTitle = ResetScopeCoordinator.ResetLayerTitle;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetLayerConfirmation"/>
        public const string ResetLayerConfirmation = ResetScopeCoordinator.ResetLayerConfirmation;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetLayerConfirmCaption"/>
        public const string ResetLayerConfirmCaption = ResetScopeCoordinator.ResetLayerConfirmCaption;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetLayoutTitle"/>
        public const string ResetLayoutTitle = ResetScopeCoordinator.ResetLayoutTitle;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetLayoutFallbackScope"/>
        public const string ResetLayoutFallbackScope = ResetScopeCoordinator.ResetLayoutFallbackScope;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetDeclineCaption"/>
        public const string ResetDeclineCaption = ResetScopeCoordinator.ResetDeclineCaption;

        /// <inheritdoc cref="ResetScopeCoordinator.BuildResetLayoutConfirmation"/>
        public static string BuildResetLayoutConfirmation(string profileCaption)
        {
            return ResetScopeCoordinator.BuildResetLayoutConfirmation(profileCaption);
        }

        /// <inheritdoc cref="ResetScopeCoordinator.BuildResetLayoutConfirmCaption"/>
        public static string BuildResetLayoutConfirmCaption(string profileCaption)
        {
            return ResetScopeCoordinator.BuildResetLayoutConfirmCaption(profileCaption);
        }

        /// <summary>
        /// The action row's <c>Discard changes</c> (issue #133). Sentence case, like every caption
        /// this app owns; <c>Reset Layout</c> beside it keeps its Title Case because that one is
        /// spec 10's verbatim.
        /// </summary>
        public const string DiscardChangesCaption = "Discard changes";

        /// <summary>Title of the confirmation raised before unsaved work is thrown away.</summary>
        public const string DiscardChangesTitle = "Discard Changes";

        /// <summary>
        /// The prompt on the Layout tab. It names <b>both</b> halves of the scope — what
        /// goes and what stays — because the whole point of the action is that it is not the whole
        /// profile.
        /// </summary>
        public const string DiscardLayoutConfirmation =
            "Do you want to throw away every unsaved change to this profile's keys and macros? They go back to what was "
            + "loaded when the profile was opened. Its lighting is untouched, and nothing is written to the keyboard.";

        /// <summary>The prompt on the Lighting tab — the same sentence with the two halves swapped.</summary>
        public const string DiscardLightingConfirmation =
            "Do you want to throw away every unsaved change to this profile's lighting? It goes back to what was loaded "
            + "when the profile was opened. Its keys and macros are untouched, and nothing is written to the keyboard.";

        /// <summary>The affirmative, named after what it does (mockup 1k). It still answers <c>Yes</c>.</summary>
        public const string DiscardConfirmCaption = "Discard changes";

        /// <summary>The way out of the discard prompt, named after its outcome. It still answers <c>No</c>.</summary>
        public const string DiscardDeclineCaption = "Keep editing";

        /// <summary>Builds the caption of the profile indicator ("Profile 1").</summary>
        public static string BuildProfileCaption(int profileNumber)
        {
            return ProfileCaptionPrefix + profileNumber.ToString(CultureInfo.InvariantCulture);
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
        /// that exists verbatim on the machine. Empty on a device with no location.
        /// </summary>
        public string MountPath => Device.Location?.RootPath ?? string.Empty;

        /// <summary>
        /// Whether there is a mount path worth printing. <b>Never in demo mode</b>, and that is
        /// load-bearing twice over: a demo board's location is the synthetic
        /// <c>kinesis-edit://demo/…</c> root, which is neither a place on this machine nor
        /// something the mono slot is allowed to hold (mono is reserved for values that exist
        /// verbatim in a config file or on disk) — and printing any path beside a session that
        /// writes nowhere would claim the opposite of what the Demo Mode bar promises.
        /// </summary>
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
        /// Always false after a load that produced no session — a board with no drive and no demo
        /// content — because there is nothing to compare against. In <b>demo mode it is genuinely
        /// true</b> once the fixture profile is edited: the session is real, so the amber Save
        /// reports honestly that the model has moved, even though Save itself is refused (03 §3.5).
        /// <see cref="ConfirmCloseAsync"/> therefore tests demo mode on its own rather than leaning
        /// on this.
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

        /// <summary>
        /// Remapped positions across every layer (<see cref="KeyboardLayout.ModifiedKeyCount"/>).
        /// <para>
        /// <b>Nothing renders this any more</b> — issue #135 took the toolbar's <c>Remap (n)</c>
        /// caption off screen — and it is still load-bearing: <see cref="RefreshCounters"/> is the
        /// funnel every layout write passes through, and it ends in
        /// <see cref="RefreshDirtyState"/>. The count is what tells that funnel it ran.
        /// </para>
        /// </summary>
        public int ModifiedKeyCount
        {
            get => _modifiedKeyCount;
            private set => SetProperty(ref _modifiedKeyCount, value);
        }

        /// <summary>
        /// Macros across the whole profile (<see cref="KeyboardLayout.MacroCount"/>). Off screen
        /// since issue #135 and kept for the reason <see cref="ModifiedKeyCount"/> is.
        /// </summary>
        public int MacroCount
        {
            get => _macroCount;
            private set => SetProperty(ref _macroCount, value);
        }

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
        /// The set itself belongs to <see cref="EditorAdvisoryProjection"/>, which builds it and
        /// fans it out; this is the name it is <em>published</em> under, and the announcement is
        /// raised in <see cref="RebuildAdvisories"/> because a property may only be raised by the
        /// class that declares it.
        /// </para>
        /// <para>
        /// <b>Nothing here gates anything.</b> No command's <c>CanExecute</c> reads it, a save with
        /// advisories succeeds, and an over-budget layout is written as it stands — the board
        /// truncates. That is the design law ("advisories never block"), and it is the reason this
        /// is a read-out and not a validator.
        /// </para>
        /// </summary>
        public EditorAdvisories Advisories => _advisoryProjection.Advisories;

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
        /// (<see cref="EditorTabViewModel.CreateAll"/>): Layout always, Settings where
        /// the board has a settings file, Lighting only where its led file is the model
        /// <see cref="LightingTabViewModel"/> edits. Every entry opens a working section — a
        /// feature the board lacks is not rendered at all rather than disabled.
        /// <para>
        /// The strip and everything the editor points at are <see cref="EditorSelection"/>'s since
        /// issue #154; this, the four selection properties below and the three commands hand out its
        /// very state, so the rehoming changed nothing the view or a test can see.
        /// </para>
        /// </summary>
        public IReadOnlyList<EditorTabViewModel> Tabs => _selection.Tabs;

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

        /// <summary>
        /// The open section. Two-way bindable, and the setter runs the same guard as the command —
        /// <see cref="EditorSelection.SelectTab"/> — so a binding cannot open a tab the strip does
        /// not carry.
        /// </summary>
        public EditorTab SelectedTab
        {
            get => _selection.SelectedTab;
            set => _selection.SelectedTab = value;
        }

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
        /// Whether the next physical keystroke already belongs to somebody — the consumers of "one
        /// keystroke, one target" (invariant 5), and the gate the editor's whole keyboard grammar
        /// is off behind.
        /// <para>
        /// It is read on demand by <see cref="Views.KeyboardEditorView"/>'s key handler and is
        /// deliberately not observable: nothing binds it, and its sources already raise their own
        /// notifications. The state machine behind it is
        /// <see cref="EditorKeystrokeRouter"/>'s since issue #154 and this property hands out its
        /// very answer, so the rehoming changed nothing the view or a test can see.
        /// </para>
        /// </summary>
        public bool IsCaptureActive => _keystrokes.IsCaptureActive;

        /// <summary>The device's layers, in model order.</summary>
        public IReadOnlyList<KeyboardLayerViewModel> Layers => _selection.Layers;

        /// <summary>The layer the picture is showing.</summary>
        public KeyboardLayerViewModel? SelectedLayer => _selection.SelectedLayer;

        /// <summary>The key every key-scoped action applies to, or null when nothing is selected.</summary>
        public KeyboardKeyViewModel? SelectedKey => _selection.SelectedKey;

        /// <summary>
        /// The key waiting for the next physical keypress, or null when nothing is listening.
        /// <para>
        /// The remap state machine is <see cref="EditorKeystrokeRouter"/>'s since issue #154 and
        /// this property reads its state; the announcement is raised here, in
        /// <see cref="IEditorKeystrokeHost.OnListeningChanged"/>, because a property may only be
        /// raised by the class that declares it.
        /// </para>
        /// </summary>
        public KeyboardKeyViewModel? ListeningKey => _keystrokes.ListeningKey;

        /// <summary>Whether a key is currently listening for its new assignment.</summary>
        public bool IsListening => _keystrokes.IsListening;

        /// <summary>
        /// The editor's one rail width — the object the Keys tab's key inspector and the Lighting
        /// tab's mode rail are both drawn from (issue #124), handed to
        /// <see cref="LightingTabViewModel"/> as the very same instance. The two properties below
        /// delegate to it and are what this class's callers, its view and its tests still speak.
        /// </summary>
        public InspectorRailWidthViewModel Rail { get; }

        /// <summary>
        /// How wide the user has dragged the key inspector rail, in DIPs — clamped, persisted per
        /// user, and free to set to the width it already has. Every one of those rules lives on
        /// <see cref="InspectorRailWidthViewModel.Width"/>, which this forwards to verbatim.
        /// <para>
        /// The view binds the <b>column</b> to <see cref="EffectiveInspectorRailWidth"/>, never this:
        /// this is the user's number, that one is the number the rail is actually drawn at.
        /// </para>
        /// </summary>
        public double InspectorRailWidth
        {
            get => Rail.Width;
            set => Rail.Width = value;
        }

        /// <summary>
        /// The width the rail is drawn at: the user's, or <b>at least</b> the 480 px macro editing
        /// is given while the Macro panel is showing. A floor and not an override — the
        /// deviation of issue #119, spelled out on
        /// <see cref="InspectorRailWidthViewModel.EffectiveWidth"/>.
        /// <para>
        /// It moves when <b>either</b> input moves, and both reach it through the rail object: the
        /// setter above, and the inspector's <c>IsWide</c>, which
        /// <see cref="OnInspectorPropertyChanged"/> pushes onto the rail.
        /// </para>
        /// </summary>
        public double EffectiveInspectorRailWidth => Rail.EffectiveWidth;

        /// <summary>Opens a section of the editor; a section this strip does not carry is refused.</summary>
        public IRelayCommand<EditorTabViewModel> SelectTabCommand => _selection.SelectTabCommand;

        /// <summary>Switches the picture to another layer, cancelling anything in progress.</summary>
        public IRelayCommand<KeyboardLayerViewModel> SelectLayerCommand => _selection.SelectLayerCommand;

        /// <inheritdoc cref="EditorSelection.SelectKeyCommand"/>
        public IRelayCommand<KeyboardKeyViewModel> SelectKeyCommand => _selection.SelectKeyCommand;

        /// <summary>
        /// Puts the selected key into listening state.
        /// <para>
        /// Both remap commands are <see cref="EditorKeystrokeRouter"/>'s since issue #154 and these
        /// two properties hand out its very commands — <see cref="CancelRemapCommand"/> is bound in
        /// <c>Views/KeyboardEditorView.axaml</c> beside the listening banner and run by the
        /// grammar in its code-behind — so the rehoming changed nothing the view or a test can see,
        /// and <see cref="NotifyCommands"/> still re-asks the very instances that are bound.
        /// </para>
        /// </summary>
        public IRelayCommand BeginRemapCommand => _keystrokes.BeginRemapCommand;

        /// <inheritdoc cref="EditorKeystrokeRouter.CancelRemapCommand"/>
        public IRelayCommand CancelRemapCommand => _keystrokes.CancelRemapCommand;

        /// <summary>
        /// Drops the selected key's remap (specs/10-apps-and-ui.md, "Reset Key").
        /// <para>
        /// The three resets are <see cref="ResetScopeCoordinator"/>'s since issue #115 and these
        /// three properties hand out its very commands, so the rehoming changed nothing the view,
        /// the key inspector, the legend row or a test can see.
        /// </para>
        /// </summary>
        public IRelayCommand ResetKeyCommand => _resetScopes.ResetKeyCommand;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetLayerCommand"/>
        public IRelayCommand ResetLayerCommand => _resetScopes.ResetLayerCommand;

        /// <inheritdoc cref="ResetScopeCoordinator.ResetLayoutCommand"/>
        public IRelayCommand ResetLayoutCommand => _resetScopes.ResetLayoutCommand;

        /// <summary>Writes the profile back to the v-Drive; never available in demo mode (03 §3.5).</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// Opens Search Keys over the macro and inserts the picked action (11 §11.6).
        /// <para>
        /// Both insertion commands are <see cref="MacroInsertionHost"/>'s since issue #115 and these
        /// two properties hand out its very commands: this one is bound in
        /// <c>Views/KeyboardEditorView.axaml</c> and <see cref="OpenSearchCommand"/> is run by the
        /// grammar in that view's code-behind, so neither may move off this class.
        /// </para>
        /// </summary>
        public IRelayCommand InsertSpecialActionCommand => _macroInsertion.InsertSpecialActionCommand;

        /// <inheritdoc cref="MacroInsertionHost.OpenSearchCommand"/>
        public IRelayCommand OpenSearchCommand => _macroInsertion.OpenSearchCommand;

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
        private readonly ResetScopeCoordinator _resetScopes;
        private readonly EditorAdvisoryProjection _advisoryProjection;
        private readonly MacroInsertionHost _macroInsertion;
        private readonly EditorKeystrokeRouter _keystrokes;
        private readonly EditorSelection _selection;

        /// <summary>
        /// The board picture, resolved once from the device (never from the profile) and shared:
        /// <see cref="EditorSelection"/> is handed this very instance for the arrow keys' geometry,
        /// while <see cref="BoardWidth"/>/<see cref="BoardHeight"/> and <see cref="Apply"/> keep
        /// reading it here. It is immutable device-level data, so two readonly references to it are
        /// not two copies of a state.
        /// </summary>
        private readonly KeyboardVisual? _visual;

        private readonly EventHandler _activeOverlayChangedHandler;
        private readonly EventHandler _lightingChangedHandler;
        private readonly PropertyChangedEventHandler _inspectorPropertyChangedHandler;
        private IProfileSession? _session;
        private IReadOnlyList<string> _invalidLineMessages = [];
        private KeyboardLayout? _layout;
        private string _profileCaption = string.Empty;
        private int _modifiedKeyCount;
        private int _macroCount;
        private bool _isLoading = true;
        private bool _isBusy;
        private bool _isDirty;
        private bool _hasLoadStarted;
        private bool _isDisposed;

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
        /// <para>
        /// <paramref name="hostPreferences"/> is the <b>other</b> store — the per-user one
        /// (docs/app/host-preferences.md) — and it carries exactly one thing this editor cares
        /// about: how wide the user dragged the inspector rail. Optional for the same reason as the
        /// two above; with none, the rail sits at its authored width and a drag is forgotten when
        /// the editor closes. The two stores are never interchangeable: this one follows the person,
        /// <c>app_settings.txt</c> follows the board.
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
            IDeviceSessionAccessor? sessions = null,
            IMotionSettings? motionSettings = null,
            IHostPreferencesStore? hostPreferences = null) : base(device)
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

            // The editor's whole multi-profile state, and the per-profile unsaved-rename set that
            // rides alongside it (KeyboardEditorViewModel.Profiles.cs, .MacroNames.cs). The cache is
            // built FIRST because the name store is handed it: a rename made in a profile the user
            // has since left is written from the layout that profile's session still holds
            // (issue #133), and the store also needs the session's app_settings.txt — neither of
            // which a field initializer can reach, since it may name neither `this` nor another
            // field. Both exist before anything below can ask whether the editor is dirty.
            _sessionCache = new ProfileSessionCache();
            _macroNames = new MacroNameStore(_preferences, _sessionCache);

            // THE RAIL'S WIDTH IS ONE NUMBER FOR THE WHOLE EDITOR, and it is built first because the
            // Lighting tab below is handed this very instance (issue #124): the mode rail and the key
            // inspector are two contents of one column, so a width dragged on either tab is the width
            // the other opens at. It used to be three members of this class; a second tab could not
            // reach them without either a second stored value or a back-reference to this editor.
            Rail = new InspectorRailWidthViewModel(hostPreferences);

            // The two width properties below delegate to it and re-announce it under their own
            // names, so the rehoming changed nothing the view or a test can see. Never unsubscribed,
            // unlike the inspector's handler: the rail is this editor's own, and a splitter that
            // reports one last width after Dispose must still reach the column the view is holding.
            Rail.PropertyChanged += OnRailPropertyChanged;

            // The board picture belongs to the device, not to the profile, so it is resolved once
            // and shared by every layer (docs/app/domain-data.md, "Visual geometry").
            _visual = VisualCatalog.TryGet(device.DeviceId, out var visual) ? visual : null;

            // The two tabs that read app_settings.txt are handed the session's store rather than
            // loading the file for themselves: one reader and one writer, or the colour picker's
            // swatches and the settings screen's preferences show each other stale state
            // (docs/app/settings.md). Neither parameter has a default, so an editor that forgot to
            // thread the store is a compile error rather than a screen that quietly reads nothing.
            Settings = new KeyboardSettingsViewModel(device, settings, notifications, _preferences, urlLauncher);
            // The lighting tab is the app's one animated surface, so it is the one view model that
            // needs the motion switch: its preview freezes at frame zero under reduce-motion. It is
            // read per frame rather than once, because since issue #96 the setting is a live user
            // preference (MotionPreferenceApplier) that the Settings screen can flip while this
            // editor is open, and IMotionSettings raises nothing when it moves.
            //
            // It is handed the SAME rail object this class delegates to — not a copy and not a
            // second preference — so the two tabs' rails are one resizable column (issue #124).
            Lighting = new LightingTabViewModel(device, notifications, _preferences, motionSettings, Rail);

            // Everything the editor is POINTED AT — the tab strip, the shown layer, the selected
            // key, the click contract and the arrow keys (EditorSelection). Built HERE, and it is
            // the one build order this class has three separate reasons for:
            //   * after Lighting, because the strip is filtered by Lighting.IsAvailable — a
            //     device-level question, asked once, since this constructor runs before any profile
            //     has been read and demo mode never reads one;
            //   * before AdvisoryStrip below, which is handed two of its methods as the Review
            //     walk's callbacks;
            //   * before the first SelectTab at the end of this constructor, which is its own.
            // It is handed the same board picture this class keeps: one immutable device fact, read
            // by the arrow keys there and by BoardWidth/BoardHeight and Apply here.
            _selection = new EditorSelection(this, device.Device, Lighting.IsAvailable, _visual);

            // The strip owns the projection and the Review walk; selecting what a note is about is
            // EditorSelection's, because the board and the macro panel are. Built before SelectTab
            // below, which projects onto it. It is handed the session's preferences because one of
            // them — `advisory_detail` — decides whether its sentence is trimmed or shown whole,
            // and it follows that store for as long as the editor is open.
            AdvisoryStrip = new AdvisoryStripViewModel(
                _selection.SelectAnchoredKey,
                _selection.SelectAnchoredMacro,
                _preferences);

            // Building the set, marking the caps it names and narrowing it onto the strip are one
            // job — invariant 21's "every advisory appears exactly twice" — so they are one type,
            // and it owns the set. This class publishes it as Advisories above and raises that name
            // when it moves; nothing here holds a second copy.
            _advisoryProjection = new EditorAdvisoryProjection(AdvisoryStrip);

            // The remap state machine, spec 10's keystroke routing and the editor's ONE
            // subscription to the capture service (EditorKeystrokeRouter). Built HERE, where its
            // two commands used to be created, because everything below it re-asks them:
            // NotifyCommands names both, and the first SelectTab at the end of this constructor
            // runs it. It is handed the same capture service EditorOverlayHost above was — two
            // consumers of one app-wide service, each stopping only what it started.
            _keystrokes = new EditorKeystrokeRouter(this, _capture);
            // The three reset scopes and the two prompts that name them (ResetScopeCoordinator).
            // Built HERE and not later: BoardLegend and CreateInspector below are both handed one of
            // its commands in their own constructors, so it has to exist before either of them.
            _resetScopes = new ResetScopeCoordinator(this, _notifications);
            // Its neighbour in the action row and its opposite in meaning: a reset clears to factory
            // defaults, a discard restores what was loaded (KeyboardEditorViewModel.Discard.cs).
            DiscardChangesCommand = new AsyncRelayCommand(DiscardChangesAsync, () => CanDiscardChanges());
            CopyKeyCommand = new RelayCommand(ArmCopyKey, () => CanCopyKey());
            // The same armed state, scoped to one macro (KeyboardEditorViewModel.Legend.cs). It is
            // built here, before CreateInspector below, because the rail's Macro panel is handed
            // both of these in its constructor.
            CopyMacroCommand = new RelayCommand(ArmMacroCopy, () => CanCopyMacro());
            CancelCopyKeyCommand = new RelayCommand(CancelCopyKey, () => IsCopyArmed);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave());
            // §11.6's macro insertion and ⌘F (MacroInsertionHost). It reads the rail lazily through
            // the host interface, so it may be built before CreateInspector runs; _recentTokens is
            // this editor's one store (KeyboardEditorViewModel.Inspector.cs) and is initialized with
            // the field, so it is already there.
            _macroInsertion = new MacroInsertionHost(this, _recentTokens);
            ExportCommand = new RelayCommand(OpenExport, () => CanExport());
            ImportCommand = new AsyncRelayCommand(ImportAsync, () => CanImport());
            CloseOverlayCommand = new RelayCommand(_overlays.Dismiss, () => ActiveOverlay is not null);

            // The profile picker (KeyboardEditorViewModel.Profiles.cs). Built here, from device
            // facts alone and before the first SelectTab below, for the same reason the tab strip
            // is: which profiles a board has is not something a load discovers.
            Profiles = BuildProfileOptions();
            SelectProfileCommand = new AsyncRelayCommand<ProfileOptionViewModel>(SelectProfileAsync, CanSelectProfile);

            // The legend row is a projection, not a decision: it holds the shown layer's five
            // counts and runs two of this class's commands. Built here so it exists before the
            // first SelectTab/SelectLayer, both of which refresh it.
            BoardLegend = new BoardLegendViewModel(CopyKeyCommand, ResetLayerCommand);

            // The key inspector rail and its two mode panels (KeyboardEditorViewModel.Inspector.cs).
            // Built here for the same reason: RefreshLegend pushes state into it, and the first
            // SelectLayer below already runs that.
            Inspector = CreateInspector();

            // EffectiveInspectorRailWidth has two inputs and the key inspector owns one of them: the
            // Macro panel raises the floor to 480 the moment it shows. IsWide announces itself, so
            // the width the column is bound to follows a mode switch without anything having to push
            // it — the handler below is what carries that announcement onto the rail object.
            _inspectorPropertyChangedHandler = OnInspectorPropertyChanged;

            Inspector.PropertyChanged += _inspectorPropertyChangedHandler;

            SelectTab(EditorTab.Keys);

            _activeOverlayChangedHandler = (_, _) => OnActiveOverlayChanged();

            // A lighting edit moves no counter, but it does move the session: ProfileSession.Save
            // writes led<n>.txt from the very model the panel mutates, so the amber Save is the
            // only thing that reports it.
            _lightingChangedHandler = (_, _) => RefreshDirtyState();

            Lighting.ModelChanged += _lightingChangedHandler;

            _overlays.ActiveChanged += _activeOverlayChangedHandler;
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
        /// The unsaved-changes guard of docs/design/handoff.md §2 ("leaving via Home asks once
        /// (modal) unless opted out"), asked by the shell before Home or another device replaces
        /// this editor. The question itself is <see cref="UnsavedChangesPrompt"/>'s, so this board
        /// and the pedal ask it in the same words, and asking it is
        /// <see cref="ConfirmDiscardingUnsavedWorkAsync"/>'s — the profile picker asks the same one
        /// before a switch.
        /// <para>
        /// A save in flight refuses outright, without a question — but with a toast, for the same
        /// reason the pedal editor does it (docs/app/savant-elite.md, decision 5): leaving would
        /// dispose this editor while <c>ProfileSession.Save</c> is still writing, and the write is
        /// short enough that the navigation works the moment it finishes.
        /// </para>
        /// <para>
        /// <b>What it cannot see:</b> the Settings tab. Settings are outside the session's dirty
        /// comparison by design (docs/app/keyboard-editor.md, "Settings are outside the dirty
        /// model"), so an unsaved settings row is invisible here — the tab has its own Save, and
        /// giving this guard a second, differently-shaped question would be worse than the gap.
        /// </para>
        /// </summary>
        public override async Task<bool> ConfirmCloseAsync()
        {
            if (IsBusy)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Title = UnsavedChangesPrompt.SaveInProgressTitle,
                    Message = UnsavedChangesPrompt.SaveInProgressMessage
                });

                return false;
            }

            return await ConfirmDiscardingUnsavedWorkAsync().ConfigureAwait(true);
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

            // An armed record button underneath owns the capture service and would swallow every
            // key aimed at the panel, Escape included; standing it down hands the service back
            // before the panel asks the host for it. The rail is under the same scrim, and the rail
            // itself stays open behind the panel — only what it had armed stops.
            DeactivateInspector();

            _overlays.Show(overlay);
        }

        /// <inheritdoc cref="EditorSelection.MoveSelection"/>
        /// <remarks>
        /// The selection is <see cref="EditorSelection"/>'s since issue #154. This stays as the name
        /// <c>Views/KeyboardEditorView.axaml.cs</c>'s arrow keys call it by — and as a
        /// <see cref="bool"/>, because the view puts the focus ring on the new cap only when it
        /// really moved.
        /// </remarks>
        public bool MoveSelection(NavigationDirection direction)
        {
            return _selection.MoveSelection(direction);
        }

        /// <inheritdoc cref="EditorKeystrokeRouter.TryTakeOverlayKeystroke"/>
        /// <remarks>
        /// The latch is <see cref="EditorKeystrokeRouter"/>'s since issue #154 — one field with one
        /// owner, where it used to be written from two partials of this class and cleared from two
        /// more. This stays as the name <c>Views/KeyboardEditorView.axaml.cs</c> calls it by.
        /// </remarks>
        public bool TryTakeOverlayKeystroke()
        {
            return _keystrokes.TryTakeOverlayKeystroke();
        }

        private LoadOutcome LoadProfile()
        {
            try
            {
                // A device with no location has no file to read: it edits a factory-default model
                // in memory. Demo mode is deliberately *not* asked about here — a demo board with
                // fixtures carries the synthetic DemoVDrive location (docs/app/app-shell.md, "The
                // demo v-Drive") and is loaded through this very path, by a file service that
                // serves the fixtures and refuses every write back to them. What demo mode forbids
                // is writing (03 §3.5), which CanSave and CanImport still answer; refusing to read
                // is what left demo mode showing an empty board.
                if (Device.Location is null)
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

            // The one place a session enters the editor's per-profile cache, so the first load, a
            // switch, an import and a discard all keep it correct without any of them saying so
            // (ProfileSessionCache). Re-filing a cached session is a no-op, and a null one — a load
            // that failed, a board with no drive — files nothing.
            _sessionCache.Add(outcome.Session);

            Layout = outcome.Layout;
            ProfileCaption = outcome.Session is null ? string.Empty : BuildProfileCaption(outcome.Session.ProfileNumber);
            InvalidLineMessages = BuildInvalidLineMessages(outcome.Session);

            // Which profile the picker reports is read off the session that just arrived
            // (KeyboardEditorViewModel.Profiles.cs), so a first load, an import and a profile
            // switch all announce it from one place.
            RefreshSelectedProfile();

            // The picture is empty when either half of it is missing — a load that failed outright,
            // or a device whose board has not been authored yet (#39–#42).
            _selection.Layers = outcome.Layout is not null && _visual is not null
                ? KeyboardLayerViewModel.BuildAll(outcome.Layout, _visual, outcome.Session?.Lighting)
                : [];

            // Before the panels: a freshly parsed layout carries no macro names at all (they ride
            // app_settings.txt, not layoutN.txt), so the stored names have to be stamped onto its
            // macros before anything reads one (KeyboardEditorViewModel.MacroNames.cs). NOT on a
            // cache hit: that layout is the one the user has been renaming macros on, and
            // MacroSites.ApplyNames writes every site unconditionally, so re-stamping it would
            // silently undo every unsaved rename.
            AttachMacroNames(outcome.Layout, applyStoredNames: !outcome.IsCacheHit);

            SelectLayer(Layers.Count > 0 ? Layers[0] : null);
            RefreshCounters();

            // The lighting panel edits the very model the session hands out, so mutating it is
            // all a lighting save takes (ProfileSession.Save writes led<n>.txt whenever Lighting
            // is non-null). It shares these layer view models, so a recoloured key repaints
            // without the picture being rebuilt — on the lighting board, which is the only
            // picture that draws an LED strip (KeyboardView.ShowsLedStrips).
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

        /// <inheritdoc cref="EditorSelection.SelectTab"/>
        /// <remarks>
        /// A forwarder, and deliberately still a method of this class: the constructor's last
        /// statement and <see cref="EditMacroAt"/> both open a section by name. Issue #154 changed
        /// what the call resolves to and nothing about where it sits.
        /// </remarks>
        private void SelectTab(EditorTab tab)
        {
            _selection.SelectTab(tab);
        }

        /// <inheritdoc cref="EditorSelection.SelectLayer"/>
        /// <remarks>A forwarder; <see cref="Apply"/> and <see cref="EditMacroAt"/> are its callers.</remarks>
        private void SelectLayer(KeyboardLayerViewModel? layer)
        {
            _selection.SelectLayer(layer);
        }

        /// <inheritdoc cref="EditorSelection.SelectKeyDirectly"/>
        /// <remarks>
        /// A forwarder, kept because <see cref="EditMacroAt"/> lands a macro site on the board
        /// through it — never through <see cref="SelectKeyCommand"/>, which would promote a second
        /// hit on the already-selected cap into a remap (invariant 24).
        /// </remarks>
        private void SelectKeyDirectly(KeyboardKeyViewModel key)
        {
            _selection.SelectKeyDirectly(key);
        }

        /// <inheritdoc cref="EditorKeystrokeRouter.BeginRemap"/>
        /// <remarks>
        /// A forwarder, and the knot issue #154 had to get through the interface: the click
        /// contract's second branch is a call to it, and the click contract is
        /// <see cref="EditorSelection"/>'s while the state machine is
        /// <see cref="EditorKeystrokeRouter"/>'s. Collaborators never see each other, so this class
        /// is what joins them — <see cref="IEditorSelectionHost.BeginRemap"/> lands here.
        /// </remarks>
        private void BeginRemap()
        {
            _keystrokes.BeginRemap();
        }

        /// <inheritdoc cref="EditorKeystrokeRouter.CancelRemap"/>
        /// <remarks>
        /// A forwarder, and deliberately still a method of this class: it is called from a dozen
        /// places across five partials, and at <see cref="ShowOverlay"/> the stand-down triple is
        /// <em>interleaved</em> with other work — as it is at <see cref="EditorSelection.SelectTab"/>
        /// and <see cref="EditorSelection.SelectLayer"/>, which reach it through
        /// <see cref="IEditorSelectionHost.CancelRemap"/>. Issue #154 changed what the call resolves
        /// to and nothing about where it sits.
        /// </remarks>
        private void CancelRemap()
        {
            _keystrokes.CancelRemap();
        }

        /// <summary>
        /// The single place the editor reacts to the open panel changing: the two bindable
        /// projections of <see cref="EditorOverlayHost.Active"/>, and the commands an open panel
        /// stands every other action down for.
        /// </summary>
        private void OnActiveOverlayChanged()
        {
            // The latch belongs to the panel that set it; a swap or a dismiss ends its claim. It is
            // the router's one field, reached here as a call (issue #154).
            _keystrokes.ClearOverlayKeystroke();

            OnPropertyChanged(nameof(ActiveOverlay));
            OnPropertyChanged(nameof(HasActiveOverlay));
            OnPropertyChanged(nameof(IsOverlayAwaitingKeystroke));

            CloseOverlayCommand.NotifyCanExecuteChanged();
            NotifyCommands();
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
            // Deliberately not gated on demo mode. Export writes to a folder the user picked, never
            // to the v-Drive (specs/11-feature-dialogs.md §11.5), so it breaks none of 03 §3.5's
            // promise — and demo mode is where it is most useful, which is why mockup 1f puts it on
            // the Demo Mode bar. The write is scoped away from the fixture drive by path, in
            // DemoVDriveFileService, not by a mode flag here.
            return _session is not null && !IsLoading && !IsBusy && ActiveOverlay is null;
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
        /// Re-reads everything the chrome says about the model: the two spec-10 counters, the
        /// legend row's five layer-scoped counts, and the dirty flag behind the amber Save.
        /// <b>Every path that can write to the layout ends here</b> — a captured remap, the three
        /// resets, a completed key copy, an accepted tap-and-hold, every write the key inspector's
        /// Macro panel announces, and <see cref="Apply"/> after a
        /// load or an import. Core announces nothing, so a path that skips it leaves everything
        /// stale.
        /// </summary>
        private void RefreshCounters()
        {
            ModifiedKeyCount = Layout?.ModifiedKeyCount ?? 0;
            MacroCount = Layout?.MacroCount ?? 0;

            // Order matters: RebuildAdvisories pushes each layer's advisory count in and the legend
            // row reads it back off the layer.
            RebuildAdvisories();
            RefreshLegend();
            RefreshDirtyState();
        }

        /// <summary>
        /// Re-reads what the app has to say about the layout and announces it: the set, the fan-out
        /// onto the per-key flag, each layer's tally and the strip are all
        /// <see cref="EditorAdvisoryProjection.Rebuild"/>'s, and this is where the move is published
        /// under <see cref="Advisories"/> — which the save toast counts and the key inspector is
        /// refreshed from. Only the raise stays here, because only this class may raise it.
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
            if (_advisoryProjection.Rebuild(Layout, Layers, SelectedTab, SelectedLayer?.Index))
            {
                OnPropertyChanged(nameof(Advisories));
            }
        }

        /// <summary>
        /// Hands the strip the current set narrowed to the open section, without rebuilding it: the
        /// tab and the layer decide which advisories the strip is about, and neither changes the
        /// model.
        /// </summary>
        private void RefreshAdvisorySummary()
        {
            _advisoryProjection.Project(SelectedTab, SelectedLayer?.Index);
        }

        /// <summary>
        /// Re-asks the session whether it still serializes to what was loaded. Split from
        /// <see cref="RefreshCounters"/> because the lighting tab moves the session without moving
        /// a counter, and calling the counter refresh from there would be a lie about what changed.
        /// <para>
        /// <b>A successful save calls it too</b>, rather than asserting <c>IsDirty = false</c>.
        /// Core moves each saved session's baseline to the lines it just wrote (issue #133,
        /// docs/app/profiles.md), so the sessions themselves already know they are clean — and with
        /// several profiles in play, only one of them may have been written. One source of truth
        /// answers; the flag is never set behind the sessions' backs.
        /// </para>
        /// </summary>
        private void RefreshDirtyState()
        {
            // Across EVERY profile the editor has open, not just the one on screen (issue #133): a
            // switch keeps the profile it leaves, so an edit made in profile 3 is still unsaved work
            // while the user is looking at profile 7 — and the amber Save, whose one press now
            // writes all of them, has to say so.
            //
            // A macro NAME is not in the layout file, so no session's line comparison can see one
            // move. It is still unsaved work the user would lose — hence the second term, the one
            // deliberate exception to "app_settings.txt sits outside the dirty model"
            // (KeyboardEditorViewModel.MacroNames.cs).
            // The open session is asked first (ProfileSessionCache.AnyIsDirty): it is the profile the
            // edit that triggered this refresh landed in, so the common path still costs exactly one
            // serialization and the other eight are never asked.
            IsDirty = _sessionCache.AnyIsDirty(_session) || HasUnsavedMacroNames;
        }

        /// <summary>
        /// The unsaved-changes question itself, and the one reading of its answer: <b>true means
        /// the caller may go ahead and abandon the open profile</b> — because there was nothing to
        /// lose, because the user discarded it, or because a save actually landed. A failed save is
        /// false: letting the caller through after one would discard the very work the question was
        /// asked about.
        /// <para>
        /// <b>It asks about every profile the editor has open, not only the one on screen</b>
        /// (issue #133). <see cref="IsDirty"/> aggregates across the session cache and
        /// <see cref="TrySaveAsync"/> writes every dirty profile, so one question and one `Save`
        /// still cover the whole editor — which is what let the <em>profile switch</em> stop asking
        /// it. Its callers are now the three exits where the editor really does go away: Home,
        /// another device, and the window's close, all of them through
        /// <see cref="ConfirmCloseAsync"/>. The wording is unchanged and stays
        /// <see cref="UnsavedChangesPrompt"/>'s, so this board and the pedal ask in the same words;
        /// naming the count would fork a string the pedal shares.
        /// </para>
        /// <para>
        /// Demo mode answers true silently, and the test is explicit rather than falling out of
        /// <see cref="IsDirty"/>. A demo session is a real session over the fixture drive, so an
        /// edit really does make it dirty — but Save can never run there (03 §3.5), so the question
        /// would offer an answer that does nothing and a Discard for work that was never going
        /// anywhere. A load that produced no session reports itself clean anyway.
        /// </para>
        /// </summary>
        private async Task<bool> ConfirmDiscardingUnsavedWorkAsync()
        {
            if (IsDemoMode || !IsDirty)
            {
                return true;
            }

            // Asked once and carried: it picks the box — a read-only profile can hold edits it can
            // never write, and offering it a Save would be a question with no working answer — and
            // it is also what Yes meant, which is a different button in each of the two shapes.
            var canSave = CanSave();

            var outcome = await TryShowMessageBoxAsync(
                UnsavedChangesPrompt.Build(UnsavedChangesPrompt.KeyboardMessage, canSave, canSuppress: true))
                .ConfigureAwait(true);

            return UnsavedChangesPrompt.Interpret(outcome, canSave) switch
            {
                UnsavedChangesAnswer.Save => await TrySaveAsync().ConfigureAwait(true),
                UnsavedChangesAnswer.Discard => true,
                _ => false
            };
        }

        private bool CanSave()
        {
            // Demo mode never writes (03 §3.5), and the Advantage 360's factory profile is
            // read-only (CanSave on the session).
            return _session is not null && _session.CanSave && !IsDemoMode && !IsLoading && !IsBusy;
        }

        /// <summary>
        /// The <see cref="SaveCommand"/> target. It drops the answer on purpose: a button press has
        /// nowhere to report a failure that <see cref="TrySaveAsync"/> has already put on screen.
        /// <see cref="ConfirmCloseAsync"/> is the caller that needs it.
        /// </summary>
        private async Task SaveAsync()
        {
            await TrySaveAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Runs the save sequence off the UI thread between the loading indicator's show and hide,
        /// then reports its outcome: the violations that stopped it (04 §5.3), or the device's
        /// post-save refresh wording as a toast.
        /// <para>
        /// <b>It writes every profile the user changed, not only the one on screen</b> (issue #133).
        /// The write set is <c>CollectSessionsToSave()</c> — every opened profile that is dirty, in
        /// file order, and <b>nothing else</b>: with nothing changed anywhere, no file is written at
        /// all and the press says so in a toast. With <b>more than one</b> in the set,
        /// validation runs as a pre-pass over all of them, so a rejected profile 5 cannot leave
        /// profiles 1 and 3 already on the drive; with one, the sequence is exactly what it always
        /// was and Core's own gate is the only one (see <c>BuildPreflightViolationMessage</c>). A
        /// profile nobody opened is never read and never written, because it has no session at all.
        /// </para>
        /// <para>
        /// <b>True means every profile in the set is on the drive</b>, and nothing else does. There
        /// are three ways not to get there — a session that cannot be written right now, a throw,
        /// and validation stopping the write — and the unsaved-changes guard has to tell all three
        /// apart from success, because letting a navigation through after any of them would
        /// discard the work the user asked to keep.
        /// </para>
        /// </summary>
        private async Task<bool> TrySaveAsync()
        {
            if (_session is null || !CanSave())
            {
                return false;
            }

            // INVARIANT 31's two halves, joined here because they live in the two types that own
            // them: every opened profile that is dirty, plus every one carrying a macro rename no
            // session can see (a name rides app_settings.txt, so the profile re-serializes
            // identically). Empty is a legitimate answer, handled just below.
            var sessions = _sessionCache.CollectSessionsToSave(_macroNames.RenamedProfiles);

            if (sessions.Count == 0)
            {
                // Nothing changed, so nothing is written — not even the profile on screen. Said out
                // loud rather than left as a dead press: the button is deliberately still live (the
                // amber is the dirty signal, and a Save greyed out beside work that looks unsaved is
                // worse than one that tells you there was nothing to do).
                _notifications.ShowToast(new ToastRequest
                {
                    Title = SaveTitle,
                    Message = NothingToSaveMessage
                });

                return false;
            }

            CancelRemap();
            CancelCopyKey();
            DeactivateInspector();

            // Before a single byte is written, and over the whole set — see
            // BuildPreflightViolationMessage.
            if (ProfileSessionCache.BuildPreflightViolationMessage(sessions) is { } rejection)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = SaveTitle,
                    Message = rejection,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            List<ProfileSaveOutcome>? results = null;
            Exception? error = null;

            IsBusy = true;

            try
            {
                // Shown inside the try, and hidden after IsBusy is cleared below: both calls fan
                // out to the overlay, and a failure in either must not leave the flag stuck — it
                // disables Save and every editing command for as long as the editor is open.
                _notifications.ShowLoading(SavingCaption);

                results = await Task.Run(() => ProfileSessionCache.SaveAll(sessions)).ConfigureAwait(true);
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

                return false;
            }

            if (ProfileSessionCache.BuildRejectedSaveMessage(results!) is { } refused)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = SaveTitle,
                    Message = refused,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            // The macro names go to app_settings.txt now, and only now: a rename is part of a
            // session's dirty model and reaches the drive when the profile does. It is written
            // AFTER the layouts landed, so a save that Core rejected cannot leave the file naming
            // macros the drive does not have (KeyboardEditorViewModel.MacroNames.cs).
            PersistMacroNames();

            // Every profile in the set is on the drive, and each of them moved its own baseline to
            // the lines it wrote — so this is a re-read rather than an assertion, and it is the
            // only honest one now that a press of Save may have written some of the held profiles
            // and not others. It runs AFTER PersistMacroNames, whose cleared marks it also reads.
            RefreshDirtyState();

            var message = AdvisoryStripViewModel.BuildPostSaveMessage(
                ProfileSessionCache.BuildSavedProfilesMessage(results!),
                Advisories.Total);

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

            return true;
        }

        /// <summary>
        /// Shows a box and returns its outcome, or <b>null</b> when it could not be put on screen.
        /// Every caller that only reports something ignores the answer; the one caller that asks a
        /// question — <see cref="ConfirmCloseAsync"/> — reads null as "the user did not answer".
        /// </summary>
        private async Task<MessageBoxOutcome?> TryShowMessageBoxAsync(MessageBoxRequest request)
        {
            try
            {
                return await _notifications.ShowMessageBoxAsync(request).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // A box that cannot be put on screen (the window is already gone) must not bring
                // the app down; the editor state already carries the outcome.
                return null;
            }
        }

        /// <summary>
        /// Re-announces the rail object's two widths under the names this class publishes, so that
        /// moving the state onto <see cref="InspectorRailWidthViewModel"/> (issue #124) changed
        /// nothing a binding or a test can see. The rail raises nothing for a width it already had,
        /// which is what keeps the "one write per real change" rule intact through the forward.
        /// </summary>
        private void OnRailPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is null or nameof(InspectorRailWidthViewModel.Width))
            {
                OnPropertyChanged(nameof(InspectorRailWidth));
            }

            if (e.PropertyName is null or nameof(InspectorRailWidthViewModel.EffectiveWidth))
            {
                OnPropertyChanged(nameof(EffectiveInspectorRailWidth));
            }
        }

        /// <summary>
        /// The key inspector's half of <see cref="EffectiveInspectorRailWidth"/>: the Macro panel
        /// raises the floor to <see cref="MacroInspectorRailWidth"/> while it is showing, and nothing
        /// else in the rail can move the width. A null property name is WPF/Avalonia's "everything
        /// changed" and is taken to include this one. It <b>pushes</b> rather than announces now:
        /// <c>IsWide</c> is computed and re-raised on every mode switch, so the announcement is left
        /// to the one object that knows whether the floor really moved.
        /// </summary>
        private void OnInspectorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is null or nameof(KeyInspectorViewModel.IsWide))
            {
                Rail.IsWide = Inspector.IsWide;
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
            // Unlike the resets, its predicate reads the open TAB — so a section switch has to
            // re-ask it, which SelectTab's own call to this method already does.
            DiscardChangesCommand.NotifyCanExecuteChanged();
            CopyKeyCommand.NotifyCanExecuteChanged();
            // Its predicate reads the rail's open macro as well as the selection, so it is re-asked
            // wherever a macro is written too — OnMacroInspectorAssigned ends here for that reason.
            CopyMacroCommand.NotifyCanExecuteChanged();
            CancelCopyKeyCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            InsertSpecialActionCommand.NotifyCanExecuteChanged();
            OpenSearchCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            ImportCommand.NotifyCanExecuteChanged();

            // Unlike the tab strip, the profile picker's rows really do come and go with the
            // editor's state: a switch is refused while a save or a load is running and while a
            // feature panel is open, and all three of those flags land here.
            SelectProfileCommand.NotifyCanExecuteChanged();
        }

        // The four host interfaces the collaborators split out by issues #115 and #154 read this
        // editor through: ResetScopeCoordinator's, MacroInsertionHost's, EditorKeystrokeRouter's and
        // EditorSelection's. Every other member each of them names is already public on this class
        // and satisfies the interface as it stands; the twenty-four below are implemented
        // EXPLICITLY, because a split must not turn a private method of the editor into part of its
        // public surface.

        /// <inheritdoc/>
        void IResetScopeHost.CancelRemap()
        {
            CancelRemap();
        }

        /// <inheritdoc/>
        void IResetScopeHost.DeactivateInspector()
        {
            DeactivateInspector();
        }

        /// <inheritdoc/>
        void IResetScopeHost.RefreshCounters()
        {
            RefreshCounters();
        }

        /// <inheritdoc/>
        void IMacroInsertionHost.FocusRemapSearch()
        {
            _remapPanel.FocusSearch();
        }

        /// <inheritdoc/>
        void IMacroInsertionHost.RefreshCounters()
        {
            RefreshCounters();
        }

        /// <inheritdoc/>
        void IEditorKeystrokeHost.CancelCopyKey()
        {
            CancelCopyKey();
        }

        /// <inheritdoc/>
        void IEditorKeystrokeHost.RefreshCounters()
        {
            RefreshCounters();
        }

        /// <inheritdoc/>
        void IEditorKeystrokeHost.NotifyCommands()
        {
            NotifyCommands();
        }

        /// <inheritdoc/>
        void IEditorKeystrokeHost.OnListeningChanged()
        {
            // Exactly what the ListeningKey setter raised before the state moved out, in exactly
            // that order: the property, its derived flag, then every editing command. Nothing more
            // — IsCaptureActive was never raised from here and adding it would be a behaviour
            // change wearing tidiness as a disguise.
            OnPropertyChanged(nameof(ListeningKey));
            OnPropertyChanged(nameof(IsListening));

            NotifyCommands();
        }

        /// <inheritdoc/>
        void IEditorKeystrokeHost.OnCaptureActiveChanged()
        {
            OnPropertyChanged(nameof(IsCaptureActive));
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.OpenInspector()
        {
            Inspector.Open();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.RefreshInspector()
        {
            RefreshInspector();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.DeactivateInspector()
        {
            DeactivateInspector();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.BeginRemap()
        {
            // The knot of issue #154: the click contract's second branch reaches the remap state
            // machine here, because EditorSelection never sees EditorKeystrokeRouter.
            BeginRemap();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.CancelRemap()
        {
            CancelRemap();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.CancelCopyKey()
        {
            CancelCopyKey();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.CompleteCopyKey(KeyboardKeyViewModel target)
        {
            CompleteCopyKey(target);
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.RefreshAdvisorySummary()
        {
            RefreshAdvisorySummary();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.RefreshLegend()
        {
            RefreshLegend();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.NotifyCommands()
        {
            NotifyCommands();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.OnLayersChanged()
        {
            // Exactly what the Layers setter raised before the state moved out: the name and
            // nothing else. It re-asked no command, because the load that writes it runs
            // RefreshCounters and a SelectLayer of its own straight afterwards.
            OnPropertyChanged(nameof(Layers));
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.OnSelectedLayerChanged()
        {
            OnPropertyChanged(nameof(SelectedLayer));

            NotifyCommands();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.OnSelectedKeyChanged()
        {
            OnPropertyChanged(nameof(SelectedKey));

            NotifyCommands();
        }

        /// <inheritdoc/>
        void IEditorSelectionHost.OnTabChanged()
        {
            // The property name only. SelectTab re-asks the commands itself, at the end and
            // unconditionally, exactly as it did while the field lived here.
            OnPropertyChanged(nameof(SelectedTab));
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

            // The router's two halves are run apart, at the two points its own members always sat
            // at: coming off the capture service first, and leaving listening state last, after the
            // rail and the overlay host have handed the service back. EditorKeystrokeRouter.Dispose
            // is the same pair for a caller with no such sequence to keep.
            _keystrokes.Detach();

            Lighting.ModelChanged -= _lightingChangedHandler;

            // The preferences store belongs to the device session, which outlives this editor, so
            // the strip has to come off it here or a closed editor keeps being re-read.
            AdvisoryStrip.Dispose();

            // Before DetachInspector, which closes the rail: a mode change on the way out would
            // otherwise raise a width notification into an editor that is being torn down.
            Inspector.PropertyChanged -= _inspectorPropertyChangedHandler;

            DetachInspector();

            _overlays.Close();
            _overlays.ActiveChanged -= _activeOverlayChangedHandler;

            _keystrokes.StopListening();
            CancelCopyKey();

            // Last, and the only path that lets a session go: since issue #133 every profile the
            // user opened is still held (ProfileSessionCache), so closing the editor is what
            // releases all of them — with whatever they still carry unsaved, which is exactly what
            // ConfirmCloseAsync asked about on the way here.
            _sessionCache.Dispose();
        }

        /// <summary>What one load attempt produced: a session (or none, in demo mode), a model, and a failure.</summary>
        private sealed record LoadOutcome
        {
            public IProfileSession? Session { get; init; }

            public KeyboardLayout? Layout { get; init; }

            public Exception? Error { get; init; }

            /// <summary>
            /// Whether this profile came out of the editor's session cache rather than off the
            /// drive. It changes exactly one thing in <see cref="Apply"/> — the stored macro names
            /// are not re-stamped — and that one thing is the difference between a switch that keeps
            /// the user's renames and one that wipes them.
            /// </summary>
            public bool IsCacheHit { get; init; }
        }
    }
}
