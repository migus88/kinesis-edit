namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Builds the Freestyle Edge RGB board picture: the 95 positions of
    /// specs/05-key-model.md §4.2 placed in key units. The device is a split keyboard
    /// (specs/02-devices.md), so the board is authored as three blocks laid out left to
    /// right — the left-edge hotkey column (hk0 plus the 2x5 grid hk1-hk10), the left
    /// typing half, then the right typing half after a <see cref="SplitGap"/>-wide gap.
    /// <para>
    /// Rows follow the index grouping of spec 05 §4.2/§4.1: the function, number, tab,
    /// caps, shift and bottom rows each run left-half-then-right-half, with the hotkey
    /// pairs sitting between them because they are physically to the left of each row.
    /// Cap widths are the usual ANSI spans (1.5U Tab, 1.75U Caps, 2.25U left Shift,
    /// 2.25U Enter, 2U Backspace); the right half carries the standard row stagger.
    /// Placement is legible-and-complete rather than millimetre-accurate.
    /// </para>
    /// <para>
    /// <b>Two sections, three blocks.</b> Mockup 1e draws the hotkey column and the left
    /// typing half inside <i>one</i> bordered box, and the right half inside another — so
    /// the first two blocks share section 0 and the right half is section 1. The
    /// <see cref="SplitGap"/> stays authored where it is: a renderer replaces it with the
    /// design's 26 px gutter, but <see cref="KeyAdjacency"/> still needs the gap to exist
    /// in the coordinate space so an arrow can cross it.
    /// </para>
    /// <para>
    /// <b>Legends are the silkscreen, not the action.</b> They are authored only where the
    /// print on the cap differs from what <c>KeyCaption</c> already produces — so the
    /// F-keys carry only their device-hotkey secondaries ("F1" is the caption already), and
    /// Esc, Backspace and Home carry nothing at all. See the substitution note on
    /// <see cref="HotkeySleep"/>.
    /// </para>
    /// </summary>
    internal static class FreestyleEdgeRgbVisual
    {
        /// <summary>Gap in key units between the two halves of the split board.</summary>
        private const double SplitGap = 1.0;

        private const double MacroColumnX = 0.0;

        /// <summary>Two 1U columns wide: hk1-hk10 sit in a 2x5 grid, hk0 spans both.</summary>
        private const double MacroColumnWidth = 2.0;

        private const double LeftHalfX = MacroColumnX + MacroColumnWidth;

        /// <summary>Widest left-half row: the 2.25U left Shift plus five 1U alphas.</summary>
        private const double LeftHalfWidth = 7.25;

        private const double RightHalfX = LeftHalfX + LeftHalfWidth + SplitGap;

        private const double RightFunctionRowX = RightHalfX + 0.5;
        private const double RightNumberRowX = RightHalfX + 0.5;
        private const double RightTabRowX = RightHalfX;
        private const double RightCapsRowX = RightHalfX + 0.25;
        private const double RightShiftRowX = RightHalfX + 0.75;
        private const double RightBottomRowX = RightHalfX + 0.5;

        private const double FunctionRowY = 0.0;
        private const double NumberRowY = 1.0;
        private const double TabRowY = 2.0;
        private const double CapsRowY = 3.0;
        private const double ShiftRowY = 4.0;
        private const double BottomRowY = 5.0;

        private const double TabWidth = 1.5;
        private const double CapsWidth = 1.75;
        private const double LeftShiftWidth = 2.25;
        private const double RightShiftWidth = 1.75;
        private const double BackspaceWidth = 2.0;
        private const double BackslashWidth = 1.5;
        private const double EnterWidth = 2.25;
        private const double ModifierWidth = 1.25;
        private const double LeftSpaceWidth = 3.25;
        private const double RightSpaceWidth = 3.5;

        /// <summary>The hotkey column and the left typing half: one bordered panel (mockup 1e).</summary>
        private const int LeftSection = 0;

        /// <summary>The right typing half: the second panel, across the split gutter.</summary>
        private const int RightSection = 1;

        /// <summary>
        /// hk0's legend. Mockup 1e prints a moon (U+263E) here, and neither embedded IBM
        /// Plex family carries that codepoint — the same finding that turned mockup 1e's
        /// search glyph into drawn geometry (docs/app/design-system.md). A keycap legend is
        /// domain text rather than an icon, so it cannot become geometry; the covered
        /// substitute is the position's own name, "hotkey 0" (spec 05 §3.11), which invents
        /// no device function the spec does not state.
        /// </summary>
        private const string HotkeySleep = "0";

        /// <summary>
        /// hk10's legend. Mockup 1e prints a sun (U+263C), which the embedded families do
        /// not carry either. Spec 05 §3.11 names the fallback itself for exactly this key:
        /// "☀ (9728) or <c>LED</c> fallback".
        /// </summary>
        private const string HotkeyLed = "LED";

        public static KeyboardVisual Create()
        {
            var builder = new KeyVisualBuilder();

            AddHotkeyColumn(builder);
            AddLeftHalf(builder);
            AddRightHalf(builder);

            return new KeyboardVisual(LayoutVariant.Qwerty, builder.Build());
        }

        /// <summary>
        /// hk0 at the top of the far-left column (the RGB's extra key, index 0), then
        /// hk1-hk10 in a 2x5 grid, one pair per typing row. The column is the indicator row
        /// mockup 1e reads out left to right, so every cap carries a legend: the caption
        /// falls back to the raw token here (hk0-hk8 are physically unlabelled, spec 05
        /// §3.11), which is honest but not what is printed on the board.
        /// </summary>
        private static void AddHotkeyColumn(KeyVisualBuilder builder)
        {
            builder
                .Section(LeftSection)
                .Row(MacroColumnX, FunctionRowY, KeyCluster.Function).Key(0, MacroColumnWidth, HotkeySleep)
                .Row(MacroColumnX, NumberRowY, KeyCluster.Function).Key(17, "1").Key(18, "2")
                .Row(MacroColumnX, TabRowY, KeyCluster.Function).Key(34, "3").Key(35, "4")
                .Row(MacroColumnX, CapsRowY, KeyCluster.Function).Key(51, "5").Key(52, "6")
                .Row(MacroColumnX, ShiftRowY, KeyCluster.Function).Key(67, "7").Key(68, "8")
                .Row(MacroColumnX, BottomRowY, KeyCluster.Function).Key(83, "Fn").Key(84, HotkeyLed);
        }

        /// <summary>
        /// Left module: esc + F1-F6, ` + 1-6, Tab + qwert, Caps + asdfg, Shift + zxcvb,
        /// and the left modifier row ending in the left space bar. The F-row's secondaries
        /// are the media actions of the Fn layer (spec 05 §4.2's bottom layer), which the
        /// board prints on the front of each cap.
        /// </summary>
        private static void AddLeftHalf(KeyVisualBuilder builder)
        {
            builder
                .Section(LeftSection)
                .Row(LeftHalfX, FunctionRowY)
                    .Key(1)
                    .RangeWithSecondaries(2, "mute", "vol−", "vol+", "play", "prev", "next")
                .Row(LeftHalfX, NumberRowY)
                    .Key(19, "`", "~")
                    .Key(20, "1", "!")
                    .Key(21, "2", "@")
                    .Key(22, "3", "#")
                    .Key(23, "4", "$")
                    .Key(24, "5", "%")
                    .Key(25, "6", "^")
                .Row(LeftHalfX, TabRowY).Key(36, TabWidth).Range(37, 41)
                .Row(LeftHalfX, CapsRowY).Key(53, CapsWidth).Range(54, 58)
                .Row(LeftHalfX, ShiftRowY).Key(69, LeftShiftWidth).Range(70, 74)
                .Row(LeftHalfX, BottomRowY)
                    .Key(85, ModifierWidth)
                    .Key(86, ModifierWidth)
                    .Key(87, ModifierWidth)
                    .Key(88, LeftSpaceWidth);
        }

        /// <summary>
        /// Right module: F7-F12 + Print/Pause/Del, 7-0 + navigation, yuiop + brackets,
        /// hjkl + Enter, nm + right Shift and the arrow keys. F7-F12's secondaries are the
        /// device hotkeys printed on the front of those caps (mockup 1e); every
        /// shifted-pair key splits its caption into the two halves the cap prints, the same
        /// way the number rows do.
        /// </summary>
        private static void AddRightHalf(KeyVisualBuilder builder)
        {
            builder
                .Section(RightSection)
                .Row(RightFunctionRowX, FunctionRowY)
                    .RangeWithSecondaries(8, "status", "vdrv", "speed", "nkro", "game", "reset")
                    .Key(14, "Prt sc")
                    .Key(15, "Pause", "insert")
                    .Key(16, "Del", "scr lk")
                .Row(RightNumberRowX, NumberRowY)
                    .Key(26, "7", "&")
                    .Key(27, "8", "*")
                    .Key(28, "9", "(")
                    .Key(29, "0", ")")
                    .Key(30, "-", "_")
                    .Key(31, "=", "+")
                    .Key(32, BackspaceWidth)
                    .Key(33)
                .Row(RightTabRowX, TabRowY)
                    .Range(42, 46)
                    .Key(47, "[", "{")
                    .Key(48, "]", "}")
                    .Key(49, BackslashWidth, "\\", "|")
                    .Key(50)
                .Row(RightCapsRowX, CapsRowY)
                    .Range(59, 62)
                    .Key(63, ";", ":")
                    .Key(64, "'", "\"")
                    .Key(65, EnterWidth)
                    .Key(66)
                .Row(RightShiftRowX, ShiftRowY)
                    .Range(75, 76)
                    .Key(77, ",", "<")
                    .Key(78, ".", ">")
                    .Key(79, "/", "?")
                    .Key(80, RightShiftWidth)
                    .Keys(81, 82)
                .Row(RightBottomRowX, BottomRowY)
                    .Key(89, RightSpaceWidth)
                    .Key(90, ModifierWidth)
                    .Key(91, ModifierWidth)
                    .Range(92, 94);
        }
    }
}
