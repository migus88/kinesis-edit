namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Builds the TKO geometry per specs/05-key-model.md §4.4: 63 key positions per
    /// layer, two layers ("Qwerty-top" 0, "Qwerty-keypad" 1), plus 33 edge-lighting
    /// zones per layer ("L1".."L9" at 0-8, "B1".."B15" at 9-23, "R1".."R9" at 24-32,
    /// spec 05 §3.13). Index 60 ("fnshf") is remappable and macro-capable unlike the
    /// other modifier positions; index 61 ("ss") is the fully locked SmartSet key.
    /// Tokens are Gen1 file tokens, so the backtick position stores "tilde".
    /// </summary>
    internal static class TkoGeometry
    {
        public static DeviceGeometry Create()
        {
            var topKeys = BuildTopKeys();

            var bottomKeys = LayerBuilder.StartFrom(topKeys)
                .Override(0, "tilde")
                .Override(1, "F1")
                .Override(2, "F2")
                .Override(3, "F3")
                .Override(4, "F4")
                .Override(5, "F5")
                .Override(6, "F6")
                .Override(7, "F7")
                .Override(8, "F8")
                .Override(9, "F9")
                .Override(10, "F10")
                .Override(11, "F11")
                .Override(12, "F12")
                .Override(13, "del")
                .Override(15, "lmous")
                .Override(16, "play")
                .Override(17, "prev")
                .Override(18, "next")
                .Override(19, "LED")
                .Override(20, "ins")
                .Override(21, "calc")
                .Override(22, "up")
                .Override(23, "pause")
                .Override(24, "pup")
                .Override(25, "home")
                .Override(26, "prnt")
                .Override(29, "rmous")
                .Override(30, "mute")
                .Override(31, "vol-")
                .Override(32, "vol+")
                .Override(33, "menu")
                .Override(34, "scrlk")
                .Override(35, "lft")
                .Override(36, "dwn")
                .Override(37, "rght")
                .Override(38, "pdn")
                .Override(39, "end")
                .Build();

            var edgeZones = BuildEdgeZones();

            var layers = new[]
            {
                new LayerGeometry("Qwerty-top", 0, topKeys, edgeZones),
                new LayerGeometry("Qwerty-keypad", 1, bottomKeys, edgeZones)
            };

            return new DeviceGeometry(LayoutVariant.Qwerty, layers);
        }

        private static IReadOnlyList<KeyPosition> BuildTopKeys()
        {
            return LayerBuilder.Start()
                .Key("esc")
                .Keys("1", "2", "3", "4", "5", "6", "7", "8", "9", "0")
                .Keys("hyph", "=", "bspc")
                .Key("tab")
                .Keys("q", "w", "e", "r", "t", "y", "u", "i", "o", "p")
                .Keys("obrk", "cbrk", "\\")
                .Key("caps")
                .Keys("a", "s", "d", "f", "g", "h", "j", "k", "l")
                .Keys("colon", "apos", "ent")
                .Modifier("lshft")
                .Keys("z", "x", "c", "v", "b", "n", "m")
                .Keys("com", "per", "/")
                .Modifier("rshft")
                .Modifier("lctrl")
                .Modifier("lwin")
                .Modifier("lalt")
                .Keys("lspc", "mspc", "rspc")
                .Modifier("ralt")
                .Key("fnshf")
                .Locked("ss")
                .Modifier("rctrl")
                .Build();
        }

        private static IReadOnlyList<KeyPosition> BuildEdgeZones()
        {
            var builder = LayerBuilder.Start();

            for (var i = 1; i <= 9; i++)
            {
                builder.Key($"L{i}");
            }

            for (var i = 1; i <= 15; i++)
            {
                builder.Key($"B{i}");
            }

            for (var i = 1; i <= 9; i++)
            {
                builder.Key($"R{i}");
            }

            return builder.Build();
        }
    }
}
