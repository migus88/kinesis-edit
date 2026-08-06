using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro Timing Delays overlay of specs/11-feature-dialogs.md §11.3: pick the random
    /// delay or a custom one, and hand the caller the delay key to append to the macro being
    /// edited. The token rules (<c>dran</c>, <c>d001</c>..<c>d999</c>) are Core's
    /// (<see cref="MacroDelayTokens"/>); this view model owns the two controls, their exclusivity,
    /// and §11.3's validation.
    /// </summary>
    public sealed class MacroDelayOverlayViewModel : EditorOverlayViewModel
    {
        /// <summary>Dialog title (§11.3).</summary>
        public const string OverlayTitle = "Macro Timing Delays";

        /// <summary>Caption of the random-delay radio (§11.3).</summary>
        public const string RandomDelayCaption = "Random Delay (1-150ms)";

        /// <summary>Caption of the custom-delay field (§11.3).</summary>
        public const string CustomDelayCaption = "Custom Delay (1-999ms)";

        /// <summary>Validation shown on Ok when neither control names a usable delay (§11.3).</summary>
        public const string InvalidDelayMessage =
            "Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.";

        /// <summary>
        /// The firmware refusal of 09 §2 for custom and random delays, used when the gate row
        /// carries no message of its own. Only the Freestyle Edge/Pro gate this feature and their
        /// rows do carry it; pinned identical to them by test.
        /// </summary>
        public const string FirmwareRefusalMessage =
            "To utilize custom or random delays, please download and install the latest firmware.";

        /// <summary>
        /// Whether the device's firmware clears the custom/random delay gate (09 §2 — Freestyle
        /// Edge/Pro only; the RGB, TKO and Advantage360 are ungated and always pass). A refusal
        /// shows the message with the <c>'Update Firmware'</c> button and returns false; demo mode
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
                FirmwareFeature.CustomMacroDelays,
                firmware,
                OverlayTitle,
                FirmwareRefusalMessage,
                notifications,
                urlLauncher);
        }

        /// <summary>
        /// Whether the random-delay radio is checked. Neither control is preselected: §11.3's
        /// result is "random, 1..999, or cancel/invalid", so an untouched dialog has to be able to
        /// fail validation.
        /// </summary>
        public bool IsRandomDelay
        {
            get => _isRandomDelay;
            set => SetProperty(ref _isRandomDelay, value);
        }

        /// <summary>
        /// The custom delay in milliseconds; 0 means the field is empty. Assigning any value
        /// un-checks <see cref="IsRandomDelay"/> (§11.3: "typing in the custom field un-checks
        /// it"). Deliberately unclamped — §11.3 clamps the field's Up/Down arrows
        /// (<see cref="IncreaseDelayCommand"/>, <see cref="DecreaseDelayCommand"/>) and validates
        /// on Ok, so an out-of-range value has to survive long enough to be reported.
        /// </summary>
        public int CustomDelayMilliseconds
        {
            get => _customDelayMilliseconds;
            set
            {
                if (SetProperty(ref _customDelayMilliseconds, value))
                {
                    IsRandomDelay = false;
                }
            }
        }

        /// <summary>Lowest custom delay §11.3 accepts (1 ms).</summary>
        public int MinimumDelayMilliseconds => MacroDelayTokens.MinDelayMilliseconds;

        /// <summary>Highest custom delay §11.3 accepts (999 ms).</summary>
        public int MaximumDelayMilliseconds => MacroDelayTokens.MaxDelayMilliseconds;

        /// <summary>The delay key produced by the accepted overlay; null until then.</summary>
        public KeyDefinition? SelectedDelay
        {
            get => _selectedDelay;
            private set => SetProperty(ref _selectedDelay, value);
        }

        /// <summary>The custom field's Up arrow, clamped to 1-999 (§11.3).</summary>
        public IRelayCommand IncreaseDelayCommand { get; }

        /// <summary>The custom field's Down arrow, clamped to 1-999 (§11.3).</summary>
        public IRelayCommand DecreaseDelayCommand { get; }

        /// <summary>Raised once with the delay key when the overlay is accepted.</summary>
        public event Action<KeyDefinition>? Accepted;

        private readonly TokenDialect _dialect;

        private bool _isRandomDelay;
        private int _customDelayMilliseconds;
        private KeyDefinition? _selectedDelay;

        /// <summary>Creates the overlay resolving its delay keys in <paramref name="dialect"/>.</summary>
        public MacroDelayOverlayViewModel(TokenDialect dialect)
            : base(OverlayTitle)
        {
            _dialect = dialect;
            IncreaseDelayCommand = new RelayCommand(() => StepDelay(1));
            DecreaseDelayCommand = new RelayCommand(() => StepDelay(-1));
        }

        /// <inheritdoc />
        protected override bool TryAccept()
        {
            var delay = ResolveDelay();

            if (delay is null)
            {
                ErrorMessage = InvalidDelayMessage;

                return false;
            }

            SelectedDelay = delay;
            Accepted?.Invoke(delay);

            return true;
        }

        /// <summary>
        /// The delay key the current selection names, or null when there is none — an
        /// out-of-range custom value, or a dialect that does not name the token at all, which
        /// §11.3 cannot distinguish and reports with the same wording.
        /// </summary>
        private KeyDefinition? ResolveDelay()
        {
            if (IsRandomDelay)
            {
                return MacroDelayTokens.ResolveRandom(_dialect);
            }

            if (!MacroDelayTokens.IsValidDelay(CustomDelayMilliseconds))
            {
                return null;
            }

            return MacroDelayTokens.ResolveCustom(CustomDelayMilliseconds, _dialect);
        }

        private void StepDelay(int step)
        {
            CustomDelayMilliseconds = Math.Clamp(
                CustomDelayMilliseconds + step,
                MacroDelayTokens.MinDelayMilliseconds,
                MacroDelayTokens.MaxDelayMilliseconds);
        }
    }
}
