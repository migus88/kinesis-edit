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
            KeyCluster cluster = KeyCluster.Main)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfNegative(x);
            ArgumentOutOfRangeException.ThrowIfNegative(y);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

            Index = index;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Cluster = cluster;
        }
    }
}
