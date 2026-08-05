using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Parses and applies the single-key rules of specs/04-layout-file-format.md §2 — remap,
    /// tap-and-hold, multi-modifier — for one already-classified line. The rule is applied via
    /// the model's tolerant load paths only when every segment proved valid (06 §2.2: an
    /// invalid line "is not applied"); duplicates overwrite, so the last line wins (04 §2.1),
    /// and a remap back to the original clears the remap.
    /// </summary>
    internal static class SingleKeyLineParser
    {
        /// <summary>The tap-and-hold marker inside the second value group (04 §2.2, 05 §5.6).</summary>
        private const string TapAndHoldMarker = "t&h";

        /// <summary>Number of value-side groups of a tap-and-hold line: tap, t&amp;h+delay, hold (04 §2.2).</summary>
        private const int TapAndHoldGroupCount = 3;

        /// <summary>Parses the line; false when it must be tracked as invalid (04 §5).</summary>
        public static bool TryParse(LayoutLineContext context)
        {
            var key = ReadPosition(context);

            // 04 §1.3: a single-key line whose value side contains "[t&h" is a tap-and-hold rule.
            var isTapAndHold = context.Processed.IndexOf(
                "[" + TapAndHoldMarker,
                context.SeparatorIndex + 1,
                StringComparison.Ordinal) >= 0;

            return isTapAndHold
                ? ParseTapAndHold(context, key)
                : ParseRemapOrMultiModifier(context, key);
        }

        /// <summary>
        /// Reads and resolves the position group (04 §2.1: the config side must start with
        /// <c>[</c>, contain <c>]</c>, and the position must exist on the addressed layer).
        /// Marks the config-side segments; null means the position segment is invalid.
        /// </summary>
        private static KeyboardKey? ReadPosition(LayoutLineContext context)
        {
            var parts = context.ConfigParts;

            if (parts.Count == 0 || parts[0].Kind != LayoutLinePartKind.SingleKeyGroup)
            {
                foreach (var part in parts)
                {
                    context.Segments.Add(part, false);
                }

                if (parts.Count == 0)
                {
                    context.MarkInvalidWithoutSegment();
                }

                return null;
            }

            var positionPart = parts[0];
            var key = ResolvePositionKey(context, positionPart.Inner);

            context.Segments.Add(positionPart, key is not null);

            // Anything after the position group on the config side is junk (04 §5.1).
            for (var i = 1; i < parts.Count; i++)
            {
                context.Segments.Add(parts[i], parts[i].IsWhitespaceText);
            }

            return key;
        }

        private static KeyboardKey? ResolvePositionKey(LayoutLineContext context, string token)
        {
            if (context.Rules.UsesKeypadTokenPrefix)
            {
                // 04 §3.2: the Advantage2 routes the layer per token (kp- prefix / exception list).
                var route = Advantage2TokenRouter.Route(token);

                context.LayerIndex = route.IsKeypadLayer ? 1 : 0;

                var layer = context.FindLayer();

                return layer is null ? null : Advantage2TokenRouter.Find(layer, route, byTrigger: false);
            }

            var positionKey = LayoutTokenResolver.Resolve(token, context.Rules.Tokens);

            return positionKey is null ? null : context.FindLayer()?.FindByPositionKeyCode(positionKey.Code);
        }

        private static bool ParseTapAndHold(LayoutLineContext context, KeyboardKey? key)
        {
            KeyDefinition? tapAction = null;
            KeyDefinition? holdAction = null;
            var delay = 0;
            var groupIndex = 0;

            foreach (var part in context.ValueParts)
            {
                if (part.Kind != LayoutLinePartKind.SingleKeyGroup)
                {
                    context.Segments.Add(part, part.IsWhitespaceText);
                    continue;
                }

                var valid = groupIndex switch
                {
                    // 04 §2.2: three groups left to right — tap action, t&h + delay, hold action.
                    0 => (tapAction = LayoutTokenResolver.Resolve(part.Inner, context.Rules.Tokens)) is not null,
                    1 => TryReadDelay(context.Rules, part.Inner, out delay),
                    2 => (holdAction = LayoutTokenResolver.Resolve(part.Inner, context.Rules.Tokens)) is not null,
                    _ => false
                };

                context.Segments.Add(part, valid);
                groupIndex++;
            }

            if (groupIndex < TapAndHoldGroupCount)
            {
                // A missing tap/hold action or delay group makes the line invalid (04 §5.1).
                context.MarkInvalidWithoutSegment();
            }

            if (context.Segments.HasInvalidSegments || key is null)
            {
                return false;
            }

            key.ApplyTapAndHold(tapAction!, holdAction!, delay);

            return true;
        }

        /// <summary>
        /// Reads the <c>t&amp;hNNN</c> group (04 §2.2). The Gen1 RGB-family/Gen2 parser marks a
        /// missing, non-numeric, or negative delay invalid; the older FS and Adv2 parsers
        /// substitute the 250 ms default instead.
        /// </summary>
        private static bool TryReadDelay(LayoutDialectRules rules, string groupContent, out int delay)
        {
            delay = rules.DefaultTapAndHoldDelay;

            if (!groupContent.StartsWith(TapAndHoldMarker, StringComparison.Ordinal))
            {
                return false;
            }

            var digits = groupContent[TapAndHoldMarker.Length..];

            if (digits.Length > 0
                && int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                delay = parsed;

                return true;
            }

            return rules.SubstitutesInvalidTapAndHoldDelay;
        }

        private static bool ParseRemapOrMultiModifier(LayoutLineContext context, KeyboardKey? key)
        {
            KeyDefinition? output = null;
            string? multiModifierCode = null;
            var groupIndex = 0;

            foreach (var part in context.ValueParts)
            {
                if (part.Kind != LayoutLinePartKind.SingleKeyGroup)
                {
                    context.Segments.Add(part, part.IsWhitespaceText);
                    continue;
                }

                var valid = false;

                if (groupIndex == 0)
                {
                    // 04 §1.3: a single-key value that is one of the 11 codes is a multi-modifier
                    // rule — on the Gen1 RGB/TKO and Gen2 parser; the older dialects treat the
                    // code as an unknown output token.
                    if (context.Rules.ReadsAndWritesMultiModifierLines && MultiModifierCodes.IsValid(part.Inner))
                    {
                        multiModifierCode = part.Inner;
                        valid = true;
                    }
                    else
                    {
                        output = LayoutTokenResolver.Resolve(part.Inner, context.Rules.Tokens);
                        valid = output is not null;
                    }
                }

                context.Segments.Add(part, valid);
                groupIndex++;
            }

            if (groupIndex == 0)
            {
                // An empty value side makes the output invalid (04 §2.1).
                context.MarkInvalidWithoutSegment();
            }

            if (context.Segments.HasInvalidSegments || key is null)
            {
                return false;
            }

            if (multiModifierCode is not null)
            {
                return key.ApplyMultiModifiers(multiModifierCode);
            }

            key.ApplyRemap(output!);

            return true;
        }
    }
}
