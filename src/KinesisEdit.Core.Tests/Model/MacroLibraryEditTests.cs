using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The library's edits — rename, propagate, delete, assign to a further key, duplicate — on both
    /// macro stores of specs/06-macros.md §1. The rule under test throughout is that a name is one
    /// logical macro with many trigger sites: an edit on one site reaches all of them, a delete
    /// removes all of them, and an assignment adds one.
    /// </summary>
    public class MacroLibraryEditTests
    {
        [Fact]
        public void Rename_RenamesEverySiteAndSanitizes()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var first = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, first);
            MacroLibraryFixtures.AssignToSlot(layout, 1, 9, second, 3);

            var library = new MacroLibrary(layout);

            var renamed = library.Rename(library.Entries[0], "  Closing\nblock  ");

            Assert.Equal("Closing block", renamed.Name);
            Assert.Equal("Closing block", first.Name);
            Assert.Equal("Closing block", second.Name);
            Assert.Equal(2, renamed.SiteCount);
        }

        [Fact]
        public void Rename_ToBlank_ClearsTheStoredNameAndFallsBackToADerivedOne()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var macro = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, macro);

            var library = new MacroLibrary(layout);

            var renamed = library.Rename(library.Entries[0], "   ");

            Assert.Equal(string.Empty, macro.Name);
            Assert.False(renamed.IsExplicitlyNamed);
            Assert.Equal("hello", renamed.Name);
            Assert.Empty(library.EnumerateStoredNames());
        }

        [Fact]
        public void Rename_OntoTheNameOfAnEquivalentMacro_MergesTheTwoEntries()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, MacroLibraryFixtures.Typing(layout, "hello", "Other"));

            var library = new MacroLibrary(layout);

            Assert.Equal(2, library.Entries.Count);

            var merged = library.Rename(library.Find("Other")!, "Sign-off");

            Assert.Single(library.Entries);
            Assert.Equal("Sign-off", merged.Name);
            Assert.Equal(2, merged.SiteCount);
        }

        [Fact]
        public void Rename_OntoTheNameOfADifferentMacro_Disambiguates()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, MacroLibraryFixtures.Typing(layout, "goodbye", "Other"));

            var library = new MacroLibrary(layout);

            var renamed = library.Rename(library.Find("Other")!, "Sign-off");

            Assert.Equal(2, library.Entries.Count);
            Assert.Equal("Sign-off (2)", renamed.Name);
        }

        [Fact]
        public void Rename_WithAStaleEntry_Throws()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);
            var stale = library.Entries[0];

            library.Rename(stale, "Renamed");

            Assert.Throws<ArgumentException>(() => library.Rename(stale, "Again"));
        }

        [Fact]
        public void Propagate_RewritesEverySiteFromTheCanonical()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var first = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, first);
            MacroLibraryFixtures.AssignToSlot(layout, 1, 9, second, 3);

            var library = new MacroLibrary(layout);

            first.AddKeystroke(new Keystroke(ModelTokens.Key("ent")));
            first.Speed = 7;
            first.RepeatFrequency = 4;
            first.AddCoTrigger(ModelTokens.Key("lshft"));

            var propagated = library.Propagate(library.Entries[0]);

            Assert.True(first.IsEquivalentTo(second));
            Assert.Equal(7, second.Speed);
            Assert.Equal(4, second.RepeatFrequency);
            Assert.Equal(1, second.CoTriggerCount);
            Assert.Equal(2, propagated.SiteCount);

            // Copies, never shared instances (05 §1.5).
            Assert.NotSame(first.Keystrokes[0], second.Keystrokes[0]);

            // The site identity is where the macro sits, not what it is, so it is left alone.
            Assert.Equal(1, second.LayerIndex);
            Assert.Equal(3, second.MacroIndex);
        }

        [Fact]
        public void Delete_RemovesEverySiteOfTheEntry()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var first = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");
            var other = MacroLibraryFixtures.Typing(layout, "keepme", "Keep");

            var firstKey = MacroLibraryFixtures.AssignToSlot(layout, 0, 5, first);
            var secondKey = MacroLibraryFixtures.AssignToSlot(layout, 1, 9, second, 3);
            MacroLibraryFixtures.AssignToSlot(layout, 0, 6, other);

            var library = new MacroLibrary(layout);

            library.Delete(library.Find("Sign-off")!);

            Assert.Null(firstKey.GetMacro(1));
            Assert.Null(secondKey.GetMacro(3));
            Assert.Equal("Keep", Assert.Single(library.Entries).Name);
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void Delete_OnTheFlatListDevice_TakesTheMacroOutOfTheFlatList()
        {
            var layout = MacroLibraryFixtures.FlatListLayout();

            MacroLibraryFixtures.AddToFlatList(layout, 0, 4, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));
            MacroLibraryFixtures.AddToFlatList(layout, 1, 6, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);

            Assert.Equal(2, Assert.Single(library.Entries).SiteCount);

            library.Delete(library.Entries[0]);

            Assert.Empty(layout.Macros);
            Assert.Empty(library.Entries);
        }

        [Fact]
        public void AssignTo_ASecondKey_AddsASiteCarryingTheCanonicalContentAndName()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var macro = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");

            macro.Speed = 6;
            macro.AddCoTrigger(ModelTokens.Key("lshft"));
            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, macro);

            var library = new MacroLibrary(layout);
            var target = layout.Layers[1].Keys[11];

            var entry = library.AssignTo(library.Entries[0], target);

            Assert.NotNull(entry);
            Assert.Equal(2, entry.SiteCount);

            var copy = target.GetMacro(1);

            Assert.NotNull(copy);
            Assert.NotSame(macro, copy);
            Assert.Equal("Sign-off", copy.Name);
            Assert.Equal(6, copy.Speed);
            Assert.Equal(1, copy.CoTriggerCount);
            Assert.True(macro.IsEquivalentTo(copy));
            Assert.Equal(1, copy.LayerIndex);
            Assert.Equal(target.TriggerKey.Code, copy.TriggerKey);
            Assert.Equal(1, copy.MacroIndex);
        }

        [Fact]
        public void AssignTo_AnExplicitSlot_UsesThatSlot()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);
            var target = layout.Layers[0].Keys[11];

            var entry = library.AssignTo(library.Entries[0], target, 4);

            Assert.NotNull(entry);
            Assert.NotNull(target.GetMacro(4));
            Assert.Equal(4, target.GetMacro(4)!.MacroIndex);
        }

        [Fact]
        public void AssignTo_AnOccupiedSlot_IsRefused()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);
            var target = layout.Layers[0].Keys[5];

            Assert.Null(library.AssignTo(library.Entries[0], target, 1));
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void AssignTo_APositionThatRejectsMacros_IsRefused()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);
            var target = layout.Layers[0].Keys.First(key => !key.CanAssignMacro);

            Assert.Null(library.AssignTo(library.Entries[0], target));
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void AssignTo_AKeyOutsideThisLayout_IsRefused()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var other = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);

            Assert.Null(library.AssignTo(library.Entries[0], other.Layers[0].Keys[11]));
        }

        [Fact]
        public void AssignTo_OnTheFlatListDevice_AppendsToTheFlatList()
        {
            var layout = MacroLibraryFixtures.FlatListLayout();

            MacroLibraryFixtures.AddToFlatList(layout, 0, 4, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);
            var target = layout.Layers[2].Keys[9];

            var entry = library.AssignTo(library.Entries[0], target);

            Assert.NotNull(entry);
            Assert.Equal(2, layout.Macros.Count);

            var copy = layout.Macros[1];

            Assert.Equal(2, copy.LayerIndex);
            Assert.Equal(target.TriggerKey.Code, copy.TriggerKey);
            Assert.Equal(MacroLibrary.FlatListSlot, copy.MacroIndex);
        }

        [Fact]
        public void AssignTo_OnTheFlatListDeviceWithASlot_IsRefused()
        {
            var layout = MacroLibraryFixtures.FlatListLayout();

            MacroLibraryFixtures.AddToFlatList(layout, 0, 4, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);

            Assert.Null(library.AssignTo(library.Entries[0], layout.Layers[2].Keys[9], 2));
            Assert.Single(layout.Macros);
        }

        [Fact]
        public void Duplicate_CreatesASecondMacroWithItsOwnName()
        {
            var layout = MacroLibraryFixtures.SlotLayout();
            var macro = MacroLibraryFixtures.Typing(layout, "hello", "Sign-off");

            var key = MacroLibraryFixtures.AssignToSlot(layout, 0, 5, macro);

            var library = new MacroLibrary(layout);

            var duplicate = library.Duplicate(library.Entries[0]);

            Assert.NotNull(duplicate);
            Assert.Equal("Sign-off (2)", duplicate.Name);
            Assert.True(duplicate.IsExplicitlyNamed);
            Assert.Equal(2, library.Entries.Count);
            Assert.NotNull(key.GetMacro(2));
            Assert.True(macro.IsEquivalentTo(key.GetMacro(2)));
            Assert.NotSame(macro, key.GetMacro(2));
        }

        [Fact]
        public void Duplicate_OfAnUnnamedMacro_NamesTheCopySoItDoesNotFoldBackIn()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            MacroLibraryFixtures.AssignToSlot(layout, 0, 5, MacroLibraryFixtures.Typing(layout, "hello"));

            var library = new MacroLibrary(layout);

            var duplicate = library.Duplicate(library.Entries[0]);

            Assert.NotNull(duplicate);
            Assert.Equal(2, library.Entries.Count);
            Assert.True(duplicate.IsExplicitlyNamed);
            Assert.Equal("hello (2)", duplicate.Name);
        }

        [Fact]
        public void Duplicate_WithEverySlotFull_IsRefused()
        {
            var layout = MacroLibraryFixtures.SlotLayout();

            for (var slot = 1; slot <= Macro.MaxMacroIndex; slot++)
            {
                MacroLibraryFixtures.AssignToSlot(
                    layout,
                    0,
                    5,
                    MacroLibraryFixtures.Typing(layout, "hello", "Sign-off " + slot),
                    slot);
            }

            var library = new MacroLibrary(layout);

            Assert.Null(library.Duplicate(library.Find("Sign-off 1")!));
            Assert.Equal(5, layout.MacroCount);
        }

        [Fact]
        public void Duplicate_OnTheFlatListDevice_AppendsAnIndependentCopy()
        {
            var layout = MacroLibraryFixtures.FlatListLayout();

            MacroLibraryFixtures.AddToFlatList(layout, 0, 4, MacroLibraryFixtures.Typing(layout, "hello", "Sign-off"));

            var library = new MacroLibrary(layout);

            var duplicate = library.Duplicate(library.Entries[0]);

            Assert.NotNull(duplicate);
            Assert.Equal(2, layout.Macros.Count);
            Assert.Equal("Sign-off (2)", layout.Macros[1].Name);
            Assert.Equal(layout.Macros[0].TriggerKey, layout.Macros[1].TriggerKey);
        }
    }
}
