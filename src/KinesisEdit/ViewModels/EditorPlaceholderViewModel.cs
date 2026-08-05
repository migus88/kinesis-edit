using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Stand-in for the per-device editor the shell swaps in on Configure. The keyboard editors
    /// are separate issues (#14-#16) — the Savant Elite2 already has a real, read-only one
    /// (<see cref="SavantElitePedalViewModel"/>) — so this view model only proves the navigation
    /// contract of specs/10-apps-and-ui.md ("Opening a device" / "Home") and carries what the
    /// shell already knows about the open device.
    /// </summary>
    public sealed class EditorPlaceholderViewModel : DeviceEditorViewModel
    {
        /// <summary>Text shown in place of the editor until the real one exists.</summary>
        public const string PlaceholderMessage = "The editor for this device is not implemented yet.";

        /// <summary>The placeholder text.</summary>
        public string Message => PlaceholderMessage;

        /// <summary>Creates the placeholder for <paramref name="device"/>.</summary>
        public EditorPlaceholderViewModel(DeviceSnapshot device)
            : base(device)
        {
        }
    }
}
