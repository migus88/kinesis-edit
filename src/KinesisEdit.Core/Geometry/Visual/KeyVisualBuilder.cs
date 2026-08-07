namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Mutable helper for authoring a board row by row: <see cref="Row"/> parks a cursor
    /// at a row origin, then every <see cref="Key(int, string?, string?)"/> /
    /// <see cref="Keys"/> / <see cref="Range"/> call drops a cap there and walks the cursor
    /// right by its width. This keeps the per-device visual files declarative — the same
    /// role <see cref="LayerBuilder"/> plays for tokens in the logical geometry.
    /// <para>
    /// Two pieces of cursor state outlive a row. <see cref="Section"/> is sticky until it is
    /// set again, because a board panel spans many rows and repeating it on each would bury
    /// the placements it is meant to group. The legends, by contrast, belong to one cap and
    /// are passed per key: they are the physical silkscreen, authored only where the print
    /// differs from what the key's caption already says.
    /// </para>
    /// </summary>
    internal sealed class KeyVisualBuilder
    {
        private readonly List<KeyVisual> _keys = new();

        private double _cursorX;
        private double _rowY;
        private double _rowHeight = 1.0;
        private KeyCluster _rowCluster = KeyCluster.Main;
        private int _section;

        /// <summary>
        /// Directs every following row into board panel <paramref name="index"/> until the
        /// next call. Sections must be authored densely from 0.
        /// </summary>
        public KeyVisualBuilder Section(int index)
        {
            _section = index;

            return this;
        }

        /// <summary>Starts a row at the given board-absolute origin, in key units.</summary>
        public KeyVisualBuilder Row(double x, double y, KeyCluster cluster = KeyCluster.Main, double height = 1.0)
        {
            _cursorX = x;
            _rowY = y;
            _rowCluster = cluster;
            _rowHeight = height;

            return this;
        }

        /// <summary>Appends a 1U-wide cap at the cursor, carrying the given silkscreen legends.</summary>
        public KeyVisualBuilder Key(int index, string? legend = null, string? secondaryLegend = null)
        {
            return Key(index, 1.0, legend, secondaryLegend);
        }

        /// <summary>Appends a cap of the given width, in key units, at the cursor.</summary>
        public KeyVisualBuilder Key(int index, double width, string? legend = null, string? secondaryLegend = null)
        {
            _keys.Add(new KeyVisual(
                index,
                _cursorX,
                _rowY,
                width,
                _rowHeight,
                _rowCluster,
                _section,
                legend,
                secondaryLegend));

            _cursorX += width;

            return this;
        }

        /// <summary>Appends 1U caps for the listed indices, left to right, with no legends.</summary>
        public KeyVisualBuilder Keys(params int[] indices)
        {
            foreach (var index in indices)
            {
                Key(index);
            }

            return this;
        }

        /// <summary>Appends 1U caps for the inclusive index range, left to right, with no legends.</summary>
        public KeyVisualBuilder Range(int firstIndex, int lastIndex)
        {
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                Key(index);
            }

            return this;
        }

        /// <summary>
        /// Appends 1U caps for the inclusive index range, taking each cap's secondary
        /// legend from <paramref name="secondaryLegends"/> in order — the shape of an
        /// F-row, whose primary legends are exactly what the caption already says and whose
        /// device hotkeys are printed underneath.
        /// </summary>
        public KeyVisualBuilder RangeWithSecondaries(int firstIndex, params string[] secondaryLegends)
        {
            ArgumentNullException.ThrowIfNull(secondaryLegends);

            for (var offset = 0; offset < secondaryLegends.Length; offset++)
            {
                Key(firstIndex + offset, legend: null, secondaryLegend: secondaryLegends[offset]);
            }

            return this;
        }

        public IReadOnlyList<KeyVisual> Build()
        {
            return _keys.ToArray();
        }
    }
}
