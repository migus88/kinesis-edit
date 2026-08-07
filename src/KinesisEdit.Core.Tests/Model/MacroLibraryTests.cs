using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The library view of a layout's macros: one entry per logical macro, many trigger sites. Two
    /// things are load-bearing and tested here — that both macro stores of specs/06-macros.md §1 are
    /// read through the same walk, and that the load-time reconciliation is lossless: two macros
    /// that share a name but differ in content stay two macros with distinct names, never merged and
    /// never dropped.
    /// </summary>
    public class MacroLibraryTests
    {
        [Fact]
        public void Entries_ForTwoKeysCarryingTheSameNamedMacro_IsOneEntryWithTwoSites()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var first = MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hello", "Sign-off"));
            var second = MacroLibraryFixtures.AssignToSlot(layout, 1, 9, Named(layout, "hello", "sign-off  "), 3);

            var library = new MacroLibrary(layout);

            var entry = Assert.Single(library.Entries);
            Assert.Equal("Sign-off", entry.Name);
            Assert.True(entry.IsExplicitlyNamed);
            Assert.Equal(2, entry.SiteCount);
            Assert.Collection(
                entry.Sites,
                site =>
                {
                    Assert.Equal(0, site.LayerIndex);
                    Assert.Equal(first.TriggerKey.Code, site.TriggerKeyCode);
                    Assert.Equal(1, site.SlotNumber);
                    Assert.Same(first, site.Key);
                    Assert.False(site.IsInFlatList);
                },
                site =>
                {
                    Assert.Equal(1, site.LayerIndex);
                    Assert.Equal(second.TriggerKey.Code, site.TriggerKeyCode);
                    Assert.Equal(3, site.SlotNumber);
                });
        }

        [Fact]
        public void Entries_ForALayerSwitchKey_RecordsTriggerIdentityNotThePosition()
        {
            // 05 §1.3: fn1s and keyt trigger as their *original* action even though the position
            // carries its own token. A site keyed by PositionKey would name "lfn", and the name
            // written to app_settings.txt would never match the one the next load looks up.
            var layout = MacroLibraryFixtures.SlotLayoutWithLayerSwitchKeys();
            var key = MacroLibraryFixtures.FindLayerSwitchKey(layout);
            var macro = Named(layout, "hello", "Layer macro");

            macro.TriggerKey = key.TriggerKey.Code;
            key.SetMacro(1, macro);

            var site = Assert.Single(MacroLibrary.EnumerateSites(layout));

            Assert.Equal(KeyboardKey.Fn1ShiftKeyCode, site.TriggerKeyCode);
            Assert.NotEqual(key.PositionKey.Code, site.TriggerKeyCode);
        }

        [Fact]
        public void Entries_ForTwoMacrosSharingANameWithDifferentContent_KeepsBothAndDisambiguates()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hello", "Greeting"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, Named(layout, "goodbye", "Greeting"));

            var library = new MacroLibrary(layout);

            Assert.Equal(2, library.Entries.Count);
            Assert.Equal("Greeting", library.Entries[0].Name);
            Assert.Equal("Greeting (2)", library.Entries[1].Name);
        }

        [Fact]
        public void Entries_ForAnExplicitlyNamedGroup_StampsTheResolvedNameBackOntoEveryMacro()
        {
            // The disambiguation has to survive the save, or the two macros collapse into one on the
            // next load — which is the "never silently merged" half of the rule.
            var layout = MacroLibraryFixtures.SlotLayout();
            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hello", "Greeting"));
            var second = Named(layout, "goodbye", "Greeting");
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, second);

            _ = new MacroLibrary(layout);

            Assert.Equal("Greeting (2)", second.Name);
        }

        [Fact]
        public void Entries_ForUnnamedMacrosWhoseDerivedNamesCollide_Disambiguates()
        {
            // Both type "hi", but the second also presses Enter — same derived name, different
            // content, so they are two macros.
            var layout = MacroLibraryFixtures.SlotLayout();
            var first = MacroLibraryFixtures.Typing(layout, "hi");
            var second = MacroLibraryFixtures.Typing(layout, "hi");

            second.AddKeystroke(new Keystroke(ModelTokens.Key("ent")));

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, first);
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, second);

            var library = new MacroLibrary(layout);

            Assert.Equal(2, library.Entries.Count);
            Assert.Equal("hi", library.Entries[0].Name);
            Assert.Equal("hi (2)", library.Entries[1].Name);
            Assert.All(library.Entries, entry => Assert.False(entry.IsExplicitlyNamed));
        }

        [Fact]
        public void Entries_ForAnUnnamedGroup_LeavesTheStoredNameEmpty()
        {
            // A derived name is recomputed every load and must never reach app_settings.txt.
            var layout = MacroLibraryFixtures.SlotLayout();
            var macro = MacroLibraryFixtures.Typing(layout, "hi");

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, macro);

            var library = new MacroLibrary(layout);

            Assert.Equal("hi", Assert.Single(library.Entries).Name);
            Assert.Equal(string.Empty, macro.Name);
            Assert.Empty(library.EnumerateStoredNames());
        }

        [Fact]
        public void Entries_ForANamedAndAnUnnamedMacroWithTheSameContent_KeepsThemApart()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hi", "hi"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, MacroLibraryFixtures.Typing(layout, "hi"));

            var library = new MacroLibrary(layout);

            Assert.Equal(2, library.Entries.Count);
            Assert.True(library.Entries[0].IsExplicitlyNamed);
            Assert.False(library.Entries[1].IsExplicitlyNamed);
            Assert.Equal("hi (2)", library.Entries[1].Name);
        }

        [Fact]
        public void Entries_ForTheFlatListDevice_ReadsTheFlatStore()
        {
            var layout = MacroLibraryFixtures.FlatListLayout();
            var key = MacroLibraryFixtures.AddToFlatList(layout, 0, 4, Named(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);

            Assert.True(library.UsesFlatMacroList);

            var entry = Assert.Single(library.Entries);
            var site = Assert.Single(entry.Sites);

            Assert.Equal(MacroLibrary.FlatListSlot, site.SlotNumber);
            Assert.True(site.IsInFlatList);
            Assert.Null(site.Key);
            Assert.Equal(key.TriggerKey.Code, site.TriggerKeyCode);
        }

        [Fact]
        public void EnumerateSites_CoversEveryMacroTheLayoutEnumerates()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "one"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "two"), 2);
            MacroLibraryFixtures.AssignToSlot(layout, 1, 7, MacroLibraryFixtures.Typing(layout, "three"));

            var sites = MacroLibrary.EnumerateSites(layout);

            Assert.Equal(layout.MacroCount, sites.Count);
            Assert.Equal(
                layout.EnumerateMacros().ToHashSet(),
                sites.Select(site => site.Macro).ToHashSet());
        }

        [Fact]
        public void EnumerateSites_IsDeterministic()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 1, 7, MacroLibraryFixtures.Typing(layout, "three"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "one"));

            Assert.Equal(MacroLibrary.EnumerateSites(layout), MacroLibrary.EnumerateSites(layout));
        }

        [Fact]
        public void ApplyNames_StampsStoredNamesOntoAFreshlyParsedLayout()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var named = MacroLibraryFixtures.Typing(layout, "hello");
            var unnamed = MacroLibraryFixtures.Typing(layout, "goodbye");

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, named);
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, unnamed);

            MacroLibrary.ApplyNames(
                layout,
                site => ReferenceEquals(site.Macro, named) ? "  Sign-off  " : null);

            Assert.Equal("Sign-off", named.Name);
            Assert.Equal(string.Empty, unnamed.Name);

            var library = new MacroLibrary(layout);

            Assert.Equal("Sign-off", library.Entries[0].Name);
            Assert.True(library.Entries[0].IsExplicitlyNamed);
            Assert.False(library.Entries[1].IsExplicitlyNamed);
        }

        [Fact]
        public void EnumerateStoredNames_ReturnsOneEntryPerSiteOfANamedMacroOnly()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hello", "Sign-off"));
            MacroLibraryFixtures.AssignToSlot(layout, 1, 9, Named(layout, "hello", "Sign-off"), 3);
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, MacroLibraryFixtures.Typing(layout, "plain"));

            var stored = new MacroLibrary(layout).EnumerateStoredNames();

            Assert.Equal(2, stored.Count);
            Assert.All(stored, pair => Assert.Equal("Sign-off", pair.Value));
        }

        [Fact]
        public void Find_MatchesTrimmedAndCaseInsensitively()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);

            Assert.NotNull(library.Find("  sign-OFF "));
            Assert.Null(library.Find("nothing"));
        }

        [Fact]
        public void FindByMacro_ReturnsTheEntryHoldingTheInstance()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var macro = Named(layout, "hello", "Sign-off");

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, macro);

            var library = new MacroLibrary(layout);

            Assert.Same(library.Entries[0], library.FindByMacro(macro));
            Assert.Null(library.FindByMacro(MacroLibraryFixtures.Typing(layout, "other")));
        }

        [Fact]
        public void CreateUniqueName_StepsPastNamesTheLibraryAlreadyHolds()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, Named(layout, "hello", "Sign-off"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, Named(layout, "goodbye", "Sign-off (2)"));

            var library = new MacroLibrary(layout);

            Assert.Equal("Sign-off (3)", library.CreateUniqueName("Sign-off"));
            Assert.Equal("Fresh", library.CreateUniqueName("Fresh"));
        }

        [Fact]
        public void Constructor_WithNullLayout_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MacroLibrary(null!));
        }

        private static Macro Named(KeyboardLayout layout, string typedText, string name)
        {
            return MacroLibraryFixtures.Typing(layout, typedText, name);
        }
    }
}
