namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's top-level sections (specs/10-apps-and-ui.md: every editor window carries the
    /// keyboard picture plus a lighting panel where the device has one and a settings section).
    /// Which of them a given board actually gets is <see cref="EditorTabViewModel.CreateAll"/>'s
    /// answer, not this enum's.
    /// <para>
    /// <b>2 is a hole, deliberately.</b> It was <c>Macros</c> — a whole section that listed the
    /// profile's macros — until issue #140 removed it: a macro is made and changed on the key
    /// inspector's Macro panel, beside the board, and a second surface for the same thing is the
    /// defect that removal was about. The remaining members keep their numbers because a renumber
    /// would move values for no gain.
    /// </para>
    /// </summary>
    public enum EditorTab
    {
        /// <summary>The keyboard picture, the remap workflow and the key inspector rail.</summary>
        Keys = 1,

        /// <summary>Per-key and effect lighting.</summary>
        Lighting = 3,

        /// <summary>Keyboard settings.</summary>
        Settings = 4
    }
}
