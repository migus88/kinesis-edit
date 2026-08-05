using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Stand-in for the per-device editor the shell swaps in on Configure, used for every device
    /// whose keyboard picture has not been authored yet (<see cref="Core.Geometry.Visual.VisualCatalog"/>
    /// resolves the Freestyle Edge RGB only; issues #39-#42 add the rest). It carries the
    /// navigation contract of specs/10-apps-and-ui.md ("Opening a device" / "Home") and nothing
    /// else — the real editor is <see cref="DeviceEditorViewModel"/>.
    /// </summary>
    public sealed class EditorPlaceholderViewModel : EditorViewModelBase
    {
        /// <summary>Text shown in place of the editor until the real one exists.</summary>
        public const string PlaceholderMessage = "The editor for this device is not implemented yet.";

        /// <summary>The placeholder text.</summary>
        public string Message => PlaceholderMessage;

        /// <summary>Creates the placeholder for <paramref name="device"/>.</summary>
        public EditorPlaceholderViewModel(DeviceSnapshot device) : base(device)
        {
        }
    }
}
