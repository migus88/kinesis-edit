using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The open profile's <see cref="Core.Model.MacroLibrary"/> and everything that keeps macro
    /// <em>names</em> alive across a load and a save (issue #93, mockup <c>2i</c>). Split out of
    /// <see cref="KeyboardEditorViewModel"/>'s main file for the same reason
    /// <c>KeyboardEditorViewModel.Legend.cs</c> and <c>…Inspector.cs</c> were: that file is the
    /// largest in the app and docs/guides/Coding Conventions.md forbids growing it into a god
    /// class. Everything here is the same class and runs on the same rules.
    ///
    /// <para><b>There is exactly one library per open profile, and it is this one.</b> The rail's
    /// Macro panel and the Macros tab are two views of the same identity — "every macro has a name,
    /// so the tab lists them once" — and two <c>MacroLibrary</c> instances over one
    /// <c>KeyboardLayout</c> would be two snapshots that disagree the moment either is edited.
    /// Whoever needs it asks <see cref="MacroLibrary"/>; nobody constructs a second.</para>
    ///
    /// <para><b>Names are not in the layout file.</b> They ride <c>settings/app_settings.txt</c> as
    /// <c>macro_name_&lt;profile&gt;_&lt;layer&gt;_&lt;trigger&gt;_&lt;slot&gt;</c>
    /// (<see cref="MacroNameKey"/>), so a freshly parsed layout always arrives unnamed and this
    /// class is what stamps the stored names back on — <see cref="ApplyStoredMacroNames"/> on load,
    /// <see cref="PersistMacroNames"/> on save.</para>
    ///
    /// <para><b>A rename is part of the session's dirty model — deliberately, and this is the one
    /// exception.</b> docs/app/settings.md's rule is that <c>app_settings.txt</c> sits outside the
    /// dirty model: a swatch and a "don't ask again" answer are written the moment they are made.
    /// A macro name is not like those. It names something the user is editing, it is meaningless
    /// beside a profile that was never saved, and discarding the session has to discard it too — so
    /// it marks the session dirty (<see cref="HasUnsavedMacroNames"/>, folded into
    /// <c>IsDirty</c>) and reaches the drive only through <c>Save</c>. Swatches and suppression
    /// answers are untouched by this and keep writing through immediately.</para>
    ///
    /// <para><b>Nothing here throws on a store that cannot write.</b> Demo mode and a drive with no
    /// <c>app_settings.txt</c> both discard the write; the names still live on
    /// <see cref="Macro.Name"/> for the rest of the session, so the library, the dropdown and the
    /// Macros tab behave identically.</para>
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <summary>
        /// The open profile's macro library, or null while nothing is loaded. <b>Rebuilt in place</b>
        /// — the instance survives every edit, so a consumer may hold it; its
        /// <see cref="Core.Model.MacroLibrary.Entries"/> snapshot does not, and is re-read after
        /// every <see cref="MacroLibraryChanged"/>.
        /// </summary>
        public MacroLibrary? MacroLibrary
        {
            get => _macroLibrary;
            private set => SetProperty(ref _macroLibrary, value);
        }

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
        public bool HasUnsavedMacroNames => _renamedProfiles.Count > 0;

        /// <summary>
        /// Raised after <see cref="MacroLibrary"/>'s snapshot was rebuilt or replaced — a load, an
        /// import, a reset, or any macro edit that went through the editor's refresh funnel. Core
        /// announces nothing, so a consumer holding an entry has to re-read it here.
        /// </summary>
        public event EventHandler? MacroLibraryChanged;

        private MacroLibrary? _macroLibrary;

        /// <summary>
        /// The profile numbers carrying a rename that is not on the drive yet — added by
        /// <see cref="MarkMacroNamesDirty"/>, removed by the save that wrote them. A <b>set</b>
        /// rather than the single flag it was until issue #133: with every visited profile kept
        /// alive in <c>_sessions</c>, a switch no longer discards the profile it leaves, so a rename
        /// made in profile 3 has to still be there — and still be saved — after a trip to profile 7.
        /// </summary>
        private readonly HashSet<int> _renamedProfiles = [];

        /// <summary>
        /// Renames every site of <paramref name="entry"/> and marks the session dirty. This is the
        /// <b>one</b> rename path in the app: the Macros tab runs it, and the key inspector's Macro
        /// panel deliberately does not offer one (mockup <c>2i</c> — the tab is the library, the
        /// rail is the editor).
        /// <para>
        /// Returns the fresh entry, because every library mutation rebuilds the snapshot and the one
        /// that was passed in is stale afterwards. Null when there is no library, or when
        /// <paramref name="entry"/> did not come from its current snapshot.
        /// </para>
        /// <para>
        /// A blank name <b>clears</b> the stored one: the macro goes back to the name Core derives
        /// from its content, and its settings key is removed by the next save.
        /// </para>
        /// </summary>
        public MacroLibraryEntry? RenameMacro(MacroLibraryEntry entry, string? newName)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (_macroLibrary is not { } library)
            {
                return null;
            }

            MacroLibraryEntry renamed;

            try
            {
                renamed = library.Rename(entry, newName);
            }
            catch (ArgumentException)
            {
                // A stale entry — the caller is holding a snapshot from before somebody else's
                // edit. Refusing is right: renaming whatever happens to sit in that slot now would
                // rename a macro the user never pointed at.
                return null;
            }

            MarkMacroNamesDirty();

            // Names are not in the layout, so nothing about this moves a counter — but the rail,
            // the legend and the dirty flag all read the library, and the funnel is the one place
            // that pushes to all of them.
            RefreshCounters();

            return renamed;
        }

        /// <summary>
        /// Records that a name moved <b>in the open profile</b>. Public so the Macros tab's own
        /// edits — a rename that goes through <see cref="RenameMacro"/> already does this, a delete
        /// or a duplicate does not — can say so without reaching into the set.
        /// </summary>
        public void MarkMacroNamesDirty()
        {
            if (!_renamedProfiles.Add(ProfileNumber))
            {
                return;
            }

            OnPropertyChanged(nameof(HasUnsavedMacroNames));

            RefreshDirtyState();
        }

        /// <summary>
        /// Drops one profile's "a macro was renamed" mark without writing anything. Two callers, and
        /// only two: the save that persisted that profile's names, and the layout half of
        /// <c>Discard changes</c>, which throws the renames away with the rest of the profile's
        /// unsaved work. A profile <b>switch</b> deliberately does not call it — since issue #133 a
        /// switch abandons nothing, so clearing the mark there would lose a rename silently.
        /// </summary>
        private void ClearUnsavedMacroNames(int profileNumber)
        {
            if (!_renamedProfiles.Remove(profileNumber))
            {
                return;
            }

            OnPropertyChanged(nameof(HasUnsavedMacroNames));
        }

        /// <summary>
        /// Stamps the stored names onto a freshly parsed layout and builds the library over it.
        /// Called from <c>Apply</c>, i.e. after a load, after an import and after a layout discard —
        /// each of those replaces the layout wholesale, so its macros arrive unnamed exactly as a
        /// loaded one's do.
        /// <para>
        /// The order is fixed: names first, library second.
        /// <see cref="Core.Model.MacroLibrary"/> groups by name, so a name applied after the
        /// snapshot was built would not be in it.
        /// </para>
        /// <para>
        /// <b><paramref name="applyStoredNames"/> is false on the one path where the layout is not
        /// fresh</b>: returning to a profile the editor already has open (issue #133). Its macros
        /// are the very objects the user renamed, and
        /// <see cref="Core.Model.MacroLibrary.ApplyNames"/> writes <c>Macro.Name</c>
        /// <em>unconditionally</em> — a site with no stored name is set back to null — so re-running
        /// it there would quietly undo every rename that has not reached
        /// <c>app_settings.txt</c> yet. The library itself is still rebuilt, because it is per
        /// profile and it groups by the names the model carries now.
        /// </para>
        /// </summary>
        private void AttachMacroLibrary(KeyboardLayout? layout, bool applyStoredNames)
        {
            if (layout is null)
            {
                MacroLibrary = null;

                MacroLibraryChanged?.Invoke(this, EventArgs.Empty);

                return;
            }

            if (applyStoredNames)
            {
                ApplyStoredMacroNames(layout);
            }

            MacroLibrary = new MacroLibrary(layout);

            MacroLibraryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reads this profile's names out of the session's one <c>app_settings.txt</c> store and
        /// puts them on the layout's macros. A site with no stored name is left unnamed, so Core
        /// derives its display name from what it types — which is what keeps a derived name
        /// following the macro's content instead of freezing at the moment it was first shown.
        /// </summary>
        private void ApplyStoredMacroNames(KeyboardLayout layout)
        {
            var profileNumber = ProfileNumber;

            if (profileNumber < 1)
            {
                // No session — a load that failed, or a device with no drive at all. There is no
                // profile to scope a name to, so the macros keep their derived names and nothing is
                // lost. Demo mode does *not* come through here: it opens a real profile (issue #96)
                // and reads its names normally; what it refuses is the write, in the store.
                return;
            }

            var settings = _preferences.Current;

            Core.Model.MacroLibrary.ApplyNames(
                layout,
                site => MacroNameKey.TryCreate(profileNumber, site.LayerIndex, site.TriggerKeyCode, site.SlotNumber, out var key)
                    ? settings.GetMacroName(key)
                    : null);
        }

        /// <summary>
        /// Re-reads the library's snapshot after somebody wrote to the layout. Hung off
        /// <c>RefreshCounters</c>, the one funnel every mutation path already ends in, so "every
        /// macro edit reaches the library" is true by construction rather than by a list somebody
        /// has to maintain — and it runs <b>before</b> the legend and the rail are refreshed,
        /// because the rail's Macro panel reads this snapshot.
        /// </summary>
        private void RefreshMacroLibrary()
        {
            if (_macroLibrary is null)
            {
                return;
            }

            _macroLibrary.Refresh();

            MacroLibraryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Writes the macro names of <b>every profile carrying a rename</b> to
        /// <c>app_settings.txt</c>, through the session's one store — never through a second
        /// <c>LoadAppSettings</c>/<c>SaveAppSettings</c> pair, which is what would let the colour
        /// picker and the settings screen serve each other stale state (docs/app/settings.md).
        /// <para>
        /// One call to <see cref="IAppPreferencesStore.UpdateMacroNames"/> per profile, and that is
        /// safe by construction: <c>AppSettings.WithMacroNamesForProfile</c> is already scoped to one
        /// profile number and tombstones only that profile's keys, so nine calls leave nine
        /// independent sets rather than the last one winning.
        /// </para>
        /// <para>
        /// The whole current picture is handed over, not a diff:
        /// <c>WithMacroNamesForProfile</c> tombstones every key of this profile the new set does not
        /// carry, so a rename, a delete and an unassignment all resolve into removals by themselves.
        /// Only <b>explicitly</b> named macros are in that set — a derived name is recomputed on
        /// every load and is never written.
        /// </para>
        /// </summary>
        private void PersistMacroNames()
        {
            if (_renamedProfiles.Count == 0)
            {
                return;
            }

            // A copy, because the clear below mutates the set this walks.
            foreach (var profileNumber in _renamedProfiles.ToArray())
            {
                if (profileNumber < 1 || FindMacroLibraryFor(profileNumber) is not { } library)
                {
                    // No session to scope the names to (a load that failed, a board with no drive).
                    // The names stay on Macro.Name for the rest of the session; there is nowhere to
                    // put them.
                    ClearUnsavedMacroNames(profileNumber);

                    continue;
                }

                PersistMacroNamesOf(profileNumber, library);

                ClearUnsavedMacroNames(profileNumber);
            }
        }

        /// <summary>
        /// The library whose names belong to <paramref name="profileNumber"/>: the editor's one
        /// live library for the open profile, and a <b>transient</b> one over any other visited
        /// profile's layout.
        /// <para>
        /// Building a second library is legal here and only here. Invariant 27 — one library per
        /// profile — is about the <em>open</em> profile, whose library the rail and the Macros tab
        /// both read; this one is read once, inside this method, and dropped. It can be built at
        /// all because a name lives on <c>Macro.Name</c>, i.e. on the model the other session is
        /// holding, not on the library.
        /// </para>
        /// </summary>
        private MacroLibrary? FindMacroLibraryFor(int profileNumber)
        {
            if (profileNumber == ProfileNumber)
            {
                return _macroLibrary;
            }

            return _sessions.TryGetValue(profileNumber, out var session)
                ? new MacroLibrary(session.Layout)
                : null;
        }

        private void PersistMacroNamesOf(int profileNumber, MacroLibrary library)
        {
            var names = new List<KeyValuePair<MacroNameKey, string>>();

            foreach (var stored in library.EnumerateStoredNames())
            {
                if (MacroNameKey.TryCreate(
                        profileNumber,
                        stored.Key.LayerIndex,
                        stored.Key.TriggerKeyCode,
                        stored.Key.SlotNumber,
                        out var key))
                {
                    names.Add(KeyValuePair.Create(key, stored.Value));
                }
            }

            _preferences.UpdateMacroNames(profileNumber, settings => settings.WithMacroNamesForProfile(profileNumber, names));
        }
    }
}
