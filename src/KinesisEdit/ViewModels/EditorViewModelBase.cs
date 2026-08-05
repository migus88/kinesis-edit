using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the shell needs from any per-device editor, whichever one it swapped in: the device
    /// the session was opened for, its name, and whether it opened in demo mode
    /// (specs/03-vdrive-and-files.md §3.5). <see cref="MainWindowViewModel.Editor"/> is typed as
    /// this base so navigation, the status indicator, and the shell's tests are independent of
    /// which editor a device resolves to.
    /// </summary>
    public abstract class EditorViewModelBase : ViewModelBase
    {
        /// <summary>The device this editor was opened for.</summary>
        public DeviceSnapshot Device { get; }

        /// <summary>Name of the open device.</summary>
        public string DeviceName => Device.DisplayName;

        /// <summary>Whether the device was opened without a connected, writable drive (03 §3.5).</summary>
        public bool IsDemoMode => Device.IsDemoMode;

        /// <summary>Creates the editor for <paramref name="device"/>.</summary>
        protected EditorViewModelBase(DeviceSnapshot device)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
        }
    }
}
