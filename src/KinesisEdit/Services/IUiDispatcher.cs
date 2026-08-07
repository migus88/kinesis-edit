namespace KinesisEdit.Services
{
    /// <summary>
    /// Marshals a callback onto the UI thread. Exists because the v-Drive scan of
    /// specs/03-vdrive-and-files.md §3.3 runs off the UI thread
    /// (<c>KinesisEdit.Core.VDrive.Discovery.VDriveMonitor</c>, driven from a thread-pool thread so
    /// a stalled mount cannot freeze the window), while everything it feeds — view models and their
    /// observable collections — must be touched on the UI thread only.
    /// Tests substitute a synchronous implementation.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>Queues <paramref name="action"/> for execution on the UI thread.</summary>
        void Post(Action action);
    }
}
