using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// Fixtures for the editor view models: a key-table lookup, hand-built geometries the
    /// catalogs do not ship (a locked position, a key with no placement), and small board
    /// pictures to join them against.
    /// </summary>
    internal static class TestLayouts
    {
        /// <summary>Index of the "1" position on the Freestyle Edge RGB (spec 05 §4.2).</summary>
        public const int RgbDigitOneKeyIndex = 20;

        /// <summary>Index of the "2" position on the Freestyle Edge RGB.</summary>
        public const int RgbDigitTwoKeyIndex = 21;

        /// <summary>Resolves a key-table entry by its Gen1 file token.</summary>
        public static KeyDefinition Gen1Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                ?? throw new InvalidOperationException($"No Gen1 key registered for token '{token}'.");
        }

        /// <summary>
        /// A Freestyle Edge RGB layout whose second position is locked (<c>CanEdit</c> false).
        /// The catalog geometry has no locked position on that device, and the remap rules of
        /// specs/05-key-model.md §5.3 still have to be exercised against it.
        /// </summary>
        public static KeyboardLayout CreateLockedKeyLayout()
        {
            var positions = new[]
            {
                new KeyPosition(0, "esc"),
                new KeyPosition(1, "F1", canEdit: false, canAssignMacro: false),
                new KeyPosition(2, "F2")
            };

            return new KeyboardLayout(
                DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb),
                new DeviceGeometry(LayoutVariant.Qwerty, [new LayerGeometry("Qwerty-top", 0, positions)]));
        }

        /// <summary>A layout of <paramref name="tokens"/> on one Gen1 layer, indices dense from 0.</summary>
        public static KeyboardLayout CreateLayout(params string[] tokens)
        {
            var positions = new KeyPosition[tokens.Length];

            for (var index = 0; index < tokens.Length; index++)
            {
                positions[index] = new KeyPosition(index, tokens[index]);
            }

            return new KeyboardLayout(
                DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb),
                new DeviceGeometry(LayoutVariant.Qwerty, [new LayerGeometry("Qwerty-top", 0, positions)]));
        }

        /// <summary>A board picture placing exactly <paramref name="indices"/>, one unit apart.</summary>
        public static KeyboardVisual CreateVisual(params int[] indices)
        {
            var keys = new KeyVisual[indices.Length];

            for (var offset = 0; offset < indices.Length; offset++)
            {
                keys[offset] = new KeyVisual(indices[offset], offset, 0);
            }

            return new KeyboardVisual(LayoutVariant.Qwerty, keys);
        }
    }
}
