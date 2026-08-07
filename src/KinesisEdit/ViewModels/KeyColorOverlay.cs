using System.Globalization;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Projects a profile's lighting model onto the keyboard picture: which key carries which
    /// painted colour. It is more than a field read for two reasons, and both are why it lives in
    /// one tested place:
    /// <list type="bullet">
    /// <item><see cref="LayerLightingState.KeyColors"/> is keyed by <b>memory key code</b>
    /// (specs/07-lighting.md §4) — which is the key every cap answers with
    /// (<c>KeyboardKeyViewModel.Key.OriginalKey.Code</c>) and the key
    /// <see cref="Core.Lighting.Preview.LightingEffectFrame.Cells"/> uses, so the paint layer and
    /// the effect layer address a cap the same way.</item>
    /// <item><see cref="KeyboardKey.KeyColor"/> exists but no parser ever fills it — the led file
    /// is parsed into the lighting model, never into the layout model — so reading the key would
    /// always show nothing, which is why the map is <b>pushed</b> onto the caps
    /// (<see cref="KeyboardLayerViewModel.ApplyLighting"/>) rather than read from them.</item>
    /// </list>
    /// <see cref="ToHex"/>/<see cref="TryParseHex"/> are the module's <see cref="LedColor"/> ↔
    /// <c>#RRGGBB</c> pair: view models expose values, never Avalonia brushes
    /// (docs/app/app-shell.md, invariant 6).
    /// </summary>
    public static class KeyColorOverlay
    {
        private static readonly IReadOnlyDictionary<int, LedColor> _empty = new Dictionary<int, LedColor>();

        /// <summary>
        /// The layer's painted colours by <b>memory key code</b>. Empty unless the device has
        /// per-key RGB hardware, the session carried a <see cref="LightingModel"/>, and the layer
        /// is one of the two that model describes (layout layer 0 ↔ its top layer, layer 1 ↔ its Fn
        /// layer; specs/07-lighting.md §1.5).
        /// <para>
        /// It returns <see cref="LayerLightingState.KeyColors"/> itself: the map is already keyed
        /// the way the caps are addressed and already holds no black entry — black is "no colour"
        /// and <see cref="LayerLightingState.SetKeyColor"/> removes it (§2.1) — so copying it would
        /// only add a way for the two to disagree.
        /// </para>
        /// </summary>
        public static IReadOnlyDictionary<int, LedColor> BuildPaint(
            DeviceDefinition device,
            object? lighting,
            KeyboardLayer layer)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(layer);

            if (device.Lighting.Kind != LightingKind.PerKeyRgb || lighting is not LightingModel model)
            {
                return _empty;
            }

            return layer.Index switch
            {
                0 => model.TopLayer.KeyColors,
                1 => model.FnLayer.KeyColors,
                _ => _empty
            };
        }

        /// <summary>Formats a colour as the <c>#RRGGBB</c> string the view turns into a brush.</summary>
        public static string ToHex(LedColor color)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}");
        }

        /// <summary>
        /// Reads a <c>#RRGGBB</c> string back into a <see cref="LedColor"/> — the return path of
        /// <see cref="ToHex"/>, used where the colour picker hands an edited colour back to a view
        /// model (the picker itself works in Avalonia colours, which never cross this boundary).
        /// The leading <c>#</c> is optional and anything else returns false rather than throwing.
        /// </summary>
        public static bool TryParseHex(string? hex, out LedColor color)
        {
            color = LedColor.Black;

            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var digits = hex.Trim();

            if (digits.StartsWith('#'))
            {
                digits = digits[1..];
            }

            if (digits.Length != 6)
            {
                return false;
            }

            if (!byte.TryParse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
                || !byte.TryParse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
                || !byte.TryParse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            {
                return false;
            }

            color = new LedColor(red, green, blue);

            return true;
        }
    }
}
