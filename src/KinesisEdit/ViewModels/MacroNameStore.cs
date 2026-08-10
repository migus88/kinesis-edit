using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Which profiles carry a macro rename that is not on the drive yet, and both directions of the
    /// traffic that puts a macro's <em>name</em> in <c>settings/app_settings.txt</c> and takes it
    /// back out (issue #93).
    /// <para>
    /// It is a type of its own rather than a <see cref="HashSet{T}"/> and five more methods on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is invariant 27 (docs/app/keyboard-editor.md): <b>a name belongs to a place, and a
    /// derived display name never reaches <see cref="Macro.Name"/>.</b> The stamp on load and the
    /// harvest on save are the two halves of one round trip — keyed by
    /// <see cref="MacroNameKey.TryCreate"/> off <c>KeyboardKey.TriggerKey.Code</c>, never the
    /// position's own code — and a round trip whose halves live in different classes is a round trip
    /// that silently stops closing.
    /// </para>
    /// <para>
    /// <b>A name belongs to a place, not to an identity.</b> There is no shared macro anywhere on
    /// disk — every key's slot holds its own copy (06 §1) — so both steps walk the layout's macro
    /// <em>sites</em> (<see cref="MacroSites"/>) and nothing groups them. The app-level
    /// <c>MacroLibrary</c> that used to disambiguate same-name macros was deleted with issue #141:
    /// it maintained an identity the hardware does not have, and renaming the macro on key A must
    /// leave the identical-looking one on key B alone.
    /// </para>
    /// <para>
    /// <b>The unsaved-rename state is a per-profile set, not a flag.</b> It was a single bool until
    /// issue #133; with every visited profile kept alive in <see cref="ProfileSessionCache"/> a
    /// switch no longer discards the profile it leaves, so a rename made in profile 3 has to still
    /// be there — and still be saved — after a trip to profile 7. That set is the one piece of state
    /// here, and it is what folds into the editor's <c>IsDirty</c>.
    /// </para>
    /// <para>
    /// <b>Its two collaborators are handed to it, and it holds no editor reference.</b> The
    /// <see cref="IAppPreferencesStore"/> is the session's one store — never a second
    /// <c>LoadAppSettings</c>/<c>SaveAppSettings</c> pair — and the
    /// <see cref="ProfileSessionCache"/> is how a name reaches the layout it belongs to when that
    /// profile is not the one on screen (issue #133). Both arrive in the constructor, which is why
    /// this is built there rather than beside its field: neither is reachable from a field
    /// initializer, and a store that had to be told its store on every call would let two callers
    /// disagree about which one it is.
    /// </para>
    /// <para>
    /// <b>It is not a view model, and it announces nothing.</b> The editor publishes
    /// <c>HasUnsavedMacroNames</c> and <c>MarkMacroNamesDirty</c> under its own names and raises
    /// <c>PropertyChanged</c> itself, so <see cref="Mark"/> and <see cref="Clear"/> report whether
    /// the set actually moved and leave the announcement to the announcer — the same delegation
    /// <see cref="InspectorRailWidthViewModel"/> is behind.
    /// </para>
    /// <para>
    /// <b>Nothing here throws on a store that cannot write.</b> Demo mode and a drive with no
    /// <c>app_settings.txt</c> both discard the write; the names still live on
    /// <see cref="Macro.Name"/> for the rest of the session, so the rail behaves identically.
    /// </para>
    /// </summary>
    public sealed class MacroNameStore
    {
        /// <summary>
        /// The profile numbers carrying a rename that is not on the drive yet. Handed to
        /// <see cref="ProfileSessionCache.CollectSessionsToSave"/>, which adds them to the dirty
        /// sessions to make the write set (invariant 31) — a profile whose only edit is a rename
        /// re-serializes identically, so no session can report it.
        /// </summary>
        public IReadOnlySet<int> RenamedProfiles => _renamedProfiles;

        /// <summary>
        /// Whether a macro has been renamed since the last save, <b>in any profile the editor has
        /// opened</b>. The editor folds it into <c>IsDirty</c>, because a name is session state that
        /// Save writes and Discard drops.
        /// </summary>
        public bool HasUnsavedNames => _renamedProfiles.Count > 0;

        private readonly IAppPreferencesStore _preferences;
        private readonly ProfileSessionCache _sessions;
        private readonly HashSet<int> _renamedProfiles = [];

        /// <summary>
        /// Creates the store over the session's <c>app_settings.txt</c> and the editor's per-profile
        /// session cache — the file every name is written to, and the only way to reach the layout a
        /// name belongs to when its profile is not the open one.
        /// </summary>
        public MacroNameStore(IAppPreferencesStore preferences, ProfileSessionCache sessions)
        {
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        /// <summary>
        /// Stamps <paramref name="profileNumber"/>'s stored names onto a freshly parsed layout: read
        /// out of the session's one <c>app_settings.txt</c> store and put on the layout's macros. A
        /// site with no stored name is left unnamed, so Core derives its display name from what it
        /// types — which is what keeps a derived name following the macro's content instead of
        /// freezing at the moment it was first shown.
        /// <para>
        /// A profile number below 1 means there is no session — a load that failed, or a device with
        /// no drive at all — so there is nothing to scope a name to and the macros keep their
        /// derived names. Demo mode does <em>not</em> come through here: it opens a real profile
        /// (issue #96) and reads its names normally; what it refuses is the write, in the store.
        /// </para>
        /// </summary>
        public void ApplyStoredNames(KeyboardLayout layout, int profileNumber)
        {
            ArgumentNullException.ThrowIfNull(layout);

            if (profileNumber < 1)
            {
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
        /// Writes one profile's macro names to <c>app_settings.txt</c> through the session's one
        /// store — never through a second <c>LoadAppSettings</c>/<c>SaveAppSettings</c> pair, which
        /// is what would let the colour picker and the settings screen serve each other stale state
        /// (docs/app/settings.md).
        /// <para>
        /// The layout is found here rather than passed in, because the profile being written is
        /// often <b>not</b> the one on screen: since issue #133 every visited profile stays in
        /// <see cref="ProfileSessionCache"/>, so a rename made in profile 3 is still saveable from
        /// profile 7. Reading another session's layout is legal because a name lives on
        /// <see cref="Macro.Name"/> — on the model that session holds — rather than in anything this
        /// type owns, so there is no second snapshot to fall out of step with the first. A profile
        /// with no session at all (a load that failed, a board with no drive) writes nothing; its
        /// names stay on <see cref="Macro.Name"/> for the rest of the session, because there is
        /// nowhere to put them.
        /// </para>
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
        /// Only <b>explicitly</b> named macros are in that set (invariant 27) — a derived name is
        /// recomputed on every load and is never written.
        /// </para>
        /// </summary>
        public void Persist(int profileNumber)
        {
            if (profileNumber < 1 || FindLayoutFor(profileNumber) is not { } layout)
            {
                return;
            }

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

        /// <summary>
        /// Records that a name moved in <paramref name="profileNumber"/> — a name is written
        /// straight onto <see cref="Macro.Name"/>, and no session can see that happen, because it is
        /// not in <c>layout&lt;n&gt;.txt</c>. <b>True only when the set actually moved</b>, so the
        /// editor announces once rather than on every keystroke of a rename.
        /// </summary>
        public bool Mark(int profileNumber)
        {
            return _renamedProfiles.Add(profileNumber);
        }

        /// <summary>
        /// Drops one profile's "a macro was renamed" mark without writing anything, reporting
        /// whether there was one to drop. Two callers, and only two: the save that persisted that
        /// profile's names, and the layout half of <c>Discard changes</c>, which throws the renames
        /// away with the rest of the profile's unsaved work. A profile <b>switch</b> deliberately
        /// does not call it — since issue #133 a switch abandons nothing, so clearing the mark there
        /// would lose a rename silently.
        /// </summary>
        public bool Clear(int profileNumber)
        {
            return _renamedProfiles.Remove(profileNumber);
        }

        /// <summary>
        /// The marked profiles as a list that survives being cleared while it is walked — which is
        /// exactly what the save does, one <see cref="Persist"/> and one <see cref="Clear"/> per
        /// profile. Empty when nothing is marked, so the save's loop is a no-op and the
        /// <c>macro_name_*</c> lines already on the drive are simply never rewritten.
        /// </summary>
        public IReadOnlyList<int> SnapshotRenamedProfiles()
        {
            return _renamedProfiles.ToArray();
        }

        /// <summary>
        /// The layout whose names belong to <paramref name="profileNumber"/>, straight off the
        /// session the editor is still holding — the open profile's included, since <c>Apply</c>
        /// files every session it opens in the cache. Null for a profile with no session at all.
        /// </summary>
        private KeyboardLayout? FindLayoutFor(int profileNumber)
        {
            return _sessions.Find(profileNumber)?.Layout;
        }
    }
}
