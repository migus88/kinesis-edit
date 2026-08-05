namespace KinesisEdit.Services
{
    /// <summary>
    /// Read-only view of the active editing session, so services that only need to know which
    /// device is open (notably <see cref="NotificationService"/> and its suppression policy) do
    /// not depend on the session's lifecycle.
    /// </summary>
    public interface IDeviceSessionAccessor
    {
        /// <summary>The session currently open, or null while the dashboard is showing.</summary>
        DeviceSession? Active { get; }
    }
}
