using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the layer switch calls each layer. The geometry's <see cref="KeyboardLayer.Name"/> is
    /// the spec-literal file-side name ("Qwerty-top" / "Qwerty-keypad" on the Gen1 boards), which
    /// is not what the editors show: specs/10-apps-and-ui.md describes the Freestyle layer switch
    /// as toggling "top ↔ Fn layer" and the Advantage2's as "top layer ↔ embedded keypad layer",
    /// both of which the app renders as <c>Top</c> / <c>Fn</c>.
    /// <para>
    /// The Advantage 360's five layers already carry presentation-ready spec names (Base, Keypad,
    /// Fn1, Fn2, Fn3), so the Gen2 dialect keeps them verbatim. Any layer the mapping does not
    /// cover falls back to its raw name rather than inventing one.
    /// </para>
    /// </summary>
    public static class LayerCaptions
    {
        /// <summary>Caption of layer 0 on the Legacy and Gen1 dialects.</summary>
        public const string TopLayerCaption = "Top";

        /// <summary>Caption of layer 1 on the Legacy and Gen1 dialects.</summary>
        public const string FnLayerCaption = "Fn";

        /// <summary>Returns the caption of <paramref name="layer"/> for a device writing <paramref name="dialect"/>.</summary>
        public static string ForLayer(KeyboardLayer layer, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(layer);

            if (dialect == TokenDialect.Gen2)
            {
                return layer.Name;
            }

            return layer.Index switch
            {
                0 => TopLayerCaption,
                1 => FnLayerCaption,
                _ => layer.Name
            };
        }
    }
}
