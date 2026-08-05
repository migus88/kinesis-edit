namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// An effect direction, written as <c>[dirdown]</c> / <c>[dirleft]</c> / <c>[dirup]</c> /
    /// <c>[dirright]</c> (specs/07-lighting.md §2.1). The default is left; invalid values fall
    /// back to left subject to the per-effect validity rules of §2.4. For Rebound, left means
    /// horizontal and up means vertical (§3).
    /// </summary>
    public enum LightingDirection
    {
        /// <summary>No direction; never written — effect lines always carry a concrete direction.</summary>
        None = 0,

        /// <summary>Downward, token <c>[dirdown]</c>.</summary>
        Down = 1,

        /// <summary>Leftward, token <c>[dirleft]</c>; the default, and Rebound's "horizontal" (§3).</summary>
        Left = 2,

        /// <summary>Upward, token <c>[dirup]</c>; Rebound's "vertical" (§3).</summary>
        Up = 3,

        /// <summary>Rightward, token <c>[dirright]</c>.</summary>
        Right = 4
    }
}
