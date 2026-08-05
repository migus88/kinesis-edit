namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// What <see cref="KeyboardKey.CopyFrom"/> transfers between two key positions: key data,
    /// macros, or both (specs/05-key-model.md §1.3, "Copying between key positions can transfer
    /// key data only, macros only, or both").
    /// </summary>
    [Flags]
    public enum KeyCopyScopes
    {
        /// <summary>Copy nothing.</summary>
        None = 0,

        /// <summary>Copy the remap, the tap-and-hold assignment, and the multi-modifier code.</summary>
        KeyData = 1 << 0,

        /// <summary>Copy the five macro slots and the active-slot selection.</summary>
        Macros = 1 << 1,

        /// <summary>Copy both key data and macros.</summary>
        All = KeyData | Macros
    }
}
