namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Read-side key lookup over settings-file lines per specs/08-settings.md §1, using the
    /// same rule as the write side (<c>VDriveFileService.UpdateSettingsFile</c>): a line
    /// carries key K iff it starts with K case-insensitively and the character at position
    /// K.Length is <c>=</c>; the value is everything after the <c>=</c>. Requiring the
    /// separator resolves the spec's prefix collisions (<c>v_drive</c> vs
    /// <c>v_drive_open_on_startup</c>, <c>cust_color_1</c> vs <c>cust_color_10</c>,
    /// <c>status</c> vs <c>status_play_speed</c>). Pure — operates on lines, never on files.
    /// </summary>
    public static class SettingsLineReader
    {
        private const char KeyValueSeparator = '=';

        /// <summary>
        /// Returns the raw value of <paramref name="key"/> in <paramref name="lines"/>, or null
        /// when no line carries the key. When several lines carry the key the last one wins,
        /// mirroring the legacy app's sequential line-by-line load (spec 08 §1).
        /// </summary>
        public static string? FindValue(IReadOnlyList<string> lines, string key)
        {
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentException.ThrowIfNullOrEmpty(key);

            string? value = null;

            foreach (var line in lines)
            {
                if (IsKeyLine(line, key))
                {
                    value = line[(key.Length + 1)..];
                }
            }

            return value;
        }

        private static bool IsKeyLine(string line, string key)
        {
            return line.Length > key.Length
                && line[key.Length] == KeyValueSeparator
                && line.StartsWith(key, StringComparison.OrdinalIgnoreCase);
        }
    }
}
