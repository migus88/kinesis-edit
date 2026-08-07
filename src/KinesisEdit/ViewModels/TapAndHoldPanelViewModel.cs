using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The key inspector's <b>Tap &amp; hold</b> panel (mockup <c>2h</c>) — specs/11-feature-dialogs.md
    /// §11.1 rehomed from a modal dialog into the rail: a tap action, a hold action, and the timing
    /// delay that separates them.
    ///
    /// <para><b>It is a port, not a redesign.</b> The rules are the overlay's, which were tested and
    /// correct: the two fields are filled by the next physical keystroke, the delay opens on the
    /// device's own <c>TapAndHoldCapability.DefaultDelayMilliseconds</c> (250 ms; <b>150</b> on the
    /// Advantage 360) and never on a literal, the Up/Down commands clamp to the device's range while
    /// direct assignment is deliberately <b>unclamped</b> so an out-of-range value survives to be
    /// reported, and <c>KeyboardKey.SetTapAndHold</c> has the last word on a position that can never
    /// be remapped (05 §5.3). What changed is the surface: no modal, no Ok/Cancel pair, no nesting.</para>
    ///
    /// <para><b>The rail is not modal, so this panel takes a keystroke only while it is armed.</b>
    /// The overlay took every keystroke on being merely <em>open</em> — spec 10's own wording, and
    /// right for a modal that nothing may be typed underneath. A rail sits beside a live board, so
    /// an unarmed panel that swallowed keys would eat the remap the user was recording on the cap
    /// next to it. <see cref="WantsKeystrokes"/> is therefore exactly <see cref="IsRecording"/>.</para>
    ///
    /// <para><b><see cref="IsRecording"/> is honest, and that is load-bearing.</b> The rail folds it
    /// into the editor's <c>IsCaptureActive</c>, which is what suppresses ⌘S and the rest of the
    /// grammar; a panel that recorded without saying so would have its hold action eaten by an
    /// accelerator.</para>
    ///
    /// <para><b>§11.1's two <c>Search</c> buttons open the shared picker <em>inside</em> the panel.</b>
    /// The overlay nested a second modal over the first, which is why <c>EditorOverlayHost</c> had a
    /// <c>ShowNested</c> at all; the rail nests nothing, so <see cref="Picker"/> is the same
    /// <see cref="TokenPickerViewModel"/> the Remap panel hosts, shown in place of the fields for as
    /// long as the pick lasts. It is what keeps a media key, a mouse button or a profile selector —
    /// none of which a keyboard can press — assignable as a tap or a hold action.</para>
    ///
    /// <para><b>Two gates, and both refuse politely rather than disappearing.</b> The firmware gate
    /// of 09 §2 and the four pre-dialog checks of §11.1 (<see cref="TapAndHoldPrecheck"/>) are read
    /// on every <see cref="Refresh"/> and answered through <see cref="IsAvailable"/> /
    /// <see cref="UnavailableReason"/>. This is the sanctioned exception to "absent features are not
    /// shown": the user pointed at this tab, and "your firmware is too old" is the answer to the
    /// question they asked. Neither gate's wording is this class's — the messages come from
    /// <see cref="FirmwareGateCatalog"/> and <see cref="TapAndHoldPrecheck.MessageFor"/>.</para>
    ///
    /// <para><b>The budget advisory is read, never recomputed.</b> It already exists as data
    /// (<see cref="EditorAdvisories"/> over <see cref="ModelViolationKind.TapAndHoldCountExceeded"/>)
    /// and arrives on <see cref="Refresh"/>. Amber, non-blocking, and not re-worded here.</para>
    ///
    /// <para><b>The Advantage 360's <c>'Macro'</c> buttons</b> (§11.1, keyboard firmware ≥ 1.0.69)
    /// are deliberately absent — that board has no editor yet (issue #41).</para>
    /// </summary>
    public sealed class TapAndHoldPanelViewModel : KeyInspectorPanelViewModel, IKeystrokeSink
    {
        /// <summary>
        /// §11.1's dialog title. It is no longer a title on screen — the rail's tab names the panel —
        /// but it is still the title of the firmware refusal box, which is a dialog.
        /// </summary>
        public const string FeatureTitle = "Assign Tap and Hold Action";

        /// <summary>Label of the tap field, verbatim from mockup <c>2h</c>.</summary>
        public const string TapFieldLabel = "Tap — a quick press sends";

        /// <summary>Label of the hold field, verbatim from mockup <c>2h</c>.</summary>
        public const string HoldFieldLabel = "Hold — past the delay it sends";

        /// <summary>
        /// The capture rule, verbatim from mockup <c>2h</c>. It is the one sentence that explains
        /// why the two fields are separate captures rather than one: a modifier pressed alone and
        /// the same modifier held inside a combination are different keystrokes to this app
        /// (docs/app/keystroke-capture.md — left and right modifiers are distinguished, and a
        /// modifier is a key like any other).
        /// </summary>
        public const string CaptureRule =
            "A bare modifier is recordable as a hold — tap-alone and held-in-combo are captured as "
            + "different things.";

        /// <summary>
        /// Caption of both record buttons. Mockup <c>2h</c> draws them as <c>● Record</c>; the dot is
        /// drawn as geometry by the view and is not part of the caption, because <b>U+25CF is in
        /// neither embedded IBM Plex family</b> and would render as tofu (the same gate
        /// <c>ShapeAndTypeTokenTests</c> holds every keycap legend to).
        /// </summary>
        public const string RecordCaption = "Record";

        /// <summary>Label over the delay slider (mockup <c>2h</c>).</summary>
        public const string DelayLabel = "Delay";

        /// <summary>
        /// The slider's own caption, mockup <c>2h</c>'s <c>default 250 · this device</c>. The number
        /// is the device's, never a literal — see <see cref="DefaultDelayMilliseconds"/>.
        /// </summary>
        public const string DelayDefaultFormat = "default {0} · this device";

        /// <summary>The mono readout beside the slider: <c>250 ms</c> (mockup <c>2h</c>).</summary>
        public const string DelayReadoutFormat = "{0} ms";

        /// <summary>
        /// The panel's one write. Named for what the rail's own exclusivity warning calls it —
        /// "Assigning this clears the remap that was on this key" — so the button and the warning
        /// are visibly about the same act.
        /// </summary>
        public const string AssignCaption = "Assign";

        /// <summary>The static note of §11.1.</summary>
        public const string NoteText = "Tap action is not sent until key is released.";

        /// <summary>Hint of the tap field (§11.1).</summary>
        public const string TapActionHint =
            "Designate the action sent when the key is tapped and released faster than the delay";

        /// <summary>Hint of the hold field (§11.1).</summary>
        public const string HoldActionHint =
            "Designate the action sent when the key is held longer than the delay";

        /// <summary>Hint of the delay control (§11.1).</summary>
        public const string DelayHint =
            "Designate the time interval used to differentiate between the Tap and Hold actions";

        /// <summary>Hint of both token-picker actions (§11.1).</summary>
        public const string SearchHint = "Search for tokens";

        /// <summary>§11.1's <c>Search</c>, which opens the picker over one of the two fields.</summary>
        public const string SearchCaption = "Search…";

        /// <summary>The way out of that pick without choosing anything.</summary>
        public const string CancelSearchCaption = "Cancel";

        /// <summary>Validation for a delay outside the device's range (§11.1).</summary>
        public const string InvalidDelayMessage = "Please select a timing delay between 1ms and 999ms.";

        /// <summary>Validation for a missing tap action (§11.1).</summary>
        public const string MissingTapActionMessage = "Please select a Tap Action";

        /// <summary>Validation for a missing hold action (§11.1).</summary>
        public const string MissingHoldActionMessage = "Please select a Hold Action";

        /// <summary>
        /// Shown when <see cref="KeyboardKey.SetTapAndHold(KeyDefinition, KeyDefinition, int)"/>
        /// refuses the position (05 §5.3). This app's wording — §11.1 quotes none, because the
        /// legacy apps never offered the dialog on such a key either; it exists so a refusal can
        /// never look like a success.
        /// </summary>
        public const string LockedKeyMessage = "This key position cannot be programmed.";

        /// <summary>
        /// The firmware refusal of §11.1, used for the gate rows that carry no message of their own
        /// (Advantage2, Edge RGB — docs/app/firmware.md). Pinned identical to the Freestyle row by
        /// test.
        /// </summary>
        public const string FirmwareRefusalMessage =
            "To utilize Tap and Hold Actions, please download and install the latest firmware.";

        /// <summary>
        /// Why the panel can do nothing with no position selected. The rail collapses entirely in
        /// that state, so this is a backstop rather than a screen the user reaches — the contract
        /// requires a reason whenever <see cref="IsAvailable"/> is false, and "none given" is the
        /// one answer a refusal may never be.
        /// </summary>
        public const string NoSelectionMessage = "Select a key on the board to assign a tap and hold.";

        /// <summary>
        /// Why the panel can do nothing on a board without the feature
        /// (<see cref="TapAndHoldCapability.IsSupported"/>). No board with a picture reaches it
        /// today; it exists because the rail draws this tab for every device rather than asking the
        /// catalog first, so the answer has to live somewhere.
        /// </summary>
        public const string DeviceUnsupportedMessage = "This keyboard does not have tap and hold actions.";

        /// <summary>
        /// Delay range used only when the device states none — a device without the feature, which
        /// this panel refuses anyway. Every supported device carries the range as catalog data and
        /// it is read from there.
        /// </summary>
        private const int FallbackMinimumDelayMilliseconds = 1;

        /// <inheritdoc cref="FallbackMinimumDelayMilliseconds" />
        private const int FallbackMaximumDelayMilliseconds = 999;

        /// <summary>
        /// Whether the device's firmware clears the tap-and-hold gate (09 §2), showing §11.1's
        /// refusal with the <c>'Update Firmware'</c> button when it does not. Demo mode always
        /// passes.
        /// <para>
        /// The <b>modal</b> half of the gate, kept exactly as the overlay had it so a caller that
        /// wants to refuse before opening anything still can. The panel itself refuses
        /// <em>inline</em> instead — see <see cref="IsAvailable"/> — because a rail that popped a
        /// dialog the moment a tab was selected would be a modal by another name.
        /// </para>
        /// </summary>
        public static Task<bool> EnsureFirmwareAvailableAsync(
            DeviceId deviceId,
            FirmwareState firmware,
            INotificationService notifications,
            IUrlLauncher urlLauncher)
        {
            return FirmwareFeatureGate.EnsureAvailableAsync(
                deviceId,
                FirmwareFeature.TapAndHold,
                firmware,
                FeatureTitle,
                FirmwareRefusalMessage,
                notifications,
                urlLauncher);
        }

        /// <summary>
        /// What the gate says on <paramref name="deviceId"/> — the row's own wording where 09 §2
        /// stores one, and <see cref="FirmwareRefusalMessage"/> where it does not. The same
        /// resolution <see cref="FirmwareFeatureGate"/> performs, so the two can never disagree.
        /// </summary>
        public static string FirmwareRefusalFor(DeviceId deviceId)
        {
            return FirmwareGateCatalog.Find(deviceId, FirmwareFeature.TapAndHold)?.Message ?? FirmwareRefusalMessage;
        }

        /// <inheritdoc />
        public override KeyInspectorMode Mode => KeyInspectorMode.TapAndHold;

        /// <inheritdoc />
        public override string Title => KeyInspectorTabViewModel.TapAndHoldCaption;

        /// <inheritdoc />
        public override bool IsAvailable => _unavailableReason.Length == 0;

        /// <inheritdoc />
        public override string UnavailableReason => _unavailableReason;

        /// <inheritdoc />
        public override bool IsRecording => _armedField != TapAndHoldField.None;

        /// <inheritdoc />
        public bool WantsKeystrokes => IsRecording;

        /// <summary>The action sent on a tap, or null while the field is empty.</summary>
        public KeyDefinition? TapAction
        {
            get => _tapAction;
            private set
            {
                if (SetProperty(ref _tapAction, value))
                {
                    OnPropertyChanged(nameof(TapActionText));
                    OnPropertyChanged(nameof(HasTapAction));
                }
            }
        }

        /// <summary>The action sent on a hold, or null while the field is empty.</summary>
        public KeyDefinition? HoldAction
        {
            get => _holdAction;
            private set
            {
                if (SetProperty(ref _holdAction, value))
                {
                    OnPropertyChanged(nameof(HoldActionText));
                    OnPropertyChanged(nameof(HasHoldAction));
                }
            }
        }

        /// <summary>What the tap field shows; empty while it is unset.</summary>
        public string TapActionText => Describe(_tapAction);

        /// <summary>What the hold field shows; empty while it is unset.</summary>
        public string HoldActionText => Describe(_holdAction);

        /// <summary>Whether the tap field has anything in it.</summary>
        public bool HasTapAction => _tapAction is not null;

        /// <summary>Whether the hold field has anything in it.</summary>
        public bool HasHoldAction => _holdAction is not null;

        /// <summary>
        /// The timing delay in milliseconds. <b>Deliberately unclamped on assignment</b> — §11.1
        /// clamps the field's Up/Down steps (<see cref="IncreaseDelayCommand"/>,
        /// <see cref="DecreaseDelayCommand"/>) and validates the value when it is assigned, so a
        /// value out of the device's range has to survive long enough to be reported. A file written
        /// by older firmware can carry one.
        /// </summary>
        public int DelayMilliseconds
        {
            get => _delayMilliseconds;
            set
            {
                if (SetProperty(ref _delayMilliseconds, value))
                {
                    OnPropertyChanged(nameof(DelaySliderValue));
                    OnPropertyChanged(nameof(DelayReadout));
                }
            }
        }

        /// <summary>
        /// What the slider is bound to. It is <see cref="DelayMilliseconds"/> <em>clamped for
        /// display</em>, and this indirection is the whole point: a <c>Slider</c> coerces its own
        /// <c>Value</c> into its range and writes the coerced number back through a two-way binding,
        /// so binding it straight at <see cref="DelayMilliseconds"/> would silently rewrite an
        /// out-of-range delay to 999 the instant the panel drew it — destroying the value §11.1
        /// exists to report. The mono readout beside it shows the real number.
        /// </summary>
        public double DelaySliderValue
        {
            get => Math.Clamp(_delayMilliseconds, MinimumDelayMilliseconds, MaximumDelayMilliseconds);
            set => DelayMilliseconds = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>The mono readout: <c>250 ms</c>.</summary>
        public string DelayReadout =>
            string.Format(CultureInfo.InvariantCulture, DelayReadoutFormat, _delayMilliseconds);

        /// <summary>Lowest delay the device accepts (1 ms; 11 §11.1, 04 §5.3).</summary>
        public int MinimumDelayMilliseconds
        {
            get => _minimumDelayMilliseconds;
            private set => SetProperty(ref _minimumDelayMilliseconds, value);
        }

        /// <summary>Highest delay the device accepts (999 ms; 11 §11.1, 04 §5.3).</summary>
        public int MaximumDelayMilliseconds
        {
            get => _maximumDelayMilliseconds;
            private set => SetProperty(ref _maximumDelayMilliseconds, value);
        }

        /// <summary>
        /// What this device starts a fresh assignment at — 250 ms, or the Advantage 360's 150 ms.
        /// Catalog data (<see cref="TapAndHoldCapability.DefaultDelayMilliseconds"/>), never a
        /// literal; null until a layout has been handed over.
        /// </summary>
        public int? DefaultDelayMilliseconds
        {
            get => _defaultDelayMilliseconds;
            private set
            {
                if (SetProperty(ref _defaultDelayMilliseconds, value))
                {
                    OnPropertyChanged(nameof(DelayDefaultCaption));
                    OnPropertyChanged(nameof(HasDelayDefaultCaption));
                }
            }
        }

        /// <summary>
        /// The slider's caption — <c>default 250 · this device</c>. Empty on a device that states no
        /// default, because "default 0" is worse than saying nothing.
        /// </summary>
        public string DelayDefaultCaption => _defaultDelayMilliseconds is { } value
            ? string.Format(CultureInfo.InvariantCulture, DelayDefaultFormat, value)
            : string.Empty;

        /// <summary>Whether there is a default to name.</summary>
        public bool HasDelayDefaultCaption => DelayDefaultCaption.Length > 0;

        /// <summary>Which field the next captured keystroke fills, if any.</summary>
        public TapAndHoldField ArmedField
        {
            get => _armedField;
            private set
            {
                var wasRecording = IsRecording;

                if (!SetProperty(ref _armedField, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(WantsKeystrokes));

                if (wasRecording != IsRecording)
                {
                    OnPropertyChanged(nameof(IsRecording));
                    OnRecordingChanged();
                }
            }
        }

        /// <summary>
        /// §11.6's catalog, hosted inside this panel for the two <c>Search</c> actions. The same
        /// control the Remap panel shows and the macro-insertion modal wraps — one picker, three call
        /// sites — over the shared session history, so an action picked here is offered by the rail's
        /// <c>Recent</c> chip too.
        /// </summary>
        public TokenPickerViewModel Picker { get; }

        /// <summary>
        /// Whether the picker is showing in place of the two fields. It is drawn <em>instead of</em>
        /// them, never beside them: 244 px of rail cannot hold a grouped list under two fields and a
        /// slider, and a pick is one question with one answer.
        /// </summary>
        public bool IsPickerOpen
        {
            get => _isPickerOpen;
            private set => SetProperty(ref _isPickerOpen, value);
        }

        /// <summary>
        /// Which field the open pick will fill, in the same words its own label uses — so the picker
        /// never leaves the user guessing which of the two they are choosing for.
        /// </summary>
        public string PickerFieldLabel
        {
            get => _pickerFieldLabel;
            private set => SetProperty(ref _pickerFieldLabel, value);
        }

        /// <summary>
        /// What the last attempt to assign refused, or an empty string. §11.1's three validation
        /// messages and the locked-position refusal land here; nothing else does, and it never
        /// blocks anything but the write it belongs to.
        /// </summary>
        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (SetProperty(ref _validationMessage, value))
                {
                    OnPropertyChanged(nameof(HasValidationMessage));
                }
            }
        }

        /// <summary>Whether there is a refusal to draw.</summary>
        public bool HasValidationMessage => _validationMessage.Length > 0;

        /// <summary>
        /// The profile's tap-and-hold budget advisory, or an empty string. <b>Read</b> off the set
        /// the rail handed over — the editor already built it from
        /// <see cref="KeyboardLayout.Validate"/> — and never recomputed or re-worded. Amber, and it
        /// blocks nothing: the layout still saves and the board keeps the first ten lines it reads.
        /// </summary>
        public string BudgetAdvisory
        {
            get => _budgetAdvisory;
            private set
            {
                if (SetProperty(ref _budgetAdvisory, value))
                {
                    OnPropertyChanged(nameof(HasBudgetAdvisory));
                }
            }
        }

        /// <summary>Whether this profile is over the device's tap-and-hold budget.</summary>
        public bool HasBudgetAdvisory => _budgetAdvisory.Length > 0;

        /// <summary>
        /// Whether the refusal can offer somewhere to go. Two conditions, and the first is the one a
        /// frame caught: the refusal has to be the <b>firmware gate's</b>. A profile that has spent
        /// its ten tap-and-holds refuses too, and offering to update the firmware there would be an
        /// answer to a question nobody asked. The second is exactly what
        /// <see cref="FirmwareFeatureGate"/> puts on its own <c>'Update Firmware'</c> button: the
        /// device has a support page.
        /// </summary>
        public bool CanUpdateFirmware => _isFirmwareRefusal && _supportUrl is not null;

        /// <summary>Caption of that action, shared with the modal gate so the two read alike.</summary>
        public string UpdateFirmwareCaption => FirmwareFeatureGate.UpdateFirmwareButtonCaption;

        /// <summary>Arms the tap field; the record button runs it.</summary>
        public IRelayCommand ArmTapActionCommand { get; }

        /// <summary>Arms the hold field.</summary>
        public IRelayCommand ArmHoldActionCommand { get; }

        /// <summary>
        /// Opens the token picker over the tap field (§11.1's <c>Search</c>, §11.6's catalog). It is
        /// shown <b>inside</b> this panel — the rail nests nothing — and taking a row calls
        /// <see cref="AssignAction"/> and closes it again.
        /// </summary>
        public IRelayCommand SearchTapActionCommand { get; }

        /// <summary>The same for the hold field.</summary>
        public IRelayCommand SearchHoldActionCommand { get; }

        /// <summary>Leaves the pick without choosing anything; both fields keep what they had.</summary>
        public IRelayCommand CloseSearchCommand { get; }

        /// <summary>The delay's Up step, clamped to the device's range (§11.1).</summary>
        public IRelayCommand IncreaseDelayCommand { get; }

        /// <summary>The delay's Down step, clamped to the device's range (§11.1).</summary>
        public IRelayCommand DecreaseDelayCommand { get; }

        /// <summary>
        /// Writes the assignment onto the position, validating in §11.1's order — delay, tap action,
        /// hold action — and letting the model have the last word.
        /// </summary>
        public IRelayCommand AssignCommand { get; }

        /// <summary>Opens the device's firmware support page; live only while the gate refuses.</summary>
        public IRelayCommand UpdateFirmwareCommand { get; }

        /// <summary>
        /// Raised after the assignment has been written to the model. The editor answers by running
        /// its own refresh funnel — Core announces no change, so nothing downstream would notice
        /// otherwise.
        /// </summary>
        public event EventHandler? Assigned;

        private readonly DeviceId _deviceId;
        private readonly FirmwareState _firmware;
        private readonly IUrlLauncher _urlLauncher;
        private readonly string? _supportUrl;

        /// <summary>
        /// How this board's files spell a token. A <b>device</b> fact
        /// (<see cref="KeyboardLayout.DialectFor"/>), not a profile one, which is why it
        /// is settled in the constructor and never re-read from a layout that may not exist yet.
        /// </summary>
        private readonly TokenDialect _dialect;

        private KeyboardKeyViewModel? _key;
        private KeyboardLayerViewModel? _layer;
        private KeyboardLayout? _layout;
        private KeyDefinition? _tapAction;
        private KeyDefinition? _holdAction;
        private int _delayMilliseconds;
        private int _minimumDelayMilliseconds = FallbackMinimumDelayMilliseconds;
        private int _maximumDelayMilliseconds = FallbackMaximumDelayMilliseconds;
        private int? _defaultDelayMilliseconds;
        private TapAndHoldField _armedField;
        private TapAndHoldField _pickerField;
        private bool _isPickerOpen;
        private string _pickerFieldLabel = string.Empty;
        private string _unavailableReason = NoSelectionMessage;
        private string _validationMessage = string.Empty;
        private string _budgetAdvisory = string.Empty;

        /// <summary>
        /// Whether <see cref="_unavailableReason"/> is the <em>firmware</em> gate's rather than one
        /// of §11.1's four pre-dialog checks. Only the firmware refusal has somewhere to send the
        /// user — see <see cref="CanUpdateFirmware"/>.
        /// </summary>
        private bool _isFirmwareRefusal;

        /// <summary>
        /// What the model said the last time the fields were loaded from it. It is how the panel
        /// tells "the user is halfway through filling this in" from "somebody else rewrote the
        /// position underneath us": the first must survive a refresh, the second must not.
        /// </summary>
        private KeyDefinition? _modelTapAction;

        /// <inheritdoc cref="_modelTapAction" />
        private KeyDefinition? _modelHoldAction;

        /// <inheritdoc cref="_modelTapAction" />
        private int _modelDelayMilliseconds;

        /// <inheritdoc cref="_modelTapAction" />
        private bool _modelIsTapAndHold;

        /// <summary>
        /// Builds the panel for one open device. The device and its firmware are the editor's
        /// <c>DeviceSnapshot</c>, which is immutable for the life of the session — the gate can
        /// therefore be resolved once and asked about on every refresh without re-reading a drive.
        /// <para>
        /// The picker's catalog is built from the same fact:
        /// <see cref="KeyboardLayout.DialectFor"/> over the device id, which is what
        /// <c>KeyboardLayout.Dialect</c> itself is. That is why the panel can be constructed before a
        /// profile has been read at all. <paramref name="recent"/> is the editor's session history,
        /// shared with every other picker it hosts.
        /// </para>
        /// </summary>
        public TapAndHoldPanelViewModel(
            DeviceId deviceId,
            FirmwareState firmware,
            IUrlLauncher urlLauncher,
            RecentTokenStore? recent = null)
        {
            _deviceId = deviceId;
            _firmware = firmware;
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _supportUrl = FirmwareSupportUrls.FindUrl(deviceId);
            _dialect = KeyboardLayout.DialectFor(deviceId);

            Picker = new TokenPickerViewModel(_dialect, recent);

            // Taking a row IS the answer: §11.6's "double-clicking an item accepts immediately", and
            // ↵ on the highlighted row. The picker and this panel live and die together, so there is
            // nothing to detach later.
            Picker.Chosen += OnPickerChosen;

            ArmTapActionCommand = new RelayCommand(() => Arm(TapAndHoldField.Tap));
            ArmHoldActionCommand = new RelayCommand(() => Arm(TapAndHoldField.Hold));
            SearchTapActionCommand = new RelayCommand(() => OpenPicker(TapAndHoldField.Tap));
            SearchHoldActionCommand = new RelayCommand(() => OpenPicker(TapAndHoldField.Hold));
            CloseSearchCommand = new RelayCommand(ClosePicker);
            IncreaseDelayCommand = new RelayCommand(() => StepDelay(1));
            DecreaseDelayCommand = new RelayCommand(() => StepDelay(-1));
            AssignCommand = new RelayCommand(Assign, () => IsAvailable);
            UpdateFirmwareCommand = new RelayCommand(OpenSupportPage, () => CanUpdateFirmware);
        }

        /// <inheritdoc />
        public override void Refresh(
            KeyboardKeyViewModel? key,
            KeyboardLayerViewModel? layer,
            KeyboardLayout? layout,
            EditorAdvisories advisories)
        {
            ArgumentNullException.ThrowIfNull(advisories);

            var isNewKey = !ReferenceEquals(key, _key);

            _key = key;
            _layer = layer;
            _layout = layout;

            ApplyCapability(layout?.Device.TapAndHold);
            ApplyAvailability();

            if (isNewKey || HasModelMovedUnderUs())
            {
                LoadFromModel();
            }

            if (isNewKey)
            {
                // A pick opened for the position the user has just left has nothing to fill.
                ClosePicker();
            }

            if (!IsAvailable)
            {
                // A gate that closed while a field was armed must not leave the app capturing for a
                // panel that can no longer write anything.
                ArmedField = TapAndHoldField.None;

                ClosePicker();
            }

            BudgetAdvisory = FindBudgetAdvisory(advisories);
        }

        /// <inheritdoc />
        public override void Deactivate()
        {
            ArmedField = TapAndHoldField.None;
            ValidationMessage = string.Empty;

            ClosePicker();
        }

        /// <summary>
        /// Takes one captured keystroke into the armed field and disarms.
        /// <para>
        /// <b>Nothing is taken while nothing is armed.</b> The rail is not modal, so an unarmed
        /// panel must let the keystroke fall through to whatever the board is doing — which is the
        /// one behavioural difference from the modal this replaces.
        /// </para>
        /// </summary>
        public void ReceiveKeystroke(CapturedKeystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            if (!WantsKeystrokes)
            {
                return;
            }

            AssignAction(_armedField, keystroke.Key);
        }

        /// <summary>
        /// Puts <paramref name="action"/> in <paramref name="field"/> and disarms. Public because
        /// the token picker writes back through it — the rail nests nothing, so whoever hosts the
        /// picker owns the round trip.
        /// </summary>
        public void AssignAction(TapAndHoldField field, KeyDefinition action)
        {
            ArgumentNullException.ThrowIfNull(action);

            switch (field)
            {
                case TapAndHoldField.Tap:
                    TapAction = action;

                    break;

                case TapAndHoldField.Hold:
                    HoldAction = action;

                    break;

                default:
                    return;
            }

            ValidationMessage = string.Empty;
            ArmedField = TapAndHoldField.None;
        }

        /// <summary>
        /// Reads the capability the open board states. A layout the panel has not been handed yet
        /// leaves the range at the 1-999 of §11.1 and names no default.
        /// </summary>
        private void ApplyCapability(TapAndHoldCapability? capability)
        {
            var range = capability?.DelayMilliseconds;

            MinimumDelayMilliseconds = range?.Minimum ?? FallbackMinimumDelayMilliseconds;
            MaximumDelayMilliseconds = range?.Maximum ?? FallbackMaximumDelayMilliseconds;
            DefaultDelayMilliseconds = capability?.DefaultDelayMilliseconds;
        }

        /// <summary>
        /// Re-asks both gates, in §11.1's own order: the device's capability, then the firmware gate
        /// of 09 §2, then the four pre-dialog checks. The first refusal wins, and every wording is
        /// the gate's own.
        /// </summary>
        private void ApplyAvailability()
        {
            var wasAvailable = IsAvailable;
            var couldUpdate = CanUpdateFirmware;
            var reason = EvaluateUnavailableReason(out var isFirmwareRefusal);

            _isFirmwareRefusal = isFirmwareRefusal;

            if (reason != _unavailableReason)
            {
                _unavailableReason = reason;

                OnPropertyChanged(nameof(UnavailableReason));
            }

            if (wasAvailable != IsAvailable)
            {
                OnPropertyChanged(nameof(IsAvailable));

                AssignCommand.NotifyCanExecuteChanged();
            }

            if (couldUpdate != CanUpdateFirmware)
            {
                OnPropertyChanged(nameof(CanUpdateFirmware));

                UpdateFirmwareCommand.NotifyCanExecuteChanged();
            }
        }

        private string EvaluateUnavailableReason(out bool isFirmwareRefusal)
        {
            isFirmwareRefusal = false;

            if (_key is null || _layer is null || _layout is null)
            {
                return NoSelectionMessage;
            }

            if (!_layout.Device.TapAndHold.IsSupported)
            {
                return DeviceUnsupportedMessage;
            }

            if (!FirmwareGateService.IsAvailable(_deviceId, FirmwareFeature.TapAndHold, _firmware))
            {
                isFirmwareRefusal = true;

                return FirmwareRefusalFor(_deviceId);
            }

            var refusal = TapAndHoldPrecheck.Evaluate(_layout, _layer.Layer, _key.Key);

            return refusal == TapAndHoldRefusal.None ? string.Empty : TapAndHoldPrecheck.MessageFor(refusal);
        }

        /// <summary>
        /// Whether the position's assignment changed without this panel doing it — a remap written
        /// from the Remap panel clears the tap-and-hold, and the fields must follow. Answered
        /// against what was last read rather than against what is shown, so a half-filled field the
        /// user is still working on is not thrown away on every counter refresh.
        /// </summary>
        private bool HasModelMovedUnderUs()
        {
            if (_key is null)
            {
                return false;
            }

            var key = _key.Key;

            return key.IsTapAndHold != _modelIsTapAndHold
                   || !ReferenceEquals(key.TapAction, _modelTapAction)
                   || !ReferenceEquals(key.HoldAction, _modelHoldAction)
                   || key.TimingDelay != _modelDelayMilliseconds;
        }

        /// <summary>
        /// Opens the fields on the position's own assignment, or on an empty pair at the device's
        /// default delay. Never a literal: the default is catalog data (§11.1 — 250 ms, 150 ms on
        /// the Advantage 360).
        /// </summary>
        private void LoadFromModel()
        {
            var key = _key?.Key;

            _modelIsTapAndHold = key?.IsTapAndHold == true;
            _modelTapAction = key?.TapAction;
            _modelHoldAction = key?.HoldAction;
            _modelDelayMilliseconds = key?.TimingDelay ?? 0;

            TapAction = _modelTapAction;
            HoldAction = _modelHoldAction;
            DelayMilliseconds = _modelIsTapAndHold
                ? _modelDelayMilliseconds
                : _defaultDelayMilliseconds ?? 0;

            ArmedField = TapAndHoldField.None;
            ValidationMessage = string.Empty;

            OnPropertyChanged(nameof(TapActionText));
            OnPropertyChanged(nameof(HoldActionText));
        }

        private void Arm(TapAndHoldField field)
        {
            if (!IsAvailable)
            {
                return;
            }

            // Pressing the armed field's own button again stands the capture down, so a record
            // started by accident does not have to be finished with a keypress.
            ArmedField = _armedField == field ? TapAndHoldField.None : field;
            ValidationMessage = string.Empty;
        }

        /// <summary>
        /// Shows §11.6's catalog over <paramref name="field"/>, in place of the two fields. Nothing
        /// is nested and nothing is written: the pick is a step inside this panel, and the assignment
        /// still lands only when <c>Assign</c> is pressed.
        /// </summary>
        private void OpenPicker(TapAndHoldField field)
        {
            if (!IsAvailable || field == TapAndHoldField.None)
            {
                return;
            }

            // The picker's query box is a real TextBox, which suspends capture the moment it takes
            // focus — so an armed field would be left waiting for a keypress that can never arrive.
            ArmedField = TapAndHoldField.None;
            ValidationMessage = string.Empty;

            _pickerField = field;

            PickerFieldLabel = field == TapAndHoldField.Tap ? TapFieldLabel : HoldFieldLabel;

            Picker.Clear();
            Picker.RequestFocus();

            IsPickerOpen = true;
        }

        /// <summary>Takes the picked row into the field the pick was opened for, and closes it.</summary>
        private void OnPickerChosen(KeyDefinition definition)
        {
            if (!IsPickerOpen)
            {
                return;
            }

            var field = _pickerField;

            ClosePicker();

            AssignAction(field, definition);

            Picker.Remember(definition);
        }

        private void ClosePicker()
        {
            if (!_isPickerOpen)
            {
                return;
            }

            _pickerField = TapAndHoldField.None;

            IsPickerOpen = false;
        }

        private void StepDelay(int step)
        {
            DelayMilliseconds = Math.Clamp(
                _delayMilliseconds + step,
                MinimumDelayMilliseconds,
                MaximumDelayMilliseconds);
        }

        /// <summary>
        /// §11.1's validation order — delay, tap action, hold action — then the write. The model has
        /// the last word: <c>SetTapAndHold</c> refuses a position that can never be remapped (05
        /// §5.3) and its answer is the panel's, so an assignment can never report success with
        /// nothing written.
        /// </summary>
        private void Assign()
        {
            if (_key is null || !IsAvailable)
            {
                return;
            }

            if (_delayMilliseconds < MinimumDelayMilliseconds || _delayMilliseconds > MaximumDelayMilliseconds)
            {
                ValidationMessage = InvalidDelayMessage;

                return;
            }

            if (_tapAction is not { } tapAction)
            {
                ValidationMessage = MissingTapActionMessage;

                return;
            }

            if (_holdAction is not { } holdAction)
            {
                ValidationMessage = MissingHoldActionMessage;

                return;
            }

            if (!_key.Key.SetTapAndHold(tapAction, holdAction, _delayMilliseconds))
            {
                ValidationMessage = LockedKeyMessage;

                return;
            }

            ValidationMessage = string.Empty;
            ArmedField = TapAndHoldField.None;

            _modelIsTapAndHold = true;
            _modelTapAction = tapAction;
            _modelHoldAction = holdAction;
            _modelDelayMilliseconds = _delayMilliseconds;

            Assigned?.Invoke(this, EventArgs.Empty);
        }

        private void OpenSupportPage()
        {
            if (_supportUrl is not null)
            {
                _urlLauncher.Open(_supportUrl);
            }
        }

        /// <summary>
        /// How a field spells what is in it: the <b>bracketed file token</b> — <c>[lctrl]</c> — and
        /// not the cap's friendly caption.
        /// <para>
        /// Mockup <c>2h</c> draws the two fields as <c>[j]</c> and <c>[lctrl]</c>, and the rail's own
        /// assignment line above them already speaks that way, so a field reading <c>Left Ctrl</c>
        /// would put two spellings of one value in one column. It is also the mono law's own answer:
        /// mono means "this is literally a value in a config file", and <c>lctrl</c> is, while
        /// <c>Left Ctrl</c> is the app's rendering of it. The overlay this panel replaces used the
        /// caption, which on a stacked legend is <em>two lines</em> — in a 268 px rail that wrapped
        /// the field to double height.
        /// </para>
        /// </summary>
        private string Describe(KeyDefinition? definition)
        {
            return definition is null ? string.Empty : KeyboardKeyViewModel.FormatToken(definition, _dialect);
        }

        /// <summary>
        /// The profile's tap-and-hold budget advisory, picked out of the set by its <b>anchor</b>:
        /// it is the only advisory on the Layout tab that names neither a layer nor a position,
        /// because <see cref="KeyboardLayout.Validate"/> reports the count for the whole layout
        /// while every other Layout-tab advisory is a duplicate token on one position. Matching the
        /// shape rather than the sentence keeps the wording in <see cref="AdvisoryText"/> alone.
        /// </summary>
        private static string FindBudgetAdvisory(EditorAdvisories advisories)
        {
            foreach (var advisory in advisories.All)
            {
                if (advisory.Tab == EditorTab.Keys && advisory.LayerIndex is null && advisory.KeyIndex is null)
                {
                    return advisory.Message;
                }
            }

            return string.Empty;
        }
    }
}
