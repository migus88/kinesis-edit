using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Parses and applies one macro line (specs/06-macros.md §2): the trigger side's co-trigger
    /// and trigger groups, then the value side via <see cref="MacroValueReader"/>. Applied only
    /// when every segment proved valid (06 §2.2: an unknown token invalidates its segment and
    /// the whole line is not applied). Non-Gen2 macros fill the first empty of the trigger
    /// key's five slots; Gen2 macros are appended to the flat list with the current header's
    /// layer (04 §4.2 step 5). All co-triggers a line carries are stored — truncation to the
    /// dialect's persisted count happens on save, never on load.
    /// </summary>
    internal static class MacroLineParser
    {
        /// <summary>Parses the line; false when it must be tracked as invalid (04 §5).</summary>
        public static bool TryParse(LayoutLineContext context)
        {
            var groups = CollectTriggerGroups(context);

            if (groups.Count == 0)
            {
                MarkAllPartsInvalid(context);

                return false;
            }

            var keys = ResolveTriggerSide(context, groups, out var routes);
            var verdicts = JudgeTriggerSide(context, groups, keys, routes, out var triggerKey);

            RecordTriggerSegments(context, verdicts);

            var reader = new MacroValueReader(context.Rules);

            ReadValueSide(context, reader);

            var keystrokes = reader.Complete();

            if (context.Segments.HasInvalidSegments)
            {
                return false;
            }

            var macro = BuildMacro(context, reader, keys, keystrokes);

            return Store(context, macro, triggerKey);
        }

        private static List<LayoutLinePart> CollectTriggerGroups(LayoutLineContext context)
        {
            var groups = new List<LayoutLinePart>();

            foreach (var part in context.ConfigParts)
            {
                if (part.Kind == LayoutLinePartKind.MacroGroup)
                {
                    groups.Add(part);
                }
            }

            return groups;
        }

        /// <summary>
        /// Resolves every trigger-side token (06 §2.1). On the Advantage2 each token is routed
        /// through the keypad-exception/prefix rules and any keypad verdict targets the line at
        /// the keypad layer (04 §3.2); the serializer likewise prefixes trigger and co-trigger
        /// tokens of keypad-layer macros.
        /// </summary>
        private static KeyDefinition?[] ResolveTriggerSide(
            LayoutLineContext context,
            List<LayoutLinePart> groups,
            out Advantage2TokenRoute[] routes)
        {
            var keys = new KeyDefinition?[groups.Count];

            routes = new Advantage2TokenRoute[groups.Count];

            if (context.Rules.UsesKeypadTokenPrefix)
            {
                var keypad = false;

                for (var i = 0; i < groups.Count; i++)
                {
                    routes[i] = Advantage2TokenRouter.Route(groups[i].Inner);
                    keys[i] = routes[i].Key;
                    keypad |= routes[i].IsKeypadLayer;
                }

                context.LayerIndex = keypad ? 1 : 0;

                return keys;
            }

            for (var i = 0; i < groups.Count; i++)
            {
                keys[i] = LayoutTokenResolver.Resolve(groups[i].Inner, context.Rules.Tokens);
            }

            return keys;
        }

        /// <summary>
        /// 06 §2.1: every token before the last must be a modifier — a co-trigger; the last is
        /// the trigger key. Non-Gen2 triggers must match a key's trigger identity on the
        /// addressed layer; Gen2 triggers are matched by original key across all layers, the
        /// layer coming from the current header (04 §4.2 step 4).
        /// </summary>
        private static bool[] JudgeTriggerSide(
            LayoutLineContext context,
            List<LayoutLinePart> groups,
            KeyDefinition?[] keys,
            Advantage2TokenRoute[] routes,
            out KeyboardKey? triggerKey)
        {
            var verdicts = new bool[groups.Count];

            for (var i = 0; i < groups.Count - 1; i++)
            {
                verdicts[i] = keys[i] is not null && MacroModifierCodes.IsModifierKeyCode(keys[i]!.Code);
            }

            triggerKey = null;

            var trigger = keys[^1];

            if (trigger is null)
            {
                return verdicts;
            }

            if (context.Rules.UsesLayerHeaders)
            {
                verdicts[^1] = ExistsAsOriginalKey(context.Layout, trigger.Code);

                return verdicts;
            }

            var layer = context.FindLayer();

            if (layer is not null)
            {
                triggerKey = context.Rules.UsesKeypadTokenPrefix
                    ? Advantage2TokenRouter.Find(layer, routes[^1], byTrigger: true)
                    : layer.FindByTriggerKeyCode(trigger.Code);
            }

            verdicts[^1] = triggerKey is not null;

            return verdicts;
        }

        private static void RecordTriggerSegments(LayoutLineContext context, bool[] verdicts)
        {
            var groupIndex = 0;

            foreach (var part in context.ConfigParts)
            {
                if (part.Kind == LayoutLinePartKind.MacroGroup)
                {
                    context.Segments.Add(part, verdicts[groupIndex]);
                    groupIndex++;
                }
                else
                {
                    // The config side must start with { (04 §5.1); whitespace is
                    // tolerated only between or after the trigger groups.
                    context.Segments.Add(part, groupIndex > 0 && part.IsWhitespaceText);
                }
            }
        }

        private static void ReadValueSide(LayoutLineContext context, MacroValueReader reader)
        {
            var headerZone = true;

            foreach (var part in context.ValueParts)
            {
                if (part.Kind != LayoutLinePartKind.MacroGroup)
                {
                    // Junk between groups and [..] groups inside a macro value are invalid;
                    // whitespace is tolerated (04 §5.1).
                    context.Segments.Add(part, part.IsWhitespaceText);
                    continue;
                }

                if (headerZone && reader.TryConsumeHeaderToken(part.Inner))
                {
                    context.Segments.Add(part, true);
                    continue;
                }

                headerZone = false;
                context.Segments.Add(part, reader.TryReadKeystroke(part.Inner));
            }
        }

        private static Macro BuildMacro(
            LayoutLineContext context,
            MacroValueReader reader,
            KeyDefinition?[] keys,
            IReadOnlyList<Keystroke> keystrokes)
        {
            // CreateMacro stamps the device's speed/repeat defaults (06 §4: "defaults if the
            // tokens were absent"); consumed tokens override them.
            var macro = context.Layout.CreateMacro();

            if (reader.Speed.HasValue)
            {
                macro.Speed = reader.Speed.Value;
            }

            if (reader.Repeat.HasValue)
            {
                macro.RepeatFrequency = reader.Repeat.Value;
            }

            for (var i = 0; i < keys.Length - 1; i++)
            {
                macro.AddCoTrigger(keys[i]!);
            }

            macro.AddKeystrokes(keystrokes);
            macro.TriggerKey = keys[^1]!.Code;
            macro.LayerIndex = context.LayerIndex;

            return macro;
        }

        /// <summary>
        /// 04 §4.2 step 5: Gen2 appends to the flat list; slot devices fill the first empty of
        /// the trigger key's five slots, so at most five macro lines per trigger key are
        /// retained — a sixth is tracked as an unapplied line instead of vanishing silently.
        /// </summary>
        private static bool Store(LayoutLineContext context, Macro macro, KeyboardKey? triggerKey)
        {
            if (context.Layout.UsesFlatMacroList)
            {
                context.Layout.AddMacro(macro);

                return true;
            }

            for (var slot = Macro.MinMacroIndex; slot <= Macro.MaxMacroIndex; slot++)
            {
                if (triggerKey!.GetMacro(slot) is null)
                {
                    triggerKey.SetMacro(slot, macro);

                    return true;
                }
            }

            return false;
        }

        /// <summary>04 §4.2 step 4: Gen2 macro triggers are matched by original key across all layers.</summary>
        private static bool ExistsAsOriginalKey(KeyboardLayout layout, int keyCode)
        {
            foreach (var layer in layout.Layers)
            {
                if (layer.FindByOriginalKeyCode(keyCode) is not null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkAllPartsInvalid(LayoutLineContext context)
        {
            foreach (var part in context.ConfigParts)
            {
                context.Segments.Add(part, part.IsWhitespaceText);
            }

            foreach (var part in context.ValueParts)
            {
                context.Segments.Add(part, true);
            }

            context.MarkInvalidWithoutSegment();
        }
    }
}
