namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Placement of one physical key on a board, in <b>key units</b>: <c>1.0</c> is the
    /// width and height of a single 1U keycap, so a renderer multiplies by whatever
    /// pixel size it wants. Coordinates are board-absolute and address the key's
    /// top-left corner, with X growing right and Y growing down.
    /// <para>
    /// <see cref="Index"/> is the same ordinal as <see cref="KeyPosition.Index"/>: the
    /// visual carries no tokens, it only says where the logical position sits.
    /// </para>
    /// <para>
    /// <see cref="Section"/> says which board <i>panel</i> the key is drawn in — the two
    /// halves of a split board are two bordered boxes in the redesign (mockup 1e). It is a
    /// grouping only: coordinates stay board-absolute in one continuous space, because
    /// <see cref="KeyAdjacency"/> navigates across the whole board and the authored split
    /// gap is the space an arrow crosses.
    /// </para>
    /// <para>
    /// <see cref="Legend"/> / <see cref="SecondaryLegend"/> are the key's <i>physical
    /// silkscreen</i> — what is printed on the cap, not what the key does. They are
    /// authored only where the print differs from what the key's own caption already says,
    /// so null means "use the caption" rather than "this cap is blank".
    /// </para>
    /// </summary>
    public sealed record KeyVisual
    {
        /// <summary>Ordinal of the position, matching <see cref="KeyPosition.Index"/>.</summary>
        public int Index { get; }

        /// <summary>Left edge in key units, board-absolute.</summary>
        public double X { get; }

        /// <summary>Top edge in key units, board-absolute.</summary>
        public double Y { get; }

        /// <summary>Cap width in key units (<c>1.0</c> = 1U).</summary>
        public double Width { get; }

        /// <summary>Cap height in key units (<c>1.0</c> = 1U).</summary>
        public double Height { get; }

        /// <summary>Presentational grouping of the key.</summary>
        public KeyCluster Cluster { get; }

        /// <summary>
        /// Which board panel the key is drawn in, <c>0</c>-based and dense across a board
        /// (see <see cref="KeyboardVisual.Sections"/>). A one-piece board leaves every key
        /// at <c>0</c>.
        /// </summary>
        public int Section { get; }

        /// <summary>
        /// The cap's primary silkscreen legend (<c>"1"</c>, <c>"Fn"</c>, <c>"Prt sc"</c>),
        /// or null when the key's caption already says it.
        /// </summary>
        public string? Legend { get; }

        /// <summary>
        /// The cap's secondary silkscreen legend — the shifted character or the device
        /// hotkey printed under the main one (<c>"!"</c>, <c>"mute"</c>, <c>"scr lk"</c>),
        /// or null when the cap carries none.
        /// </summary>
        public string? SecondaryLegend { get; }

        /// <summary>Right edge in key units: <see cref="X"/> + <see cref="Width"/>.</summary>
        public double Right => X + Width;

        /// <summary>Bottom edge in key units: <see cref="Y"/> + <see cref="Height"/>.</summary>
        public double Bottom => Y + Height;

        public KeyVisual(
            int index,
            double x,
            double y,
            double width = 1.0,
            double height = 1.0,
            KeyCluster cluster = KeyCluster.Main,
            int section = 0,
            string? legend = null,
            string? secondaryLegend = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfNegative(x);
            ArgumentOutOfRangeException.ThrowIfNegative(y);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            ArgumentOutOfRangeException.ThrowIfNegative(section);

            Index = index;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Cluster = cluster;
            Section = section;
            Legend = legend;
            SecondaryLegend = secondaryLegend;
        }
    }
}
