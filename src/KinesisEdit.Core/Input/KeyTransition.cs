namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// The key-up / key-down transition a captured event carries. The legacy Windows hook
    /// decodes it from the message, the macOS form from the previewed event
    /// (specs/10-apps-and-ui.md, "Keyboard-input capture"); both directions matter, because a
    /// modifier tapped alone is only recognized on key-up (specs/12-savant-elite.md
    /// §"Programming a pedal" step 3).
    /// </summary>
    public enum KeyTransition
    {
        /// <summary>No transition — never produced by a real key event.</summary>
        None = 0,

        /// <summary>The key went down.</summary>
        Down = 1,

        /// <summary>The key came up.</summary>
        Up = 2,
    }
}
