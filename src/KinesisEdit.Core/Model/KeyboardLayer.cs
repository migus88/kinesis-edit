namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One layer of the runtime keyboard model — the legacy <c>TKBLayer</c>
    /// (specs/05-key-model.md §1.4): its key positions in index order, the TKO edge-lighting
    /// zones, the layer identity, and its name. The key list is fixed at construction (§7.2): all
    /// editing happens on the <see cref="KeyboardKey"/> objects it holds.
    /// </summary>
    public sealed class KeyboardLayer
    {
        /// <summary>Layer identity: 0 = top/base, 1 = bottom/keypad, Advantage360 uses 0..4 (§1.4).</summary>
        public int Index { get; }

        /// <summary>Spec-literal layer name, e.g. "Qwerty-top", "Base", "Fn1" (§1.4).</summary>
        public string Name { get; }

        /// <summary>
        /// Legacy layout type (§1.4): QWERTY 0 / Dvorak 1 on Legacy-dialect devices; on the
        /// Advantage360 it mirrors <see cref="Index"/>.
        /// </summary>
        public int LayerType { get; }

        /// <summary>Key positions ordered by <see cref="KeyboardKey.Index"/>, dense 0..N-1 (§1.4, §7.4).</summary>
        public IReadOnlyList<KeyboardKey> Keys { get; }

        /// <summary>
        /// Edge-lighting zones, populated only for the TKO (§1.4 <c>EdgeKeyList</c>, §3.13, §4.4).
        /// Empty for every other device.
        /// </summary>
        public IReadOnlyList<KeyboardKey> EdgeKeys { get; }

        /// <summary>Creates a layer from its already-built key positions (§1.4).</summary>
        public KeyboardLayer(
            string name,
            int index,
            int layerType,
            IReadOnlyList<KeyboardKey> keys,
            IReadOnlyList<KeyboardKey>? edgeKeys = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfNegative(layerType);
            ArgumentNullException.ThrowIfNull(keys);

            Name = name;
            Index = index;
            LayerType = layerType;
            Keys = CopyValidated(keys, nameof(keys));
            EdgeKeys = edgeKeys is null ? [] : CopyValidated(edgeKeys, nameof(edgeKeys));
        }

        /// <summary>Returns the key at ordinal <paramref name="index"/>, or null (§1.5 lookup by index).</summary>
        public KeyboardKey? FindByIndex(int index)
        {
            return index >= 0 && index < Keys.Count ? Keys[index] : null;
        }

        /// <summary>Returns the first key whose original action has code <paramref name="keyCode"/>, or null (§1.5).</summary>
        public KeyboardKey? FindByOriginalKeyCode(int keyCode)
        {
            foreach (var key in Keys)
            {
                if (key.OriginalKey.Code == keyCode)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the first key whose position key has code <paramref name="keyCode"/>, or null —
        /// the lookup remap lines use, since they address positions (§1.5; 04 §4.2).
        /// </summary>
        public KeyboardKey? FindByPositionKeyCode(int keyCode)
        {
            foreach (var key in Keys)
            {
                if (key.PositionKey.Code == keyCode)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the first key whose <see cref="KeyboardKey.TriggerKey"/> has code
        /// <paramref name="keyCode"/>, or null — the lookup macro triggers use, which resolves the
        /// Fn1-shift/keypad-toggle exception of §1.3 (§1.5; 06 §1, 04 §4.2).
        /// </summary>
        public KeyboardKey? FindByTriggerKeyCode(int keyCode)
        {
            foreach (var key in Keys)
            {
                if (key.TriggerKey.Code == keyCode)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>Returns the edge-lighting zone at ordinal <paramref name="index"/>, or null (§1.5, §4.4).</summary>
        public KeyboardKey? FindEdgeKeyByIndex(int index)
        {
            return index >= 0 && index < EdgeKeys.Count ? EdgeKeys[index] : null;
        }

        /// <summary>Resets every key of the layer to its factory state (§1.3 reset, applied layer-wide).</summary>
        public void Reset()
        {
            foreach (var key in Keys)
            {
                key.Reset();
            }

            foreach (var edgeKey in EdgeKeys)
            {
                edgeKey.Reset();
            }
        }

        private static KeyboardKey[] CopyValidated(IReadOnlyList<KeyboardKey> keys, string parameterName)
        {
            var copy = new KeyboardKey[keys.Count];

            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];

                if (key is null)
                {
                    throw new ArgumentException($"Key at list offset {i} is null.", parameterName);
                }

                if (key.Index != i)
                {
                    throw new ArgumentException(
                        $"Key at list offset {i} has index {key.Index}; indices must be dense and in list order.",
                        parameterName);
                }

                copy[i] = key;
            }

            return copy;
        }
    }
}
