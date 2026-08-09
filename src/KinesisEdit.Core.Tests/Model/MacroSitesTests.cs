using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The site walk and the two name steps built on it. Three things are load-bearing and tested
    /// here — that both macro stores of specs/06-macros.md §1 are read through the same walk, that a
    /// site records <b>trigger</b> identity rather than the position (05 §1.3), and that names are
    /// stamped and harvested per site with no grouping: two keys carrying the same name are two
    /// macros in two places.
    /// </summary>
    public class MacroSitesTests
    {
        [Fact]
        public void Enumerate_CoversEveryMacroTheLayoutEnumerates()
        {
            var layout = MacroSiteFixtures.SlotLayout();

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, MacroSiteFixtures.Typing(layout, "one"));
            MacroSiteFixtures.AssignToSlot(layout, 0, 5, MacroSiteFixtures.Typing(layout, "two"), 2);
            MacroSiteFixtures.AssignToSlot(layout, 1, 7, MacroSiteFixtures.Typing(layout, "three"));

            var sites = MacroSites.Enumerate(layout);

            Assert.Equal(layout.MacroCount, sites.Count);
            Assert.Equal(
                layout.EnumerateMacros().ToHashSet(),
                sites.Select(site => site.Macro).ToHashSet());
        }

        [Fact]
        public void Enumerate_IsDeterministic()
        {
            var layout = MacroSiteFixtures.SlotLayout();

            MacroSiteFixtures.AssignToSlot(layout, 1, 7, MacroSiteFixtures.Typing(layout, "three"));
            MacroSiteFixtures.AssignToSlot(layout, 0, 5, MacroSiteFixtures.Typing(layout, "one"));

            Assert.Equal(MacroSites.Enumerate(layout), MacroSites.Enumerate(layout));
        }

        [Fact]
        public void Enumerate_ForALayerSwitchKey_RecordsTriggerIdentityNotThePosition()
        {
            // 05 §1.3: fn1s and keyt trigger as their *original* action even though the position
            // carries its own token. A site keyed by PositionKey would name "lfn", and the name
            // written to app_settings.txt would never match the one the next load looks up.
            var layout = MacroSiteFixtures.SlotLayoutWithLayerSwitchKeys();
            var key = MacroSiteFixtures.FindLayerSwitchKey(layout);
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Layer macro");

            macro.TriggerKey = key.TriggerKey.Code;
            key.SetMacro(1, macro);

            var site = Assert.Single(MacroSites.Enumerate(layout));

            Assert.Equal(KeyboardKey.Fn1ShiftKeyCode, site.TriggerKeyCode);
            Assert.NotEqual(key.PositionKey.Code, site.TriggerKeyCode);
        }

        [Fact]
        public void Enumerate_ForTheFlatListDevice_ReadsTheFlatStore()
        {
            var layout = MacroSiteFixtures.FlatListLayout();
            var key = MacroSiteFixtures.AddToFlatList(
                layout,
                0,
                4,
                MacroSiteFixtures.Typing(layout, "hello", "Sign-off"));

            var site = Assert.Single(MacroSites.Enumerate(layout));

            Assert.Equal(MacroSites.FlatListSlot, site.SlotNumber);
            Assert.True(site.IsInFlatList);
            Assert.Null(site.Key);
            Assert.Equal(0, site.LayerIndex);
            Assert.Equal(key.TriggerKey.Code, site.TriggerKeyCode);
        }

        [Fact]
        public void Enumerate_OnASlotDeviceWithNoFlatList_YieldsOnlyTheSlots()
        {
            // No UsesFlatMacroList branch is needed: an empty flat list simply contributes nothing.
            var layout = MacroSiteFixtures.SlotLayout();

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, MacroSiteFixtures.Typing(layout, "one"));

            Assert.Empty(layout.Macros);
            Assert.All(MacroSites.Enumerate(layout), site => Assert.False(site.IsInFlatList));
        }

        [Fact]
        public void ApplyNames_StampsStoredNamesOntoAFreshlyParsedLayout()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var named = MacroSiteFixtures.Typing(layout, "hello");
            var unnamed = MacroSiteFixtures.Typing(layout, "goodbye");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, named);
            MacroSiteFixtures.AssignToSlot(layout, 0, 6, unnamed);

            MacroSites.ApplyNames(
                layout,
                site => ReferenceEquals(site.Macro, named) ? "  Sign-off  " : null);

            Assert.Equal("Sign-off", named.Name);
            Assert.Equal(string.Empty, unnamed.Name);
        }

        [Fact]
        public void ApplyNames_ForASiteWithNoStoredName_ClearsTheNameItAlreadyCarried()
        {
            // The write is unconditional, which is what makes the stamp a faithful picture of the
            // file — and why re-running it over a layout the user has been editing wipes unsaved
            // renames. Pinned here so nobody "optimises" it into a null-skipping write.
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Typed by hand");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            MacroSites.ApplyNames(layout, _ => null);

            Assert.Equal(string.Empty, macro.Name);
        }

        [Fact]
        public void EnumerateStoredNames_EmitsOnePairPerNamedSite()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var first = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroSiteFixtures.Typing(layout, "goodbye", "Farewell");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, first);
            MacroSiteFixtures.AssignToSlot(layout, 1, 9, second, 3);

            var stored = MacroSites.EnumerateStoredNames(layout);

            Assert.Collection(
                stored,
                pair =>
                {
                    Assert.Same(first, pair.Key.Macro);
                    Assert.Equal(1, pair.Key.SlotNumber);
                    Assert.Equal("Sign-off", pair.Value);
                },
                pair =>
                {
                    Assert.Same(second, pair.Key.Macro);
                    Assert.Equal(3, pair.Key.SlotNumber);
                    Assert.Equal("Farewell", pair.Value);
                });
        }

        [Fact]
        public void EnumerateStoredNames_ForAnUnnamedMacro_EmitsNothing()
        {
            // A derived display name is recomputed every load and must never reach app_settings.txt.
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hi");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            Assert.Equal(string.Empty, macro.Name);
            Assert.Empty(MacroSites.EnumerateStoredNames(layout));
        }

        [Fact]
        public void EnumerateStoredNames_ForTwoKeysCarryingTheSameName_EmitsTwoIndependentPairs()
        {
            // There is no shared macro on disk (06 §1): the same name in two places is two settings
            // keys, and nothing here folds them into one.
            var layout = MacroSiteFixtures.SlotLayout();
            var first = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            var firstKey = MacroSiteFixtures.AssignToSlot(layout, 0, 5, first);
            var secondKey = MacroSiteFixtures.AssignToSlot(layout, 1, 9, second, 3);

            var stored = MacroSites.EnumerateStoredNames(layout);

            Assert.Equal(2, stored.Count);
            Assert.All(stored, pair => Assert.Equal("Sign-off", pair.Value));
            Assert.Equal(firstKey.TriggerKey.Code, stored[0].Key.TriggerKeyCode);
            Assert.Equal(secondKey.TriggerKey.Code, stored[1].Key.TriggerKeyCode);
            Assert.NotSame(stored[0].Key.Macro, stored[1].Key.Macro);
        }

        [Fact]
        public void EnumerateStoredNames_OnTheFlatListDevice_KeysThePairByTheFlatListSlot()
        {
            var layout = MacroSiteFixtures.FlatListLayout();

            MacroSiteFixtures.AddToFlatList(layout, 0, 4, MacroSiteFixtures.Typing(layout, "hello", "Sign-off"));

            var pair = Assert.Single(MacroSites.EnumerateStoredNames(layout));

            Assert.Equal(MacroSites.FlatListSlot, pair.Key.SlotNumber);
            Assert.Equal("Sign-off", pair.Value);
        }

        [Fact]
        public void NullArguments_Throw()
        {
            var layout = MacroSiteFixtures.SlotLayout();

            Assert.Throws<ArgumentNullException>(() => MacroSites.Enumerate(null!));
            Assert.Throws<ArgumentNullException>(() => MacroSites.EnumerateStoredNames(null!));
            Assert.Throws<ArgumentNullException>(() => MacroSites.ApplyNames(null!, _ => null));
            Assert.Throws<ArgumentNullException>(() => MacroSites.ApplyNames(layout, null!));
        }
    }
}
