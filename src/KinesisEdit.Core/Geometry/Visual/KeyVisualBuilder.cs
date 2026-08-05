namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Mutable helper for authoring a board row by row: <see cref="Row"/> parks a cursor
    /// at a row origin, then every <see cref="Key(int)"/> / <see cref="Keys"/> /
    /// <see cref="Range"/> call drops a cap there and walks the cursor right by its
    /// width. This keeps the per-device visual files declarative — the same role
    /// <see cref="LayerBuilder"/> plays for tokens in the logical geometry.
    /// </summary>
    internal sealed class KeyVisualBuilder
    {
        private readonly List<KeyVisual> _keys = new();

        private double _cursorX;
        private double _rowY;
        private double _rowHeight = 1.0;
        private KeyCluster _rowCluster = KeyCluster.Main;

        /// <summary>Starts a row at the given board-absolute origin, in key units.</summary>
        public KeyVisualBuilder Row(double x, double y, KeyCluster cluster = KeyCluster.Main, double height = 1.0)
        {
            _cursorX = x;
            _rowY = y;
            _rowCluster = cluster;
            _rowHeight = height;

            return this;
        }

        /// <summary>Appends a 1U-wide cap at the cursor.</summary>
        public KeyVisualBuilder Key(int index)
        {
            return Key(index, 1.0);
        }

        /// <summary>Appends a cap of the given width, in key units, at the cursor.</summary>
        public KeyVisualBuilder Key(int index, double width)
        {
            _keys.Add(new KeyVisual(index, _cursorX, _rowY, width, _rowHeight, _rowCluster));
            _cursorX += width;

            return this;
        }

        /// <summary>Appends 1U caps for the listed indices, left to right.</summary>
        public KeyVisualBuilder Keys(params int[] indices)
        {
            foreach (var index in indices)
            {
                Key(index);
            }

            return this;
        }

        /// <summary>Appends 1U caps for the inclusive index range, left to right.</summary>
        public KeyVisualBuilder Range(int firstIndex, int lastIndex)
        {
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                Key(index);
            }

            return this;
        }

        public IReadOnlyList<KeyVisual> Build()
        {
            return _keys.ToArray();
        }
    }
}
