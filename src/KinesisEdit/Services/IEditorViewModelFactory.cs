using KinesisEdit.ViewModels;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Decides which editor a device opens into. The shell asks for one and swaps in whatever it
    /// gets, so it never learns which devices have a keyboard picture yet.
    /// </summary>
    public interface IEditorViewModelFactory
    {
        /// <summary>Creates the editor for <paramref name="device"/>; never returns null.</summary>
        EditorViewModelBase Create(DeviceSnapshot device);
    }
}
