using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The token picker's <c>Recent</c> chip: the actions this editing session has assigned, most
    /// recent first (mockups <c>1e</c>/<c>2a</c>, which draw <c>Recent</c> as the seventh chip).
    ///
    /// <para><b>Per-session and in memory, deliberately.</b> Nothing here is written to disk.
    /// Persisting it would be an <c>app_settings.txt</c> question — a new key, a new parser row and
    /// a migration — which belongs with the app-preferences work of issue #96 and not with a picker;
    /// the mockups never require the list to survive a restart, and a "recent" list that outlived the
    /// board it was collected on would be wrong on the next device anyway.</para>
    ///
    /// <para><b>Identity is the registry instance.</b> <see cref="KeyDefinition"/> is a record, so
    /// structural equality would fold two registrations that differ in nothing but their numeric
    /// code — which is exactly what an alias is (05 §7, "duplicated codes and tokens are
    /// intentional"). Every definition the app ever holds comes out of
    /// <see cref="KeyRegistry.Entries"/>, so reference identity is both available and exact, and it
    /// keeps the alias row and the row it duplicates as two separate memories.</para>
    ///
    /// <para><b>It is not a view model.</b> It carries no captions and raises no property change; it
    /// is state the picker reads and the panel writes, and it is deliberately outside
    /// <c>ViewModelBase</c> so <c>ViewResolutionTests</c> never has to name it.</para>
    /// </summary>
    public sealed class RecentTokenStore
    {
        /// <summary>
        /// How many actions are remembered. Small on purpose: the chip is a shortcut back to what
        /// the user has just been doing, and a list longer than the picker's own visible run of rows
        /// stops being that and becomes a second catalog.
        /// </summary>
        public const int Capacity = 8;

        /// <summary>The remembered actions, most recently assigned first.</summary>
        public IReadOnlyList<KeyDefinition> Entries => _entries;

        /// <summary>Whether anything has been assigned in this session yet.</summary>
        public bool IsEmpty => _entries.Count == 0;

        /// <summary>Raised whenever <see cref="Entries"/> moves, so an open picker can re-filter.</summary>
        public event EventHandler? Changed;

        private readonly List<KeyDefinition> _entries = [];

        /// <summary>
        /// Records <paramref name="key"/> as the most recent assignment. Re-assigning something
        /// already remembered moves it to the front rather than adding a second copy, and the oldest
        /// memory falls off the end once <see cref="Capacity"/> is reached.
        /// </summary>
        public void Remember(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            Forget(key);

            _entries.Insert(0, key);

            while (_entries.Count > Capacity)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Whether <paramref name="key"/> is one of the remembered actions.</summary>
        public bool Contains(KeyDefinition? key)
        {
            return key is not null && IndexOf(key) >= 0;
        }

        /// <summary>Empties the list — a new profile is a new session's worth of history.</summary>
        public void Clear()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _entries.Clear();

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Forget(KeyDefinition key)
        {
            var index = IndexOf(key);

            if (index >= 0)
            {
                _entries.RemoveAt(index);
            }
        }

        private int IndexOf(KeyDefinition key)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                if (ReferenceEquals(_entries[index], key))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
