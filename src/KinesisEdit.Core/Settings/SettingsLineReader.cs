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

        /// <summary>
        /// Returns every line whose key starts with <paramref name="keyPrefix"/>, as (key remainder
        /// after the prefix, value) pairs <b>in file order</b>. For a key family whose full names
        /// are not known in advance — today only the macro names of
        /// <see cref="SettingsKeys.MacroNamePrefix"/>, whose key carries the profile, layer, trigger
        /// and slot (<see cref="MacroNameKey"/>).
        /// <para>
        /// The <c>=</c> rule of spec 08 §1 still holds, applied to the whole key: a line qualifies
        /// only when it contains a <c>=</c> <b>after</b> the prefix, and the key is everything
        /// before that first <c>=</c>. A line with nothing between the prefix and the separator is
        /// not a member of the family and is skipped, so no foreign line is ever claimed.
        /// </para>
        /// <para>
        /// Duplicates are returned, not collapsed: file order is preserved so a caller applying them
        /// in sequence lands on the same "last one wins" answer <see cref="FindValue"/> gives.
        /// </para>
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> FindPrefixedValues(
            IReadOnlyList<string> lines,
            string keyPrefix)
        {
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentException.ThrowIfNullOrEmpty(keyPrefix);

            var matches = new List<KeyValuePair<string, string>>();

            foreach (var line in lines)
            {
                if (!line.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(KeyValueSeparator, keyPrefix.Length);

                if (separatorIndex <= keyPrefix.Length)
                {
                    continue;
                }

                matches.Add(KeyValuePair.Create(
                    line[keyPrefix.Length..separatorIndex],
                    line[(separatorIndex + 1)..]));
            }

            return matches;
        }

        private static bool IsKeyLine(string line, string key)
        {
            return line.Length > key.Length
                && line[key.Length] == KeyValueSeparator
                && line.StartsWith(key, StringComparison.OrdinalIgnoreCase);
        }
    }
}
