using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="INotificationSuppressionStore"/> holding preferences in memory and
    /// recording every write, so the suppression policy can be checked without a filesystem.
    /// </summary>
    internal sealed class FakeNotificationSuppressionStore : INotificationSuppressionStore
    {
        public List<KeyValuePair<string, bool>> Writes { get; } = [];

        private readonly Dictionary<string, bool> _hiddenKeys = new(StringComparer.OrdinalIgnoreCase);

        public void SetInitiallyHidden(string key)
        {
            _hiddenKeys[key] = true;
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
    }
}
