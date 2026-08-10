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
        /// What the reset prompts call the profile when there is no number to name — demo mode,
        /// which reads no file and so has no <see cref="ProfileCaption"/>.
        /// </summary>
        public const string ResetLayoutFallbackScope = "this profile";

        /// <summary>
        /// The whole-profile prompt. Same shape as the layer's, and deliberately different words:
        /// the two scopes share one suppression key, so the sentence is the only thing that tells
        /// the user which of them is about to run.
        /// <para>
        /// <b>It names the profile, and says the others are safe</b> (issue #135). The command has
        /// always cleared the open profile and only that one — it calls
        /// <c>KeyboardLayout.Reset()</c> on the open session's layout and never walks the session
        /// cache — but the old wording ("every layer of this profile", affirmed by a button reading
        /// <c>Clear all layers</c>) was read as <i>every profile</i>, which is the one mistake a
        /// reset prompt must not invite. Since #133 the editor really does hold several profiles in
        /// memory at once, so the reassurance is load-bearing rather than decorative.
        /// </para>
        /// </summary>
        public static string BuildResetLayoutConfirmation(string profileCaption)
        {
            return "Do you want to clear every remap and macro on every layer of "
                   + ResolveResetScope(profileCaption)
                   + "? No other profile is touched, and nothing is written to the keyboard until you save.";
        }

        /// <summary>
        /// The affirmative of the whole-profile prompt, named after what it does (mockup 1k) and —
        /// since issue #135 — after <em>what it does it to</em>. It still answers <c>Yes</c>.
        /// </summary>
        public static string BuildResetLayoutConfirmCaption(string profileCaption)
        {
            return "Clear " + ResolveResetScope(profileCaption);
        }

        /// <summary>
        /// What the two reset strings call the thing being cleared: the profile's own caption when
        /// there is one, and <see cref="ResetLayoutFallbackScope"/> when there is not. One helper so
        /// the sentence and the button can never name different scopes.
        /// </summary>
        private static string ResolveResetScope(string profileCaption)
        {
            return string.IsNullOrWhiteSpace(profileCaption) ? ResetLayoutFallbackScope : profileCaption;
        }

        /// <summary>The way out of either reset prompt. It still answers <c>No</c>.</summary>
        public const string ResetDeclineCaption = "Cancel";

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
        /// (<see cref="EditorTabViewModel.CreateAll"/>): Layout always, Settings where
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

        /// <summary>Opens Search Keys over the macro and inserts the picked action (11 §11.6).</summary>
        public IRelayCommand InsertSpecialActionCommand { get; }

        /// <summary>
        /// ⌘F, the grammar's "focus the token search from anywhere in the editor"
        /// (docs/design/mockups.md <c>2b</c>).
        /// <para>
        /// It has somewhere to write now. On the Layout tab it puts the caret in the <b>key
        /// inspector's</b> own search field, where ↵ assigns the picked action to the selected
        /// position — which is what the accelerator was always meant to do, and could not before the
        /// rail existed. With the rail on its Macro panel it <b>is</b> the insertion picker
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
        private readonly EventHandler _lightingChangedHandler;
        private readonly PropertyChangedEventHandler _inspectorPropertyChangedHandler;
        private IProfileSession? _session;
        private IReadOnlyList<KeyboardLayerViewModel> _layers = [];
        private IReadOnlyList<string> _invalidLineMessages = [];
        private KeyboardLayerViewModel? _selectedLayer;
        private KeyboardKeyViewModel? _selectedKey;
        private KeyboardKeyViewModel? _listeningKey;
        private KeyboardLayout? _layout;
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
            InsertSpecialActionCommand = new RelayCommand(InsertSpecialAction, () => CanInsertIntoMacro());
            OpenSearchCommand = new RelayCommand(OpenSearch, () => CanOpenSearch());
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
            // (KeyboardEditorViewModel.Profiles.cs). Re-filing a cached session is a no-op.
            CacheSession(outcome.Session);

            Layout = outcome.Layout;
            ProfileCaption = outcome.Session is null ? string.Empty : BuildProfileCaption(outcome.Session.ProfileNumber);
            InvalidLineMessages = BuildInvalidLineMessages(outcome.Session);

            // Which profile the picker reports is read off the session that just arrived
            // (KeyboardEditorViewModel.Profiles.cs), so a first load, an import and a profile
            // switch all announce it from one place.
            RefreshSelectedProfile();

            Layers = outcome.Layout is not null && _visual is not null
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

            // Listening belongs to the keyboard picture, which only the Layout tab draws, so it is
            // ended here or the capture service keeps swallowing keystrokes behind the section the
            // user moved to. There is no second consumer to stand down: the rail is the app's one
            // recording surface and it is deactivated just below.
            CancelRemap();

            // An armed copy is finished with a click on the board; a section that does not draw
            // the board could never finish it, so it ends here too.
            CancelCopyKey();

            // The rail is drawn on the Layout tab only, so a record button armed in it must not go
            // on capturing behind a section that does not show it. The rail is stood down, not
            // closed: coming back to the Layout tab must find it as it was left.
            DeactivateInspector();

            // The property name is passed explicitly: the caller-member default would name this
            // method rather than the property the view is bound to.
            SetProperty(ref _selectedTab, tab, nameof(SelectedTab));

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

                // Nothing is selected, so the rail has nothing to be about. A selection change
                // writes nothing, so it never reaches RefreshCounters — hence the explicit push
                // here and in SelectKeyDirectly.
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
            // An armed rail panel and a listening key would fight over the same keystrokes, and an
            // open feature panel owns them outright, so neither may start a remap.
            return SelectedKey is not null
                   && SelectedKey.CanEdit
                   && !IsLoading
                   && !IsBusy
                   && !Inspector.IsRecording
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

            // A lambda, not a method group: InsertIntoOpenMacro reports "no macro is being edited"
            // with a bool this path has nothing to do with, and the panel may be gone by then.
            var insert = new Action<KeyDefinition>(key => InsertIntoOpenMacro(key));

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
        /// ⌘F. With the rail on its Macro panel this <em>is</em> the insertion picker, so the
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
        /// §11.6's insertion targets the macro the <b>key inspector</b> has open — since issue #93
        /// that is the app's one macro editor — so three things have to be true: the rail is open,
        /// it is showing its Macro panel, and the selected position really carries a macro. Without
        /// the mode test the button would stay live beside a Remap panel and the picked token would
        /// be appended to a macro the user cannot see.
        /// </summary>
        private bool CanInsertIntoMacro()
        {
            return Layout is not null
                   && Inspector.IsOpen
                   && Inspector.SelectedMode == KeyInspectorMode.Macro
                   && FindOpenMacro() is not null
                   && !IsLoading
                   && !IsBusy
                   && ActiveOverlay is null;
        }

        /// <summary>
        /// The macro the rail's Macro panel is editing, or null. It is read off the model rather than
        /// asked of the panel: the panel holds it privately, and both stores answer the same two
        /// questions the panel asks — the key's <b>active</b> slot on a slot device (which the panel
        /// normalises to the first populated one when it reads), the layer-plus-trigger entry on a
        /// flat-list one (06 §1).
        /// </summary>
        private Macro? FindOpenMacro()
        {
            if (SelectedKey is not { } key || Layout is not { } layout)
            {
                return null;
            }

            if (!layout.UsesFlatMacroList)
            {
                return key.Key.GetMacro(key.Key.ActiveMacroIndex);
            }

            var flat = layout.FindMacros(SelectedLayer?.Index ?? Macro.UnassignedIndex, key.Key.TriggerKey.Code);

            return flat.Count > 0 ? flat[0] : null;
        }

        /// <summary>
        /// Appends one key to the macro the rail has open — §11.6's <c>Search Keys (Macro)</c> hook.
        /// The write lands on the model and then goes through the editor's one refresh funnel, which
        /// is what re-reads the rail's step list, the counters, the advisories and the dirty flag;
        /// Core announces nothing on its own. False when no macro is open.
        /// </summary>
        private bool InsertIntoOpenMacro(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (FindOpenMacro() is not { } macro)
            {
                return false;
            }

            macro.AddKeystroke(new Keystroke(key));

            RefreshCounters();

            return true;
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
        /// <c>Review N</c>'s macro half: opens the anchored macro where it is edited — the board's
        /// position, on the key inspector's Macro panel. The strip's other callback, for the same
        /// reason the key half is one: the board and the rail are this class's.
        /// <para>
        /// An anchor names a <em>site</em> (layer, key, slot) while a macro is one logical thing that
        /// may fire from three of them, so a review that merely highlighted the macro could be
        /// pointing at all three at once. Landing on the anchored position is the answer that is
        /// always about the one the advisory is about.
        /// </para>
        /// </summary>
        private void SelectAnchoredMacro(AdvisoryAnchor anchor)
        {
            if (anchor.KeyIndex is not int keyIndex || anchor.LayerIndex is not int layerIndex)
            {
                return;
            }

            EditMacroAt(layerIndex, keyIndex, anchor.MacroIndex ?? MacroSites.FlatListSlot, startRecording: false);
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
            IsDirty = AnyProfileIsDirty() || HasUnsavedMacroNames;
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
            DeactivateInspector();

            layer.Layer.Reset();
            layer.RefreshFromModel();

            // KeyboardLayer.Reset clears every rule including the macro slots, so the rail is
            // sitting on macros that no longer exist. RefreshCounters rebuilds the library snapshot
            // and pushes it.
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

            if (!await ConfirmResetAsync(
                    ResetLayoutTitle,
                    BuildResetLayoutConfirmation(ProfileCaption),
                    BuildResetLayoutConfirmCaption(ProfileCaption)).ConfigureAwait(true))
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
            DeactivateInspector();

            layout.Reset();

            foreach (var layer in Layers)
            {
                layer.RefreshFromModel();
            }

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

            var sessions = CollectSessionsToSave();

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
            if (BuildPreflightViolationMessage(sessions) is { } rejection)
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

                results = await Task.Run(() => SaveAll(sessions)).ConfigureAwait(true);
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

            if (BuildRejectedSaveMessage(results!) is { } refused)
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
                BuildSavedProfilesMessage(results!),
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

            // Before DetachInspector, which closes the rail: a mode change on the way out would
            // otherwise raise a width notification into an editor that is being torn down.
            Inspector.PropertyChanged -= _inspectorPropertyChangedHandler;

            DetachInspector();

            _overlays.Close();
            _overlays.ActiveChanged -= _activeOverlayChangedHandler;

            StopListening();
            CancelCopyKey();

            // Last, and the only path that lets a session go: since issue #133 every profile the
            // user opened is still held (KeyboardEditorViewModel.Profiles.cs), so closing the
            // editor is what releases all of them — with whatever they still carry unsaved, which
            // is exactly what ConfirmCloseAsync asked about on the way here.
            DisposeSessions();
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
