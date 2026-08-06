namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// One drawn panel of a board: the keys that share a <see cref="KeyVisual.Section"/>,
    /// plus the bounding box they occupy. A split keyboard is two of these (mockup 1e draws
    /// the hotkey column and the left typing half inside one bordered box and the right half
    /// inside another); a one-piece board is a single section.
    /// <para>
    /// Every value is in <b>key units</b> and stays <b>board-absolute</b>:
    /// <see cref="X"/>/<see cref="Y"/> are the section's own origin inside the whole board, so
    /// a renderer that lays sections out side by side subtracts them, and a renderer that
    /// draws the board in one piece ignores them. Nothing here re-bases a coordinate —
    /// <see cref="KeyAdjacency"/> needs the one continuous space the keys were authored in.
    /// </para>
    /// </summary>
    /// <param name="Index">0-based panel ordinal; dense across a board.</param>
    /// <param name="Keys">The section's keys, in the board's authoring order.</param>
    /// <param name="X">Smallest <see cref="KeyVisual.X"/> in the section.</param>
    /// <param name="Y">Smallest <see cref="KeyVisual.Y"/> in the section.</param>
    /// <param name="Width">Largest <see cref="KeyVisual.Right"/> minus <paramref name="X"/>.</param>
    /// <param name="Height">Largest <see cref="KeyVisual.Bottom"/> minus <paramref name="Y"/>.</param>
    public sealed record KeyboardSection(
        int Index,
        IReadOnlyList<KeyVisual> Keys,
        double X,
        double Y,
        double Width,
        double Height)
    {
        /// <summary>Right edge in key units, board-absolute: <see cref="X"/> + <see cref="Width"/>.</summary>
        public double Right => X + Width;

        /// <summary>Bottom edge in key units, board-absolute: <see cref="Y"/> + <see cref="Height"/>.</summary>
        public double Bottom => Y + Height;
    }
}
