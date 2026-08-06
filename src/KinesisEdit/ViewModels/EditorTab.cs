namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's top-level sections (specs/10-apps-and-ui.md: every editor window carries the
    /// keyboard picture plus a macro panel, a lighting panel where the device has one, and a
    /// settings section). Which of them a given board actually gets is
    /// <see cref="EditorTabViewModel.CreateAll"/>'s answer, not this enum's.
    /// </summary>
    public enum EditorTab
    {
        /// <summary>The keyboard picture and the remap workflow.</summary>
        Keys = 1,

        /// <summary>Macro recording and the macro list.</summary>
        Macros = 2,

        /// <summary>Per-key and effect lighting.</summary>
        Lighting = 3,

        /// <summary>Keyboard settings.</summary>
        Settings = 4
    }
}
