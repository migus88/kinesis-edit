using System.Globalization;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Low-level led-file line mechanics (specs/07-lighting.md §2.1): the <c>fn </c> layer
    /// prefix, splitting a line at the <c>&gt;</c> separator, extracting <c>[...]</c> bracket
    /// tokens, and parsing the color / <c>[spdN]</c> / <c>[dirX]</c> value tokens with their
    /// tolerant-read fallbacks (§2.4).
    /// </summary>
    internal static class LedLineScanner
    {
        /// <summary>The Fn/embedded-layer line prefix (specs/07-lighting.md §2.1).</summary>
        public const string FnLinePrefix = "fn ";

        private const char TokenOpen = '[';
        private const char TokenClose = ']';
        private const char ValueSeparator = '>';
        private const string SpeedTokenPrefix = "spd";
        private const string DirectionTokenPrefix = "dir";

        /// <summary>Whether the line belongs to the Fn layer (case-insensitive prefix match).</summary>
        public static bool HasFnPrefix(string line)
        {
            return line.StartsWith(FnLinePrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Removes the <c>fn </c> prefix; the line must carry it.</summary>
        public static string StripFnPrefix(string line)
        {
            return line[FnLinePrefix.Length..];
        }

        /// <summary>
        /// Splits a lowercased line into its config token (the first bracket token before the
        /// <c>&gt;</c>) and its value tokens (the bracket tokens after it). Returns null when
        /// the config part carries no bracket token.
        /// </summary>
        public static LedLine? Scan(string line)
        {
            var separatorIndex = line.IndexOf(ValueSeparator);
            var configPart = separatorIndex < 0 ? line : line[..separatorIndex];
            var configTokens = ExtractBracketTokens(configPart);

            if (configTokens.Count == 0)
            {
                return null;
            }

            var valueTokens = separatorIndex < 0
                ? []
                : ExtractBracketTokens(line[(separatorIndex + 1)..]);

            return new LedLine
            {
                ConfigToken = configTokens[0],
                ValueTokens = valueTokens
            };
        }

        /// <summary>
        /// Parses the leading <c>[R][G][B]</c> color: the first three value tokens must all be
        /// decimal integers 0-255 (§2.1). Returns false (leaving black) otherwise.
        /// </summary>
        public static bool TryParseColor(IReadOnlyList<string> valueTokens, out LedColor color)
        {
            color = LedColor.Black;

            if (valueTokens.Count < 3)
            {
                return false;
            }

            if (!TryParseComponent(valueTokens[0], out var red)
                || !TryParseComponent(valueTokens[1], out var green)
                || !TryParseComponent(valueTokens[2], out var blue))
            {
                return false;
            }

            color = new LedColor(red, green, blue);
            return true;
        }

        /// <summary>
        /// Parses the <c>[spdN]</c> token; a missing or invalid speed yields the default 5
        /// (§2.1, §2.4).
        /// </summary>
        public static int ParseSpeed(IReadOnlyList<string> valueTokens)
        {
            foreach (var token in valueTokens)
            {
                if (!token.StartsWith(SpeedTokenPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var digits = token[SpeedTokenPrefix.Length..];

                if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var speed)
                    && speed >= LayerLightingState.MinimumSpeed
                    && speed <= LayerLightingState.MaximumSpeed)
                {
                    return speed;
                }

                return LayerLightingState.DefaultSpeed;
            }

            return LayerLightingState.DefaultSpeed;
        }

        /// <summary>
        /// Parses the <c>[dirX]</c> token; a missing or unrecognized direction yields the
        /// default left (§2.1). Per-effect validity (§2.4) is the caller's rule.
        /// </summary>
        public static LightingDirection ParseDirection(IReadOnlyList<string> valueTokens)
        {
            foreach (var token in valueTokens)
            {
                if (!token.StartsWith(DirectionTokenPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                return token switch
                {
                    "dirdown" => LightingDirection.Down,
                    "dirleft" => LightingDirection.Left,
                    "dirup" => LightingDirection.Up,
                    "dirright" => LightingDirection.Right,
                    _ => LightingDirection.Left
                };
            }

            return LightingDirection.Left;
        }

        private static IReadOnlyList<string> ExtractBracketTokens(string text)
        {
            var tokens = new List<string>();
            var searchIndex = 0;

            while (searchIndex < text.Length)
            {
                var openIndex = text.IndexOf(TokenOpen, searchIndex);

                if (openIndex < 0)
                {
                    break;
                }

                var closeIndex = text.IndexOf(TokenClose, openIndex + 1);

                if (closeIndex < 0)
                {
                    break;
                }

                tokens.Add(text[(openIndex + 1)..closeIndex]);
                searchIndex = closeIndex + 1;
            }

            return tokens;
        }

        private static bool TryParseComponent(string token, out byte component)
        {
            component = 0;

            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value > byte.MaxValue)
            {
                return false;
            }

            component = (byte)value;
            return true;
        }

        /// <summary>A scanned led-file line: its config token and value tokens (§2.1).</summary>
        internal sealed record LedLine
        {
            /// <summary>The first bracket token of the config part, lowercased.</summary>
            public required string ConfigToken { get; init; }

            /// <summary>The bracket tokens after the <c>&gt;</c>; empty for bare lines.</summary>
            public required IReadOnlyList<string> ValueTokens { get; init; }
        }
    }
}
