using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    /// <summary>
    /// Shared helper rendering a layer's default tokens as one space-separated string
    /// so tests can assert whole layers against the spec listings in
    /// specs/05-key-model.md §4. Empty tokens (the Advantage2 Keypad/Program buttons)
    /// render as "(none)".
    /// </summary>
    internal static class GeometryTokens
    {
        public static string RenderDefaults(LayerGeometry layer)
        {
            return string.Join(" ", layer.Keys.Select(key => key.DefaultToken.Length == 0 ? "(none)" : key.DefaultToken));
        }
    }
}
