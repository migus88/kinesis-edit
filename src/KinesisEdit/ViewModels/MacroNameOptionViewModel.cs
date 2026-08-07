using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the key inspector's macro-name dropdown (mockup <c>2i</c>: <c>Sign-off block ▾</c>)
    /// — a logical macro of the profile, offered to be put on the selected key.
    /// <para>
    /// <b>It is a projection of a library entry, not a copy of one.</b>
    /// <see cref="MacroLibrary.Entries"/> is a snapshot rebuilt after every mutation, so the entry
    /// held here is only valid until the next one; the panel rebuilds its options from the fresh
    /// snapshot on every refresh rather than mutating these rows.
    /// </para>
    /// <para>
    /// The <b>empty</b> option — <see cref="IsNone"/> — is how the dropdown says "this key has no
    /// macro yet". It carries no entry, and picking it is not an action: the panel refuses it, so
    /// the dropdown can never be the thing that deletes a macro. Deleting is the Macros tab's.
    /// </para>
    /// </summary>
    public sealed class MacroNameOptionViewModel : ViewModelBase
    {
        /// <summary>What the dropdown reads while the selected key carries no macro at all.</summary>
        public const string NoMacroCaption = "No macro on this key";

        /// <summary>The name to show — the entry's, which is unique inside the library.</summary>
        public string Caption { get; }

        /// <summary>
        /// The library entry this row offers, or null for <see cref="IsNone"/>. Valid only against
        /// the snapshot it was built from.
        /// </summary>
        public MacroLibraryEntry? Entry { get; }

        /// <summary>Whether this is the "no macro" placeholder rather than a real macro.</summary>
        public bool IsNone => Entry is null;

        /// <summary>How many keys and layers fire this macro today (the "Also on" count).</summary>
        public int SiteCount => Entry?.SiteCount ?? 0;

        /// <summary>Creates the placeholder row.</summary>
        public MacroNameOptionViewModel()
        {
            Caption = NoMacroCaption;
        }

        /// <summary>Creates a row for one logical macro of the profile.</summary>
        public MacroNameOptionViewModel(MacroLibraryEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Caption = entry.Name;
        }
    }
}
