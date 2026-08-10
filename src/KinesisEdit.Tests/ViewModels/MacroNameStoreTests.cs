using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The macro-name round trip on its own — <b>no editor and no drive</b>, only the two
    /// collaborators the store is handed: a fake <see cref="IAppPreferencesStore"/> and a
    /// <see cref="ProfileSessionCache"/> holding fake sessions. That is the point of the type
    /// existing: every claim about which profiles carry an unsaved rename, and about what the
    /// harvest is allowed to write, used to have to be made through a loaded
    /// <c>KeyboardEditorViewModel</c> with a save driven end to end.
    /// <para>
    /// Invariant 27 is what this suite is mostly about (docs/app/keyboard-editor.md): <b>a name
    /// belongs to a place, and a derived display name never reaches <see cref="Macro.Name"/></b> —
    /// the harvest writes every non-empty name and nothing else, keyed off
    /// <c>KeyboardKey.TriggerKey.Code</c> rather than the position's own code.
    /// </para>
    /// </summary>
    public class MacroNameStoreTests
    {
        [AvaloniaFact]
        public void Mark_ReportsOnlyTheFirstMarkOfAProfile()
        {
            // The editor announces HasUnsavedMacroNames off this answer, so a second rename in the
            // same profile must not raise it again.
            var store = CreateStore();

            Assert.False(store.HasUnsavedNames);

            Assert.True(store.Mark(3));
            Assert.False(store.Mark(3));
            Assert.True(store.HasUnsavedNames);
        }

        [AvaloniaFact]
        public void Mark_IsPerProfile_SoARenameSurvivesATripToAnotherProfile()
        {
            // Issue #133: with every visited profile kept alive, a switch abandons nothing — so a
            // rename made in profile 3 is still there, and still saveable, after a visit to 7.
            var store = CreateStore();

            store.Mark(3);
            store.Mark(7);

            Assert.Equal([3, 7], store.SnapshotRenamedProfiles().Order());
            Assert.Equal([3, 7], store.RenamedProfiles.Order());
        }

        [AvaloniaFact]
        public void Clear_DropsOneProfilesMarkAndLeavesTheOthersAlone()
        {
            var store = CreateStore();

            store.Mark(3);
            store.Mark(7);

            Assert.True(store.Clear(3));
            Assert.False(store.Clear(3));

            Assert.Equal([7], store.RenamedProfiles);
            Assert.True(store.HasUnsavedNames);

            store.Clear(7);

            Assert.False(store.HasUnsavedNames);
        }

        [AvaloniaFact]
        public void TheSnapshot_SurvivesBeingClearedWhileItIsWalked()
        {
            // Exactly what the save does — one write and one clear per profile — and the reason the
            // walk takes a copy rather than the set itself.
            var store = CreateStore();

            store.Mark(1);
            store.Mark(2);

            foreach (var profileNumber in store.SnapshotRenamedProfiles())
            {
                store.Clear(profileNumber);
            }

            Assert.False(store.HasUnsavedNames);
        }

        [AvaloniaFact]
        public void TheSnapshot_WithNothingMarked_IsEmptySoASaveWritesNoName()
        {
            // How a stored name survives issue #146: with no production caller of the mark, the
            // save's walk is a no-op and the macro_name_* lines on the drive are never rewritten.
            var preferences = new FakeAppPreferencesStore();
            var store = new MacroNameStore(preferences, new ProfileSessionCache());

            Assert.Empty(store.SnapshotRenamedProfiles());
            Assert.Empty(preferences.MacroNameWrites);
        }

        [AvaloniaFact]
        public void ApplyStoredNames_StampsTheStoredNameOntoTheFreshlyParsedLayout()
        {
            // A name is NOT in layoutN.txt: a parsed layout always arrives unnamed, and this is the
            // step that puts the stored one back.
            var layout = CreateLayoutWithOneMacro(out var triggerCode);
            var key = MacroNameKey.TryCreate(1, 0, triggerCode, 1, out var stored)
                ? stored
                : throw new InvalidOperationException("The staged site is expected to be nameable.");

            var preferences = new FakeAppPreferencesStore();

            preferences.SetInitial(AppSettings.Empty.WithMacroName(key, "Sign-off block"));

            new MacroNameStore(preferences, new ProfileSessionCache()).ApplyStoredNames(layout, 1);

            Assert.Equal("Sign-off block", FindStagedMacro(layout).Name);
        }

        [AvaloniaFact]
        public void ApplyStoredNames_WithNoStoredName_LeavesTheMacroToDeriveOne()
        {
            var layout = CreateLayoutWithOneMacro(out _);
            var macro = FindStagedMacro(layout);

            macro.Name = "stale";

            CreateStore().ApplyStoredNames(layout, 1);

            // ApplyNames writes every site unconditionally, which is exactly why the editor must not
            // re-run it over a cache hit: an unsaved rename would be reset to empty like this.
            Assert.Equal(string.Empty, macro.Name);
        }

        [AvaloniaFact]
        public void ApplyStoredNames_WithNoProfileToScopeTo_TouchesNothing()
        {
            // Profile 0 is "no session at all" — a load that failed, or a board with no drive. There
            // is nothing to key a name by, so the macros keep whatever they carry.
            var layout = CreateLayoutWithOneMacro(out _);
            var macro = FindStagedMacro(layout);

            macro.Name = "Sign-off block";

            CreateStore().ApplyStoredNames(layout, 0);

            Assert.Equal("Sign-off block", macro.Name);
        }

        [AvaloniaFact]
        public void Persist_WritesEveryNonEmptyNameAndNothingElse()
        {
            // INVARIANT 27. The harvest is over Macro.Name, and only a name somebody typed is on it:
            // a derived display name is recomputed on every load and must never be written, or the
            // next load would stamp a frozen caption back over content that has moved on.
            var layout = CreateLayoutWithOneMacro(out var triggerCode);
            var macro = FindStagedMacro(layout);
            var preferences = new FakeAppPreferencesStore();
            var store = CreateStore(preferences, (1, layout));

            store.Persist(1);

            Assert.Equal([1], preferences.MacroNameWrites);
            Assert.Empty(preferences.Current.MacroNames);
            Assert.NotEqual(string.Empty, MacroNaming.DeriveDisplayName(macro, layout));

            macro.Name = "Sign-off block";

            store.Persist(1);

            var stored = Assert.Single(preferences.Current.MacroNames);

            Assert.Equal("Sign-off block", stored.Value);
            Assert.Equal(1, stored.Key.ProfileNumber);
            Assert.Equal(1, stored.Key.SlotNumber);

            // THE TRAP: the key's trigger component is KeyboardKey.TriggerKey.Code and never the
            // position's own code. A site keyed by the position writes a settings key the next load
            // never looks up, so the name would round-trip to nothing, silently.
            Assert.Equal(triggerCode, stored.Key.TriggerKeyCode);
        }

        [AvaloniaFact]
        public void Persist_ScopesTheWriteToOneProfile()
        {
            // One app_settings.txt holds nine profiles' names, and WithMacroNamesForProfile
            // tombstones only the profile it is given — which is what makes one call per renamed
            // profile safe rather than last-one-wins.
            var first = CreateLayoutWithOneMacro(out _);
            var third = CreateLayoutWithOneMacro(out _);
            var preferences = new FakeAppPreferencesStore();
            var store = CreateStore(preferences, (1, first), (3, third));

            FindStagedMacro(first).Name = "First";
            FindStagedMacro(third).Name = "Third";

            store.Persist(1);
            store.Persist(3);

            Assert.Equal([1, 3], preferences.MacroNameWrites);
            Assert.Equal(2, preferences.Current.MacroNames.Count);
            Assert.Contains(preferences.Current.MacroNames, entry => entry.Key.ProfileNumber == 1 && entry.Value == "First");
            Assert.Contains(preferences.Current.MacroNames, entry => entry.Key.ProfileNumber == 3 && entry.Value == "Third");
        }

        [AvaloniaFact]
        public void Persist_FindsTheLayoutThroughTheSessionCache_NotOnlyTheOpenProfile()
        {
            // Issue #133 is the whole reason the cache is a collaborator: a rename made in profile 3
            // has to be saveable while the user is looking at profile 7, and the only place that
            // profile's model still lives is the session the editor kept.
            var third = CreateLayoutWithOneMacro(out _);
            var preferences = new FakeAppPreferencesStore();
            var store = CreateStore(preferences, (3, third), (7, CreateLayoutWithOneMacro(out _)));

            FindStagedMacro(third).Name = "Sign-off block";

            store.Persist(3);

            var stored = Assert.Single(preferences.Current.MacroNames);

            Assert.Equal(3, stored.Key.ProfileNumber);
            Assert.Equal("Sign-off block", stored.Value);
        }

        [AvaloniaFact]
        public void Persist_WithNoSessionToScopeTheNamesTo_WritesNothing()
        {
            // A load that failed and a board with no drive: the names stay on Macro.Name for the
            // rest of the session, because there is nowhere to put them. Profile 0 never has one.
            var preferences = new FakeAppPreferencesStore();
            var store = CreateStore(preferences, (1, CreateLayoutWithOneMacro(out _)));

            store.Persist(0);
            store.Persist(4);

            Assert.Empty(preferences.MacroNameWrites);
        }

        [AvaloniaFact]
        public void Persist_HandsTheWholePictureOver_SoARemovalResolvesByItself()
        {
            // Not a diff: the tombstones of WithMacroNamesForProfile turn a rename, a delete and an
            // unassignment into removals without any of them being modelled here.
            var layout = CreateLayoutWithOneMacro(out _);
            var macro = FindStagedMacro(layout);
            var preferences = new FakeAppPreferencesStore();
            var store = CreateStore(preferences, (1, layout));

            macro.Name = "Sign-off block";

            store.Persist(1);

            Assert.Single(preferences.Current.MacroNames);

            macro.Name = string.Empty;

            store.Persist(1);

            // A tombstone, not a dropped entry: the key stays with an empty value so Core's writer
            // deletes the line, which a merge could not tell from "unchanged" otherwise.
            var tombstone = Assert.Single(preferences.Current.MacroNames);

            Assert.Equal(string.Empty, tombstone.Value);
            Assert.Null(preferences.Current.GetMacroName(tombstone.Key));
        }

        [AvaloniaFact]
        public void Persist_ThroughAStoreThatCannotWrite_DoesNotThrow()
        {
            // Demo mode and a drive with no app_settings.txt. The name still lives on Macro.Name for
            // the rest of the session, so nothing downstream can tell the difference.
            var layout = CreateLayoutWithOneMacro(out _);

            FindStagedMacro(layout).Name = "Sign-off block";

            CreateStore(NullAppPreferencesStore.Instance, (1, layout)).Persist(1);

            Assert.Equal("Sign-off block", FindStagedMacro(layout).Name);
        }

        [AvaloniaFact]
        public void Constructor_RefusesEitherMissingCollaborator()
        {
            Assert.Throws<ArgumentNullException>(() => new MacroNameStore(null!, new ProfileSessionCache()));
            Assert.Throws<ArgumentNullException>(() => new MacroNameStore(new FakeAppPreferencesStore(), null!));
        }

        /// <summary>
        /// A store over a fake preferences file and a cache holding one fake session per
        /// <paramref name="profiles"/> entry — the two collaborators, and nothing else.
        /// </summary>
        private static MacroNameStore CreateStore(
            IAppPreferencesStore? preferences = null,
            params (int ProfileNumber, KeyboardLayout Layout)[] profiles)
        {
            var sessions = new ProfileSessionCache();

            foreach (var (profileNumber, layout) in profiles)
            {
                sessions.Add(new FakeProfileSession(layout) { ProfileNumber = profileNumber });
            }

            return new MacroNameStore(preferences ?? new FakeAppPreferencesStore(), sessions);
        }

        /// <summary>
        /// A Freestyle Edge RGB layout with one macro in slot 1 of the "1" position, typing one key
        /// so it has content to derive a display name from. Hands back the <b>trigger</b> key code
        /// the site is named by, which is the position's <c>TriggerKey</c> and not its index.
        /// </summary>
        private static KeyboardLayout CreateLayoutWithOneMacro(out int triggerCode)
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            key.SetMacro(1, macro);

            triggerCode = key.TriggerKey.Code;

            return layout;
        }

        private static Macro FindStagedMacro(KeyboardLayout layout)
        {
            return layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].GetMacro(1)
                   ?? throw new InvalidOperationException("The staged macro is expected to be in slot 1.");
        }
    }
}
