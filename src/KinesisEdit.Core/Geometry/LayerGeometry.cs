namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// One layer of a device's physical geometry: its spec-literal name (e.g.
    /// "Qwerty-top", "Base"), its layer index (top 0 / bottom 1; Advantage360 Base 0,
    /// Keypad 1, Fn1 2, Fn2 3, Fn3 4 — specs/05-key-model.md §1.4), the ordered key
    /// positions, and (TKO only) the edge-lighting zones (spec 05 §4.4). Immutable.
    /// </summary>
    public sealed class LayerGeometry
    {
        /// <summary>Spec-literal layer name, e.g. "Qwerty-keypad", "Dvorak-top", "Fn1".</summary>
        public string Name { get; }

        /// <summary>Layer index per spec 05 §1.4.</summary>
        public int Index { get; }

        /// <summary>Key positions in ascending index order; dense indices 0..N-1.</summary>
        public IReadOnlyList<KeyPosition> Keys { get; }

        /// <summary>
        /// Edge-lighting zones ("L1".."L9", "B1".."B15", "R1".."R9"), populated only
        /// for the TKO (spec 05 §1.4, §3.13, §4.4). Empty for every other device.
        /// These are lighting zones, not typing keys, so they are kept out of
        /// <see cref="Keys"/> and the edit/macro flags carry no meaning for them.
        /// </summary>
        public IReadOnlyList<KeyPosition> EdgeZones { get; }

        public LayerGeometry(string name, int index, IReadOnlyList<KeyPosition> keys, IReadOnlyList<KeyPosition>? edgeZones = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentNullException.ThrowIfNull(keys);

            Name = name;
            Index = index;
            Keys = CopyValidated(keys, nameof(keys));
            EdgeZones = edgeZones is null ? Array.Empty<KeyPosition>() : CopyValidated(edgeZones, nameof(edgeZones));
        }

        private static KeyPosition[] CopyValidated(IReadOnlyList<KeyPosition> positions, string parameterName)
        {
            var copy = new KeyPosition[positions.Count];

            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];

                if (position is null)
                {
                    throw new ArgumentException($"Position at list offset {i} is null.", parameterName);
                }

                if (position.Index != i)
                {
                    throw new ArgumentException(
                        $"Position at list offset {i} has index {position.Index}; indices must be dense and in list order.",
                        parameterName);
                }

                copy[i] = position;
            }

            return copy;
        }
    }
}
