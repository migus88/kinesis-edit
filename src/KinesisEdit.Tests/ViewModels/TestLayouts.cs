using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

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

        /// <summary>Index of the "3" position on the Freestyle Edge RGB.</summary>
        public const int RgbDigitThreeKeyIndex = 22;

        /// <summary>
        /// Index of the Left Shift position on the Freestyle Edge RGB — a modifier position, so
        /// specs/05-key-model.md §5.3 marks it <c>CanAssignMacro == false</c> and it is what the
        /// macro panel's refusal is exercised against.
        /// </summary>
        public const int RgbLeftShiftKeyIndex = 69;

        /// <summary>Resolves a key-table entry by its Gen1 file token.</summary>
        public static KeyDefinition Gen1Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                ?? throw new InvalidOperationException($"No Gen1 key registered for token '{token}'.");
        }

        /// <summary>Wraps <paramref name="key"/> in a cap view model with a throwaway placement.</summary>
        public static KeyboardKeyViewModel CreateKeyViewModel(KeyboardKey key, TokenDialect dialect = TokenDialect.Gen1)
        {
            ArgumentNullException.ThrowIfNull(key);

            return new KeyboardKeyViewModel(key, new KeyVisual(key.Index, 0, 0), dialect);
        }

        /// <summary>
        /// Fills <paramref name="count"/> macro slots of the layout's first layer, in key and slot
        /// order — how a test drives the profile up to its macro count limit (06 §6).
        /// </summary>
        public static void FillMacroSlots(KeyboardLayout layout, int count)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var added = 0;

            foreach (var key in layout.Layers[0].Keys)
            {
                if (!key.CanAssignMacro)
                {
                    continue;
                }

                for (var slot = 1; slot <= key.Macros.Count && added < count; slot++)
                {
                    key.SetMacro(slot, layout.CreateMacro());

                    added++;
                }

                if (added >= count)
                {
                    return;
                }
            }
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

        /// <summary>
        /// The real Freestyle Edge RGB board over a device definition that does <b>not</b> support
        /// tap-and-hold. The three devices whose catalog entry says so (Savant Elite2, CROSSFIRE,
        /// Advantage360 Professional) have no keyboard picture at all, so §11.1's "does the device
        /// have the feature" guard can only be exercised against a hand-built definition.
        /// </summary>
        public static KeyboardLayout CreateLayoutWithoutTapAndHold()
        {
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb) with
            {
                TapAndHold = TapAndHoldCapability.None
            };

            return new KeyboardLayout(device, GeometryCatalog.FreestyleEdgeRgb);
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
