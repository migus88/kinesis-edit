using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The five things the Macros tab has to ask the editor for. <see cref="MacroLibraryViewModel"/>
    /// is built over this rather than over <c>KeyboardEditorViewModel</c> itself, for the reason
    /// <see cref="KeyInspectorViewModel"/> is built over three passed-in commands: the editor is
    /// already the largest class in the app and docs/guides/Coding Conventions.md forbids making it
    /// circular with one of its own sections.
    ///
    /// <para><b>Every write goes back through the editor, deliberately.</b> The library edits
    /// (<c>Delete</c>, <c>Duplicate</c>) are Core's and the tab runs them on the editor's
    /// <b>one</b> <see cref="MacroLibrary"/> directly — but a rename, a dirty flag and the refresh
    /// funnel every mutation ends in are the editor's, and a second path to any of them would be a
    /// second thing to keep in step.</para>
    ///
    /// <para><b>The tab never records.</b> There is exactly one recording surface in this app — the
    /// key inspector rail — so an empty slot's <c>＋ Record a macro</c> is
    /// <see cref="EditMacroAt"/>: it goes to the board, selects the position and arms the rail's own
    /// Record.</para>
    /// </summary>
    public interface IMacroLibraryHost
    {
        /// <summary>
        /// The editor's <b>one</b> rename path (<c>KeyboardEditorViewModel.RenameMacro</c>): renames
        /// every site of <paramref name="entry"/>, marks the session dirty and re-runs the funnel.
        /// Returns the fresh entry — every library mutation rebuilds the snapshot — or null when the
        /// entry is stale or there is no library.
        /// </summary>
        MacroLibraryEntry? RenameMacro(MacroLibraryEntry entry, string? newName);

        /// <summary>
        /// Says that the tab wrote to the library some other way — a delete or a duplicate, which
        /// move names about without renaming anything. Marks the names dirty and runs the editor's
        /// refresh funnel, which rebuilds the snapshot, the counters, the advisories, the legend and
        /// the rail.
        /// </summary>
        void CommitMacroLibraryEdit();

        /// <summary>
        /// Runs the same funnel for a change that wrote <b>nothing the file carries</b> — which slot
        /// of a key is the active one is an in-memory field (05 §1.3, <c>ActiveMacroIndex</c>) and is
        /// never serialized. The session is deliberately <em>not</em> marked dirty by it: an
        /// unsaved-changes prompt for a choice no save could persist would be a lie.
        /// </summary>
        void RefreshMacroViews();

        /// <summary>
        /// Opens one macro position in the key inspector: goes to the Layout tab, puts the board's
        /// selection on <paramref name="keyIndex"/> of <paramref name="layerIndex"/>, points the
        /// rail at its Macro panel and — when <paramref name="startRecording"/> — arms Record.
        /// <paramref name="slot"/> is 1..5 on a slot device and 0 on a flat-list one.
        /// </summary>
        void EditMacroAt(int layerIndex, int keyIndex, int slot, bool startRecording);

        /// <summary>
        /// <c>Pick another key</c> (mockup <c>1i</c>): leaves for the Layout tab, which is where the
        /// board is. The Macros tab draws no board of its own.
        /// </summary>
        void PickMacroKey();

        /// <summary>
        /// Puts a question on the app's shared message box and reports whether the answer was yes.
        /// False for every other answer, <b>including a box that could not be shown at all</b> — a
        /// confirmation that failed must delete nothing.
        /// </summary>
        Task<bool> ConfirmMacroDeleteAsync(string title, string message, string confirmCaption, string declineCaption);
    }
}
