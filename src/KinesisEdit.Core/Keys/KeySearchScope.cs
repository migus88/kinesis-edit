namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Where a searchable row lives on the board, derived from its <see cref="KeyTable"/> by
    /// <see cref="KeySearchCatalog.ScopeFor"/>. It answers the picker's scoped result rows —
    /// docs/design/mockups.md §1e draws "Keypad layer Esc" and "Device hotkey" beside the row.
    /// <para>
    /// The mockups show those rows under invented tokens (<c>[kp-esc]</c>, <c>[hk-esc]</c>). No
    /// such token exists: <c>kp-</c> is an Advantage2 layout-file layer marker, not a key token,
    /// and there is no <c>hk-</c> prefix in any dialect. The §3.6 keypad rows and the
    /// <c>hk0</c>..<c>hk10</c> rows of §3.11 are ordinary registry rows under their real tokens,
    /// so the scope is attached from the key table instead of read off a token prefix.
    /// </para>
    /// <para>
    /// Scope is orthogonal to <see cref="KeySearchEntry.IsAlias"/>. The §3.6/§3.7/§3.9
    /// keypad-layer duplicates are both scoped and aliases; a plain keypad key is scoped and
    /// canonical.
    /// </para>
    /// </summary>
    public enum KeySearchScope
    {
        /// <summary>An ordinary key with no scope of its own.</summary>
        None = 0,

        /// <summary>A keypad key — <see cref="KeyTable.KeypadKeys"/> (§3.6).</summary>
        Keypad = 1,

        /// <summary>
        /// One of the board's own hotkeys, <c>hk0</c>..<c>hk10</c> (§3.11).
        /// </summary>
        DeviceHotkey = 2,

        /// <summary>A profile selector, <c>pro0</c>..<c>pro9</c> (§3.11).</summary>
        Profile = 3
    }
}
