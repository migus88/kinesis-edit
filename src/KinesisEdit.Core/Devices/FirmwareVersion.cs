using System.Globalization;

namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// A firmware version as reported in device version files, e.g.
    /// "1.0.1709.us (4MB), 03/08/2019" or "1.0.521". Per specs/09-firmware.md the
    /// first three dot-separated numeric tokens form major/minor/revision; a
    /// non-numeric minor or revision token parses as 0 and trailing text is ignored.
    /// Comparison is lexicographic on major, then minor, then revision.
    /// </summary>
    public readonly struct FirmwareVersion : IEquatable<FirmwareVersion>, IComparable<FirmwareVersion>
    {
        public static bool TryParse(string? text, out FirmwareVersion version)
        {
            version = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var tokens = text.Trim().Split('.');

            if (!TryParseToken(tokens[0], out var major))
            {
                return false;
            }

            var minor = tokens.Length > 1 && TryParseToken(tokens[1], out var parsedMinor) ? parsedMinor : 0;
            var revision = tokens.Length > 2 && TryParseToken(tokens[2], out var parsedRevision) ? parsedRevision : 0;

            version = new FirmwareVersion(major, minor, revision);
            return true;
        }

        public static bool operator ==(FirmwareVersion left, FirmwareVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FirmwareVersion left, FirmwareVersion right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(FirmwareVersion left, FirmwareVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(FirmwareVersion left, FirmwareVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(FirmwareVersion left, FirmwareVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(FirmwareVersion left, FirmwareVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Revision { get; }

        public FirmwareVersion(int major, int minor, int revision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(major);
            ArgumentOutOfRangeException.ThrowIfNegative(minor);
            ArgumentOutOfRangeException.ThrowIfNegative(revision);

            Major = major;
            Minor = minor;
            Revision = revision;
        }

        public int CompareTo(FirmwareVersion other)
        {
            var majorComparison = Major.CompareTo(other.Major);

            if (majorComparison != 0)
            {
                return majorComparison;
            }

            var minorComparison = Minor.CompareTo(other.Minor);

            if (minorComparison != 0)
            {
                return minorComparison;
            }

            return Revision.CompareTo(other.Revision);
        }

        public bool Equals(FirmwareVersion other)
        {
            return Major == other.Major && Minor == other.Minor && Revision == other.Revision;
        }

        public override bool Equals(object? obj)
        {
            return obj is FirmwareVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Major, Minor, Revision);
        }

        public override string ToString()
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Revision}");
        }

        private static bool TryParseToken(string token, out int value)
        {
            return int.TryParse(token.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
