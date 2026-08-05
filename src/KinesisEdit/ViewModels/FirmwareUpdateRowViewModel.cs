using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the "Check for Updates" dialog (specs/09-firmware.md §3): a fixed label and a
    /// button whose caption is the row's state. All wording lives here — <c>KinesisEdit.Core</c>
    /// answers with <see cref="UpdateRowState"/> only. The button is clickable in exactly one
    /// state, <see cref="UpdateRowState.UpdateAvailable"/>, where §3 step 7 opens the support
    /// site; it never transfers firmware. The accent color and hand cursor of that state are
    /// mapped in XAML from <see cref="State"/>, so this view model stays free of Avalonia types.
    /// </summary>
    public sealed class FirmwareUpdateRowViewModel : ViewModelBase
    {
        /// <summary>Label of the keyboard-firmware row (note the spaced colon, verbatim from §3).</summary>
        public const string KeyboardRowLabel = "Keyboard Firmware :";

        /// <summary>Label of the lighting-firmware row.</summary>
        public const string LightingRowLabel = "Lighting Firmware :";

        /// <summary>Label of the app row.</summary>
        public const string AppRowLabel = "SmartSet App :";

        /// <summary>Initial button caption, before the check produces a result.</summary>
        public const string CheckingCaption = "Checking for update...";

        /// <summary>Button caption when the local version is older than the published one.</summary>
        public const string UpdateNowCaption = "Update Now";

        /// <summary>Button caption when the local version is current or newer.</summary>
        public const string UpToDateCaption = "No update available";

        /// <summary>Button caption when the device's own version file could not be read (§3 step 1).</summary>
        public const string LocalUnreadableCaption = "Error reading firmware file";

        /// <summary>Button caption when the manifest carries no keyboard version (§3 step 5).</summary>
        public const string KeyboardRemoteMissingCaption = "Error fetching keyboard firmware";

        /// <summary>Button caption when the manifest carries no lighting version.</summary>
        public const string LightingRemoteMissingCaption = "Error fetching lighting firmware";

        /// <summary>Button caption when the manifest carries no app version.</summary>
        public const string AppRemoteMissingCaption = "Error fetching app version";

        /// <summary>Button caption after a failed endpoint request (§3 step 6).</summary>
        public const string ConnectionErrorCaption = "Check connection";

        /// <summary>The row's fixed label.</summary>
        public static string GetLabel(UpdateRowKind kind)
        {
            return kind switch
            {
                UpdateRowKind.Keyboard => KeyboardRowLabel,
                UpdateRowKind.Lighting => LightingRowLabel,
                UpdateRowKind.App => AppRowLabel,
                _ => string.Empty
            };
        }

        /// <summary>
        /// The §3 caption for a state; the "Error fetching …" wording of
        /// <see cref="UpdateRowState.RemoteMissing"/> is the only one that also depends on the row.
        /// </summary>
        public static string GetButtonCaption(UpdateRowKind kind, UpdateRowState state)
        {
            return state switch
            {
                UpdateRowState.Checking => CheckingCaption,
                UpdateRowState.UpdateAvailable => UpdateNowCaption,
                UpdateRowState.UpToDate => UpToDateCaption,
                UpdateRowState.LocalUnreadable => LocalUnreadableCaption,
                UpdateRowState.RemoteMissing => GetRemoteMissingCaption(kind),
                UpdateRowState.ConnectionError => ConnectionErrorCaption,
                _ => string.Empty
            };
        }

        /// <summary>The comparison result this row renders.</summary>
        public UpdateCheckRow Row { get; }

        /// <summary>Which of the three rows this is.</summary>
        public UpdateRowKind Kind => Row.Kind;

        /// <summary>The row's state; XAML maps it to the accent style of the actionable state.</summary>
        public UpdateRowState State => Row.State;

        /// <summary>The row's label.</summary>
        public string Label => GetLabel(Kind);

        /// <summary>The button's caption for the current state.</summary>
        public string ButtonCaption => GetButtonCaption(Kind, State);

        /// <summary>
        /// Whether the button does anything: only an update that is actually available and has a
        /// support page behind it (§3 steps 5 and 7). Every other state renders as a disabled button.
        /// </summary>
        public bool IsActionable => State == UpdateRowState.UpdateAvailable && !string.IsNullOrWhiteSpace(Row.TargetUrl);

        /// <summary>Opens the row's support page; does nothing when the row is not actionable.</summary>
        public IRelayCommand OpenCommand { get; }

        private readonly IUrlLauncher _urlLauncher;

        /// <summary>Creates the view model for <paramref name="row"/>.</summary>
        public FirmwareUpdateRowViewModel(UpdateCheckRow row, IUrlLauncher urlLauncher)
        {
            Row = row ?? throw new ArgumentNullException(nameof(row));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));

            OpenCommand = new RelayCommand(Open, () => IsActionable);
        }

        private static string GetRemoteMissingCaption(UpdateRowKind kind)
        {
            return kind switch
            {
                UpdateRowKind.Keyboard => KeyboardRemoteMissingCaption,
                UpdateRowKind.Lighting => LightingRemoteMissingCaption,
                UpdateRowKind.App => AppRemoteMissingCaption,
                _ => string.Empty
            };
        }

        private void Open()
        {
            // The guard is not redundant with CanExecute: a command can be invoked directly, and
            // "Update Now" is the only caption the spec attaches an action to.
            if (!IsActionable)
            {
                return;
            }

            _urlLauncher.Open(Row.TargetUrl!);
        }
    }
}
