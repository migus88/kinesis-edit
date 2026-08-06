namespace KinesisEdit.Services
{
    /// <summary>
    /// The production <see cref="ISystemClock"/>: the machine clock, read in UTC so a daylight-saving
    /// shift can never make an elapsed-time readout run backwards.
    /// </summary>
    public sealed class SystemClock : ISystemClock
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
