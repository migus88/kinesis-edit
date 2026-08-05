namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Builds the Freestyle Edge, Freestyle Edge RGB, and Freestyle Pro geometries per
    /// specs/05-key-model.md §4.1-§4.3: 95 positions per layer, two layers each
    /// ("Qwerty-top" 0, "Qwerty-keypad" 1). The RGB and both keypad layers are spec'd
    /// as deltas against the Edge top layer and are built as overlays here. Tokens are
    /// Gen1 file tokens (spec 05 §3), so the backtick position stores "tilde".
    /// </summary>
    internal static class FreestyleGeometry
    {
        private const string TopLayerName = "Qwerty-top";
        private const string BottomLayerName = "Qwerty-keypad";

        public static DeviceGeometry CreateEdge()
        {
            var topKeys = BuildEdgeTopKeys();

            var bottomKeys = LayerBuilder.StartFrom(topKeys)
                .Override(1, "mute")
                .Override(2, "vol-")
                .Override(3, "vol+")
                .Override(4, "play")
                .Override(5, "prev")
                .Override(6, "next")
                .Override(15, "ins")
                .Build();

            return CreatePair(topKeys, bottomKeys);
        }

        public static DeviceGeometry CreateEdgeRgb()
        {
            var edgeTopKeys = BuildEdgeTopKeys();

            var topKeys = LayerBuilder.StartFrom(edgeTopKeys)
                .Override(0, "hk0")
                .Override(1, "esc")
                .Override(2, "F1")
                .Override(3, "F2")
                .Override(4, "F3")
                .Override(5, "F4")
                .Override(6, "F5")
                .Override(7, "F6")
                .Override(8, "F7")
                .Override(9, "F8")
                .Override(10, "F9")
                .Override(11, "F10")
                .Override(12, "F11")
                .Override(13, "F12")
                .Override(14, "prnt")
                .Override(15, "pause")
                .Override(16, "del")
                .Build();

            var bottomKeys = LayerBuilder.StartFrom(edgeTopKeys)
                .Override(0, "hk0")
                .Override(1, "esc")
                .Override(2, "mute")
                .Override(3, "vol-")
                .Override(4, "vol+")
                .Override(5, "play")
                .Override(6, "prev")
                .Override(7, "next")
                .Override(8, "F7")
                .Override(9, "F8")
                .Override(10, "F9")
                .Override(11, "F10")
                .Override(12, "F11")
                .Override(13, "F12")
                .Override(14, "prnt")
                .Override(15, "ins")
                .Override(16, "scrlk")
                .Build();

            return CreatePair(topKeys, bottomKeys);
        }

        public static DeviceGeometry CreatePro()
        {
            var topKeys = BuildEdgeTopKeys();

            var bottomKeys = LayerBuilder.StartFrom(topKeys)
                .Override(1, "mute")
                .Override(2, "vol-")
                .Override(3, "vol+")
                .Override(4, "play")
                .Override(5, "prev")
                .Override(6, "next")
                .Override(14, "numlk")
                .Override(15, "ins")
                .Override(26, "kp7")
                .Override(27, "kp8")
                .Override(28, "kp9")
                .Override(30, "kp*")
                .Override(43, "kp4")
                .Override(44, "kp5")
                .Override(45, "kp6")
                .Override(46, "kp-")
                .Override(60, "kp1")
                .Override(61, "kp2")
                .Override(62, "kp3")
                .Override(63, "kp+")
                .Override(65, "kpent")
                .Override(76, "kp0")
                .Override(78, "kp.")
                .Override(79, "kp/")
                .Build();

            return CreatePair(topKeys, bottomKeys);
        }

        private static DeviceGeometry CreatePair(IReadOnlyList<KeyPosition> topKeys, IReadOnlyList<KeyPosition> bottomKeys)
        {
            var layers = new[]
            {
                new LayerGeometry(TopLayerName, 0, topKeys),
                new LayerGeometry(BottomLayerName, 1, bottomKeys)
            };

            return new DeviceGeometry(LayoutVariant.Qwerty, layers);
        }

        private static IReadOnlyList<KeyPosition> BuildEdgeTopKeys()
        {
            return LayerBuilder.Start()
                .Key("esc")
                .Keys("F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12")
                .Keys("prnt", "scrlk", "pause", "del")
                .Keys("hk1", "hk2")
                .Key("tilde")
                .Keys("1", "2", "3", "4", "5", "6", "7", "8", "9", "0")
                .Keys("hyph", "=", "bspc", "home")
                .Keys("hk3", "hk4")
                .Key("tab")
                .Keys("q", "w", "e", "r", "t", "y", "u", "i", "o", "p")
                .Keys("obrk", "cbrk", "\\", "end")
                .Keys("hk5", "hk6")
                .Key("caps")
                .Keys("a", "s", "d", "f", "g", "h", "j", "k", "l")
                .Keys("colon", "apos", "ent", "pup")
                .Keys("hk7", "hk8")
                .Modifier("lshft")
                .Keys("z", "x", "c", "v", "b", "n", "m")
                .Keys("com", "per", "/")
                .Modifier("rshft")
                .Keys("up", "pdn")
                .Keys("hk9", "hk10")
                .Modifier("lctrl")
                .Modifier("lwin")
                .Modifier("lalt")
                .Keys("lspc", "rspc")
                .Modifier("ralt")
                .Modifier("rctrl")
                .Keys("lft", "dwn", "rght")
                .Build();
        }
    }
}
