using System.Globalization;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Reports what a <see cref="KeyboardLayout"/> breaches against its device's catalog limits and
    /// the macro rules of specs/06-macros.md §5/§6, specs/04-layout-file-format.md §5.3 and
    /// specs/11-feature-dialogs.md §11.1. Pure reporting: nothing here changes the model, and the
    /// model's tolerant load paths never refuse an edit because of a limit.
    /// </summary>
    internal static class KeyboardLayoutValidator
    {
        /// <summary>
        /// Returns every breach of <paramref name="layout"/> in a fixed order: the layout-wide
        /// budgets first, then per layer its keys in index order (each key's permissions, then its
        /// tap-and-hold, then its macro slots in slot order) followed by its edge-lighting zones,
        /// then the Gen2 flat-list findings in flat-list order. Nothing here enumerates a hash
        /// container, so two calls on an unchanged layout return identical lists.
        /// </summary>
        public static IReadOnlyList<ModelViolation> Validate(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var violations = new List<ModelViolation>();

            ValidateLayoutBudgets(layout, violations);
            ValidateKeys(layout, violations);
            ValidateFlatMacros(layout, violations);

            return violations;
        }

        private static void ValidateLayoutBudgets(KeyboardLayout layout, List<ModelViolation> violations)
        {
            var macros = layout.Device.Macros;

            // 06 §6: the raised, firmware-gated count (MacroCapability.GatedMaxMacroCount) is only
            // reachable once the gate is evaluated against real firmware, which happens elsewhere.
            if (macros.MaxMacroCount is { } maxMacroCount && layout.MacroCount > maxMacroCount)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.MacroCountExceeded,
                    Message = $"The layout holds {layout.MacroCount} macros; the device allows {maxMacroCount}.",
                    Limit = maxMacroCount,
                    ActualValue = layout.MacroCount
                });
            }

            if (macros.MaxTotalKeystrokes is { } maxKeystrokes && layout.TotalKeystrokes > maxKeystrokes)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.MacroKeystrokeBudgetExceeded,
                    Message = $"The layout uses {layout.TotalKeystrokes} macro keystrokes; the budget is {maxKeystrokes}.",
                    Limit = maxKeystrokes,
                    ActualValue = layout.TotalKeystrokes
                });
            }

            var tapAndHoldCount = layout.TapAndHoldCount;

            if (layout.Device.TapAndHold.MaxPerLayout is { } maxTapAndHold && tapAndHoldCount > maxTapAndHold)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.TapAndHoldCountExceeded,
                    Message = $"The layout holds {tapAndHoldCount} tap-and-hold keys; the device allows {maxTapAndHold}.",
                    Limit = maxTapAndHold,
                    ActualValue = tapAndHoldCount
                });
            }
        }

        private static void ValidateKeys(KeyboardLayout layout, List<ModelViolation> violations)
        {
            foreach (var layer in layout.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    ValidateKeyPermissions(layout, layer, key, violations);
                    ValidateTapAndHold(layout, layer, key, violations);
                    ValidateKeyMacros(layout, layer, key, violations);
                }

                ValidateEdgeZones(layer, violations);
            }
        }

        /// <summary>
        /// 05 §5.3: a locked position may hold none of the three single-key rules of 04 §2; 04 §4.3
        /// writes at most one of them per position; and 11 §11.2 scopes multi-modifiers to the
        /// Advantage360. All three are reachable only through the tolerant load paths, so they are
        /// reported instead of being dropped silently.
        /// </summary>
        private static void ValidateKeyPermissions(
            KeyboardLayout layout,
            KeyboardLayer layer,
            KeyboardKey key,
            List<ModelViolation> violations)
        {
            if (!key.CanEdit && (key.IsModified || key.IsTapAndHold || key.HasMultiModifiers))
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.EditOnLockedKey,
                    Message = $"Key {key.Index} carries a remap, tap-and-hold or multi-modifier but the position can never be remapped.",
                    LayerIndex = layer.Index,
                    KeyIndex = key.Index
                });
            }

            if (CountSingleKeyRules(key) > 1)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.ConflictingSingleKeyRules,
                    Message = $"Key {key.Index} carries more than one single-key rule; saving writes the tap-and-hold line, else the multi-modifier line, else the remap, and drops the rest.",
                    LayerIndex = layer.Index,
                    KeyIndex = key.Index
                });
            }

            if (key.HasMultiModifiers && !layout.Device.SupportsMultiModifiers)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.MultiModifiersNotSupported,
                    Message = $"Key {key.Index} carries the multi-modifier code \"{key.MultiModifiers}\", which device {layout.Device.Id} does not support.",
                    LayerIndex = layer.Index,
                    KeyIndex = key.Index
                });
            }
        }

        private static int CountSingleKeyRules(KeyboardKey key)
        {
            var count = 0;

            if (key.IsModified)
            {
                count++;
            }

            if (key.IsTapAndHold)
            {
                count++;
            }

            if (key.HasMultiModifiers)
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// The TKO edge-lighting zones (05 §1.4, §4.4) are built locked and slotless, so the editor
        /// paths refuse them — but the tolerant load paths can still write, and the zones are
        /// deliberately outside every counter. Without this walk that state would be invisible.
        /// A zone holds a colour and nothing else: its lines live in <c>led*.txt</c>, never in a
        /// layout file (04 §3.4).
        /// </summary>
        private static void ValidateEdgeZones(KeyboardLayer layer, List<ModelViolation> violations)
        {
            foreach (var zone in layer.EdgeKeys)
            {
                if (zone.IsModified || zone.IsTapAndHold || zone.HasMultiModifiers)
                {
                    violations.Add(new ModelViolation
                    {
                        Kind = ModelViolationKind.EditOnLockedKey,
                        Message = $"Edge-lighting zone {zone.Index} carries a remap, tap-and-hold or multi-modifier; a lighting zone holds only a colour.",
                        LayerIndex = layer.Index,
                        KeyIndex = zone.Index
                    });
                }

                if (zone.IsMacro)
                {
                    violations.Add(new ModelViolation
                    {
                        Kind = ModelViolationKind.MacroOnRestrictedKey,
                        Message = $"Edge-lighting zone {zone.Index} carries a macro; a lighting zone holds only a colour.",
                        LayerIndex = layer.Index,
                        KeyIndex = zone.Index
                    });
                }
            }
        }

        private static void ValidateTapAndHold(
            KeyboardLayout layout,
            KeyboardLayer layer,
            KeyboardKey key,
            List<ModelViolation> violations)
        {
            if (!key.IsTapAndHold)
            {
                return;
            }

            var capability = layout.Device.TapAndHold;

            if (!capability.IsSupported)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.TapAndHoldNotSupported,
                    Message = $"Key {key.Index} carries a tap-and-hold assignment, which device {layout.Device.Id} does not support.",
                    LayerIndex = layer.Index,
                    KeyIndex = key.Index
                });

                return;
            }

            if (capability.DelayMilliseconds is not { } delay || delay.Contains(key.TimingDelay))
            {
                return;
            }

            violations.Add(new ModelViolation
            {
                Kind = ModelViolationKind.TapAndHoldDelayOutOfRange,
                Message = $"Key {key.Index} holds a {key.TimingDelay} ms threshold; the range is {delay.Minimum}-{delay.Maximum} ms.",
                LayerIndex = layer.Index,
                KeyIndex = key.Index,
                Limit = delay.Maximum,
                ActualValue = key.TimingDelay
            });
        }

        private static void ValidateKeyMacros(
            KeyboardLayout layout,
            KeyboardLayer layer,
            KeyboardKey key,
            List<ModelViolation> violations)
        {
            if (!key.IsMacro)
            {
                return;
            }

            if (!key.CanAssignMacro)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.MacroOnRestrictedKey,
                    Message = $"Key {key.Index} carries a macro but the position does not accept macros.",
                    LayerIndex = layer.Index,
                    KeyIndex = key.Index
                });
            }

            if (layout.UsesFlatMacroList)
            {
                violations.Add(new ModelViolation
                {
                    Kind = ModelViolationKind.MacroOutsideFlatList,
                    Message = $"Key {key.Index} holds macros in its slots, but device {layout.Device.Id} writes only the flat macro list.",
                    LayerIndex = layer.Index,
                    KeyIndex = key.Index
                });
            }

            var slotMacros = new List<SlotMacro>(KeyboardKey.MacroSlotCount);

            // The slot a macro sits in is the slot being iterated: Macro.MacroIndex is a public
            // setter and may name a slot this key does not hold.
            for (var slot = Macro.MinMacroIndex; slot <= KeyboardKey.MacroSlotCount; slot++)
            {
                var macro = key.Macros[slot - 1];

                if (macro is null)
                {
                    continue;
                }

                ValidateMacro(layout, macro, layer.Index, key.Index, slot, violations);
                slotMacros.Add(new SlotMacro(macro, slot));
            }

            ReportCollisions(slotMacros, layer.Index, key.Index, violations);
        }

        private static void ValidateFlatMacros(KeyboardLayout layout, List<ModelViolation> violations)
        {
            if (!layout.UsesFlatMacroList)
            {
                return;
            }

            var groups = new Dictionary<(int Layer, int Trigger), List<SlotMacro>>();

            // A Dictionary enumerates in unspecified order, so the groups are reported in the order
            // the flat list first produced them — Validate's result order is part of its contract.
            var groupOrder = new List<(int Layer, int Trigger)>();

            // An instance parked in both stores was already measured against the device's limits by
            // the key walk above; measuring it again would report every breach of it twice. It still
            // takes part in the trigger grouping below, because in the flat list it is on a trigger.
            var alreadyValidated = CollectSlotMacros(layout);

            foreach (var macro in layout.Macros)
            {
                var layerIndex = NormalizeLayerIndex(macro.LayerIndex);

                if (!alreadyValidated.Contains(macro))
                {
                    ValidateMacro(layout, macro, layerIndex, keyIndex: null, macroIndex: null, violations);
                }

                // 06 §5 groups by trigger + layer, so a macro that names neither is not on any
                // trigger yet and cannot collide with anything; it is reported on its own instead.
                if (!IsBound(macro))
                {
                    violations.Add(Create(
                        ModelViolationKind.UnboundMacro,
                        "The macro names no trigger key or no layer.",
                        layerIndex,
                        keyIndex: null,
                        macroIndex: null));

                    continue;
                }

                var group = (macro.LayerIndex, macro.TriggerKey);

                if (!groups.TryGetValue(group, out var members))
                {
                    members = [];
                    groups[group] = members;
                    groupOrder.Add(group);
                }

                members.Add(new SlotMacro(macro, Slot: null));
            }

            foreach (var group in groupOrder)
            {
                ReportCollisions(groups[group], group.Layer, keyIndex: null, violations);
            }
        }

        private static HashSet<Macro> CollectSlotMacros(KeyboardLayout layout)
        {
            var slotMacros = new HashSet<Macro>(ReferenceEqualityComparer.Instance);

            foreach (var layer in layout.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    foreach (var macro in key.Macros)
                    {
                        if (macro is not null)
                        {
                            slotMacros.Add(macro);
                        }
                    }
                }
            }

            return slotMacros;
        }

        /// <summary>06 §1: a flat-list macro is tagged with its trigger key and its layer.</summary>
        private static bool IsBound(Macro macro)
        {
            return macro.TriggerKey != Macro.UnassignedIndex && macro.LayerIndex != Macro.UnassignedIndex;
        }

        /// <summary>
        /// The unassigned sentinel is not a layer: a report carrying <c>LayerIndex = -1</c> would
        /// send a caller looking up a layer that cannot exist, so it becomes "not layer-specific".
        /// </summary>
        private static int? NormalizeLayerIndex(int layerIndex)
        {
            return layerIndex == Macro.UnassignedIndex ? null : layerIndex;
        }

        private static void ValidateMacro(
            KeyboardLayout layout,
            Macro macro,
            int? layerIndex,
            int? keyIndex,
            int? macroIndex,
            List<ModelViolation> violations)
        {
            var capability = layout.Device.Macros;
            var isGen2 = layout.UsesFlatMacroList;

            if (isGen2 && macro.IsEmpty)
            {
                violations.Add(Create(ModelViolationKind.EmptyMacro, "The macro has no keystrokes.", layerIndex, keyIndex, macroIndex));
            }

            ValidateMacroLength(macro, capability, isGen2, layout.Dialect, layerIndex, keyIndex, macroIndex, violations);

            if (capability.MaxCoTriggersPerMacro is { } maxCoTriggers && macro.CoTriggerCount > maxCoTriggers)
            {
                violations.Add(Create(
                    ModelViolationKind.MacroCoTriggerLimitExceeded,
                    $"The macro holds {macro.CoTriggerCount} co-triggers; the device allows {maxCoTriggers}.",
                    layerIndex,
                    keyIndex,
                    macroIndex,
                    maxCoTriggers,
                    macro.CoTriggerCount));
            }

            ValidateMacroValue(macro.Speed, capability.Speed, ModelViolationKind.MacroSpeedOutOfRange, "speed", layerIndex, keyIndex, macroIndex, violations);
            ValidateMacroValue(macro.RepeatFrequency, capability.Repeat, ModelViolationKind.MacroRepeatOutOfRange, "repeat", layerIndex, keyIndex, macroIndex, violations);

            if (!isGen2)
            {
                return;
            }

            if (!macro.HasBalancedKeyDirections())
            {
                violations.Add(Create(
                    ModelViolationKind.UnbalancedMacroKeyDirection,
                    "The macro has a key-down without a matching key-up.",
                    layerIndex,
                    keyIndex,
                    macroIndex));
            }

            if (IsReservedTrigger(macro.TriggerKey) && macro.CoTriggerCount == 0)
            {
                violations.Add(Create(
                    ModelViolationKind.ReservedMacroTriggerWithoutCoTrigger,
                    "A macro triggered by the Fn1 shift or keypad toggle key requires at least one co-trigger.",
                    layerIndex,
                    keyIndex,
                    macroIndex));
            }
        }

        /// <summary>
        /// 06 §6 measures the per-macro limit two different ways.
        /// <list type="bullet">
        /// <item>The Advantage360's 500 is "the length of the serialized *macro* text": the value
        /// side of 06 §3, i.e. the keystroke tokens alone
        /// (<see cref="MacroKeystrokeRenderer.KeystrokeTextLength"/>), excluding the trigger, the
        /// co-triggers and the <c>{sN}</c>/<c>{xN}</c> markers.</item>
        /// <item>Every other family's 300 is a keystroke count, and 04 §5.3 defines keystroke
        /// accounting once for the whole document — 1 per keystroke plus 2 per attached modifier —
        /// so it is the same weighted count that feeds the 7200 layout budget.</item>
        /// </list>
        /// </summary>
        private static void ValidateMacroLength(
            Macro macro,
            MacroCapability capability,
            bool isGen2,
            TokenDialect dialect,
            int? layerIndex,
            int? keyIndex,
            int? macroIndex,
            List<ModelViolation> violations)
        {
            if (capability.MaxCharactersPerMacro is not { } maxCharacters)
            {
                return;
            }

            var length = isGen2
                ? MacroKeystrokeRenderer.KeystrokeTextLength(macro, dialect)
                : macro.WeightedKeystrokeCount;

            if (length <= maxCharacters)
            {
                return;
            }

            var metric = isGen2 ? "serialized keystroke characters" : "weighted keystrokes";

            violations.Add(Create(
                ModelViolationKind.MacroLengthExceeded,
                $"The macro holds {length.ToString(CultureInfo.InvariantCulture)} {metric}; the device allows {maxCharacters}.",
                layerIndex,
                keyIndex,
                macroIndex,
                maxCharacters,
                length));
        }

        private static void ValidateMacroValue(
            int value,
            ValueRange? range,
            ModelViolationKind kind,
            string name,
            int? layerIndex,
            int? keyIndex,
            int? macroIndex,
            List<ModelViolation> violations)
        {
            if (range is null || range.Contains(value))
            {
                return;
            }

            violations.Add(Create(
                kind,
                $"The macro {name} is {value.ToString(CultureInfo.InvariantCulture)}; the range is {range.Minimum}-{range.Maximum}.",
                layerIndex,
                keyIndex,
                macroIndex,
                range.Maximum,
                value));
        }

        private static void ReportCollisions(
            List<SlotMacro> macros,
            int? layerIndex,
            int? keyIndex,
            List<ModelViolation> violations)
        {
            for (var first = 0; first < macros.Count - 1; first++)
            {
                for (var second = first + 1; second < macros.Count; second++)
                {
                    if (!macros[first].Macro.CollidesWith(macros[second].Macro))
                    {
                        continue;
                    }

                    violations.Add(Create(
                        ModelViolationKind.MacroTriggerCollision,
                        "Two macros share the same trigger key and co-trigger set.",
                        layerIndex,
                        keyIndex,
                        macros[second].Slot));
                }
            }
        }

        private static bool IsReservedTrigger(int keyCode)
        {
            return keyCode is KeyboardKey.Fn1ShiftKeyCode or KeyboardKey.KeypadToggleKeyCode;
        }

        private static ModelViolation Create(
            ModelViolationKind kind,
            string message,
            int? layerIndex,
            int? keyIndex,
            int? macroIndex,
            int? limit = null,
            int? actualValue = null)
        {
            return new ModelViolation
            {
                Kind = kind,
                Message = message,
                LayerIndex = layerIndex,
                KeyIndex = keyIndex,
                MacroIndex = macroIndex,
                Limit = limit,
                ActualValue = actualValue
            };
        }

        /// <summary>A macro paired with the slot it was found in; null for flat-list macros (06 §1).</summary>
        private readonly record struct SlotMacro(Macro Macro, int? Slot);
    }
}
