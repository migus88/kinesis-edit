using System.Globalization;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// The value domain of the mode-string form of <c>led_mode</c>
    /// (<see cref="LedModeKind.ModeString"/>, specs/08-settings.md §2 and §5.3): a backlight
    /// brightness <c>0</c>-<c>9</c>, <c>P</c> (pitch black) or <c>B</c> (breathe). It lives next
    /// to <see cref="KeyboardSettingsSerializer"/>, which <b>throws</b> on anything outside it, so
    /// a picker offering these options cannot drift from the set the serializer accepts.
    /// <para>
    /// The other form of the key — the paired led file name of the RGB/TKO
    /// (<see cref="LedModeKind.LedFileName"/>) — is <see cref="StartupProfileSettings"/>'s, not
    /// this type's.
    /// </para>
    /// </summary>
    public static class LedModeValues
    {
        /// <summary>The pitch-black mode value, canonical uppercase (spec 08 §2).</summary>
        public const string PitchBlack = "P";

        /// <summary>The breathe mode value, canonical uppercase (spec 08 §2).</summary>
        public const string Breathe = "B";

        /// <summary>Dimmest backlight brightness — <c>0</c> is the backlight off (spec 08 §2).</summary>
        public const int MinimumBrightness = 0;

        /// <summary>Brightest backlight brightness (spec 08 §2).</summary>
        public const int MaximumBrightness = 9;

        /// <summary>
        /// Every accepted value in the order a picker shows them: the brightnesses ascending,
        /// then <see cref="PitchBlack"/> and <see cref="Breathe"/>.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = CreateAll();

        /// <summary>
        /// The canonical spelling of <paramref name="value"/> — trimmed, digits as written,
        /// <c>p</c>/<c>b</c> uppercased — or null when it is not a legal mode string. Null in,
        /// null out. Parsing is case-insensitive everywhere in spec 08 §1, so this is the one
        /// place that decides whether a <c>led_mode</c> a device wrote is one this app knows.
        /// </summary>
        public static string? Normalize(string? value)
        {
            if (value is null)
            {
                return null;
            }

            var trimmedValue = value.Trim();

            if (trimmedValue.Length != 1)
            {
                return null;
            }

            var mode = char.ToUpperInvariant(trimmedValue[0]);

            if (char.IsAsciiDigit(mode))
            {
                return trimmedValue;
            }

            if (mode == PitchBlack[0])
            {
                return PitchBlack;
            }

            if (mode == Breathe[0])
            {
                return Breathe;
            }

            return null;
        }

        /// <summary>Whether <paramref name="value"/> is one of the ten brightness values rather than a special mode.</summary>
        public static bool IsBrightness(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            return Normalize(value) is string normalized && char.IsAsciiDigit(normalized[0]);
        }

        private static IReadOnlyList<string> CreateAll()
        {
            var values = new List<string>(MaximumBrightness - MinimumBrightness + 3);

            for (var brightness = MinimumBrightness; brightness <= MaximumBrightness; brightness++)
            {
                values.Add(brightness.ToString(CultureInfo.InvariantCulture));
            }

            values.Add(PitchBlack);
            values.Add(Breathe);

            return values;
        }
    }
}
