using System.Globalization;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Parses and serializes the Advantage360 indicator dialect (specs/07-lighting.md §5):
    /// each line is <c>[INDn]&gt;</c> (n = 1..6) followed by a function token and, except for
    /// Battery and Disabled, an <c>[R][G][B]</c> color. The Layer function writes five lines,
    /// one color per layer. Parsing is case-insensitive and tolerant: unknown indicators and
    /// function tokens are skipped, never rejected.
    /// </summary>
    internal static class IndicatorFileEngine
    {
        private const string IndicatorTokenPrefix = "ind";

        /// <summary>Parses <paramref name="lines"/> into a freshly reset model.</summary>
        public static Advantage360LightingModel Parse(IReadOnlyList<string> lines)
        {
            var model = new Advantage360LightingModel();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var scanned = LedLineScanner.Scan(line.ToLowerInvariant().TrimEnd());

                if (scanned is null)
                {
                    continue;
                }

                ParseIndicatorLine(model, scanned);
            }

            return model;
        }

        /// <summary>
        /// Serializes every indicator in file order. Disabled indicators are written as
        /// <c>[INDn]&gt;[null]</c> — the Disabled function's token per the §5 table.
        /// </summary>
        public static List<string> Serialize(Advantage360LightingModel model)
        {
            var lines = new List<string>(Advantage360LightingModel.IndicatorCount);

            for (var index = 0; index < Advantage360LightingModel.IndicatorCount; index++)
            {
                AppendIndicator(lines, model.Indicators[index], index + 1);
            }

            return lines;
        }

        private static void ParseIndicatorLine(Advantage360LightingModel model, LedLineScanner.LedLine scanned)
        {
            var indicator = FindIndicator(model, scanned.ConfigToken);

            if (indicator is null || scanned.ValueTokens.Count == 0)
            {
                return;
            }

            var match = IndicatorFunctionCatalog.FindByToken(scanned.ValueTokens[0]);

            if (match is null)
            {
                return;
            }

            indicator.Function = match.Definition.Function;

            if (!match.Definition.HasColor)
            {
                return;
            }

            var colorTokens = scanned.ValueTokens.Skip(1).ToList();

            if (LedLineScanner.TryParseColor(colorTokens, out var color))
            {
                indicator.SetColor(match.Layer, color);
            }
        }

        private static IndicatorState? FindIndicator(Advantage360LightingModel model, string configToken)
        {
            if (!configToken.StartsWith(IndicatorTokenPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var digits = configToken[IndicatorTokenPrefix.Length..];

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var indicatorNumber)
                || indicatorNumber < 1
                || indicatorNumber > Advantage360LightingModel.IndicatorCount)
            {
                return null;
            }

            return model.Indicators[indicatorNumber - 1];
        }

        private static void AppendIndicator(List<string> lines, IndicatorState indicator, int indicatorNumber)
        {
            var definition = IndicatorFunctionCatalog.Find(indicator.Function);

            for (var tokenIndex = 0; tokenIndex < definition.Tokens.Count; tokenIndex++)
            {
                var valueText = "[" + definition.Tokens[tokenIndex] + "]";

                if (definition.HasColor)
                {
                    var color = indicator.GetColor((IndicatorLayer)tokenIndex);

                    valueText += FormatColor(color);
                }

                lines.Add(BuildConfigPart(indicatorNumber) + valueText);
            }
        }

        private static string BuildConfigPart(int indicatorNumber)
        {
            return "[IND" + indicatorNumber.ToString(CultureInfo.InvariantCulture) + "]>";
        }

        private static string FormatColor(LedColor color)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"[{color.Red}][{color.Green}][{color.Blue}]");
        }
    }
}
