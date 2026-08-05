using System.Text.Json;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The parsed body of the published-versions endpoint (specs/09-firmware.md §3 step 3),
    /// e.g. <c>{"keyboard_ver":"1.0.0","lighting_ver":"1.0.0","app_ver":"2.0.20", "mac_app_ver":"2.1.3"}</c>.
    /// Every documented key is optional and unknown keys are kept (read tolerantly), so the
    /// manifest is a case-insensitive key/value bag with typed accessors on top. The exact
    /// response text is preserved in <see cref="RawJson"/> because firmware-debug mode dumps
    /// it in a dialog (§3 step 3). Pure data — fetching is <see cref="IVersionManifestClient"/>.
    /// </summary>
    public sealed class VersionManifest
    {
        /// <summary>The manifest with no values at all; also the out value of a failed parse.</summary>
        public static VersionManifest Empty { get; } = new(string.Empty, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        private static readonly JsonDocumentOptions _parseOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        /// <summary>
        /// Parses an endpoint response. Returns false — with <paramref name="manifest"/> set to
        /// <see cref="Empty"/>, never null — when the text is null, blank, not valid JSON, or not
        /// a JSON object; that is the "malformed payload" signal, distinct from a key simply being
        /// absent (which <see cref="GetValue"/> reports as null). String values are taken as-is;
        /// numeric and boolean values are read as their raw text; object, array, and null values
        /// are skipped. Values that are empty or whitespace are dropped, so a present-but-empty
        /// key is indistinguishable from a missing one — both count as absent (§3 step 5 only
        /// needs "no usable remote value").
        /// </summary>
        public static bool TryParse(string? json, out VersionManifest manifest)
        {
            manifest = Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json, _parseOptions);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!TryReadValue(property.Value, out var value))
                    {
                        continue;
                    }

                    values[property.Name] = value;
                }

                manifest = new VersionManifest(json, values);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>The response text exactly as received; empty for <see cref="Empty"/>.</summary>
        public string RawJson { get; }

        /// <summary>Every usable key/value pair of the payload, including keys this app does not know. Case-insensitive.</summary>
        public IReadOnlyDictionary<string, string> Values { get; }

        /// <summary>Edge RGB keyboard firmware (<c>keyboard_ver</c>); null when absent.</summary>
        public string? KeyboardVersion => GetValue(VersionManifestKeys.KeyboardVersion);

        /// <summary>Edge RGB lighting firmware (<c>lighting_ver</c>); null when absent.</summary>
        public string? LightingVersion => GetValue(VersionManifestKeys.LightingVersion);

        /// <summary>Windows gaming app version (<c>app_ver</c>); null when absent.</summary>
        public string? WindowsGamingAppVersion => GetValue(VersionManifestKeys.WindowsGamingAppVersion);

        /// <summary>Windows office app version (<c>pc_app_version</c>); null when absent.</summary>
        public string? WindowsOfficeAppVersion => GetValue(VersionManifestKeys.WindowsOfficeAppVersion);

        /// <summary>macOS gaming app version (<c>mac_app_ver</c>); null when absent.</summary>
        public string? MacGamingAppVersion => GetValue(VersionManifestKeys.MacGamingAppVersion);

        /// <summary>macOS office app version (<c>mac_app_version</c>); null when absent.</summary>
        public string? MacOfficeAppVersion => GetValue(VersionManifestKeys.MacOfficeAppVersion);

        /// <summary>TKO keyboard firmware (<c>tko_keyboard_version</c>); null when absent.</summary>
        public string? TkoKeyboardVersion => GetValue(VersionManifestKeys.TkoKeyboardVersion);

        /// <summary>TKO lighting firmware (<c>tko_lighting_version</c>); null when absent.</summary>
        public string? TkoLightingVersion => GetValue(VersionManifestKeys.TkoLightingVersion);

        /// <summary>Advantage 360 firmware (<c>kb360_version</c>); null when absent.</summary>
        public string? Advantage360Version => GetValue(VersionManifestKeys.Advantage360Version);

        private VersionManifest(string rawJson, IReadOnlyDictionary<string, string> values)
        {
            RawJson = rawJson;
            Values = values;
        }

        /// <summary>
        /// The value stored under <paramref name="key"/> (matched case-insensitively), or null
        /// when the key is absent, blank, or carried an empty value.
        /// </summary>
        public string? GetValue(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return Values.TryGetValue(key, out var value) ? value : null;
        }

        private static bool TryReadValue(JsonElement element, out string value)
        {
            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => element.GetRawText(),
                JsonValueKind.False => element.GetRawText(),
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
