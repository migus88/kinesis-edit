using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Parses one configuration context's led-file lines into a <see cref="LightingModel"/>
    /// per the rules of specs/07-lighting.md §2.4: lines are lowercased, split into top and
    /// Fn layer sections by the <c>fn </c> prefix, and each section is mode-detected from its
    /// first (and second) line, with Freestyle as the tolerant-read fallback — a section is
    /// never rejected.
    /// </summary>
    internal static class LightingSectionParser
    {
        /// <summary>Parses <paramref name="lines"/> into a freshly reset model (§2.4 item 1).</summary>
        public static LightingModel Parse(IReadOnlyList<string> lines, LightingSectionContext context)
        {
            var model = new LightingModel();

            ParseInto(model, lines, context);

            return model;
        }

        /// <summary>Parses <paramref name="lines"/> into <paramref name="model"/> after resetting it.</summary>
        public static void ParseInto(LightingModel model, IReadOnlyList<string> lines, LightingSectionContext context)
        {
            model.Reset();

            var topLines = new List<string>();
            var fnLines = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var lowered = line.ToLowerInvariant().TrimEnd();

                if (LedLineScanner.HasFnPrefix(lowered))
                {
                    fnLines.Add(LedLineScanner.StripFnPrefix(lowered));
                }
                else
                {
                    topLines.Add(lowered);
                }
            }

            ParseLayerSection(model.TopLayer, topLines, context, isFnLayer: false);
            ParseLayerSection(model.FnLayer, fnLines, context, isFnLayer: true);
        }

        private static void ParseLayerSection(
            LayerLightingState layer,
            IReadOnlyList<string> sectionLines,
            LightingSectionContext context,
            bool isFnLayer)
        {
            if (sectionLines.Count == 0)
            {
                return;
            }

            var scannedLines = new List<LedLineScanner.LedLine>(sectionLines.Count);

            foreach (var sectionLine in sectionLines)
            {
                var scanned = LedLineScanner.Scan(sectionLine);

                if (scanned is not null)
                {
                    scannedLines.Add(scanned);
                }
            }

            if (scannedLines.Count == 0)
            {
                // Only an empty section is Disabled; a non-empty section that yields no
                // parseable line still falls back to Freestyle (§2.4 item 3).
                layer.Mode = LightingMode.Freestyle;
                return;
            }

            var firstDefinition = context.FindReadableModeByToken(scannedLines[0].ConfigToken);

            if (firstDefinition is not null
                && TryParseAsDetectedMode(layer, scannedLines, context, isFnLayer, firstDefinition, isFirstLine: true))
            {
                return;
            }

            if (scannedLines.Count > 1)
            {
                var secondDefinition = context.FindReadableModeByToken(scannedLines[1].ConfigToken);

                if (secondDefinition is not null
                    && TryParseAsDetectedMode(layer, scannedLines, context, isFnLayer, secondDefinition, isFirstLine: false))
                {
                    return;
                }
            }

            layer.Mode = LightingMode.Freestyle;
            ParseColorBody(layer, scannedLines, context, isFnLayer);
        }

        private static bool TryParseAsDetectedMode(
            LayerLightingState layer,
            IReadOnlyList<LedLineScanner.LedLine> scannedLines,
            LightingSectionContext context,
            bool isFnLayer,
            LightingModeDefinition definition,
            bool isFirstLine)
        {
            // Two-line effects are detected on line 1 or line 2 (§2.4 item 3).
            if (definition.HasBaseMonoLine)
            {
                ParseTwoLineEffect(layer, scannedLines, context, definition);
                return true;
            }

            if (!isFirstLine)
            {
                return false;
            }

            switch (definition.Mode)
            {
                case LightingMode.Monochrome when scannedLines.Count == 1:
                    layer.Mode = LightingMode.Monochrome;
                    ApplyColorWhenPresent(scannedLines[0].ValueTokens, color => layer.EffectColor = color);
                    return true;

                case LightingMode.Breathe:
                    layer.Mode = LightingMode.Breathe;
                    layer.Speed = LedLineScanner.ParseSpeed(scannedLines[0].ValueTokens);
                    ParseColorBody(layer, scannedLines.Skip(1).ToList(), context, isFnLayer);
                    return true;

                case LightingMode.FrozenWave:
                    layer.Mode = LightingMode.FrozenWave;
                    ParseColorBody(layer, scannedLines.Skip(1).ToList(), context, isFnLayer);
                    return true;

                case LightingMode.Spectrum:
                case LightingMode.Wave:
                case LightingMode.Pulse:
                    layer.Mode = definition.Mode;
                    layer.Speed = LedLineScanner.ParseSpeed(scannedLines[0].ValueTokens);
                    ApplyDirection(layer, scannedLines[0].ValueTokens, context, definition);
                    return true;

                default:
                    return false;
            }
        }

        private static void ParseTwoLineEffect(
            LayerLightingState layer,
            IReadOnlyList<LedLineScanner.LedLine> scannedLines,
            LightingSectionContext context,
            LightingModeDefinition definition)
        {
            layer.Mode = definition.Mode;

            var effectToken = context.GetModeToken(definition);

            // Base vs. effect colors are assigned by token, never by position — files in the
            // field use both line orders (§2.4 item 5 compatibility note).
            foreach (var scannedLine in scannedLines)
            {
                if (scannedLine.ConfigToken == context.MonoToken)
                {
                    ApplyColorWhenPresent(scannedLine.ValueTokens, color => layer.BaseColor = color);
                }
                else if (scannedLine.ConfigToken == effectToken)
                {
                    ApplyColorWhenPresent(scannedLine.ValueTokens, color => layer.EffectColor = color);
                    layer.Speed = LedLineScanner.ParseSpeed(scannedLine.ValueTokens);
                    ApplyDirection(layer, scannedLine.ValueTokens, context, definition);
                }
            }
        }

        private static void ParseColorBody(
            LayerLightingState layer,
            IReadOnlyList<LedLineScanner.LedLine> bodyLines,
            LightingSectionContext context,
            bool isFnLayer)
        {
            foreach (var bodyLine in bodyLines)
            {
                if (bodyLine.ConfigToken == context.MonoToken)
                {
                    FillAllKeys(layer, bodyLine.ValueTokens, context, isFnLayer);
                    continue;
                }

                var entry = KeyRegistry.FindByToken(bodyLine.ConfigToken, TokenDialect.Gen1);

                if (entry is null)
                {
                    continue;
                }

                // An unparseable per-key color falls back to the default effect color (§2.4 item 4).
                var color = LedLineScanner.TryParseColor(bodyLine.ValueTokens, out var parsedColor)
                    ? parsedColor
                    : LedColor.DefaultEffectColor;

                layer.SetKeyColor(MapLoadedKeyCode(entry.Code, context, isFnLayer), color);
            }
        }

        private static void FillAllKeys(
            LayerLightingState layer,
            IReadOnlyList<string> valueTokens,
            LightingSectionContext context,
            bool isFnLayer)
        {
            // A [mono]> line in a Freestyle/Breathe body sets ALL keys of the layer; later
            // per-key lines override individual keys (§2.4 item 4). A black or absent fill
            // color clears — black is the "no color" value (§2.1).
            var fillColor = LedLineScanner.TryParseColor(valueTokens, out var parsedColor)
                ? parsedColor
                : LedColor.Black;

            if (fillColor.IsBlack)
            {
                layer.ClearKeyColors();
                return;
            }

            foreach (var keyCode in context.AddressableKeyCodes)
            {
                layer.SetKeyColor(MapLoadedKeyCode(keyCode, context, isFnLayer), fillColor);
            }
        }

        private static int MapLoadedKeyCode(int fileKeyCode, LightingSectionContext context, bool isFnLayer)
        {
            return context.AppliesFnTranslation && isFnLayer
                ? FnLayerTokenTranslator.TranslateForLoad(fileKeyCode)
                : fileKeyCode;
        }

        private static void ApplyDirection(
            LayerLightingState layer,
            IReadOnlyList<string> valueTokens,
            LightingSectionContext context,
            LightingModeDefinition definition)
        {
            var validDirections = context.GetDirections(definition);

            if (validDirections.Count == 0)
            {
                return;
            }

            var direction = LedLineScanner.ParseDirection(valueTokens);

            layer.Direction = validDirections.Contains(direction) ? direction : LightingDirection.Left;
        }

        private static void ApplyColorWhenPresent(IReadOnlyList<string> valueTokens, Action<LedColor> assign)
        {
            if (LedLineScanner.TryParseColor(valueTokens, out var color))
            {
                assign(color);
            }
        }
    }
}
