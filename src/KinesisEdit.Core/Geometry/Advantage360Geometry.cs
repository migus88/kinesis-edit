namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Builds the Advantage360 (SmartSet) geometry per specs/05-key-model.md §4.7:
    /// 77 positions per layer, five layers (Base 0, Keypad 1, Fn1 2, Fn2 3, Fn3 4).
    /// Distinct position tokens decouple the physical position name from the layer's
    /// default action: the keypad toggle at 6 (pos "kp"), the thumb Fn keys at 54/63
    /// (pos "lfn"/"rfn"), the pedal jack at 76 (pos "pedl"), the Keypad layer's
    /// embedded keypad, and the Fn layers' F-keys on the number-row positions.
    /// Index 7 is the locked SmartSet key; unlike Gen1 devices, Advantage360 modifier
    /// positions are remappable and macro-capable (spec 05 §5.3). Fn2 and Fn3 are
    /// identical to Fn1 apart from layer index and name. The Advantage360
    /// Professional is configured via the ZMK web GUI, not SmartSet (specs/02), so
    /// no geometry exists for it.
    /// </summary>
    internal static class Advantage360Geometry
    {
        public static DeviceGeometry Create()
        {
            var baseKeys = BuildBaseKeys();

            var keypadKeys = LayerBuilder.StartFrom(baseKeys)
                .Override(6, "deft", "kp")
                .Override(9, "nmlk", "7")
                .Override(10, "kp=", "8")
                .Override(11, "kp/", "9")
                .Override(12, "kp*", "0")
                .Override(23, "kp7", "u")
                .Override(24, "kp8", "i")
                .Override(25, "kp9", "o")
                .Override(26, "kp-", "p")
                .Override(37, "kp4", "j")
                .Override(38, "kp5", "k")
                .Override(39, "kp6", "l")
                .Override(40, "kp+", "scol")
                .Override(49, "kp1", "m")
                .Override(50, "kp2", "comm")
                .Override(51, "kp3", "perd")
                .Override(52, "kpen", "fsls")
                .Override(61, "kp.", "obrk")
                .Override(73, "kp0", "spc")
                .Build();

            var fn1Keys = LayerBuilder.StartFrom(baseKeys)
                .Override(0, "F1", "eql")
                .Override(1, "F2", "1")
                .Override(2, "F3", "2")
                .Override(3, "F4", "3")
                .Override(4, "F5", "4")
                .Override(5, "F6", "5")
                .Override(8, "F7", "6")
                .Override(9, "F8", "7")
                .Override(10, "F9", "8")
                .Override(11, "F10", "9")
                .Override(12, "F11", "0")
                .Override(13, "F12", "hyph")
                .Override(54, "defs", "lfn")
                .Override(63, "defs", "rfn")
                .Build();

            var layers = new[]
            {
                new LayerGeometry("Base", 0, baseKeys),
                new LayerGeometry("Keypad", 1, keypadKeys),
                new LayerGeometry("Fn1", 2, fn1Keys),
                new LayerGeometry("Fn2", 3, fn1Keys),
                new LayerGeometry("Fn3", 4, fn1Keys)
            };

            return new DeviceGeometry(LayoutVariant.None, layers);
        }

        private static IReadOnlyList<KeyPosition> BuildBaseKeys()
        {
            return LayerBuilder.Start()
                .Key("eql")
                .Keys("1", "2", "3", "4", "5")
                .Key("keyt", "kp")
                .Locked("ss")
                .Keys("6", "7", "8", "9", "0")
                .Key("hyph")
                .Key("tab")
                .Keys("q", "w", "e", "r", "t")
                .Keys("hk1", "hk3")
                .Keys("y", "u", "i", "o", "p")
                .Key("bsls")
                .Key("esc")
                .Keys("a", "s", "d", "f", "g")
                .Keys("hk2", "hk4")
                .Keys("h", "j", "k", "l")
                .Keys("scol", "apos")
                .Key("lshf")
                .Keys("z", "x", "c", "v", "b")
                .Keys("n", "m")
                .Keys("comm", "perd", "fsls")
                .Key("rshf")
                .Key("fn1s", "lfn")
                .Keys("grav", "caps", "left", "rght", "up", "down", "obrk", "cbrk")
                .Key("fn1s", "rfn")
                .Keys("lctr", "lalt", "rwin", "rctr")
                .Keys("bspc", "del", "home", "pgup", "ent", "spc")
                .Keys("end", "pgdn")
                .Key("null", "pedl")
                .Build();
        }
    }
}
