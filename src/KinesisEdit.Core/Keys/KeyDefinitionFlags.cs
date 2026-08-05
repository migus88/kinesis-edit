namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Behavior flags a key-table entry carries, per specs/05-key-model.md §1.1 and the
    /// notes in the §3 sub-tables.
    /// </summary>
    [Flags]
    public enum KeyDefinitionFlags
    {
        None = 0,

        /// <summary>
        /// Hidden from the key-search UI (the legacy <c>SKIP_SEARCH</c> sentinel, §1.1);
        /// applies to duplicate/internal entries, all §3.12 pseudo-keys, and §3.13 edge zones.
        /// </summary>
        HiddenFromSearch = 1 << 0,

        /// <summary>
        /// The entry has no file token in any dialect and can never be written to a layout
        /// file (e.g. the Program button 10013 and the layer key 10014).
        /// </summary>
        NotWritable = 1 << 1,

        /// <summary>
        /// Windows renders the caption through the OS keyboard-layout translation
        /// (<c>ToUnicode</c>, §1.1/§6); set for letters, digits, and layout-dependent punctuation.
        /// </summary>
        ConvertToUnicode = 1 << 2,

        /// <summary>
        /// The shifted value is shown/used while Shift is an active modifier (§1.1).
        /// </summary>
        ShowShiftedValue = 1 << 3,

        /// <summary>
        /// Macro output writes the key once instead of separate press/release events
        /// (legacy <c>WriteDownUp = False</c>, §1.1); set for speed/delay pseudo-keys (§3.12).
        /// </summary>
        SingleEvent = 1 << 4
    }
}
