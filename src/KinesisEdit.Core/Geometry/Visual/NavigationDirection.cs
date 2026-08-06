namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Direction of a spatial move across a board picture, as an arrow key expresses it.
    /// Resolved against the authored key rectangles by <see cref="KeyAdjacency"/>; it names a
    /// physical direction on the board, never a row/column step.
    /// </summary>
    public enum NavigationDirection
    {
        /// <summary>No direction. Never a valid argument to <see cref="KeyAdjacency.Next"/>.</summary>
        None = 0,

        /// <summary>Towards the top edge of the board (decreasing Y).</summary>
        Up = 1,

        /// <summary>Towards the bottom edge of the board (increasing Y).</summary>
        Down = 2,

        /// <summary>Towards the left edge of the board (decreasing X).</summary>
        Left = 3,

        /// <summary>Towards the right edge of the board (increasing X).</summary>
        Right = 4
    }
}
