using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's side of the macro names that keep a macro's <em>name</em> alive across a load
    /// and a save (issue #93, mockup <c>2i</c>): which profile scopes a name, when the stamp and the
    /// harvest run, and the announcements the rename state owes the chrome. The state itself and
    /// both writes live in <see cref="MacroNameStore"/> — see that type for why a name belongs to a
    /// place rather than an identity, and why the unsaved-rename state is a per-profile set.
    ///
    /// <para>Split out of <see cref="KeyboardEditorViewModel"/>'s main file for the reason
    /// <c>…Legend.cs</c>, <c>…Inspector.cs</c> and <c>…Profiles.cs</c> were — that file is the
    /// largest in the app and docs/guides/Coding Conventions.md forbids growing it into a god class.
    /// Everything here is the same class and runs on the same rules.</para>
    ///
    /// <para><b>A rename is part of the session's dirty model — deliberately, and it is the one
    /// exception.</b> docs/app/settings.md's rule is that <c>app_settings.txt</c> sits outside the
    /// dirty model: a swatch and a "don't ask again" answer are written the moment they are made.
    /// A macro name is not like those. It names something the user is editing, it is meaningless
    /// beside a profile that was never saved, and discarding the session has to discard it too — so
    /// it marks the session dirty (<see cref="HasUnsavedMacroNames"/>, folded into <c>IsDirty</c>)
    /// and reaches the drive only through <c>Save</c>. Swatches and suppression answers are
    /// untouched by this and keep writing through immediately.</para>
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <summary>
        /// The layout profile the editor has open, or <b>0</b> when there is no session at all
        /// (demo mode, and a load that failed). It is what scopes a macro name to a profile: one
        /// <c>app_settings.txt</c> holds nine profiles' names and saving this one must never remove
        /// another's.
        /// </summary>
        public int ProfileNumber => _session?.ProfileNumber ?? 0;

        /// <summary>
        /// Whether a macro has been renamed since the last save, <b>in any profile the editor has
        /// opened</b>. Folded into <c>IsDirty</c>, because a name is session state that Save writes
        /// and Discard drops — see the type's remarks.
        /// </summary>
        public bool HasUnsavedMacroNames => _macroNames.HasUnsavedNames;

        /// <summary>
        /// The per-profile unsaved-rename set and both directions of the <c>app_settings.txt</c>
        /// name traffic (<see cref="MacroNameStore"/>). Built in this class's constructor, where its
        /// two collaborators are: the session's preferences store, and the session cache it finds
        /// another profile's layout in. Neither is reachable from a field initializer.
        /// </summary>
        private readonly MacroNameStore _macroNames;

        /// <summary>
        /// Records that a name moved <b>in the open profile</b> — a name is written straight onto
        /// <see cref="Macro.Name"/>, and no session can see that happen, because it is not in
        /// <c>layout&lt;n&gt;.txt</c>.
        /// <para>
        /// <b>It has no production caller today</b>, and that is the whole of why a stored name
        /// survives issue #146, which removed the rail's inline name field: with nothing marking a
        /// profile, <see cref="PersistMacroNames"/> walks an empty set and the <c>macro_name_*</c>
        /// lines already in <c>app_settings.txt</c> are simply never rewritten. Public because it is
        /// the seam that says "this profile's names are out of date" — a fact about the editor
        /// rather than about whatever noticed it — and because the naming surface that will call it
        /// again is a UI decision, not a change to this file.
        /// </para>
        /// </summary>
        public void MarkMacroNamesDirty()
        {
            if (!_macroNames.Mark(ProfileNumber))
            {
                return;
            }

            OnPropertyChanged(nameof(HasUnsavedMacroNames));

            RefreshDirtyState();
        }

        /// <summary>
        /// Drops one profile's "a macro was renamed" mark without writing anything, and announces it
        /// — the store holds the set, this class owns the property over it. Two callers, and only
        /// two: the save that persisted that profile's names, and the layout half of
        /// <c>Discard changes</c>. A profile <b>switch</b> deliberately does not call it (issue
        /// #133): a switch abandons nothing, so clearing the mark there would lose a rename silently.
        /// </summary>
        private void ClearUnsavedMacroNames(int profileNumber)
        {
            if (!_macroNames.Clear(profileNumber))
            {
                return;
            }

            OnPropertyChanged(nameof(HasUnsavedMacroNames));
        }

        /// <summary>
        /// Stamps the stored names onto a freshly parsed layout. Called from <c>Apply</c>, i.e.
        /// after a load, after an import and after a layout discard — each of those replaces the
        /// layout wholesale, so its macros arrive unnamed exactly as a loaded one's do.
        /// <para>
        /// <b><paramref name="applyStoredNames"/> is false on the one path where the layout is not
        /// fresh</b>: returning to a profile the editor already has open (issue #133). Its macros
        /// are the very objects the user renamed, and <see cref="MacroSites.ApplyNames"/> writes
        /// <c>Macro.Name</c> <em>unconditionally</em> — a site with no stored name is reset to empty
        /// — so re-running it there would quietly undo every rename that has not reached
        /// <c>app_settings.txt</c> yet, with nothing on screen to say so.
        /// </para>
        /// </summary>
        private void AttachMacroNames(KeyboardLayout? layout, bool applyStoredNames)
        {
            if (layout is null || !applyStoredNames)
            {
                return;
            }

            _macroNames.ApplyStoredNames(layout, ProfileNumber);
        }

        /// <summary>
        /// Writes the macro names of <b>every profile carrying a rename</b> to
        /// <c>app_settings.txt</c>, one <see cref="MacroNameStore.Persist"/> and one clear each.
        /// <b>The walk stays here</b> because the clear announces
        /// <see cref="HasUnsavedMacroNames"/>, and a property may only be raised by the class that
        /// declares it; the snapshot is what makes clearing it mid-walk safe. Finding each profile's
        /// layout is the store's, which is why the loop is now two calls and no reasoning.
        /// <para>
        /// A profile with no session to scope its names to (a load that failed, a board with no
        /// drive) is cleared without a write: the names stay on <see cref="Macro.Name"/> for the
        /// rest of the session, because there is nowhere to put them.
        /// </para>
        /// </summary>
        private void PersistMacroNames()
        {
            foreach (var profileNumber in _macroNames.SnapshotRenamedProfiles())
            {
                _macroNames.Persist(profileNumber);

                ClearUnsavedMacroNames(profileNumber);
            }
        }
    }
}
