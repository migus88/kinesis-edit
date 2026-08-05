namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Builds the Advantage2 geometries per specs/05-key-model.md §4.5/§4.6: 89
    /// positions per layer, two layers per layout variant (QWERTY "Qwerty-top"/
    /// "Qwerty-keypad", Dvorak "Dvorak-top"/"Dvorak-keypad"). Tokens are Legacy-dialect
    /// file tokens (spec 05 §3), so the keypad-layer operators store "kpdiv"/"kpmult"/
    /// "kpmin"/"kpplus". Indices 16/17 are the locked Keypad/Program buttons whose
    /// tokens are empty (never written to files, spec 05 §3.9/§3.10). Top-layer
    /// indices 86-88 default to the pedal tokens and carry the combined-master-app
    /// alternatives "tab"/"kpshft"/"kpenter"; the keypad layer always uses the pedal
    /// tokens. The keypad layer's "lwin"/"ralt" defaults at 1/2 are treated as
    /// modifier positions (macro not allowed, spec 05 §5.3).
    /// </summary>
    internal static class Advantage2Geometry
    {
        public static DeviceGeometry CreateQwerty()
        {
            var topKeys = BuildQwertyTopKeys();
            var bottomKeys = ApplyKeypadOverlay(LayerBuilder.StartFrom(topKeys)).Build();

            var layers = new[]
            {
                new LayerGeometry("Qwerty-top", 0, topKeys),
                new LayerGeometry("Qwerty-keypad", 1, bottomKeys)
            };

            return new DeviceGeometry(LayoutVariant.Qwerty, layers);
        }

        public static DeviceGeometry CreateDvorak()
        {
            var topKeys = LayerBuilder.StartFrom(BuildQwertyTopKeys())
                .Override(31, "'")
                .Override(32, ",")
                .Override(33, ".")
                .Override(34, "p")
                .Override(35, "y")
                .Override(36, "f")
                .Override(37, "g")
                .Override(38, "c")
                .Override(39, "r")
                .Override(40, "l")
                .Override(41, "/")
                .Override(43, "a")
                .Override(44, "o")
                .Override(45, "e")
                .Override(46, "u")
                .Override(47, "i")
                .Override(52, "d")
                .Override(53, "h")
                .Override(54, "t")
                .Override(55, "n")
                .Override(56, "s")
                .Override(57, "\\")
                .Override(59, ";")
                .Override(60, "q")
                .Override(61, "j")
                .Override(62, "k")
                .Override(63, "x")
                .Override(70, "b")
                .Override(71, "m")
                .Override(72, "w")
                .Override(73, "v")
                .Override(74, "z")
                .Build();

            var bottomKeys = ApplyKeypadOverlay(LayerBuilder.StartFrom(topKeys)).Build();

            var layers = new[]
            {
                new LayerGeometry("Dvorak-top", 0, topKeys),
                new LayerGeometry("Dvorak-keypad", 1, bottomKeys)
            };

            return new DeviceGeometry(LayoutVariant.Dvorak, layers);
        }

        private static IReadOnlyList<KeyPosition> BuildQwertyTopKeys()
        {
            return LayerBuilder.Start()
                .Key("escape")
                .Keys("F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12")
                .Keys("prtscr", "scroll", "pause")
                .Locked(string.Empty)
                .Locked(string.Empty)
                .Key("=")
                .Keys("1", "2", "3", "4", "5", "6", "7", "8", "9", "0")
                .Key("hyphen")
                .Key("tab")
                .Keys("q", "w", "e", "r", "t", "y", "u", "i", "o", "p")
                .Key("\\")
                .Key("caps")
                .Keys("a", "s", "d", "f", "g")
                .Modifier("lctrl")
                .Modifier("lalt")
                .Modifier("rwin")
                .Modifier("rctrl")
                .Keys("h", "j", "k", "l", ";", "'")
                .Modifier("lshift")
                .Keys("z", "x", "c", "v", "b")
                .Keys("bspace", "delete", "home", "pup", "enter", "space")
                .Keys("n", "m", ",", ".", "/")
                .Modifier("rshift")
                .Keys("`", "intl-\\", "left", "right", "end", "pdown", "up", "down", "obrack", "cbrack")
                .HostDependentKey("lp-tab", "tab")
                .HostDependentKey("mp-kpshf", "kpshft")
                .HostDependentKey("rp-kpent", "kpenter")
                .Build();
        }

        private static LayerBuilder ApplyKeypadOverlay(LayerBuilder builder)
        {
            return builder
                .OverrideModifier(1, "lwin")
                .OverrideModifier(2, "ralt")
                .Override(3, "menu")
                .Override(4, "play")
                .Override(5, "prev")
                .Override(6, "next")
                .Override(7, "calc")
                .Override(8, "kpshft")
                .Override(13, "mute")
                .Override(14, "vol-")
                .Override(15, "vol+")
                .Override(25, "numlk")
                .Override(26, "kp=")
                .Override(27, "kpdiv")
                .Override(28, "kpmult")
                .Override(37, "kp7")
                .Override(38, "kp8")
                .Override(39, "kp9")
                .Override(40, "kpmin")
                .Override(53, "kp4")
                .Override(54, "kp5")
                .Override(55, "kp6")
                .Override(56, "kpplus")
                .Override(69, "kp0")
                .Override(71, "kp1")
                .Override(72, "kp2")
                .Override(73, "kp3")
                .Override(74, "kpenter1")
                .Override(77, "insert")
                .Override(84, "kp.")
                .Override(85, "kpenter2")
                .Override(86, "lp-tab")
                .Override(87, "mp-kpshf")
                .Override(88, "rp-kpent");
        }
    }
}
