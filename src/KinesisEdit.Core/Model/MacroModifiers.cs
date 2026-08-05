namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The modifiers held while a macro keystroke is struck (specs/05-key-model.md §5.1 and
    /// specs/06-macros.md §1). The legacy model stores them as a comma-separated string of
    /// two-character codes; this flags enum is the in-memory form and
    /// <see cref="MacroModifierCodes"/> round-trips it to and from those codes.
    /// </summary>
    [Flags]
    public enum MacroModifiers
    {
        /// <summary>No modifier is held.</summary>
        None = 0,

        /// <summary>Generic Shift, code <c>"S "</c> (note the trailing space, §5.1).</summary>
        Shift = 1 << 0,

        /// <summary>Left Shift, code <c>"LS"</c>.</summary>
        LeftShift = 1 << 1,

        /// <summary>Right Shift, code <c>"RS"</c>.</summary>
        RightShift = 1 << 2,

        /// <summary>Generic Ctrl, code <c>"C "</c>.</summary>
        Control = 1 << 3,

        /// <summary>Left Ctrl, code <c>"LC"</c>.</summary>
        LeftControl = 1 << 4,

        /// <summary>Right Ctrl, code <c>"RC"</c>.</summary>
        RightControl = 1 << 5,

        /// <summary>Generic Alt, code <c>"A "</c>.</summary>
        Alt = 1 << 6,

        /// <summary>Left Alt, code <c>"LA"</c>.</summary>
        LeftAlt = 1 << 7,

        /// <summary>Right Alt, code <c>"RA"</c>.</summary>
        RightAlt = 1 << 8,

        /// <summary>Generic Win, code <c>"W "</c>; maps back to the Left Win key (§5.1).</summary>
        Win = 1 << 9,

        /// <summary>Left Win, code <c>"LW"</c>.</summary>
        LeftWin = 1 << 10,

        /// <summary>Right Win, code <c>"RW"</c>.</summary>
        RightWin = 1 << 11
    }
}
