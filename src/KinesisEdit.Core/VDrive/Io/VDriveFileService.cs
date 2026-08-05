using System.Text;

namespace KinesisEdit.Core.VDrive.Io
{
    /// <summary>
    /// Default <see cref="IVDriveFileService"/> implementation of specs/03-vdrive-and-files.md §5
    /// and specs/08-settings.md §1. All bytes are read and written with <see cref="Encoding.Latin1"/>,
    /// which round-trips bytes 0x00–0xFF losslessly and thereby implements the spec's
    /// "raw 8-bit strings, no encoding conversion, no BOM handling" rule; a BOM present in a
    /// file simply comes through as ordinary characters and is preserved.
    /// </summary>
    public sealed class VDriveFileService : IVDriveFileService
    {
        private const char CarriageReturn = '\r';

        private const char LineFeed = '\n';

        private const char KeyValueSeparator = '=';

        /// <summary>
        /// Reads all lines as Latin1 (specs/03-vdrive-and-files.md §5.1), splitting on CRLF,
        /// LF, or lone CR; a trailing final newline yields no empty last element.
        /// </summary>
        public IReadOnlyList<string> ReadAllLines(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ThrowIfFileMissing(path);

            // File.ReadAllText would detect and strip a BOM even with an explicit encoding;
            // decoding the raw bytes keeps every byte, per the "no BOM handling" rule.
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));

            return SplitLines(text);
        }

        /// <summary>
        /// Truncates and fully rewrites the file in place with Latin1 bytes, each line followed
        /// by <see cref="Environment.NewLine"/> (specs/03-vdrive-and-files.md §5.2). Refused
        /// with <see cref="FileNotFoundException"/> for a missing file unless
        /// <paramref name="allowCreate"/> is true; missing parent directories are never created.
        /// </summary>
        public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(lines);

            if (!allowCreate)
            {
                ThrowIfFileMissing(path);
            }

            var builder = new StringBuilder();

            foreach (var line in lines)
            {
                builder.Append(line);
                builder.Append(Environment.NewLine);
            }

            File.WriteAllText(path, builder.ToString(), Encoding.Latin1);
        }

        /// <summary>
        /// Read-modify-write settings update per specs/08-settings.md §1: a line matches a
        /// managed key iff it starts with the key case-insensitively and the next character is
        /// '=' (the separator requirement resolves the spec's prefix collisions, e.g. v_drive
        /// vs v_drive_open_on_startup and cust_color_1 vs cust_color_10). Every matching line
        /// is replaced in place with "key=value" using the caller's key casing; unmanaged lines
        /// are preserved verbatim in order; keys with no matching line are appended in caller
        /// order. The file must already exist (specs/03-vdrive-and-files.md §5.2).
        /// </summary>
        public void UpdateSettingsFile(string path, IEnumerable<KeyValuePair<string, string>> values)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(values);

            var lines = new List<string>(ReadAllLines(path));

            foreach (var pair in values)
            {
                ApplyManagedKey(lines, pair.Key, pair.Value);
            }

            WriteAllLines(path, lines);
        }

        private static void ApplyManagedKey(List<string> lines, string key, string value)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(value);

            var replacement = key + KeyValueSeparator + value;
            var isMatched = false;

            for (var index = 0; index < lines.Count; index++)
            {
                if (IsManagedKeyLine(lines[index], key))
                {
                    lines[index] = replacement;
                    isMatched = true;
                }
            }

            if (!isMatched)
            {
                lines.Add(replacement);
            }
        }

        private static bool IsManagedKeyLine(string line, string key)
        {
            return line.Length > key.Length
                && line[key.Length] == KeyValueSeparator
                && line.StartsWith(key, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            var lines = new List<string>();
            var start = 0;
            var index = 0;

            while (index < text.Length)
            {
                var current = text[index];

                if (current != CarriageReturn && current != LineFeed)
                {
                    index++;
                    continue;
                }

                lines.Add(text[start..index]);

                if (current == CarriageReturn && index + 1 < text.Length && text[index + 1] == LineFeed)
                {
                    index++;
                }

                index++;
                start = index;
            }

            if (start < text.Length)
            {
                lines.Add(text[start..]);
            }

            return lines;
        }

        private static void ThrowIfFileMissing(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"{path} not found", path);
            }
        }
    }
}
