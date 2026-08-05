using System.Globalization;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// An immutable RGB color as persisted by the custom-color keys of app_settings.txt
    /// (specs/08-settings.md §3): the file form is <c>[R][G][B]</c> with decimal 0-255
    /// components, e.g. <c>[255][0][128]</c>. <see cref="ToString"/> produces exactly that
    /// form; <see cref="TryParse"/> accepts it and nothing else.
    /// </summary>
    public readonly struct SettingsColor : IEquatable<SettingsColor>
    {
        private const char ComponentOpener = '[';
        private const char ComponentCloser = ']';
        private const int ComponentCount = 3;
        private const int MaximumComponentValue = 255;
        private const int MaximumComponentDigits = 3;

        /// <summary>
        /// Parses the <c>[R][G][B]</c> decimal file form of spec 08 §3. Returns false — never
        /// throws — for null, empty, malformed, or out-of-range (component &gt; 255) input.
        /// </summary>
        public static bool TryParse(string? text, out SettingsColor color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var remainder = text.Trim();
            Span<byte> components = stackalloc byte[ComponentCount];

            for (var index = 0; index < ComponentCount; index++)
            {
                if (!TryReadComponent(ref remainder, out components[index]))
                {
                    return false;
                }
            }

            if (remainder.Length > 0)
            {
                return false;
            }

            color = new SettingsColor(components[0], components[1], components[2]);
            return true;
        }

        public static bool operator ==(SettingsColor left, SettingsColor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SettingsColor left, SettingsColor right)
        {
            return !left.Equals(right);
        }

        /// <summary>Red component, 0-255.</summary>
        public byte Red { get; }

        /// <summary>Green component, 0-255.</summary>
        public byte Green { get; }

        /// <summary>Blue component, 0-255.</summary>
        public byte Blue { get; }

        public SettingsColor(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public bool Equals(SettingsColor other)
        {
            return Red == other.Red && Green == other.Green && Blue == other.Blue;
        }

        public override bool Equals(object? obj)
        {
            return obj is SettingsColor other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Red, Green, Blue);
        }

        /// <summary>The exact file form of spec 08 §3, e.g. <c>[255][0][128]</c>.</summary>
        public override string ToString()
        {
            return string.Create(CultureInfo.InvariantCulture, $"[{Red}][{Green}][{Blue}]");
        }

        private static bool TryReadComponent(ref string remainder, out byte component)
        {
            component = 0;

            if (remainder.Length < 3 || remainder[0] != ComponentOpener)
            {
                return false;
            }

            var closerIndex = remainder.IndexOf(ComponentCloser);

            if (closerIndex < 2 || closerIndex > MaximumComponentDigits + 1)
            {
                return false;
            }

            var digits = remainder[1..closerIndex];

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value > MaximumComponentValue)
            {
                return false;
            }

            component = (byte)value;
            remainder = remainder[(closerIndex + 1)..];
            return true;
        }
    }
}
