namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Layout variant of a device geometry. Only the Advantage2 ships two variants
    /// (QWERTY and Dvorak layer pairs, specs/05-key-model.md §4.5/§4.6); the
    /// Advantage360 has no layout-type concept (its legacy layer type mirrors the
    /// layer index, spec 05 §1.4) and uses <see cref="None"/>. The legacy numeric
    /// layout-type values (QWERTY 0, Dvorak 1) are spec data, not these enum values.
    /// </summary>
    public enum LayoutVariant
    {
        None = 0,
        Qwerty = 1,
        Dvorak = 2
    }
}
