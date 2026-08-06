namespace KinesisEdit.Services
{
    /// <summary>
    /// The current wall-clock time, as a seam. Exists so anything that renders elapsed time — the
    /// dashboard's "refreshed 0.4s ago" readout over
    /// <see cref="DeviceMonitorService.LastRefreshedUtc"/> — can be tested without sleeping.
    /// Toolkit-free by design: this is a plain service, not a UI timer.
    /// </summary>
    public interface ISystemClock
    {
        /// <summary>The current time, in UTC.</summary>
        DateTimeOffset UtcNow { get; }
    }
}
