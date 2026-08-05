using System.Collections.ObjectModel;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Transfer
{
    /// <summary>
    /// Decides whether an imported <c>.txt</c> file is a led file or a layout file, per the
    /// import rule of specs/07-lighting.md §1.4. Lines in, verdict out — reading the file,
    /// enforcing <see cref="MaxImportBytes"/>, and handing the lines to
    /// <c>LedFileParser</c>/<c>LayoutFileParser</c> are the caller's job.
    /// <para>
    /// The branch comes from the device's lighting hardware
    /// (<see cref="DeviceDefinition.Lighting"/>), not from a device list: the per-key RGB boards
    /// (Edge RGB, TKO) use the §1.4 heuristic, the indicator-LED board (Advantage 360) uses its
    /// <c>[ind</c> discriminator, and a device with no led files has nothing to import as one.
    /// </para>
    /// </summary>
    public static class ImportClassifier
    {
        /// <summary>Largest imported file the apps accept: 50 KB (07 §1.4).</summary>
        public const int MaxImportBytes = 51200;

        private const char TokenOpen = '[';
        private const char TokenClose = ']';
        private const char ValueSeparator = '>';

        /// <summary>The Advantage360 discriminator: its led file addresses indicator LEDs (07 §5).</summary>
        private const string IndicatorTokenPrefix = "[ind";

        /// <summary>Number of <c>[</c> in a value that makes it a color style, e.g. <c>[255][128][0]</c> (07 §1.4, §2.1).</summary>
        private const int ColorStyleTokenOpenCount = 2;

        private static readonly IReadOnlyList<string> _modeTokens = BuildModeTokens();

        /// <summary>
        /// Classifies <paramref name="lines"/> for <paramref name="deviceId"/> (07 §1.4). An empty
        /// file — and any file the led heuristic rejects — is <see cref="ImportedFileKind.Layout"/>.
        /// </summary>
        public static ImportedFileKind Classify(DeviceId deviceId, IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var device = DeviceCatalog.GetById(deviceId);

            return device.Lighting.Kind switch
            {
                LightingKind.IndicatorLeds => ClassifyIndicatorFile(lines),
                LightingKind.PerKeyRgb => ClassifyKeyBacklightFile(lines),
                _ => ImportedFileKind.Layout
            };
        }

        /// <summary>
        /// The Advantage360 rule of 07 §1.4: "the discriminator is simply whether the first line
        /// contains <c>[ind</c>".
        /// </summary>
        private static ImportedFileKind ClassifyIndicatorFile(IReadOnlyList<string> lines)
        {
            var firstLine = FindFirstContentLine(lines);

            return firstLine is not null && firstLine.Contains(IndicatorTokenPrefix, StringComparison.OrdinalIgnoreCase)
                ? ImportedFileKind.Lighting
                : ImportedFileKind.Layout;
        }

        /// <summary>
        /// The RGB/TKO rule of 07 §1.4: a led file when the first line starts with <c>[</c> and
        /// contains <c>&gt;</c>, <em>and</em> the file carries a known mode token or a color-style
        /// value. Because a color style is only "a second <c>[</c> in the value part of a line",
        /// a layout file whose first line is a tap-and-hold rule
        /// (<c>[caps]&gt;[a][t&amp;h250][lctrl]</c>, 11 §11.1) satisfies it too — that is the
        /// legacy heuristic as specified, not an oversight here.
        /// </summary>
        private static ImportedFileKind ClassifyKeyBacklightFile(IReadOnlyList<string> lines)
        {
            var firstLine = FindFirstContentLine(lines);

            if (firstLine is null || firstLine[0] != TokenOpen || !firstLine.Contains(ValueSeparator))
            {
                return ImportedFileKind.Layout;
            }

            return ContainsModeToken(lines) || ContainsColorStyleValue(lines)
                ? ImportedFileKind.Lighting
                : ImportedFileKind.Layout;
        }

        /// <summary>
        /// The first line with content, trimmed. Blank leading lines are skipped and surrounding
        /// whitespace ignored — the led/layout parsers read the same way, and a stray blank first
        /// line must not decide the file's kind.
        /// </summary>
        private static string? FindFirstContentLine(IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }

            return null;
        }

        private static bool ContainsModeToken(IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                foreach (var token in _modeTokens)
                {
                    if (line.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsColorStyleValue(IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                var separator = line.IndexOf(ValueSeparator);

                if (separator < 0)
                {
                    continue;
                }

                if (CountTokenOpens(line.AsSpan(separator + 1)) >= ColorStyleTokenOpenCount)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountTokenOpens(ReadOnlySpan<char> value)
        {
            var count = 0;

            foreach (var character in value)
            {
                if (character == TokenOpen)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Every mode token of 07 §2.2 and §2.3 in bracketed form, taken from
        /// <see cref="LightingModeCatalog"/> so the list can never drift from the lighting engine.
        /// The reserved <c>[black]</c> row is kept: no other file type carries it, so it is still
        /// evidence of a led file.
        /// </summary>
        private static IReadOnlyList<string> BuildModeTokens()
        {
            var tokens = new List<string>();

            foreach (var definition in LightingModeCatalog.All)
            {
                AddToken(tokens, definition.KeyBacklightToken);
                AddToken(tokens, definition.EdgeToken);
            }

            return new ReadOnlyCollection<string>(tokens);
        }

        private static void AddToken(List<string> tokens, string? token)
        {
            if (token is not null)
            {
                tokens.Add($"{TokenOpen}{token}{TokenClose}");
            }
        }
    }
}
