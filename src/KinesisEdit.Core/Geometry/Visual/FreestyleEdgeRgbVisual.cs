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
        /// hk1-hk10 in a 2x5 grid, one pair per typing row.
        /// </summary>
        private static void AddHotkeyColumn(KeyVisualBuilder builder)
        {
            builder
                .Row(MacroColumnX, FunctionRowY, KeyCluster.Function).Key(0, MacroColumnWidth)
                .Row(MacroColumnX, NumberRowY, KeyCluster.Function).Keys(17, 18)
                .Row(MacroColumnX, TabRowY, KeyCluster.Function).Keys(34, 35)
                .Row(MacroColumnX, CapsRowY, KeyCluster.Function).Keys(51, 52)
                .Row(MacroColumnX, ShiftRowY, KeyCluster.Function).Keys(67, 68)
                .Row(MacroColumnX, BottomRowY, KeyCluster.Function).Keys(83, 84);
        }

        /// <summary>
        /// Left module: esc + F1-F6, ` + 1-6, Tab + qwert, Caps + asdfg, Shift + zxcvb,
        /// and the left modifier row ending in the left space bar.
        /// </summary>
        private static void AddLeftHalf(KeyVisualBuilder builder)
        {
            builder
                .Row(LeftHalfX, FunctionRowY).Range(1, 7)
                .Row(LeftHalfX, NumberRowY).Range(19, 25)
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
        /// hjkl + Enter, nm + right Shift and the arrow keys.
        /// </summary>
        private static void AddRightHalf(KeyVisualBuilder builder)
        {
            builder
                .Row(RightFunctionRowX, FunctionRowY).Range(8, 16)
                .Row(RightNumberRowX, NumberRowY).Range(26, 31).Key(32, BackspaceWidth).Key(33)
                .Row(RightTabRowX, TabRowY).Range(42, 48).Key(49, BackslashWidth).Key(50)
                .Row(RightCapsRowX, CapsRowY).Range(59, 64).Key(65, EnterWidth).Key(66)
                .Row(RightShiftRowX, ShiftRowY).Range(75, 79).Key(80, RightShiftWidth).Keys(81, 82)
                .Row(RightBottomRowX, BottomRowY)
                    .Key(89, RightSpaceWidth)
                    .Key(90, ModifierWidth)
                    .Key(91, ModifierWidth)
                    .Range(92, 94);
        }
    }
}
