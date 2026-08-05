using System.Globalization;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Classifies TKO led-file lines as edge-lighting syntax (specs/07-lighting.md §2.3): a
    /// line is edge syntax if, after stripping an optional <c>fn </c> prefix, it starts with
    /// <c>[lN]</c>, <c>[rN]</c>, or <c>[bN]</c> (the classifier accepts N = 1..30, though only
    /// L1-L9, B1-B15, R1-R9 exist) or with any <c>_edge</c> mode token. On load, edge lines
    /// are separated out before the key-backlight section is parsed (§1.4).
    /// </summary>
    public static class TkoEdgeLineClassifier
    {
        private const int MinimumEdgeAddress = 1;
        private const int MaximumEdgeAddress = 30;

        /// <summary>Whether <paramref name="line"/> is edge-lighting syntax (§2.3).</summary>
        public static bool IsEdgeLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var lowered = line.ToLowerInvariant();

            if (LedLineScanner.HasFnPrefix(lowered))
            {
                lowered = LedLineScanner.StripFnPrefix(lowered);
            }

            var token = ReadLeadingBracketToken(lowered);

            if (token is null)
            {
                return false;
            }

            return IsEdgeAddressToken(token) || LightingModeCatalog.FindByEdgeToken(token) is not null;
        }

        private static string? ReadLeadingBracketToken(string line)
        {
            if (line.Length < 2 || line[0] != '[')
            {
                return null;
            }

            var closeIndex = line.IndexOf(']', 1);

            return closeIndex < 0 ? null : line[1..closeIndex];
        }

        private static bool IsEdgeAddressToken(string token)
        {
            if (token.Length < 2)
            {
                return false;
            }

            var side = token[0];

            if (side != 'l' && side != 'r' && side != 'b')
            {
                return false;
            }

            var digits = token[1..];

            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var address)
                && address >= MinimumEdgeAddress
                && address <= MaximumEdgeAddress;
        }
    }
}
