using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Stand-in for the per-device editor the shell swaps in on Configure, used for every device
    /// that has no editor of its own yet. The Savant Elite2 has a real, read-only one
    /// (<see cref="SavantElitePedalViewModel"/>) and every device with an authored keyboard
    /// picture gets <see cref="KeyboardEditorViewModel"/> — today the Freestyle Edge RGB only,
    /// because <see cref="Core.Geometry.Visual.VisualCatalog"/> resolves nothing else (issues
    /// #39-#42 add the rest). This view model therefore only carries the navigation contract of
    /// specs/10-apps-and-ui.md ("Opening a device" / "Home") and what the shell already knows
    /// about the open device.
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
