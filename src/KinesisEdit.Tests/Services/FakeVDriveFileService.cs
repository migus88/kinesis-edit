using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// In-memory <see cref="IVDriveFileService"/> for tests that only need file content, not the
    /// spec's byte-level write semantics. <see cref="UpdateSettingsFile"/> records the attempt
    /// without merging: the merge rule of specs/08-settings.md §1 is exercised against the real
    /// service over a temp directory, so no second implementation of it can drift, but the write
    /// must still be observable — a fake that threw instead would let
    /// <see cref="KinesisEdit.Services.VDriveNotificationSuppressionStore"/> swallow it and make
    /// "demo mode persists nothing" pass even when demo mode persists everything.
    /// </summary>
    internal sealed class FakeVDriveFileService : IVDriveFileService
    {
        public int ReadCount { get; private set; }

        public List<string> WrittenPaths { get; } = [];

        public List<KeyValuePair<string, string>> SettingsUpdates { get; } = [];

        /// <summary>
        /// When set, a write blocks until the test completes this source — the only way to observe
        /// an editor while its save is genuinely in flight. Blocking is deliberate: the real service
        /// is synchronous and the editors call it from <see cref="Task.Run(Action)"/>.
        /// </summary>
        public TaskCompletionSource? WriteGate { get; set; }

        private readonly Dictionary<string, IReadOnlyList<string>> _files = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unreadablePaths = new(StringComparer.Ordinal);

        public void SetFile(string path, params string[] lines)
        {
            _files[path] = lines;
            _unreadablePaths.Remove(path);
        }

        public void RemoveFile(string path)
        {
            _files.Remove(path);
        }

        public void SetUnreadable(string path)
        {
            _unreadablePaths.Add(path);
        }

        public IReadOnlyList<string> ReadAllLines(string path)
        {
            ReadCount++;

            if (_unreadablePaths.Contains(path))
            {
                throw new IOException($"{path} cannot be read");
            }

            if (!_files.TryGetValue(path, out var lines))
            {
                throw new FileNotFoundException($"{path} not found", path);
            }

            return lines;
        }

        public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
        {
            WriteGate?.Task.Wait();

            if (!allowCreate && !_files.ContainsKey(path))
            {
                throw new FileNotFoundException($"{path} not found", path);
            }

            WrittenPaths.Add(path);
            _files[path] = lines;
        }

        public void UpdateSettingsFile(string path, IEnumerable<KeyValuePair<string, string>> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            if (!_files.ContainsKey(path))
            {
                throw new FileNotFoundException($"{path} not found", path);
            }

            WrittenPaths.Add(path);
            SettingsUpdates.AddRange(values);
        }
    }
}
