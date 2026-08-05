using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the shell knows about whatever it swapped in for an open device
    /// (specs/10-apps-and-ui.md, "Opening a device"): the snapshot Configure was pressed on, its
    /// name, and whether the session runs in demo mode. Every editor derives from it, so
    /// <see cref="MainWindowViewModel.Editor"/> stays typed without the shell knowing which
    /// device-specific editor it holds — the branch lives in one place,
    /// <see cref="KinesisEdit.Services.IEditorViewModelFactory"/>.
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

        /// <summary>
        /// Work the editor does after it is on screen; the shell fires it once and does not await
        /// it. Editors that read the v-Drive do it here rather than in their constructor, so the
        /// view swap is never blocked by I/O — and because nothing awaits the task, an override
        /// must report its own failures instead of throwing. The default does nothing.
        /// </summary>
        public virtual Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Whether the shell may close this editor — its chance to ask about unsaved work before
        /// Home or another device replaces it. Returning false aborts the navigation and leaves the
        /// editor on screen. Like <see cref="LoadAsync"/> it must be total: the shell awaits it from
        /// a command, so a throw would surface as an unhandled failure rather than a dialog. The
        /// default lets every navigation through — an editor that cannot lose anything has nothing
        /// to ask.
        /// </summary>
        public virtual Task<bool> ConfirmCloseAsync()
        {
            return Task.FromResult(true);
        }
    }
}
