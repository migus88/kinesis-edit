using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="ISystemClock"/> that only moves when a test moves it, so elapsed-time
    /// behaviour — the dashboard's "refreshed 0.4s ago" readout and the timestamp behind it — is
    /// asserted exactly instead of being slept for.
    /// </summary>
    internal sealed class FakeSystemClock : ISystemClock
    {
        /// <summary>An arbitrary fixed instant; tests assert against it rather than a real clock.</summary>
        public static readonly DateTimeOffset Start = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        public DateTimeOffset UtcNow { get; set; } = Start;

        public void Advance(TimeSpan amount)
        {
            UtcNow += amount;
        }
    }
}
