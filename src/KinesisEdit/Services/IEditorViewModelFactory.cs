using KinesisEdit.ViewModels;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Decides which editor a device opens into. The shell asks for one and swaps in whatever it
    /// gets, so it never learns which devices have an editor of their own yet — and, just as
    /// importantly, never has to hold their dependencies.
    /// </summary>
    public interface IEditorViewModelFactory
    {
        /// <summary>Creates the editor for <paramref name="device"/>; never returns null.</summary>
        DeviceEditorViewModel Create(DeviceSnapshot device);
    }
}
