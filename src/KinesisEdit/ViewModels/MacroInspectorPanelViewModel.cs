using System.Globalization;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The key inspector's <b>Macro</b> panel (mockup <c>2i</c>): "selecting a key edits its macro
    /// right here — the Macros tab is a library, not the editor". It is the surface issue #93 built
    /// in place of the rail's old bridge out to the Macros tab, and it is where
    /// specs/11-feature-dialogs.md §11.3's <c>Macro Timing Delays</c> dialog now lives
    /// (<see cref="MacroInspectorStepsViewModel"/>).
    ///
    /// <para><b>There is no Assign button, deliberately.</b> Editing in place means the first thing
    /// the user records <em>is</em> the macro: <see cref="EnsureMacro"/> creates it, stamps its
    /// trigger and layer the way <c>MacroLineParser</c> does (04 §4.2), and puts it in the key's
    /// first free slot — or appends it to the Advantage 360's flat list. The old panel's
    /// draft-then-assign dance was a modal's shape, not a rail's.</para>
    ///
    /// <para><b>The name dropdown picks; it does not rename.</b> Mockup <c>2i</c> splits the two:
    /// the tab is the library ("rename, see which keys and layers fire each one, duplicate,
    /// delete") and the rail is the editor. Picking an existing name here is
    /// <c>MacroLibrary.AssignTo</c> — "assigning a named macro to a second key is a pick from the
    /// inspector's own dropdown" — and renaming is
    /// <c>KeyboardEditorViewModel.RenameMacro</c>, which this panel never calls.</para>
    ///
    /// <para><b>It records through the editor, never around it.</b> The editor owns the single
    /// subscription to <c>IKeystrokeCaptureService</c> and routes one keystroke to one consumer;
    /// this panel says it wants one through <see cref="IsRecording"/> /
    /// <see cref="IKeystrokeSink.WantsKeystrokes"/> and takes it through
    /// <see cref="ReceiveKeystroke"/>. Saying so honestly is load bearing — the editor folds
    /// <see cref="IsRecording"/> into <c>IsCaptureActive</c>, which is what stops ⌘S firing on the
    /// keypress the user meant as a macro step.</para>
    ///
    /// <para><b>Every limit comes from the device</b> (<see cref="MacroCapability"/>): the slots the
    /// dialect actually <em>writes</em>, the co-trigger cap the serializer keeps, the speed and
    /// repeat ranges, the per-macro and per-layout budgets, and the firmware-resolved macro count.
    /// Breaching one is <b>reported and never refused</b> — the meters go amber and the profile
    /// still saves (docs/design/README.md), with <c>KeyboardLayout.Validate</c> as the backstop.</para>
    ///
    /// <para><b><see cref="Refresh"/> re-reads and never writes.</b> It runs after somebody else's
    /// mutation, so a panel that wrote from it would write on every counter refresh — and forever,
    /// because the write ends in another refresh. It must also survive a null key and the key it
    /// already had.</para>
    ///
    /// <para><b>It is the one panel that reverts itself</b> (<see cref="TryRevert"/>, issue #122).
    /// The rail's <c>Revert key</c> runs the editor's <c>ClearRemap()</c>, which touches only the
    /// remap and was therefore a no-op here; this panel takes first refusal and puts back the
    /// <see cref="MacroKeySnapshot"/> it read when the inspector was pointed at the position. The
    /// baseline is taken in <see cref="Refresh"/> and <b>only when the key identity changed</b> —
    /// an unconditional snapshot would be overwritten by the very edit the user wants undone.</para>
    ///
    /// <para><b>It has a second way in, which needs no keypress at all</b> — the chord composer of
    /// <c>MacroInspectorPanelViewModel.Composer.cs</c> (issue #128). Split into a partial for the
    /// same reason <c>KeyboardEditorViewModel.Inspector.cs</c> was: this file is already long and
    /// docs/guides/Coding Conventions.md forbids growing it into a god class.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel : KeyInspectorPanelViewModel, IKeystrokeSink
    {
        /// <summary>The panel's own name, and its mode tab's caption.</summary>
        public const string PanelTitle = KeyInspectorTabViewModel.MacroCaption;

        /// <summary>The section label over the name dropdown. This app's wording.</summary>
        public const string NameLabel = "MACRO";

        /// <summary>
        /// What a named macro promises, verbatim from mockup <c>2i</c>. Shown only once the macro
        /// really carries a name, because that is what makes it pickable for a second key.
        /// </summary>
        public const string ReuseNote = "Named, so it can be picked for another key from this same dropdown.";

        /// <summary>Opens the "where else does this fire" line (mockup <c>2i</c>: "Also on [f7] · Fn").</summary>
        public const string AlsoOnPrefix = "Also on ";

        /// <summary>Between a site's token and its layer in that line.</summary>
        public const string SiteSeparator = " · ";

        /// <summary>Between two sites in that line.</summary>
        public const string SiteJoin = ", ";

        /// <summary>The record button at rest (mockup <c>2i</c>: <c>● Record</c>; the dot is geometry).</summary>
        public const string RecordCaption = "Record";

        /// <summary>The record button while capture is armed.</summary>
        public const string RecordingCaption = "Stop";

        /// <summary>
        /// The live capture banner; <c>{0}</c> is the step the next keystroke lands in, and it moves
        /// as the macro grows.
        /// <para>
        /// <b>A deliberate deviation from mockup <c>2i</c>, which ends the sentence "Esc stops."</b>
        /// Escape is a remappable position like any other, so a macro has to be able to record one
        /// (issue #122, AC 2) — and a banner that promises Escape as the way out while the keystroke
        /// is being appended as a step is the very lie this panel's capture rules exist to avoid.
        /// The replacement names what really ends a recording: the Stop button, or a click anywhere
        /// else in the app.
        /// </para>
        /// </summary>
        public const string RecordingBannerFormat =
            "Recording into step {0} — your typing goes here, not into the app. Click Stop, or anywhere else, to finish.";

        /// <summary>What the capture actually does with what it hears, stated in the panel (2i).</summary>
        public const string CaptureRule =
            "Arrows = press/release. A bare modifier records as tap. Search and shortcuts are suspended until you stop.";

        /// <summary>
        /// The one thing recording cannot do, said plainly beside the rule that says what it can
        /// (issue #128). A chord the window system keeps — <c>Ctrl+1</c> on macOS, or anything a
        /// hotkey utility has registered — is consumed <b>above</b> the application: it is never
        /// delivered to the window, so no handler and no local event monitor can see it or swallow
        /// it, and no amount of capture work would change that (docs/app/keystroke-capture.md,
        /// "Permissions and platform reach"). The sentence therefore does not apologise — it points
        /// at <see cref="ComposeChordCaption"/>, which authors exactly those chords without pressing
        /// them.
        /// <para>
        /// Drawn as its own line rather than folded into <see cref="CaptureRule"/>, which is mockup
        /// <c>2i</c>'s wording verbatim and pinned as such. Both lines sit under the record banner,
        /// so the sentence is on screen at the moment the user discovers the limitation.
        /// </para>
        /// </summary>
        public const string OsReservedNote =
            "Some chords never reach this app at all — Ctrl+1, and anything a hotkey utility has claimed, "
            + "are taken by the system first. Build those with Compose chord below.";

        /// <summary>Label of the playback-speed meter, verbatim from mockup <c>2i</c>.</summary>
        public const string SpeedMeterLabel = "Playback speed";

        /// <summary>Label of the per-macro budget meter, verbatim from mockup <c>2i</c>.</summary>
        public const string MacroLengthMeterLabel = "this macro";

        /// <summary>Label of the per-layout budget meter, verbatim from mockup <c>2i</c>.</summary>
        public const string LayoutKeystrokeMeterLabel = "layout keystrokes";

        /// <summary>Label of the repeat control. This app's wording; <c>2i</c> draws only speed.</summary>
        public const string RepeatLabel = "Repeat";

        /// <summary>Section label over the six co-trigger latches (06 §5). This app's wording.</summary>
        public const string CoTriggersLabel = "CO-TRIGGERS";

        /// <summary>Shown on a device without macro support (<see cref="MacroCapability.IsSupported"/>).</summary>
        public const string NotSupportedMessage = "This device does not support macros.";

        /// <summary>Shown while no cap is selected — the rail is closed then, so it is a guard.</summary>
        public const string NoSelectionMessage = "Select a key on the keyboard to give it a macro.";

        /// <summary>specs/02-devices.md, verbatim: a macro aimed at a position that cannot hold one.</summary>
        public const string RestrictedKeyMessage = "You cannot assign a macro to a modifier key";

        /// <summary>Refusal when every macro slot of the trigger key is taken (06 §1).</summary>
        public const string NoFreeSlotMessage = "Every macro slot of this key is taken.";

        /// <summary>Refusal when the profile already holds its macro count (06 §6).</summary>
        public const string MacroCountLimitMessageFormat = "This profile already holds its maximum of {0} macros.";

        /// <summary>Refusal when the co-trigger cap of 06 §5 is already reached.</summary>
        public const string CoTriggerLimitMessageFormat = "A macro can hold at most {0} co-triggers.";

        /// <summary>Builds the macro-count refusal for <paramref name="limit"/> macros (06 §6).</summary>
        public static string BuildMacroCountLimitMessage(int limit)
        {
            return string.Format(CultureInfo.InvariantCulture, MacroCountLimitMessageFormat, limit);
        }

        /// <summary>Builds the co-trigger refusal for <paramref name="limit"/> slots (06 §5).</summary>
        public static string BuildCoTriggerLimitMessage(int limit)
        {
            return string.Format(CultureInfo.InvariantCulture, CoTriggerLimitMessageFormat, limit);
        }

        /// <summary>Builds the capture banner for the step the next keystroke lands in.</summary>
        public static string BuildRecordingBanner(string stepNumber)
        {
            return string.Format(CultureInfo.InvariantCulture, RecordingBannerFormat, stepNumber);
        }

        /// <inheritdoc />
        public override KeyInspectorMode Mode => KeyInspectorMode.Macro;

        /// <inheritdoc />
        public override string Title => PanelTitle;

        /// <inheritdoc />
        public override bool IsAvailable => _unavailableReason.Length == 0;

        /// <inheritdoc />
        public override string UnavailableReason => _unavailableReason;

        /// <inheritdoc />
        public override bool IsRecording => _isRecording;

        /// <summary>
        /// The rail widens from 268 px to 300 px while this panel is showing
        /// (docs/design/handoff.md § Geometry: "inspector rail 268px on Layout, 300px on the
        /// macro-editing variant"). It is a fact about the panel rather than about the rail, so the
        /// rail reads it off whichever panel is active.
        /// </summary>
        public override bool WantsWideRail => true;

        /// <summary>The step editor, and §11.3's delays inside it.</summary>
        public MacroInspectorStepsViewModel Steps { get; }

        /// <summary>Every logical macro of the profile, offered for this key (mockup <c>2i</c>).</summary>
        public IReadOnlyList<MacroNameOptionViewModel> NameOptions
        {
            get => _nameOptions;
            private set => SetProperty(ref _nameOptions, value);
        }

        /// <summary>
        /// The dropdown's current row. Setting it to another macro's row <b>assigns that macro to
        /// this key</b> (<c>MacroLibrary.AssignTo</c>); the placeholder row is refused, because a
        /// dropdown must never be the thing that deletes a macro.
        /// </summary>
        public MacroNameOptionViewModel? SelectedName
        {
            get => _selectedName;
            set => PickName(value);
        }

        /// <summary>Whether the macro under edit carries a name a second key could pick.</summary>
        public bool IsNamed
        {
            get => _isNamed;
            private set => SetProperty(ref _isNamed, value);
        }

        /// <summary>"Also on [f7] · Fn" — every other place this macro fires from, or empty.</summary>
        public string AlsoOnText
        {
            get => _alsoOnText;
            private set
            {
                if (SetProperty(ref _alsoOnText, value))
                {
                    OnPropertyChanged(nameof(HasAlsoOnText));
                }
            }
        }

        /// <summary>Whether this macro fires from anywhere but the selected position.</summary>
        public bool HasAlsoOnText => _alsoOnText.Length > 0;

        /// <summary>The banner shown while capture is armed, naming the step being recorded into.</summary>
        public string RecordingBanner => BuildRecordingBanner(Steps.NextStepNumberText);

        /// <summary>The record button's caption, which moves with <see cref="IsRecording"/>.</summary>
        public string RecordCommandCaption => _isRecording ? RecordingCaption : RecordCaption;

        /// <summary>The playback-speed meter, <c>3 / 5</c> (mockup <c>2i</c>).</summary>
        public MacroMeterViewModel SpeedMeter { get; }

        /// <summary>The per-macro budget meter, <c>128 / 500</c> (06 §6).</summary>
        public MacroMeterViewModel MacroLengthMeter { get; }

        /// <summary>The per-layout budget meter, <c>5 140 / 7 200</c> (04 §5.3).</summary>
        public MacroMeterViewModel LayoutKeystrokeMeter { get; }

        /// <summary>Playback speed of the macro under edit, clamped to the device's range (06 §4).</summary>
        public int Speed
        {
            get => _speed;
            set => ApplySpeed(value);
        }

        /// <summary>Lowest playback speed the device accepts (06 §4).</summary>
        public int SpeedMinimum => _capability.Speed?.Minimum ?? 0;

        /// <summary>Highest playback speed the device accepts (06 §4).</summary>
        public int SpeedMaximum => _capability.Speed?.Maximum ?? 0;

        /// <summary>Whether the device has a per-macro speed setting at all.</summary>
        public bool HasSpeed => _capability.Speed is not null;

        /// <summary>Repeat/multiplay factor of the macro under edit, clamped to the range (06 §4).</summary>
        public int Repeat
        {
            get => _repeat;
            set => ApplyRepeat(value);
        }

        /// <summary>Lowest repeat factor the device accepts (06 §4).</summary>
        public int RepeatMinimum => _capability.Repeat?.Minimum ?? 0;

        /// <summary>Highest repeat factor the device accepts (06 §4).</summary>
        public int RepeatMaximum => _capability.Repeat?.Maximum ?? 0;

        /// <summary>
        /// Whether the device has a per-macro repeat setting <b>the file keeps</b>. The Advantage2
        /// has a repeat range in the model but its serializer writes no <c>{xN}</c> token at all
        /// (06 §3), so the control is absent there rather than offering a value the next save
        /// discards.
        /// </summary>
        public bool HasRepeat => _capability.Repeat is not null && _capability.PersistsRepeat;

        /// <summary>The six Left/Right Shift, Ctrl and Alt latches of 06 §5.</summary>
        public IReadOnlyList<MacroCoTriggerViewModel> CoTriggers { get; }

        /// <summary>How many co-triggers the dialect actually writes (06 §2.1, §3).</summary>
        public int MaxCoTriggers { get; }

        /// <summary>Whether the device has co-triggers at all.</summary>
        public bool HasCoTriggers => MaxCoTriggers > 0;

        /// <summary>The panel's refusal or status line, or an empty string.</summary>
        public string Message
        {
            get => _message;
            private set
            {
                if (SetProperty(ref _message, value))
                {
                    OnPropertyChanged(nameof(HasMessage));
                }
            }
        }

        /// <summary>Whether the panel has something to say about the last action.</summary>
        public bool HasMessage => _message.Length > 0;

        /// <summary>Arms capture for the next physical keystroke, or stands it down again.</summary>
        public IRelayCommand RecordCommand { get; }

        /// <summary>
        /// The trailing <c>＋ insert step</c> row. It is the <em>same</em> action as <c>● Record</c>:
        /// a step's content is a keystroke, and recording one is how it is made. Two affordances for
        /// one action, because the mock draws both — the button over the list and the row at the end
        /// of it — and the banner names the step either of them records into.
        /// </summary>
        public IRelayCommand InsertStepCommand { get; }

        /// <summary>Turns one co-trigger on or off, enforcing the device's cap (06 §5).</summary>
        public IRelayCommand<MacroCoTriggerViewModel> ToggleCoTriggerCommand { get; }

        /// <summary>
        /// Raised after the panel wrote to the profile's macros, so the editor can run its one
        /// refresh funnel — Core announces nothing, so nothing downstream notices otherwise.
        /// </summary>
        public event EventHandler? Assigned;

        private readonly MacroCapability _capability;
        private readonly TokenDialect _dialect;
        private readonly Func<MacroLibrary?> _resolveLibrary;
        private readonly int? _maxMacroCount;
        private readonly bool _usesMacroSlots;
        private IReadOnlyList<MacroNameOptionViewModel> _nameOptions = [];
        private MacroNameOptionViewModel? _selectedName;
        private KeyboardKeyViewModel? _key;
        private KeyboardLayerViewModel? _layer;
        private KeyboardLayout? _layout;
        private Macro? _macro;

        /// <summary>
        /// What the position's macros looked like when the inspector was pointed at it — see
        /// <see cref="TryRevert"/>. Null while nothing is selected.
        /// </summary>
        private MacroKeySnapshot? _snapshot;

        private string _unavailableReason = NoSelectionMessage;
        private string _alsoOnText = string.Empty;
        private string _message = string.Empty;
        private int _speed;
        private int _repeat;
        private bool _isNamed;
        private bool _isRecording;

        /// <summary>
        /// Builds the panel for one open device. Everything it needs at construction is a
        /// <em>device</em> fact — the macro capability, the firmware gate behind §11.3's delays and
        /// the token dialect (<c>KeyboardLayout.DialectFor</c>) — which is what lets the rail be
        /// built once, eagerly, before any profile has been read.
        /// <para>
        /// <paramref name="resolveLibrary"/> is how the panel reaches the editor's <b>one</b>
        /// <see cref="MacroLibrary"/>. It is a function rather than a stored instance because the
        /// library arrives with the profile and is replaced by a load or an import, and two
        /// libraries over one layout would be two sources of truth.
        /// </para>
        /// <para>
        /// <paramref name="recentTokens"/> is the editor's <b>one</b> session history, shared with
        /// the rail's Remap picker and the macro-insertion modal so the chord composer's
        /// <c>Recent</c> chip offers what the user has just been assigning. Optional, because a
        /// panel built without one keeps a history of its own — which is what a test wants.
        /// </para>
        /// </summary>
        public MacroInspectorPanelViewModel(
            DeviceSnapshot device,
            IUrlLauncher urlLauncher,
            Func<MacroLibrary?> resolveLibrary,
            RecentTokenStore? recentTokens = null)
        {
            ArgumentNullException.ThrowIfNull(device);

            _resolveLibrary = resolveLibrary ?? throw new ArgumentNullException(nameof(resolveLibrary));
            _capability = device.Device.Macros;
            _dialect = KeyboardLayout.DialectFor(device.DeviceId);
            _maxMacroCount = MacroLimits.ResolveMaxMacroCount(device);
            _usesMacroSlots = _capability.PersistedSlotsPerKey is > 0 && !_capability.UsesFlatMacroList;

            MaxCoTriggers = _capability.PersistedCoTriggersPerMacro ?? _capability.MaxCoTriggersPerMacro ?? 0;

            Steps = new MacroInspectorStepsViewModel(device.DeviceId, device.Firmware, urlLauncher);
            CoTriggers = MacroCoTriggerViewModel.CreateAll(_dialect);

            SpeedMeter = new MacroMeterViewModel(SpeedMeterLabel);
            MacroLengthMeter = new MacroMeterViewModel(MacroLengthMeterLabel);
            LayoutKeystrokeMeter = new MacroMeterViewModel(LayoutKeystrokeMeterLabel);

            RecordCommand = new RelayCommand(ToggleRecording, CanRecord);
            InsertStepCommand = new RelayCommand(StartRecording, CanRecord);
            ToggleCoTriggerCommand = new RelayCommand<MacroCoTriggerViewModel>(ToggleCoTrigger, _ => IsAvailable);

            // The step editor writes into the macro directly; the editor's funnel is what everything
            // else hangs off, so one hop is all this needs.
            Steps.Changed += (_, _) => OnMacroWritten();

            CreateComposer(_dialect, recentTokens);
        }

        /// <inheritdoc />
        public override void Refresh(
            KeyboardKeyViewModel? key,
            KeyboardLayerViewModel? layer,
            KeyboardLayout? layout,
            EditorAdvisories advisories)
        {
            var isNewKey = !ReferenceEquals(key, _key);

            _key = key;
            _layer = layer;
            _layout = layout;

            if (isNewKey)
            {
                // Anything half-recorded belongs to the position it was started on.
                StopRecording();

                Message = string.Empty;

                // The revert baseline, and ONLY on a new key: Refresh runs after every edit of
                // every path, so re-reading it here on a same-key refresh would replace the
                // "before" with the "after" the moment the user typed a step.
                TakeSnapshot();
            }

            SetUnavailableReason(EvaluateUnavailableReason());

            ReadFromModel();

            RecordCommand.NotifyCanExecuteChanged();
            InsertStepCommand.NotifyCanExecuteChanged();
            ToggleCoTriggerCommand.NotifyCanExecuteChanged();

            // The composer is about the position the rail is pointed at, so a new one closes it and
            // every refresh re-asks whether it may be opened at all.
            RefreshComposer(isNewKey);
        }

        /// <summary>
        /// Puts the position's macros back the way this panel found them (issue #122, AC 1): the
        /// steps, the active slot, the speed, the repeat factor and the co-triggers of every slot
        /// the position carried when the inspector was pointed at it. A position that carried
        /// nothing then is left carrying nothing.
        ///
        /// <para><b>It is idempotent.</b> Restoring clones out of the baseline rather than handing
        /// it over, and the baseline is <em>never</em> re-taken here — so the second Revert lands on
        /// the same state as the first.</para>
        ///
        /// <para><b>What the baseline outlives.</b> It is a fact about the <em>selection</em>, so it
        /// survives <see cref="Deactivate"/> (a mode switch stands capture down; it does not move
        /// the position) and it survives a save (saving writes the profile out, it does not change
        /// what this key held when it was clicked). It is replaced when the selection moves to
        /// another position — which a load or an import does too, because both rebuild every
        /// <see cref="KeyboardKeyViewModel"/> and the next <see cref="Refresh"/> therefore arrives
        /// with a new key.</para>
        ///
        /// <para>It refuses — and lets the footer fall through to the editor's <c>ResetKeyCommand</c>
        /// — whenever there is nothing of this panel's to revert: no selection, no baseline, or a
        /// position/device that cannot carry a macro at all.</para>
        /// </summary>
        public override bool TryRevert()
        {
            if (_snapshot is not { } snapshot || !IsAvailable || _key is not { } key)
            {
                return false;
            }

            snapshot.RestoreTo(key.Key, _layout);

            Message = string.Empty;

            ReadFromModel();

            OnMacroWritten();

            return true;
        }

        /// <inheritdoc />
        public override bool IsRecordingControl(ICommand? command)
        {
            // Both, because both arm capture: `● Record` toggles it and `＋ insert step` starts it.
            return ReferenceEquals(command, RecordCommand) || ReferenceEquals(command, InsertStepCommand);
        }

        /// <inheritdoc />
        public override void Deactivate()
        {
            StopRecording();
        }

        /// <inheritdoc />
        bool IKeystrokeSink.WantsKeystrokes => _isRecording;

        /// <summary>
        /// Appends one captured keystroke to the macro under edit, folding the modifiers held at
        /// that moment into the step (05 §5.1). The macro is <b>created on the first keystroke</b>
        /// if the position had none — that is what "edited in place" means here.
        /// </summary>
        public void ReceiveKeystroke(CapturedKeystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            if (!_isRecording)
            {
                return;
            }

            TryAppendKeystroke(BuildKeystroke(keystroke));
        }

        /// <summary>
        /// Appends one already-built keystroke to the macro under edit, creating the macro if the
        /// position had none. It is the single write path shared by the two ways a step is made —
        /// the captured keypress of <see cref="ReceiveKeystroke"/> and the composed chord of
        /// <c>InsertChord</c> (issue #128) — so a step authored from the composer is indistinguishable
        /// from a recorded one the moment it lands. False when there is no macro to write into and
        /// none could be created (the count limit of 06 §6, or a full slot strip), which
        /// <see cref="EnsureMacro"/> has already reported.
        /// </summary>
        private bool TryAppendKeystroke(Keystroke keystroke)
        {
            if (EnsureMacro() is not { } macro)
            {
                return false;
            }

            macro.AddKeystroke(keystroke);

            Steps.RefreshFromModel();

            OnMacroWritten();

            return true;
        }

        /// <summary>
        /// Folds the modifiers held at the moment of the strike into the step (05 §5.1). A captured
        /// keystroke whose key is itself a modifier carries none by design, and
        /// <see cref="Keystroke"/> drops any that were offered anyway.
        /// </summary>
        private static Keystroke BuildKeystroke(CapturedKeystroke captured)
        {
            var modifiers = MacroModifiers.None;

            foreach (var held in captured.HeldModifiers)
            {
                if (MacroModifierCodes.TryFromKeyCode(held.Code, out var modifier))
                {
                    modifiers |= modifier;
                }
            }

            return new Keystroke(captured.Key, modifiers);
        }

        /// <summary>
        /// Re-reads everything the panel derives from the model, and writes nothing. It is the body
        /// of <see cref="Refresh"/> and it is also what <see cref="TryRevert"/> runs after restoring
        /// — a revert that left the step list and the meters showing the state it had just undone
        /// would be a revert the user could not see.
        /// </summary>
        private void ReadFromModel()
        {
            _macro = ReadMacro();

            Steps.Load(_macro);

            LoadSpeedAndRepeat();
            RefreshCoTriggers();
            RefreshNames();
            RefreshMeters();

            OnPropertyChanged(nameof(RecordingBanner));
        }

        /// <summary>
        /// Reads the revert baseline off the position now selected, or drops it when there is none.
        /// Called from <see cref="Refresh"/> on a key change only; see <see cref="TryRevert"/>.
        /// </summary>
        private void TakeSnapshot()
        {
            _snapshot = _key is { } key
                ? MacroKeySnapshot.Capture(key.Key, _layout, _layer?.Index ?? Macro.UnassignedIndex)
                : null;
        }

        /// <summary>
        /// Which macro the selected position is carrying, or null. The key's own
        /// <c>ActiveMacroIndex</c> wins when it points at a populated slot — that is what a slot
        /// click on the Macros tab moved — and otherwise the first populated slot is opened, so
        /// selecting a cap that carries a macro always shows it rather than an empty editor.
        /// </summary>
        private Macro? ReadMacro()
        {
            if (!IsAvailable || _key is not { } key || _layout is null)
            {
                return null;
            }

            if (!_usesMacroSlots)
            {
                var flat = _layout.FindMacros(_layer?.Index ?? Macro.UnassignedIndex, key.Key.TriggerKey.Code);

                return flat.Count > 0 ? flat[0] : null;
            }

            if (key.Key.GetMacro(key.Key.ActiveMacroIndex) is { } active)
            {
                return active;
            }

            var slots = _capability.PersistedSlotsPerKey ?? Macro.MaxMacroIndex;

            for (var slot = Macro.MinMacroIndex; slot <= slots; slot++)
            {
                if (key.Key.GetMacro(slot) is { } macro)
                {
                    key.Key.ActiveMacroIndex = slot;

                    return macro;
                }
            }

            return null;
        }

        /// <summary>
        /// The macro the next write goes into, creating and assigning it when the position has none.
        /// The only path in this panel that adds a macro to the profile, so it is the only place the
        /// macro-count limit of 06 §6 is asked about — and a refusal reports itself and writes
        /// nothing.
        /// </summary>
        private Macro? EnsureMacro()
        {
            if (_macro is not null)
            {
                return _macro;
            }

            if (!IsAvailable || _key is not { } key || _layout is not { } layout)
            {
                return null;
            }

            if (_maxMacroCount is int limit && layout.MacroCount + 1 > limit)
            {
                Message = BuildMacroCountLimitMessage(limit);

                return null;
            }

            var macro = layout.CreateMacro();

            // Both stores stamp the trigger and the layer, exactly as MacroLineParser does before it
            // routes a parsed macro to one or the other (04 §4.2): the Gen2 list needs them to
            // serialize at all, and on the slot families they are what lets the macro find its way
            // back to this position on the next load.
            macro.TriggerKey = key.Key.TriggerKey.Code;
            macro.LayerIndex = _layer?.Index ?? Macro.UnassignedIndex;

            if (!_usesMacroSlots)
            {
                layout.AddMacro(macro);
            }
            else
            {
                var slot = key.Key.AssignMacro(macro);

                if (slot == 0)
                {
                    Message = NoFreeSlotMessage;

                    return null;
                }

                key.Key.ActiveMacroIndex = slot;
            }

            Message = string.Empty;
            _macro = macro;

            Steps.Load(macro);

            return macro;
        }

        private string EvaluateUnavailableReason()
        {
            if (!_capability.IsSupported)
            {
                return NotSupportedMessage;
            }

            if (_key is not { } key)
            {
                return NoSelectionMessage;
            }

            // 05 §5.3 marks exactly the modifier positions as unable to carry a macro, which is what
            // the legacy message names.
            return key.CanAssignMacro ? string.Empty : RestrictedKeyMessage;
        }

        private void SetUnavailableReason(string reason)
        {
            if (_unavailableReason == reason)
            {
                return;
            }

            var wasAvailable = IsAvailable;

            _unavailableReason = reason;

            OnPropertyChanged(nameof(UnavailableReason));

            if (wasAvailable != IsAvailable)
            {
                OnPropertyChanged(nameof(IsAvailable));
            }
        }

        private void LoadSpeedAndRepeat()
        {
            // Loading must not write back: the fields move, the model does not.
            SetProperty(ref _speed, _macro?.Speed ?? _capability.Speed?.Default ?? 0, nameof(Speed));
            SetProperty(ref _repeat, _macro?.RepeatFrequency ?? _capability.Repeat?.Default ?? 0, nameof(Repeat));
        }

        private void ApplySpeed(int value)
        {
            var clamped = _capability.Speed is { } range ? Math.Clamp(value, range.Minimum, range.Maximum) : value;

            if (!SetProperty(ref _speed, clamped, nameof(Speed)))
            {
                return;
            }

            RefreshMeters();

            if (EnsureMacro() is not { } macro)
            {
                return;
            }

            macro.Speed = clamped;

            OnMacroWritten();
        }

        private void ApplyRepeat(int value)
        {
            var clamped = _capability.Repeat is { } range ? Math.Clamp(value, range.Minimum, range.Maximum) : value;

            if (!SetProperty(ref _repeat, clamped, nameof(Repeat)) || EnsureMacro() is not { } macro)
            {
                return;
            }

            macro.RepeatFrequency = clamped;

            OnMacroWritten();
        }

        private bool CanRecord()
        {
            return IsAvailable && _layout is not null;
        }

        private void ToggleRecording()
        {
            if (_isRecording)
            {
                StopRecording();

                return;
            }

            StartRecording();
        }

        private void StartRecording()
        {
            if (!CanRecord())
            {
                return;
            }

            Message = string.Empty;

            SetRecording(true);
        }

        private void StopRecording()
        {
            SetRecording(false);
        }

        private void SetRecording(bool isRecording)
        {
            if (_isRecording == isRecording)
            {
                return;
            }

            _isRecording = isRecording;

            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(RecordCommandCaption));

            OnRecordingChanged();
        }

        private void ToggleCoTrigger(MacroCoTriggerViewModel? toggle)
        {
            if (toggle is null || EnsureMacro() is not { } macro)
            {
                return;
            }

            if (toggle.IsOn)
            {
                RemoveCoTrigger(macro, toggle.Key);
            }
            else
            {
                // Macro.AddCoTrigger deliberately neither de-duplicates nor refuses (06 §5), so the
                // cap is the panel's to hold; Validate() stays the backstop.
                if (macro.CoTriggerCount >= MaxCoTriggers)
                {
                    Message = BuildCoTriggerLimitMessage(MaxCoTriggers);

                    return;
                }

                macro.AddCoTrigger(toggle.Key);

                Message = string.Empty;
            }

            RefreshCoTriggers();
            OnMacroWritten();
        }

        private static void RemoveCoTrigger(Macro macro, KeyDefinition coTrigger)
        {
            for (var index = macro.CoTriggers.Count - 1; index >= 0; index--)
            {
                if (macro.CoTriggers[index].Code == coTrigger.Code)
                {
                    macro.RemoveCoTriggerAt(index);
                }
            }
        }

        private void RefreshCoTriggers()
        {
            foreach (var toggle in CoTriggers)
            {
                toggle.IsOn = _macro?.ContainsCoTrigger(toggle.Key.Code) == true;
            }
        }

        private void RefreshMeters()
        {
            SpeedMeter.Set(_speed, HasSpeed ? SpeedMaximum : null);
            MacroLengthMeter.Set(
                _macro is not null && _layout is not null ? MacroLengthMetric.Measure(_macro, _layout) : 0,
                _capability.MaxCharactersPerMacro);
            LayoutKeystrokeMeter.Set(_layout?.TotalKeystrokes ?? 0, _capability.MaxTotalKeystrokes);
        }

        /// <summary>
        /// Rebuilds the dropdown from the editor's <b>current</b> library snapshot, and re-reads what
        /// this key's macro is called and where else it fires. Every mutation rebuilds
        /// <c>Entries</c>, so an option held across one is stale — which is why the rows are rebuilt
        /// rather than re-marked.
        /// </summary>
        private void RefreshNames()
        {
            var library = _resolveLibrary();

            if (library is null)
            {
                NameOptions = [];
                SetSelectedName(null);
                IsNamed = false;
                AlsoOnText = string.Empty;

                return;
            }

            var current = _macro is not null ? library.FindByMacro(_macro) : null;
            var options = new List<MacroNameOptionViewModel>(library.Entries.Count + 1);
            MacroNameOptionViewModel? selected = null;

            if (current is null)
            {
                var none = new MacroNameOptionViewModel();

                options.Add(none);

                selected = none;
            }

            foreach (var entry in library.Entries)
            {
                var option = new MacroNameOptionViewModel(entry);

                options.Add(option);

                if (ReferenceEquals(entry, current))
                {
                    selected = option;
                }
            }

            NameOptions = options;

            SetSelectedName(selected);

            IsNamed = current?.IsExplicitlyNamed == true;
            AlsoOnText = BuildAlsoOnText(current);
        }

        /// <summary>
        /// "Also on [f7] · Fn" — every site of this macro but the one the rail is showing. Built
        /// from the library entry rather than from a scan, because the entry <em>is</em> the answer
        /// to "which keys and layers fire this one".
        /// </summary>
        private string BuildAlsoOnText(MacroLibraryEntry? entry)
        {
            if (entry is null || _layout is null || entry.SiteCount <= 1)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            foreach (var site in entry.Sites)
            {
                if (ReferenceEquals(site.Macro, _macro))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(SiteJoin);
                }

                builder.Append(DescribeSite(site));
            }

            return builder.Length > 0 ? AlsoOnPrefix + builder : string.Empty;
        }

        private string DescribeSite(MacroSite site)
        {
            var trigger = KeyRegistry.FindByCode(site.TriggerKeyCode);
            var token = trigger is not null
                ? KeyboardKeyViewModel.FormatToken(trigger, _dialect)
                : string.Empty;

            var layer = _layout is not null && site.LayerIndex >= 0 && site.LayerIndex < _layout.Layers.Count
                ? LayerCaptions.ForLayer(_layout.Layers[site.LayerIndex], _dialect)
                : string.Empty;

            if (token.Length == 0)
            {
                return layer;
            }

            return layer.Length == 0 ? token : token + SiteSeparator + layer;
        }

        private void SetSelectedName(MacroNameOptionViewModel? option)
        {
            if (ReferenceEquals(_selectedName, option))
            {
                return;
            }

            _selectedName = option;

            OnPropertyChanged(nameof(SelectedName));
        }

        /// <summary>
        /// The dropdown's write path: putting an existing named macro on this key
        /// (<c>MacroLibrary.AssignTo</c>, mockup <c>2i</c>). The current slot is emptied first, so
        /// the pick <em>replaces</em> what the position was carrying rather than quietly filling a
        /// second slot with it — the rail edits one macro and the exclusivity the whole inspector is
        /// built on says so.
        /// </summary>
        private void PickName(MacroNameOptionViewModel? option)
        {
            if (option is null || ReferenceEquals(option, _selectedName))
            {
                return;
            }

            // The placeholder is a state, not an action: a dropdown must never be the thing that
            // deletes a macro. The selection simply snaps back.
            if (option.Entry is not { } entry)
            {
                OnPropertyChanged(nameof(SelectedName));

                return;
            }

            if (!IsAvailable || _key is not { } key || _resolveLibrary() is not { } library)
            {
                OnPropertyChanged(nameof(SelectedName));

                return;
            }

            var slot = _usesMacroSlots ? key.Key.ActiveMacroIndex : MacroLibrary.FlatListSlot;

            if (_usesMacroSlots && (slot < Macro.MinMacroIndex || slot > Macro.MaxMacroIndex))
            {
                slot = MacroLibrary.FirstEmptySlot;
            }

            if (_usesMacroSlots && slot >= Macro.MinMacroIndex && key.Key.GetMacro(slot) is not null)
            {
                key.Key.SetMacro(slot, null);
            }

            var assigned = library.AssignTo(entry, key.Key, slot);

            if (assigned is null)
            {
                Message = NoFreeSlotMessage;

                OnPropertyChanged(nameof(SelectedName));

                return;
            }

            Message = string.Empty;

            OnMacroWritten();
        }

        /// <summary>
        /// One hop out, plus the readouts this panel owns outright. The meters are derived from the
        /// macro the panel just wrote to, so they move here rather than waiting for the round trip —
        /// this is the panel reacting to <em>its own</em> write, which is the opposite of
        /// <see cref="Refresh"/>'s "re-read and never write" and does not re-enter it.
        /// <para>
        /// Everything else — the counters, the advisories, the legend, the library snapshot the name
        /// dropdown is built from, and the dirty flag — is the editor's funnel's, because Core
        /// announces nothing and half a refresh is worse than none.
        /// </para>
        /// </summary>
        private void OnMacroWritten()
        {
            RefreshMeters();

            OnPropertyChanged(nameof(RecordingBanner));

            Assigned?.Invoke(this, EventArgs.Empty);
        }
    }
}
