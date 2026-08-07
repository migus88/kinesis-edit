namespace KinesisEdit.Services
{
    /// <summary>
    /// Whether the app is writing to a v-Drive right now. One fact, shared by whoever writes and
    /// whoever must keep out of the way while a write is in flight — today
    /// <see cref="DeviceLivenessWatcher"/>, which skips a tick rather than stat a mount point in
    /// the middle of a save.
    /// <para>
    /// It is a <b>counter</b>, not a flag: a save writes several files and a settings merge can
    /// nest inside one, so the app is writing until the last bracket closes.
    /// </para>
    /// </summary>
    public interface IVDriveWriteActivity
    {
        /// <summary>Whether at least one write bracket is currently open.</summary>
        bool IsWriting { get; }

        /// <summary>
        /// Opens a write bracket; disposing the returned scope closes it. Disposal is idempotent,
        /// so a scope released twice cannot drive the count below zero.
        /// </summary>
        IDisposable Begin();
    }
}
