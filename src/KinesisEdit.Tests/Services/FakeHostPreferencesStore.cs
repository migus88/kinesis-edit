using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// An in-memory <see cref="IHostPreferencesStore"/> for everything above the store — view
    /// models, the appliers — that needs preferences but not a file. It keeps the two contract
    /// rules the real store has: a mutation always sees the newest state, and an equal result
    /// raises nothing.
    /// </summary>
    internal sealed class FakeHostPreferencesStore : IHostPreferencesStore
    {
        /// <inheritdoc />
        public HostPreferences Current { get; private set; } = HostPreferences.Default;

        /// <summary>How many times <see cref="Update"/> changed something.</summary>
        public int UpdateCount { get; private set; }

        /// <summary>How many times <see cref="Changed"/> was raised.</summary>
        public int ChangedCount { get; private set; }

        /// <inheritdoc />
        public event Action? Changed;

        public FakeHostPreferencesStore()
        {
            Changed += () => ChangedCount++;
        }

        public FakeHostPreferencesStore(HostPreferences current)
            : this()
        {
            Current = current;
        }

        /// <inheritdoc />
        public void Update(Func<HostPreferences, HostPreferences> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            var updated = mutate(Current);

            if (updated == Current)
            {
                return;
            }

            Current = updated;
            UpdateCount++;

            Changed?.Invoke();
        }
    }
}
