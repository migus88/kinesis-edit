namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's top-level sections (specs/10-apps-and-ui.md: every editor window carries the
    /// keyboard picture plus a macro panel, a lighting panel where the device has one, and a
    /// settings section). Only <see cref="Keys"/> is implemented; the rest are placeholders for
    /// issues #15 and #16.
    /// </summary>
    public enum EditorTab
    {
        /// <summary>The keyboard picture and the remap workflow.</summary>
        Keys = 1,

        /// <summary>Macro recording and the macro list (issue #15).</summary>
        Macros = 2,

        /// <summary>Per-key and effect lighting (issue #16).</summary>
        Lighting = 3,

        /// <summary>Keyboard settings (issue #16).</summary>
        Settings = 4
    }
}
