using System.Globalization;
using System.Text;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Serializes one configuration context of a lighting model into canonical led-file lines
    /// (specs/07-lighting.md §2.2, §2.3): top-layer lines first, then <c>fn </c>-prefixed
    /// Fn-layer lines; two-line effects write the base <c>[mono]</c> line first; value tokens
    /// in the order color, <c>[spdN]</c>, <c>[dirX]</c> (§2.1). Per-key tokens use the key
    /// registry's canonical casing; out-of-range speeds and invalid directions are written as
    /// their defaults (§2.1).
    /// </summary>
    internal static class LightingSectionSerializer
    {
        /// <summary>Serializes both layers of <paramref name="model"/> in <paramref name="context"/>.</summary>
        public static List<string> Serialize(LightingModel model, LightingSectionContext context)
        {
            var lines = new List<string>();

            AppendLayer(lines, model.TopLayer, context, isFnLayer: false);
            AppendLayer(lines, model.FnLayer, context, isFnLayer: true);

            return lines;
        }

        private static void AppendLayer(
            List<string> lines,
            LayerLightingState layer,
            LightingSectionContext context,
            bool isFnLayer)
        {
            // Disabled writes nothing (§2.2); the reserved Pitch Black token is never written
            // (§2.2), and a mode the context has no token for cannot be expressed in it.
            if (layer.Mode == LightingMode.Disabled || layer.Mode == LightingMode.PitchBlack)
            {
                return;
            }

            var definition = LightingModeCatalog.Find(layer.Mode);

            if (layer.Mode == LightingMode.Freestyle)
            {
                AppendPerKeyColorLines(lines, layer, context, isFnLayer);
                return;
            }

            if (context.GetModeToken(definition) is not { } modeToken)
            {
                return;
            }

            if (definition.HasBaseMonoLine)
            {
                lines.Add(BuildLine(context.MonoToken, FormatColor(layer.BaseColor), isFnLayer));
            }

            var valueText = BuildModeValueText(layer, context, definition);

            if (definition.Mode == LightingMode.FrozenWave)
            {
                // Frozen Wave is a bare token with no > value (§2.3).
                lines.Add(BuildBareLine(modeToken, isFnLayer));
            }
            else
            {
                lines.Add(BuildLine(modeToken, valueText, isFnLayer));
            }

            if (definition.HasPerKeyColors)
            {
                var perKeyLineCount = AppendPerKeyColorLines(lines, layer, context, isFnLayer);

                // Breathe with no key colors appends [mono]>[0][0][0] instead (§2.2).
                if (definition.Mode == LightingMode.Breathe && perKeyLineCount == 0)
                {
                    lines.Add(BuildLine(context.MonoToken, FormatColor(LedColor.Black), isFnLayer));
                }
            }
        }

        private static string BuildModeValueText(
            LayerLightingState layer,
            LightingSectionContext context,
            LightingModeDefinition definition)
        {
            var builder = new StringBuilder();

            if (definition.WritesEffectColor)
            {
                builder.Append(FormatColor(layer.EffectColor));
            }

            if (definition.WritesSpeed)
            {
                builder.Append(FormatSpeed(layer.Speed));
            }

            var validDirections = context.GetDirections(definition);

            if (validDirections.Count > 0)
            {
                var direction = validDirections.Contains(layer.Direction)
                    ? layer.Direction
                    : LightingDirection.Left;

                builder.Append(FormatDirection(direction));
            }

            return builder.ToString();
        }

        private static int AppendPerKeyColorLines(
            List<string> lines,
            LayerLightingState layer,
            LightingSectionContext context,
            bool isFnLayer)
        {
            var entries = new List<(int Ordinal, int FileKeyCode, LedColor Color)>(layer.KeyColors.Count);

            foreach (var (memoryKeyCode, color) in layer.KeyColors)
            {
                var fileKeyCode = context.AppliesFnTranslation && isFnLayer
                    ? FnLayerTokenTranslator.TranslateForSave(memoryKeyCode)
                    : memoryKeyCode;

                entries.Add((context.GetSerializationOrdinal(fileKeyCode), fileKeyCode, color));
            }

            entries.Sort((left, right) =>
            {
                var byOrdinal = left.Ordinal.CompareTo(right.Ordinal);

                return byOrdinal != 0 ? byOrdinal : left.FileKeyCode.CompareTo(right.FileKeyCode);
            });

            var appendedCount = 0;

            foreach (var (_, fileKeyCode, color) in entries)
            {
                var token = KeyRegistry.FindByCode(fileKeyCode)?.GetToken(TokenDialect.Gen1);

                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                lines.Add(BuildLine(token, FormatColor(color), isFnLayer));
                appendedCount++;
            }

            return appendedCount;
        }

        private static string BuildLine(string configToken, string valueText, bool isFnLayer)
        {
            var prefix = isFnLayer ? LedLineScanner.FnLinePrefix : string.Empty;

            return prefix + "[" + configToken + "]>" + valueText;
        }

        private static string BuildBareLine(string configToken, bool isFnLayer)
        {
            var prefix = isFnLayer ? LedLineScanner.FnLinePrefix : string.Empty;

            return prefix + "[" + configToken + "]";
        }

        private static string FormatColor(LedColor color)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"[{color.Red}][{color.Green}][{color.Blue}]");
        }

        private static string FormatSpeed(int speed)
        {
            // Out-of-range values are written as the default 5 (§2.1).
            var validSpeed = speed >= LayerLightingState.MinimumSpeed && speed <= LayerLightingState.MaximumSpeed
                ? speed
                : LayerLightingState.DefaultSpeed;

            return "[spd" + validSpeed.ToString(CultureInfo.InvariantCulture) + "]";
        }

        private static string FormatDirection(LightingDirection direction)
        {
            return direction switch
            {
                LightingDirection.Down => "[dirdown]",
                LightingDirection.Up => "[dirup]",
                LightingDirection.Right => "[dirright]",
                _ => "[dirleft]"
            };
        }
    }
}
