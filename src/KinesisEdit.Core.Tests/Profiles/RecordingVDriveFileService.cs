using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// A path-keyed in-memory <see cref="IVDriveFileService"/> that records every call, for the
    /// tests of <see cref="Core.Profiles.ProfileSession"/>'s service seam. Nothing here touches a
    /// disk: a session driven through this service must be able to load and save with no drive
    /// mounted and no directory in existence, which is the whole point of the seam.
    /// <para>
    /// It keeps the real service's contracts that the session depends on: a read of an unknown
    /// path throws <see cref="FileNotFoundException"/>, a write to an unknown path is refused the
    /// same way unless <c>allowCreate</c> is set (specs/03-vdrive-and-files.md §5.2), and
    /// <c>UpdateSettingsFile</c> is a read-modify-write that leaves unmanaged lines alone
    /// (specs/08-settings.md §1).
    /// </para>
    /// </summary>
    internal sealed class RecordingVDriveFileService : IVDriveFileService
    {
        private const char KeyValueSeparator = '=';

        /// <summary>Every path handed to <see cref="ReadAllLines"/>, in call order.</summary>
        public List<string> ReadPaths { get; } = [];

        /// <summary>Every path handed to <see cref="WriteAllLines"/>, in call order.</summary>
        public List<string> WrittenPaths { get; } = [];

        /// <summary>Every path handed to <see cref="UpdateSettingsFile"/>, in call order.</summary>
        public List<string> MergedSettingsPaths { get; } = [];

        /// <summary>The <c>allowCreate</c> flag of each <see cref="WriteAllLines"/> call, in call order.</summary>
        public List<bool> WriteAllowCreateFlags { get; } = [];

        /// <summary>The key/value pairs of each <see cref="UpdateSettingsFile"/> call, flattened in call order.</summary>
        public List<KeyValuePair<string, string>> MergedSettingsValues { get; } = [];

        private readonly Dictionary<string, List<string>> _files = new(StringComparer.Ordinal);

        /// <summary>Seeds a file at <paramref name="path"/>, as if it were already on the drive.</summary>
        public void AddFile(string path, params string[] lines)
        {
            _files[path] = [.. lines];
        }

        /// <summary>The current content of <paramref name="path"/>, or null when no such file exists.</summary>
        public IReadOnlyList<string>? GetFile(string path)
        {
            return _files.TryGetValue(path, out var lines) ? lines : null;
        }

        public IReadOnlyList<string> ReadAllLines(string path)
        {
            ReadPaths.Add(path);

            if (!_files.TryGetValue(path, out var lines))
            {
                throw new FileNotFoundException("No such file in the in-memory drive.", path);
            }

            return lines;
        }

        public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
        {
            WrittenPaths.Add(path);
            WriteAllowCreateFlags.Add(allowCreate);

            if (!allowCreate && !_files.ContainsKey(path))
            {
                throw new FileNotFoundException("No such file in the in-memory drive.", path);
            }

            _files[path] = [.. lines];
        }

        public void UpdateSettingsFile(
            string path,
            IEnumerable<KeyValuePair<string, string>> values,
            IEnumerable<string>? removedKeys = null)
        {
            MergedSettingsPaths.Add(path);

            if (!_files.TryGetValue(path, out var lines))
            {
                throw new FileNotFoundException("No such file in the in-memory drive.", path);
            }

            foreach (var key in removedKeys ?? [])
            {
                lines.RemoveAll(line => HasKey(line, key));
            }

            foreach (var pair in values)
            {
                MergedSettingsValues.Add(pair);

                var index = lines.FindIndex(line => HasKey(line, pair.Key));
                var replacement = pair.Key + KeyValueSeparator + pair.Value;

                if (index < 0)
                {
                    lines.Add(replacement);
                }
                else
                {
                    lines[index] = replacement;
                }
            }
        }

        private static bool HasKey(string line, string key)
        {
            return line.Length > key.Length
                && line.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                && line[key.Length] == KeyValueSeparator;
        }
    }
}
