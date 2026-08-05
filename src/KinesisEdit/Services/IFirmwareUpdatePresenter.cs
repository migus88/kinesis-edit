namespace KinesisEdit.Services
{
    /// <summary>
    /// Shows the "Check for Updates" dialog of specs/09-firmware.md §3 for a device and completes
    /// when the user closes it. Implemented by the view layer, exactly like
    /// <see cref="IMessageBoxPresenter"/>, so the dashboard's view models can open the dialog
    /// without referencing a UI toolkit.
    /// </summary>
    public interface IFirmwareUpdatePresenter
    {
        /// <summary>Presents the dialog for <paramref name="device"/> and completes once it closes.</summary>
        Task PresentAsync(DeviceSnapshot device);
    }
}
