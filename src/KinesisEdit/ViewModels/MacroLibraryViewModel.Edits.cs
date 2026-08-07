using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the Macros tab's actions actually do: the two navigations, the slot card's selection and
    /// <c>Make active</c>, and the four per-macro edits mockup <c>2i</c> names — "rename, see which
    /// keys and layers fire each one, duplicate, delete". Split from the tab's state for the reason
    /// <c>MacroLibrary.Edits.cs</c> is split from <c>MacroLibrary</c> in Core, and because
    /// docs/guides/Coding Conventions.md wants small classes.
    ///
    /// <para><b>Every edit is Core's, run on the editor's one library, and announced back through
    /// the host.</b> Nothing here re-derives a name, re-counts a budget or touches the dirty flag:
    /// <c>MacroLibrary</c> owns the mutation and <c>KeyboardEditorViewModel</c> owns the funnel that
    /// rebuilds the snapshot, the counters, the advisories, the legend, the rail and the dirty
    /// state.</para>
    ///
    /// <para><b>A stale entry is refused, never guessed at.</b> Every library mutation rebuilds
    /// <c>Entries</c>, so a row that survived somebody else's edit points at an entry Core no longer
    /// owns and <c>ArgumentException</c> is its answer. Catching it and doing nothing is right:
    /// renaming or deleting whatever happens to sit there now would touch a macro the user never
    /// pointed at.</para>
    /// </summary>
    public sealed partial class MacroLibraryViewModel
    {
        /// <summary>
        /// The header's own action. <c>Pick another key</c> simply leaves for the board; <c>New
        /// macro</c> opens the selected position's rail with Record armed, because a macro is made
        /// by recording one and the rail is the only surface in this app that records.
        /// </summary>
        private void RunPrimaryAction()
        {
            if (!UsesFlatMacroList)
            {
                _host.PickMacroKey();

                return;
            }

            if (_key is not { CanAssignMacro: true } key || _layer is not { } layer)
            {
                _host.PickMacroKey();

                return;
            }

            _host.EditMacroAt(layer.Index, key.Index, MacroLibrary.FlatListSlot, startRecording: true);
        }

        /// <summary>Points the <c>this macro</c> meter at one card. A reading, so it writes nothing.</summary>
        private void SelectSlot(MacroSlotViewModel? card)
        {
            if (card is null)
            {
                return;
            }

            _selectedSlot = card.Slot;

            foreach (var candidate in Slots)
            {
                candidate.IsSelected = ReferenceEquals(candidate, card);
            }

            RefreshMeters();
        }

        /// <summary>
        /// <c>Make active</c>: which of the key's macros it fires (06 §1's <c>MacroIdx</c>). The
        /// field is in-memory only and is never serialized, so this runs the refresh funnel
        /// <b>without</b> marking the session dirty — an unsaved-changes prompt for a choice the file
        /// cannot carry would be a lie.
        /// </summary>
        private void MakeActive(MacroSlotViewModel? card)
        {
            if (card is not { CanMakeActive: true } || !UsesMacroSlots || _key is not { } key)
            {
                return;
            }

            key.Key.ActiveMacroIndex = card.Slot;
            _selectedSlot = card.Slot;

            Message = string.Empty;

            _host.RefreshMacroViews();
        }

        /// <summary>
        /// <c>＋ Record a macro</c> on an empty card. It <b>jumps to the inspector</b> — board,
        /// position, Macro panel, Record armed — rather than capturing here: there is exactly one
        /// recording surface in this app, and mockup <c>1i</c>'s capture overlay on this tab would be
        /// a second one (see the deviation in docs/app/design-system.md).
        /// </summary>
        private void RecordMacro(MacroSlotViewModel? card)
        {
            if (card is null || _key is not { } key || _layer is not { } layer)
            {
                return;
            }

            _host.EditMacroAt(layer.Index, key.Index, card.Slot, startRecording: true);
        }

        /// <summary>Opens a row's macro where it is edited: its own position, on the rail.</summary>
        private void EditMacro(MacroLibraryRowViewModel? row)
        {
            if (row is null || row.Entry.Sites.Count == 0)
            {
                return;
            }

            var site = row.Entry.Sites[0];

            if (ResolveKeyIndex(site) is not int keyIndex)
            {
                return;
            }

            _host.EditMacroAt(site.LayerIndex, keyIndex, site.SlotNumber, startRecording: false);
        }

        /// <summary>
        /// Which position a site sits on. A slot site names its key outright; a flat-list one names
        /// only its trigger code (06 §1), so the layer is asked which position carries it.
        /// </summary>
        private int? ResolveKeyIndex(MacroSite site)
        {
            if (site.Key is { } key)
            {
                return key.Index;
            }

            if (_layout is not { } layout || site.LayerIndex < 0 || site.LayerIndex >= layout.Layers.Count)
            {
                return null;
            }

            return layout.Layers[site.LayerIndex].FindByTriggerKeyCode(site.TriggerKeyCode)?.Index;
        }

        /// <summary>Opens the in-place rename field on one row, closing whichever was open.</summary>
        private void BeginRename(MacroLibraryRowViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            foreach (var candidate in _allRows)
            {
                candidate.IsRenaming = false;
            }

            row.ResetRenameText();

            row.IsRenaming = true;
            _renamingName = row.Name;

            Message = string.Empty;
        }

        /// <summary>Closes it without writing, and puts the field back to what the macro is called.</summary>
        private void CancelRename(MacroLibraryRowViewModel? row)
        {
            _renamingName = null;

            if (row is null)
            {
                return;
            }

            row.IsRenaming = false;

            row.ResetRenameText();
        }

        /// <summary>
        /// Writes the typed name through <see cref="IMacroLibraryHost.RenameMacro"/> — the app's one
        /// rename path, which renames every site of the macro, marks the session dirty and runs the
        /// funnel that rebuilds these rows. A <b>blank</b> name clears the stored one, so the macro
        /// goes back to the name Core derives from its content.
        /// </summary>
        private void CommitRename(MacroLibraryRowViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            var name = row.RenameText;

            row.IsRenaming = false;
            _renamingName = null;

            if (_host.RenameMacro(row.Entry, name) is null)
            {
                // No library, or an entry from before somebody else's edit. Nothing was renamed, so
                // the rows are rebuilt from what is actually there.
                Refresh(_key, _layer, _layout);
            }
        }

        /// <summary>
        /// An independent second macro with the same content, named uniquely
        /// (<see cref="MacroLibrary.Duplicate"/>). It lands on the <em>same</em> trigger, which
        /// <c>KeyboardLayout.Validate</c> reports as a duplicate trigger (06 §5) — reported, never
        /// refused, like every other limit in this model. Null is "there was no room", which is the
        /// one thing this reports.
        /// </summary>
        private void Duplicate(MacroLibraryRowViewModel? row)
        {
            if (row is null || _resolveLibrary() is not { } library)
            {
                return;
            }

            MacroLibraryEntry? copy;

            try
            {
                copy = library.Duplicate(row.Entry);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (copy is null)
            {
                Message = DuplicateRefusedMessage;

                return;
            }

            Message = string.Empty;

            _host.CommitMacroLibraryEdit();
        }

        /// <summary>
        /// Removes the macro from <b>every</b> site it occupies — and <b>says which ones first</b>.
        /// A macro is one row however many keys fire it, so deleting it takes it off all of them, and
        /// a question that did not name them would be hiding the consequence. A declined question
        /// erases nothing at all.
        /// </summary>
        private async Task DeleteAsync(MacroLibraryRowViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            var confirmed = await _host.ConfirmMacroDeleteAsync(
                DeleteTitle,
                BuildDeleteMessage(row.Name, row.SiteListText, row.SiteCount),
                DeleteConfirmCaption,
                DeleteDeclineCaption).ConfigureAwait(true);

            if (!confirmed || _resolveLibrary() is not { } library)
            {
                return;
            }

            try
            {
                library.Delete(row.Entry);
            }
            catch (ArgumentException)
            {
                // The snapshot moved while the question was on screen.
                return;
            }

            Message = string.Empty;

            _host.CommitMacroLibraryEdit();
        }
    }
}
