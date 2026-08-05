namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Identifies which sub-table of specs/05-key-model.md §3 a <see cref="KeyDefinition"/>
    /// comes from. Values match the spec table numbers (§3.1 → <see cref="LettersAndDigits"/>,
    /// … §3.13 → <see cref="EdgeZones"/>).
    /// </summary>
    public enum KeyTable
    {
        None = 0,
        LettersAndDigits = 1,
        Punctuation = 2,
        Navigation = 3,
        FunctionKeys = 4,
        Modifiers = 5,
        KeypadKeys = 6,
        MediaKeys = 7,
        MouseActions = 8,
        SpecialActions = 9,
        LayerKeys = 10,
        ProfilesAndHotkeys = 11,
        MacroTiming = 12,
        EdgeZones = 13
    }
}
