using System.Globalization;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Projects a profile's lighting model onto the keyboard picture: which key index shows which
    /// colour swatch. Two mismatches make this more than a lookup, and both are the reason it
    /// lives in one tested place:
    /// <list type="bullet">
    /// <item><see cref="LayerLightingState.KeyColors"/> is keyed by <b>memory key code</b>
    /// (specs/07-lighting.md §4), not by key index, so every entry is resolved through
    /// <see cref="KeyboardLayer.FindByOriginalKeyCode"/>.</item>
    /// <item><see cref="KeyboardKey.KeyColor"/> exists but no parser ever fills it — the led file
    /// is parsed into the lighting model, never into the layout model — so reading the key would
    /// always show nothing.</item>
    /// </list>
    /// Colours are returned as <c>#RRGGBB</c> strings: view models expose values, never Avalonia
    /// brushes (docs/app/app-shell.md, invariant 6).
    /// </summary>
    public static class KeyColorOverlay
    {
        private static readonly IReadOnlyDictionary<int, string> _empty = new Dictionary<int, string>();

        /// <summary>
        /// Builds the key-index → <c>#RRGGBB</c> map for one layer. Empty unless the device has
        /// per-key RGB hardware, the session carried a <see cref="LightingModel"/>, and the layer
        /// is one of the two that model describes (layout layer 0 ↔ its top layer, layer 1 ↔ its Fn
        /// layer; specs/07-lighting.md §1.5). Black is the "no colour" value (§2.1) and yields no
        /// entry.
        /// </summary>
        public static IReadOnlyDictionary<int, string> Build(DeviceDefinition device, object? lighting, KeyboardLayer layer)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(layer);

            if (device.Lighting.Kind != LightingKind.PerKeyRgb || lighting is not LightingModel model)
            {
                return _empty;
            }

            var state = layer.Index switch
            {
                0 => model.TopLayer,
                1 => model.FnLayer,
                _ => null
            };

            if (state is null || state.KeyColors.Count == 0)
            {
                return _empty;
            }

            var overlays = new Dictionary<int, string>(state.KeyColors.Count);

            foreach (var (keyCode, color) in state.KeyColors)
            {
                if (color.IsBlack)
                {
                    continue;
                }

                var key = layer.FindByOriginalKeyCode(keyCode);

                if (key is not null)
                {
                    overlays[key.Index] = ToHex(color);
                }
            }

            return overlays;
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
