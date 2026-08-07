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
        /// Whether a macro has been renamed since the last save. Folded into <c>IsDirty</c>, because
        /// a name is session state that Save writes and Discard drops — see the type's remarks.
        /// </summary>
        public bool HasUnsavedMacroNames => _hasUnsavedMacroNames;

        /// <summary>
        /// Raised after <see cref="MacroLibrary"/>'s snapshot was rebuilt or replaced — a load, an
        /// import, a reset, or any macro edit that went through the editor's refresh funnel. Core
        /// announces nothing, so a consumer holding an entry has to re-read it here.
        /// </summary>
        public event EventHandler? MacroLibraryChanged;

        private MacroLibrary? _macroLibrary;

        /// <summary>Set by <see cref="RenameMacro"/>, cleared by a save. See the type's remarks.</summary>
        private bool _hasUnsavedMacroNames;

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
        /// Records that a name moved. Public so the Macros tab's own edits — a rename that goes
        /// through <see cref="RenameMacro"/> already does this, a delete or a duplicate does not —
        /// can say so without reaching into the flag.
        /// </summary>
        public void MarkMacroNamesDirty()
        {
            if (_hasUnsavedMacroNames)
            {
                return;
            }

            _hasUnsavedMacroNames = true;

            OnPropertyChanged(nameof(HasUnsavedMacroNames));

            RefreshDirtyState();
        }

        /// <summary>
        /// Stamps the stored names onto a freshly parsed layout and builds the library over it.
        /// Called from <c>Apply</c>, i.e. after a load <b>and</b> after an import — an imported file
        /// replaces the layout wholesale, so its macros arrive unnamed exactly as a loaded one's do.
        /// <para>
        /// The order is fixed: names first, library second.
        /// <see cref="Core.Model.MacroLibrary"/> groups by name, so a name applied after the
        /// snapshot was built would not be in it.
        /// </para>
        /// </summary>
        private void AttachMacroLibrary(KeyboardLayout? layout)
        {
            if (layout is null)
            {
                MacroLibrary = null;

                MacroLibraryChanged?.Invoke(this, EventArgs.Empty);

                return;
            }

            ApplyStoredMacroNames(layout);

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
                // Demo mode reads no profile, so no name can be scoped to one. The macros keep
                // their derived names and nothing is lost.
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
        /// Writes this profile's macro names to <c>app_settings.txt</c>, through the session's one
        /// store — never through a second <c>LoadAppSettings</c>/<c>SaveAppSettings</c> pair, which
        /// is what would let the colour picker and the settings screen serve each other stale state
        /// (docs/app/settings.md).
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
            var profileNumber = ProfileNumber;

            if (_macroLibrary is not { } library || profileNumber < 1)
            {
                return;
            }

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

            _hasUnsavedMacroNames = false;

            OnPropertyChanged(nameof(HasUnsavedMacroNames));
        }
    }
}
