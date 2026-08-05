using System.Globalization;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Regenerates a layout file from the in-memory model per specs/04-layout-file-format.md
    /// §4.3 — nothing from the old file survives except tracked lines explicitly marked kept.
    /// For each layer in list order (Gen2 with its <c>&lt;...&gt;</c> header first) and each
    /// key in physical index order: the tap-and-hold line, else the multi-modifier line, else
    /// the remap line; then the key's persisted macro slots (Gen2 instead appends the flat
    /// list's macros of that layer after the keys); then that layer's kept invalid lines
    /// verbatim. Tokens are written in canonical registry casing (04 §1.2).
    /// </summary>
    public static class LayoutFileSerializer
    {
        /// <summary>The Gen1-family bottom-layer line prefix (04 §3.1).</summary>
        private const string FnLinePrefix = "fn ";

        /// <summary>Index of the bottom (Fn/keypad) layer on two-layer devices (04 §3.1, §3.2).</summary>
        private const int BottomLayerIndex = 1;

        /// <summary>Serializes <paramref name="layout"/> with no kept lines (04 §4.3).</summary>
        public static IReadOnlyList<string> Serialize(KeyboardLayout layout)
        {
            return Serialize(layout, []);
        }

        /// <summary>
        /// Serializes <paramref name="layout"/>, appending every tracked line whose
        /// <see cref="LayoutInvalidLine.Keep"/> is set verbatim after its layer's rules
        /// (04 §4.3 step 4, §5.2). Unkept lines are dropped — permanently, once this output is
        /// written to disk.
        /// </summary>
        public static IReadOnlyList<string> Serialize(KeyboardLayout layout, IEnumerable<LayoutInvalidLine> trackedLines)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(trackedLines);

            var rules = LayoutDialectRules.Create(layout.Device.Id);
            var keptLines = CollectKeptLines(trackedLines);
            var lines = new List<string>();

            foreach (var layer in layout.Layers)
            {
                if (rules.UsesLayerHeaders)
                {
                    lines.Add(Gen2LayerHeaders.Lines[layer.Index]);
                }

                foreach (var key in layer.Keys)
                {
                    WriteSingleKeyRule(lines, key, layer, rules);

                    if (!layout.UsesFlatMacroList)
                    {
                        WriteKeyMacros(lines, key, layer, rules);
                    }
                }

                if (layout.UsesFlatMacroList)
                {
                    WriteFlatListMacros(lines, layout, layer, rules);
                }

                WriteKeptLines(lines, keptLines, layer.Index);
            }

            // Defensive: a tracked line naming a layer this device does not have still must not
            // vanish once the user marked it kept.
            foreach (var kept in keptLines)
            {
                if (kept is not null)
                {
                    lines.Add(kept.Text);
                }
            }

            return lines;
        }

        private static List<LayoutInvalidLine?> CollectKeptLines(IEnumerable<LayoutInvalidLine> trackedLines)
        {
            var kept = new List<LayoutInvalidLine?>();

            foreach (var line in trackedLines)
            {
                if (line is not null && line.Keep)
                {
                    kept.Add(line);
                }
            }

            return kept;
        }

        private static void WriteKeptLines(List<string> lines, List<LayoutInvalidLine?> keptLines, int layerIndex)
        {
            for (var i = 0; i < keptLines.Count; i++)
            {
                var kept = keptLines[i];

                if (kept is null || kept.LayerIndex != layerIndex)
                {
                    continue;
                }

                // 04 §4.3: kept invalid lines are preserved exactly as loaded, including any
                // "fn " layer prefix.
                lines.Add(kept.Text);
                keptLines[i] = null;
            }
        }

        /// <summary>
        /// 04 §4.3 step 2: at most one single-key line per position — tap-and-hold, else
        /// multi-modifier (written by the RGB-family/Gen2 serializer only), else the remap when
        /// the key is modified. Unmodified keys produce no lines.
        /// </summary>
        private static void WriteSingleKeyRule(List<string> lines, KeyboardKey key, KeyboardLayer layer, LayoutDialectRules rules)
        {
            var positionToken = LayoutTokenResolver.GetWriteToken(key.PositionKey, rules.Tokens);

            if (positionToken.Length == 0)
            {
                // The Advantage2 Keypad/Program buttons have no file token and are never
                // written (05 §3.9); they cannot carry rules through the editor paths anyway.
                return;
            }

            var prefix = GetLinePrefix(layer, rules);
            var position = FormatPositionToken(positionToken, layer, rules);

            if (key.IsTapAndHold)
            {
                var tap = LayoutTokenResolver.GetWriteToken(key.TapAction!, rules.Tokens);
                var hold = LayoutTokenResolver.GetWriteToken(key.HoldAction!, rules.Tokens);

                if (tap.Length > 0 && hold.Length > 0)
                {
                    var delay = key.TimingDelay.ToString(CultureInfo.InvariantCulture);

                    lines.Add($"{prefix}[{position}]>[{tap}][t&h{delay}][{hold}]");
                }

                return;
            }

            if (key.HasMultiModifiers && rules.ReadsAndWritesMultiModifierLines)
            {
                lines.Add($"{prefix}[{position}]>[{key.MultiModifiers}]");

                return;
            }

            if (key.IsModified)
            {
                var output = LayoutTokenResolver.GetWriteToken(key.ModifiedKey!, rules.Tokens);

                if (output.Length > 0)
                {
                    lines.Add($"{prefix}[{position}]>[{output}]");
                }
            }
        }

        /// <summary>
        /// 04 §4.3: the persisted macro slots of one key, in slot order — slots 1-3 on the FS
        /// and Adv2 dialects, 1-5 on the RGB family (06 §1). Empty macros write nothing (06 §3).
        /// </summary>
        private static void WriteKeyMacros(List<string> lines, KeyboardKey key, KeyboardLayer layer, LayoutDialectRules rules)
        {
            var prefix = GetLinePrefix(layer, rules);
            var isBottomLayer = layer.Index == BottomLayerIndex;

            for (var slot = Macro.MinMacroIndex; slot <= rules.PersistedMacroSlots; slot++)
            {
                var macro = key.GetMacro(slot);

                if (macro is null || macro.IsEmpty)
                {
                    continue;
                }

                lines.Add(MacroLineWriter.Write(macro, key.TriggerKey, rules, isBottomLayer, prefix));
            }
        }

        /// <summary>
        /// 04 §4.3 step 3 (Gen2 only): all macros of the flat list whose layer matches the
        /// current layer, in list order. A macro that names no resolvable trigger cannot be
        /// written — <see cref="KeyboardLayout.Validate"/> reports it as unbound.
        /// </summary>
        private static void WriteFlatListMacros(List<string> lines, KeyboardLayout layout, KeyboardLayer layer, LayoutDialectRules rules)
        {
            foreach (var macro in layout.Macros)
            {
                if (macro.LayerIndex != layer.Index || macro.IsEmpty)
                {
                    continue;
                }

                var trigger = KeyRegistry.FindByCode(macro.TriggerKey);

                if (trigger is null)
                {
                    continue;
                }

                lines.Add(MacroLineWriter.Write(macro, trigger, rules, isBottomLayer: false, linePrefix: string.Empty));
            }
        }

        private static string GetLinePrefix(KeyboardLayer layer, LayoutDialectRules rules)
        {
            // 04 §3.1: the Gen1 family prepends "fn " to every bottom-layer line.
            return rules.UsesFnLinePrefix && layer.Index == BottomLayerIndex ? FnLinePrefix : string.Empty;
        }

        private static string FormatPositionToken(string positionToken, KeyboardLayer layer, LayoutDialectRules rules)
        {
            // 04 §3.2: the Advantage2 prepends "kp-" inside the bracket for bottom-layer keys,
            // except for the keypad-exception tokens.
            if (rules.UsesKeypadTokenPrefix
                && layer.Index == BottomLayerIndex
                && !Advantage2KeypadExceptions.IsExceptionToken(positionToken))
            {
                return Advantage2KeypadExceptions.KeypadPrefix + positionToken;
            }

            return positionToken;
        }
    }
}
