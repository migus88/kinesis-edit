using System.Text.Json;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Reads a <see cref="HostPreferences"/> out of the JSON file's text — <b>tolerantly</b>, in
    /// the sense every file engine in this repo means it: an absent file, an unparseable one, an
    /// unknown key, a value of the wrong type and a number outside its range all degrade to the
    /// default for that field, and <see cref="Parse"/> never throws.
    /// <para>
    /// That is why this reads a <see cref="JsonDocument"/> field by field rather than calling
    /// <c>JsonSerializer.Deserialize&lt;T&gt;</c>: the serializer throws on a wrong-typed value and
    /// takes the whole file with it, so one hand-edited line would cost the user their theme
    /// <i>and</i> their window position. Field-by-field, a bad <c>theme</c> costs the theme only.
    /// </para>
    /// <para>
    /// Matching is case-insensitive, both for property names and for enum values, mirroring the
    /// case-insensitive parse rule of the on-device file formats (specs/README.md).
    /// </para>
    /// </summary>
    public static class HostPreferencesParser
    {
        /// <summary>
        /// The preferences <paramref name="json"/> describes, with anything unreadable falling back
        /// to <see cref="HostPreferences.Default"/>. Null, empty and whitespace are the "no file
        /// yet" case and are not errors.
        /// </summary>
        public static HostPreferences Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return HostPreferences.Default;
            }

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                return HostPreferences.Default;
            }

            using (document)
            {
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return HostPreferences.Default;
                }

                return new HostPreferences
                {
                    Theme = ReadEnum(root, HostPreferencesJsonNames.Theme, HostPreferences.Default.Theme),
                    Motion = ReadEnum(root, HostPreferencesJsonNames.Motion, HostPreferences.Default.Motion),
                    Window = ReadWindow(root)
                };
            }
        }

        private static WindowGeometry? ReadWindow(JsonElement root)
        {
            if (!TryGetProperty(root, HostPreferencesJsonNames.Window, out var window)
                || window.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var width = ReadNumber(window, HostPreferencesJsonNames.Width);
            var height = ReadNumber(window, HostPreferencesJsonNames.Height);

            if (width is null || height is null)
            {
                return null;
            }

            // TryCreate owns the range rule; a stored size outside it means "no stored geometry",
            // which is the fresh-install state rather than a window that cannot be shown.
            return WindowGeometry.TryCreate(
                width.Value,
                height.Value,
                ReadCoordinate(window, HostPreferencesJsonNames.X),
                ReadCoordinate(window, HostPreferencesJsonNames.Y),
                ReadBoolean(window, HostPreferencesJsonNames.Maximized));
        }

        private static TEnum ReadEnum<TEnum>(JsonElement parent, string name, TEnum fallback)
            where TEnum : struct, Enum
        {
            if (!TryGetProperty(parent, name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return fallback;
            }

            var text = value.GetString();

            if (!Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                // Enum.TryParse also accepts the underlying number as text ("7"), which IsDefined
                // is what rejects: a value this app does not model is not a preference.
                return fallback;
            }

            return parsed;
        }

        private static double? ReadNumber(JsonElement parent, string name)
        {
            if (!TryGetProperty(parent, name, out var value) || value.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            return value.TryGetDouble(out var number) ? number : null;
        }

        private static int? ReadCoordinate(JsonElement parent, string name)
        {
            var number = ReadNumber(parent, name);

            if (number is null || !double.IsFinite(number.Value) || number.Value < int.MinValue || number.Value > int.MaxValue)
            {
                return null;
            }

            return (int)number.Value;
        }

        private static bool ReadBoolean(JsonElement parent, string name)
        {
            return TryGetProperty(parent, name, out var value) && value.ValueKind == JsonValueKind.True;
        }

        private static bool TryGetProperty(JsonElement parent, string name, out JsonElement value)
        {
            foreach (var property in parent.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;

                    return true;
                }
            }

            value = default;

            return false;
        }
    }
}
