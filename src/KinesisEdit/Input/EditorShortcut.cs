namespace KinesisEdit.Input
{
    /// <summary>
    /// One intent of the editor's keyboard grammar (docs/design/mockups.md, <c>2b</c> §"Keyboard
    /// grammar"). It names <em>what the user asked for</em>, never which key was pressed: the
    /// platform difference between ⌘ and Ctrl is resolved in <see cref="EditorShortcuts.Map"/>, so
    /// everything downstream of it is platform-independent.
    /// </summary>
    public enum EditorShortcut
    {
        /// <summary>The key event is not part of the grammar; whoever has focus keeps it.</summary>
        None = 0,

        /// <summary>↑ — move the key selection to the physical neighbour above.</summary>
        MoveUp = 1,

        /// <summary>↓ — move the key selection to the physical neighbour below.</summary>
        MoveDown = 2,

        /// <summary>← — move the key selection to the physical neighbour on the left.</summary>
        MoveLeft = 3,

        /// <summary>→ — move the key selection to the physical neighbour on the right.</summary>
        MoveRight = 4,

        /// <summary>⌥1 — show the first layer.</summary>
        SelectLayer1 = 5,

        /// <summary>⌥2 — show the second layer.</summary>
        SelectLayer2 = 6,

        /// <summary>⌥3 — show the third layer.</summary>
        SelectLayer3 = 7,

        /// <summary>⌥4 — show the fourth layer.</summary>
        SelectLayer4 = 8,

        /// <summary>⌥5 — show the fifth layer.</summary>
        SelectLayer5 = 9,

        /// <summary>⌘F — open the Search Keys panel and put the caret in its field.</summary>
        FocusSearch = 10,

        /// <summary>⌘S — write the profile back to the v-Drive.</summary>
        Save = 11,

        /// <summary>⌘W — leave the editor for the dashboard.</summary>
        GoHome = 12
    }
}
