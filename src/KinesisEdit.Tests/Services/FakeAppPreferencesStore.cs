using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IAppPreferencesStore"/> holding preferences in memory and recording
    /// every write, so the suppression policy and the preferences screen can be checked without a
    /// filesystem. The suppression pair is kept as its own dictionary rather than derived from
    /// <see cref="Current"/>, so a test can stage a key the catalog does not model.
    /// </summary>
    internal sealed class FakeAppPreferencesStore : IAppPreferencesStore
    {
        public List<KeyValuePair<string, bool>> Writes { get; } = [];

        public AppSettings Current { get; private set; } = AppSettings.Empty;

        public bool IsWritable { get; set; } = true;

        public int UpdateCount { get; private set; }

        public event Action? Changed;

        private readonly Dictionary<string, bool> _hiddenKeys = new(StringComparer.OrdinalIgnoreCase);

        public void SetInitiallyHidden(string key)
        {
            _hiddenKeys[key] = true;
        }

        public void SetInitial(AppSettings settings)
        {
            Current = settings;
        }

        public bool IsHidden(string key)
        {
            return _hiddenKeys.TryGetValue(key, out var isHidden) && isHidden;
        }

        public void SetHidden(string key, bool hidden)
        {
            Writes.Add(KeyValuePair.Create(key, hidden));
            _hiddenKeys[key] = hidden;
        }

        public void Update(Func<AppSettings, AppSettings> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            UpdateCount++;
            Current = mutate(Current);

            Changed?.Invoke();
        }
    }
}
