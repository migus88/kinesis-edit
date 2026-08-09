using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Everything that keeps a macro's <em>name</em> alive across a load and a save (issue #93,
    /// mockup <c>2i</c>). Split out of <see cref="KeyboardEditorViewModel"/>'s main file for the
    /// same reason <c>KeyboardEditorViewModel.Legend.cs</c> and <c>…Inspector.cs</c> were: that file
    /// is the largest in the app and docs/guides/Coding Conventions.md forbids growing it into a god
    /// class. Everything here is the same class and runs on the same rules.
    ///
    /// <para><b>A name belongs to a place, not to an identity.</b> There is no shared macro anywhere
    /// on disk — every key's slot holds its own copy (06 §1) — so the two steps below walk the
    /// layout's macro <em>sites</em> (<see cref="MacroSites"/>) and nothing groups them. The
    /// app-level <c>MacroLibrary</c> that used to sit here, disambiguating same-name macros and
    /// propagating a rename across every site of a group, was deleted with issue #141: it maintained
    /// an identity the hardware does not have, and renaming the macro on key A must leave the
    /// identical-looking one on key B alone.</para>
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
    /// <see cref="Macro.Name"/> for the rest of the session, so the rail's name field behaves
    /// identically.</para>
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
        public bool HasUnsavedMacroNames => _renamedProfiles.Count > 0;

        /// <summary>
        /// The profile numbers carrying a rename that is not on the drive yet — added by
        /// <see cref="MarkMacroNamesDirty"/>, removed by the save that wrote them. A <b>set</b>
        /// rather than the single flag it was until issue #133: with every visited profile kept
        /// alive in <c>_sessions</c>, a switch no longer discards the profile it leaves, so a rename
        /// made in profile 3 has to still be there — and still be saved — after a trip to profile 7.
        /// </summary>
        private readonly HashSet<int> _renamedProfiles = [];

        /// <summary>
        /// Records that a name moved <b>in the open profile</b>. Its production caller is the rail's
        /// Macro panel, whose inline name field raises <c>NameChanged</c> for exactly this
        /// (<c>KeyboardEditorViewModel.Inspector.cs</c>) — a name is written straight onto
        /// <see cref="Macro.Name"/> by the field's own setter, and no session can see that happen,
        /// because it is not in <c>layout&lt;n&gt;.txt</c>.
        /// <para>
        /// Public because it is the seam that says "this profile's names are out of date", which is
        /// a fact about the editor rather than about the panel that noticed it.
        /// </para>
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

            ApplyStoredMacroNames(layout);
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

            MacroSites.ApplyNames(
                layout,
                site => MacroNameKey.TryCreate(profileNumber, site.LayerIndex, site.TriggerKeyCode, site.SlotNumber, out var key)
                    ? settings.GetMacroName(key)
                    : null);
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
                if (profileNumber < 1 || FindLayoutFor(profileNumber) is not { } layout)
                {
                    // No session to scope the names to (a load that failed, a board with no drive).
                    // The names stay on Macro.Name for the rest of the session; there is nowhere to
                    // put them.
                    ClearUnsavedMacroNames(profileNumber);

                    continue;
                }

                PersistMacroNamesOf(profileNumber, layout);

                ClearUnsavedMacroNames(profileNumber);
            }
        }

        /// <summary>
        /// The layout whose names belong to <paramref name="profileNumber"/>: the open profile's,
        /// and any other visited profile's straight off the session the editor is still holding
        /// (issue #133). Null for a profile with no session at all.
        /// <para>
        /// Reading another session's layout is legal because a name lives on <see cref="Macro.Name"/>
        /// — on the model that session holds — rather than in anything this class owns, so there is
        /// no second snapshot to fall out of step with the first.
        /// </para>
        /// </summary>
        private KeyboardLayout? FindLayoutFor(int profileNumber)
        {
            if (profileNumber == ProfileNumber)
            {
                return Layout;
            }

            return _sessions.TryGetValue(profileNumber, out var session)
                ? session.Layout
                : null;
        }

        private void PersistMacroNamesOf(int profileNumber, KeyboardLayout layout)
        {
            var names = new List<KeyValuePair<MacroNameKey, string>>();

            foreach (var stored in MacroSites.EnumerateStoredNames(layout))
            {
                // A site the settings layer cannot name — an unbound flat-list macro has no trigger
                // and no layer — is simply absent, exactly as it was before it was ever named.
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
