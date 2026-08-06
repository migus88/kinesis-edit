using Avalonia;
using Avalonia.Media;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Looks a drawn mark up by resource key — the one thing the geometry converters in this
    /// namespace share.
    /// <para>
    /// The three geometry catalogs (Themes/Icons.axaml, Themes/DeviceArt.axaml,
    /// Themes/LightingMarks.axaml) are plain resource dictionaries with no theme variants: a
    /// device does not change shape when the OS flips, so the lookup is variant-free and a
    /// resolved <see cref="Geometry"/> can be handed straight to a binding. Colour is the exact
    /// opposite and never passes through here — a call site binds <c>Icon.Stroke</c>/<c>Fill</c>
    /// to a token brush with <c>{DynamicResource}</c> so it re-resolves on a theme flip.
    /// </para>
    /// <para>
    /// A key that is absent yields null rather than throwing, and an <c>Icon</c> handed null
    /// <c>Data</c> draws nothing and measures zero. That is what the design's "a feature a device
    /// lacks is not rendered at all" rule needs: the two catalog entries with no artwork (the
    /// never-shipped CROSSFIRE keypad and the web-configured Advantage 360 Professional) leave a
    /// gap instead of a placeholder.
    /// </para>
    /// </summary>
    public static class GeometryResources
    {
        /// <summary>
        /// The geometry registered under <paramref name="key"/>, or null when there is no
        /// application, no such key, or the key holds something that is not a geometry.
        /// </summary>
        public static Geometry? Find(string? key)
        {
            if (string.IsNullOrEmpty(key) || Application.Current is not { } application)
            {
                return null;
            }

            return application.TryGetResource(key, null, out var value) ? value as Geometry : null;
        }
    }
}
