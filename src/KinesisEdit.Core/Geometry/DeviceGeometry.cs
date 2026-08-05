namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// The full physical geometry of one device layout family: its ordered layers,
    /// per specs/05-key-model.md §1.5 and §4. Immutable; obtained from
    /// <see cref="GeometryCatalog"/>.
    /// </summary>
    public sealed class DeviceGeometry
    {
        /// <summary>Layout variant of this geometry (QWERTY/Dvorak on Legacy-dialect devices).</summary>
        public LayoutVariant Variant { get; }

        /// <summary>Layers in the spec's order (spec 05 §1.5), fully materialized.</summary>
        public IReadOnlyList<LayerGeometry> Layers { get; }

        public DeviceGeometry(LayoutVariant variant, IReadOnlyList<LayerGeometry> layers)
        {
            ArgumentNullException.ThrowIfNull(layers);

            if (layers.Count == 0)
            {
                throw new ArgumentException("A device geometry requires at least one layer.", nameof(layers));
            }

            var copy = new LayerGeometry[layers.Count];

            for (var i = 0; i < layers.Count; i++)
            {
                copy[i] = layers[i] ?? throw new ArgumentException($"Layer at offset {i} is null.", nameof(layers));
            }

            Variant = variant;
            Layers = copy;
        }
    }
}
