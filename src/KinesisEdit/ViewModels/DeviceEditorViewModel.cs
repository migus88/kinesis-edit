using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the shell knows about whatever it swapped in for an open device
    /// (specs/10-apps-and-ui.md, "Opening a device"): the snapshot Configure was pressed on, its
    /// name, and whether the session runs in demo mode. Every editor derives from it, so
    /// <see cref="MainWindowViewModel.Editor"/> stays typed without the shell knowing which
    /// device-specific editor it holds — the branch lives in one place,
    /// <see cref="MainWindowViewModel.OpenDevice"/>.
    /// </summary>
    public abstract class DeviceEditorViewModel : ViewModelBase
    {
        /// <summary>The device this editor was opened for.</summary>
        public DeviceSnapshot Device { get; }

        /// <summary>Name of the open device.</summary>
        public string DeviceName => Device.DisplayName;

        /// <summary>Whether the device was opened without a connected, writable drive (03 §3.5).</summary>
        public bool IsDemoMode => Device.IsDemoMode;

        /// <summary>Creates the editor for <paramref name="device"/>.</summary>
        protected DeviceEditorViewModel(DeviceSnapshot device)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
        }
    }
}
