namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Direction of a macro keystroke (specs/05-key-model.md §1.1 <c>UpDown</c>): an explicit
    /// key-down or key-up event instead of a press+release. Serialized as the "-" (down) and
    /// "+" (up) token prefixes of specs/06-macros.md §2.2 and §3.
    /// </summary>
    public enum KeyDirection
    {
        /// <summary>No explicit direction — the keystroke is a press+release, written <c>{token}</c>.</summary>
        None = 0,

        /// <summary>Explicit key-down, written <c>{-token}</c> (specs/06-macros.md §2.2).</summary>
        Down = 1,

        /// <summary>Explicit key-up, written <c>{+token}</c> (specs/06-macros.md §2.2).</summary>
        Up = 2
    }
}
