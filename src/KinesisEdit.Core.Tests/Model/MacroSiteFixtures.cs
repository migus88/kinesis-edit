using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// Layouts and macros for the <see cref="MacroSites"/> and <see cref="MacroPlacement"/> tests:
    /// the two macro stores of specs/06-macros.md §1 (per-key slots and the Advantage360 flat list)
    /// and a hybrid used to exercise the <c>fn1s</c>/<c>keyt</c> trigger-identity exception on a
    /// slot store — no shipped slot device has such a key, so the only way to cover the rule is to
    /// combine the Advantage360 geometry with a slot-based macro capability, which is exactly what
    /// the <see cref="KeyboardLayout"/> two-argument constructor exists for.
    /// </summary>
    internal static class MacroSiteFixtures
    {
        public static KeyboardLayout SlotLayout()
        {
            return KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
        }

        public static KeyboardLayout FlatListLayout()
        {
            return KeyboardLayout.Create(DeviceId.Advantage360);
        }

        public static KeyboardLayout SlotLayoutWithLayerSwitchKeys()
        {
            var device = DeviceCatalog.GetById(DeviceId.Advantage360) with
            {
                Macros = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros
            };

            if (!GeometryCatalog.TryGet(DeviceId.Advantage360, LayoutVariant.None, out var geometry))
            {
                throw new InvalidOperationException("The Advantage360 geometry is missing.");
            }

            return new KeyboardLayout(device, geometry);
        }

        public static Macro Typing(KeyboardLayout layout, string text, string? name = null)
        {
            var macro = layout.CreateMacro();

            foreach (var character in text)
            {
                macro.AddKeystroke(new Keystroke(ModelTokens.Key(character.ToString())));
            }

            if (name is not null)
            {
                macro.Name = name;
            }

            return macro;
        }

        /// <summary>
        /// Puts <paramref name="macro"/> in a key slot the way the layout parser does: the slot is
        /// stamped by <see cref="KeyboardKey.SetMacro"/> and the trigger/layer metadata is filled in
        /// (see <c>MacroLineParser</c>, which sets both on every macro whichever store it lands in).
        /// </summary>
        public static KeyboardKey AssignToSlot(
            KeyboardLayout layout,
            int layerIndex,
            int keyIndex,
            Macro macro,
            int slot = 1)
        {
            var layer = layout.Layers[layerIndex];
            var key = layer.Keys[keyIndex];

            macro.LayerIndex = layer.Index;
            macro.TriggerKey = key.TriggerKey.Code;
            key.SetMacro(slot, macro);

            return key;
        }

        /// <summary>Appends <paramref name="macro"/> to the Gen2 flat list, tagged like the parser tags it.</summary>
        public static KeyboardKey AddToFlatList(KeyboardLayout layout, int layerIndex, int keyIndex, Macro macro)
        {
            var layer = layout.Layers[layerIndex];
            var key = layer.Keys[keyIndex];

            macro.LayerIndex = layer.Index;
            macro.TriggerKey = key.TriggerKey.Code;
            layout.AddMacro(macro);

            return key;
        }

        public static KeyboardKey FindLayerSwitchKey(KeyboardLayout layout)
        {
            foreach (var layer in layout.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    if (key.OriginalKey.Code == KeyboardKey.Fn1ShiftKeyCode
                        && key.PositionKey.Code != key.OriginalKey.Code)
                    {
                        return key;
                    }
                }
            }

            throw new InvalidOperationException("No fn1s position with a distinct position key was found.");
        }
    }
}
