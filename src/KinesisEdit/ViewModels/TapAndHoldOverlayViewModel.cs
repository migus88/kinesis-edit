using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Assign Tap and Hold Action overlay of specs/11-feature-dialogs.md §11.1: a Tap Action
    /// and a Hold Action, each filled by the next physical keystroke or by the Search Keys
    /// overlay, plus the timing delay that separates them.
    /// <para>
    /// The two gates §11.1 puts <em>before</em> the dialog are static members here rather than
    /// caller code: <see cref="TryCreate"/> runs the four pre-dialog checks
    /// (<see cref="TapAndHoldPrecheck"/>) and <see cref="EnsureFirmwareAvailableAsync"/> runs the
    /// firmware gate, so the editor only has to show whichever of the two answers comes back.
    /// </para>
    /// <para>
    /// The Advantage360's <c>'Macro'</c> buttons (§11.1, keyboard firmware >= 1.0.69) are
    /// deliberately absent — that board has no editor yet (issue #41).
    /// </para>
    /// </summary>
    public sealed class TapAndHoldOverlayViewModel : EditorOverlayViewModel, IKeystrokeSink
    {
        /// <summary>Dialog title (§11.1).</summary>
        public const string OverlayTitle = "Assign Tap and Hold Action";

        /// <summary>Label of the Tap Action field (§11.1).</summary>
        public const string TapActionLabel = "Tap Action";

        /// <summary>Hint of the Tap Action field (§11.1).</summary>
        public const string TapActionHint =
            "Designate the action sent when the key is tapped and released faster than the delay";

        /// <summary>Label of the Hold Action field (§11.1).</summary>
        public const string HoldActionLabel = "Hold Action";

        /// <summary>Hint of the Hold Action field (§11.1).</summary>
        public const string HoldActionHint =
            "Designate the action sent when the key is held longer than the delay";

        /// <summary>Label of the delay field (§11.1).</summary>
        public const string DelayLabel = "Delay (1-999ms)";

        /// <summary>Hint of the delay field (§11.1).</summary>
        public const string DelayHint =
            "Designate the time interval used to differentiate between the Tap and Hold actions";

        /// <summary>Hint of both Search buttons (§11.1).</summary>
        public const string SearchHint = "Search for tokens";

        /// <summary>The static note under the fields (§11.1).</summary>
        public const string NoteText = "Tap action is not sent until key is released.";

        /// <summary>Validation for a delay outside the device's range (§11.1).</summary>
        public const string InvalidDelayMessage = "Please select a timing delay between 1ms and 999ms.";

        /// <summary>Validation for a missing tap action (§11.1).</summary>
        public const string MissingTapActionMessage = "Please select a Tap Action";

        /// <summary>Validation for a missing hold action (§11.1).</summary>
        public const string MissingHoldActionMessage = "Please select a Hold Action";

        /// <summary>
        /// Shown when <see cref="KeyboardKey.SetTapAndHold(KeyDefinition, KeyDefinition, int)"/>
        /// refuses the position (05 §5.3, a key that can never be remapped). This app's wording —
        /// §11.1 quotes none, because the legacy apps never offered the dialog on such a key
        /// either; it is here so a refusal can never close the panel reporting success.
        /// </summary>
        public const string LockedKeyMessage = "This key position cannot be programmed.";

        /// <summary>
        /// The firmware refusal of §11.1, used for the gate rows that carry no message of their
        /// own — the specs quote it under the Freestyle apps, but §11.1 describes one refusal for
        /// every gated app and the Advantage2 and Edge RGB rows store none
        /// (docs/app/firmware.md). Pinned identical to the Freestyle row by test.
        /// </summary>
        public const string FirmwareRefusalMessage =
            "To utilize Tap and Hold Actions, please download and install the latest firmware.";

        /// <summary>
        /// Delay range used only when the device's <see cref="TapAndHoldCapability"/> states
        /// none — a device without the feature, which never opens this overlay. The supported
        /// devices all carry the range as catalog data and it is read from there.
        /// </summary>
        private const int FallbackMinimumDelayMilliseconds = 1;

        /// <inheritdoc cref="FallbackMinimumDelayMilliseconds" />
        private const int FallbackMaximumDelayMilliseconds = 999;

        /// <summary>
        /// Runs the four pre-dialog checks of §11.1 against <paramref name="key"/> and either
        /// builds the overlay or reports the refusal the legacy app would have shown.
        /// </summary>
        public static TapAndHoldOpenResult TryCreate(KeyboardLayout layout, KeyboardLayer layer, KeyboardKey key)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(layer);
            ArgumentNullException.ThrowIfNull(key);

            var refusal = TapAndHoldPrecheck.Evaluate(layout, layer, key);

            if (refusal != TapAndHoldRefusal.None)
            {
                return TapAndHoldOpenResult.Refused(TapAndHoldPrecheck.MessageFor(refusal));
            }

            return TapAndHoldOpenResult.Allowed(
                new TapAndHoldOverlayViewModel(key, layout.Device.TapAndHold, layout.Dialect));
        }

        /// <summary>
        /// Whether the device's firmware clears the tap-and-hold gate (09 §2). A refusal shows
        /// §11.1's message with the <c>'Update Firmware'</c> button and returns false; demo mode
        /// always passes.
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
                OverlayTitle,
                FirmwareRefusalMessage,
                notifications,
                urlLauncher);
        }

        /// <summary>The action sent on a tap, or null while the field is empty.</summary>
        public KeyDefinition? TapAction
        {
            get => _tapAction;
            private set
            {
                if (SetProperty(ref _tapAction, value))
                {
                    OnPropertyChanged(nameof(TapActionText));
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
                }
            }
        }

        /// <summary>What the Tap Action field shows; empty while it is unset.</summary>
        public string TapActionText => Describe(TapAction);

        /// <summary>What the Hold Action field shows; empty while it is unset.</summary>
        public string HoldActionText => Describe(HoldAction);

        /// <summary>
        /// The timing delay in milliseconds. Deliberately unclamped on assignment — §11.1 clamps
        /// the field's Up/Down arrows (<see cref="IncreaseDelayCommand"/>,
        /// <see cref="DecreaseDelayCommand"/>) and validates the value on Ok, so an out-of-range
        /// value has to survive long enough to be reported.
        /// </summary>
        public int DelayMilliseconds
        {
            get => _delayMilliseconds;
            set => SetProperty(ref _delayMilliseconds, value);
        }

        /// <summary>Lowest delay the device accepts (1 ms; 11 §11.1, 04 §5.3).</summary>
        public int MinimumDelayMilliseconds { get; }

        /// <summary>Highest delay the device accepts (999 ms; 11 §11.1, 04 §5.3).</summary>
        public int MaximumDelayMilliseconds { get; }

        /// <summary>Which field the next captured keystroke fills, if any.</summary>
        public TapAndHoldField ArmedField
        {
            get => _armedField;
            private set
            {
                if (SetProperty(ref _armedField, value))
                {
                    OnPropertyChanged(nameof(WantsKeystrokes));
                }
            }
        }

        /// <inheritdoc />
        public bool WantsKeystrokes => !IsClosed && ArmedField != TapAndHoldField.None;

        /// <summary>Arms the Tap Action field; the view runs it when the field takes focus.</summary>
        public IRelayCommand ArmTapActionCommand { get; }

        /// <summary>Arms the Hold Action field.</summary>
        public IRelayCommand ArmHoldActionCommand { get; }

        /// <summary>The Tap Action Search button (§11.1); raises <see cref="SearchRequested"/>.</summary>
        public IRelayCommand SearchTapActionCommand { get; }

        /// <summary>The Hold Action Search button (§11.1).</summary>
        public IRelayCommand SearchHoldActionCommand { get; }

        /// <summary>The delay field's Up arrow, clamped to the device's range (§11.1).</summary>
        public IRelayCommand IncreaseDelayCommand { get; }

        /// <summary>The delay field's Down arrow, clamped to the device's range (§11.1).</summary>
        public IRelayCommand DecreaseDelayCommand { get; }

        /// <summary>
        /// Raised with a ready-to-show Search Keys overlay when either Search button is pressed.
        /// The editor only has to display it and drop it when it closes: the title is already the
        /// §11.6 one for the field, and the picked action is written back here.
        /// </summary>
        public event Action<SearchKeysOverlayViewModel>? SearchRequested;

        private readonly KeyboardKey _key;
        private readonly TokenDialect _dialect;

        private KeyDefinition? _tapAction;
        private KeyDefinition? _holdAction;
        private int _delayMilliseconds;
        private TapAndHoldField _armedField;

        /// <summary>
        /// Creates the overlay for <paramref name="key"/>. The delay range and its starting value
        /// come from <paramref name="capability"/> — 250 ms, or the Advantage360's 150 ms — never
        /// from a literal here.
        /// </summary>
        public TapAndHoldOverlayViewModel(KeyboardKey key, TapAndHoldCapability capability, TokenDialect dialect)
            : base(OverlayTitle)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(capability);

            _key = key;
            _dialect = dialect;

            var range = capability.DelayMilliseconds;

            MinimumDelayMilliseconds = range?.Minimum ?? FallbackMinimumDelayMilliseconds;
            MaximumDelayMilliseconds = range?.Maximum ?? FallbackMaximumDelayMilliseconds;

            _tapAction = key.TapAction;
            _holdAction = key.HoldAction;
            _delayMilliseconds = key.IsTapAndHold ? key.TimingDelay : capability.DefaultDelayMilliseconds ?? 0;

            ArmTapActionCommand = new RelayCommand(() => Arm(TapAndHoldField.Tap));
            ArmHoldActionCommand = new RelayCommand(() => Arm(TapAndHoldField.Hold));
            SearchTapActionCommand = new RelayCommand(() => OpenSearch(TapAndHoldField.Tap));
            SearchHoldActionCommand = new RelayCommand(() => OpenSearch(TapAndHoldField.Hold));
            IncreaseDelayCommand = new RelayCommand(() => StepDelay(1));
            DecreaseDelayCommand = new RelayCommand(() => StepDelay(-1));
        }

        /// <inheritdoc />
        public void ReceiveKeystroke(CapturedKeystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            if (!WantsKeystrokes)
            {
                return;
            }

            Assign(ArmedField, keystroke.Key);
        }

        /// <summary>
        /// Validates in the order of §11.1's table — delay, tap action, hold action — and applies
        /// the assignment to the key. The model has the last word: <c>SetTapAndHold</c> refuses a
        /// position that can never be remapped (05 §5.3) and its answer is the panel's, so an
        /// accept can never close reporting success with nothing written.
        /// </summary>
        protected override bool TryAccept()
        {
            if (DelayMilliseconds < MinimumDelayMilliseconds || DelayMilliseconds > MaximumDelayMilliseconds)
            {
                ErrorMessage = InvalidDelayMessage;

                return false;
            }

            if (TapAction is not { } tapAction)
            {
                ErrorMessage = MissingTapActionMessage;

                return false;
            }

            if (HoldAction is not { } holdAction)
            {
                ErrorMessage = MissingHoldActionMessage;

                return false;
            }

            if (!_key.SetTapAndHold(tapAction, holdAction, DelayMilliseconds))
            {
                ErrorMessage = LockedKeyMessage;

                return false;
            }

            return true;
        }

        private void Arm(TapAndHoldField field)
        {
            if (IsClosed)
            {
                return;
            }

            ArmedField = field;
        }

        private void OpenSearch(TapAndHoldField field)
        {
            if (IsClosed)
            {
                return;
            }

            // The picker owns the keyboard while it is up, so nothing here may keep capturing.
            ArmedField = TapAndHoldField.None;

            var overlay = new SearchKeysOverlayViewModel(TitleFor(field), _dialect);

            overlay.Selected += definition => Assign(field, definition);

            SearchRequested?.Invoke(overlay);
        }

        private void Assign(TapAndHoldField field, KeyDefinition definition)
        {
            switch (field)
            {
                case TapAndHoldField.Tap:
                    TapAction = definition;

                    break;

                case TapAndHoldField.Hold:
                    HoldAction = definition;

                    break;
            }

            ArmedField = TapAndHoldField.None;
        }

        private void StepDelay(int step)
        {
            DelayMilliseconds = Math.Clamp(
                DelayMilliseconds + step,
                MinimumDelayMilliseconds,
                MaximumDelayMilliseconds);
        }

        private string Describe(KeyDefinition? definition)
        {
            return definition is null ? string.Empty : KeyCaption.For(definition, _dialect, KeyCaption.IsMacOs);
        }

        private static string TitleFor(TapAndHoldField field)
        {
            return field == TapAndHoldField.Hold
                ? SearchKeysOverlayViewModel.HoldActionTitle
                : SearchKeysOverlayViewModel.TapActionTitle;
        }
    }
}
