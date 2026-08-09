using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// Putting a macro on a key and taking one off again, on both macro stores of
    /// specs/06-macros.md §1. Two rules run through every case: a copy is an <b>independent</b>
    /// macro (05 §1.5), and everything a user can reach by clicking — a full key, an occupied slot,
    /// a position that rejects macros, a key from another layout — is a refusal, not an exception.
    /// </summary>
    public class MacroPlacementTests
    {
        [Fact]
        public void CopyTo_ASecondKey_LandsInItsFirstEmptySlotWithTheContentAndName()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            macro.Speed = 6;
            macro.AddCoTrigger(ModelTokens.Key("lshft"));
            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            var target = layout.Layers[1].Keys[11];

            var copy = MacroPlacement.CopyTo(layout, macro, target);

            Assert.NotNull(copy);
            Assert.Same(copy, target.GetMacro(1));
            Assert.NotSame(macro, copy);
            Assert.Equal("Sign-off", copy.Name);
            Assert.Equal(6, copy.Speed);
            Assert.Equal(1, copy.CoTriggerCount);
            Assert.True(macro.IsEquivalentTo(copy));

            // The copy sits where it landed, not where it came from.
            Assert.Equal(1, copy.LayerIndex);
            Assert.Equal(target.TriggerKey.Code, copy.TriggerKey);
            Assert.Equal(1, copy.MacroIndex);
        }

        [Fact]
        public void CopyTo_ProducesAClone_SoEditingItLeavesTheSourceAlone()
        {
            // There is no shared macro on disk (06 §1). This is the whole reason the macro library
            // went: a copy that stayed linked would be a fiction the hardware does not have.
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            var copy = MacroPlacement.CopyTo(layout, macro, layout.Layers[0].Keys[11]);

            Assert.NotNull(copy);

            copy.Name = "Renamed";
            copy.Speed = 9;
            copy.AddKeystroke(new Keystroke(ModelTokens.Key("ent")));

            Assert.Equal("Sign-off", macro.Name);
            Assert.NotEqual(9, macro.Speed);
            Assert.NotEqual(copy.Keystrokes.Count, macro.Keystrokes.Count);
            Assert.NotSame(macro.Keystrokes[0], copy.Keystrokes[0]);
        }

        [Fact]
        public void CopyTo_BackOntoTheSourceKey_DuplicatesIntoItsNextEmptySlot()
        {
            // Copying a macro onto its own key is not a no-op the way a whole-key copy is: it
            // writes a second, independent macro on the same trigger.
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            var key = MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            var copy = MacroPlacement.CopyTo(layout, macro, key);

            Assert.NotNull(copy);
            Assert.Same(copy, key.GetMacro(2));
            Assert.NotSame(macro, copy);
            Assert.Equal(2, layout.MacroCount);
        }

        [Fact]
        public void CopyTo_AnExplicitSlot_UsesThatSlot()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            var target = layout.Layers[0].Keys[11];

            var copy = MacroPlacement.CopyTo(layout, macro, target, 4);

            Assert.NotNull(copy);
            Assert.Same(copy, target.GetMacro(4));
            Assert.Equal(4, copy.MacroIndex);
        }

        [Fact]
        public void CopyTo_AnOccupiedSlot_IsRefused()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            Assert.Null(MacroPlacement.CopyTo(layout, macro, layout.Layers[0].Keys[5], 1));
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void CopyTo_AKeyWithEverySlotFull_IsRefused()
        {
            var layout = MacroSiteFixtures.SlotLayout();

            for (var slot = Macro.MinMacroIndex; slot <= Macro.MaxMacroIndex; slot++)
            {
                MacroSiteFixtures.AssignToSlot(
                    layout,
                    0,
                    5,
                    MacroSiteFixtures.Typing(layout, "hello", "Sign-off " + slot),
                    slot);
            }

            var full = layout.Layers[0].Keys[5];

            Assert.Null(MacroPlacement.CopyTo(layout, full.GetMacro(1)!, full));
            Assert.Equal(Macro.MaxMacroIndex, layout.MacroCount);
        }

        [Fact]
        public void CopyTo_APositionThatRejectsMacros_IsRefused()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            var restricted = layout.Layers[0].Keys.First(key => !key.CanAssignMacro);

            Assert.Null(MacroPlacement.CopyTo(layout, macro, restricted));
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void CopyTo_AKeyOutsideThisLayout_IsRefused()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var other = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            Assert.Null(MacroPlacement.CopyTo(layout, macro, other.Layers[0].Keys[11]));
            Assert.Equal(0, other.MacroCount);
        }

        [Theory]
        [InlineData(MacroSites.FlatListSlot)]
        [InlineData(Macro.MaxMacroIndex + 1)]
        [InlineData(-2)]
        public void CopyTo_WithASlotThisDeviceDoesNotHave_IsRefused(int slot)
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            Assert.Null(MacroPlacement.CopyTo(layout, macro, layout.Layers[0].Keys[11], slot));
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void CopyTo_OnTheFlatListDevice_AppendsToTheFlatList()
        {
            var layout = MacroSiteFixtures.FlatListLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AddToFlatList(layout, 0, 4, macro);

            var target = layout.Layers[2].Keys[9];

            var copy = MacroPlacement.CopyTo(layout, macro, target);

            Assert.NotNull(copy);
            Assert.Equal(2, layout.Macros.Count);
            Assert.Same(copy, layout.Macros[1]);
            Assert.Equal(2, copy.LayerIndex);
            Assert.Equal(target.TriggerKey.Code, copy.TriggerKey);
            Assert.Equal(MacroSites.FlatListSlot, copy.MacroIndex);
        }

        [Fact]
        public void CopyTo_OnTheFlatListDeviceWithAPerKeySlot_IsRefused()
        {
            var layout = MacroSiteFixtures.FlatListLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AddToFlatList(layout, 0, 4, macro);

            Assert.Null(MacroPlacement.CopyTo(layout, macro, layout.Layers[2].Keys[9], 2));
            Assert.Single(layout.Macros);
        }

        [Fact]
        public void Remove_ClearsOnlyTheNamedSlot()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var first = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var elsewhere = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            var key = MacroSiteFixtures.AssignToSlot(layout, 0, 5, first);

            MacroSiteFixtures.AssignToSlot(layout, 0, 5, second, 2);

            var otherKey = MacroSiteFixtures.AssignToSlot(layout, 1, 9, elsewhere, 3);

            Assert.True(MacroPlacement.Remove(layout, key, first, 1));

            Assert.Null(key.GetMacro(1));
            Assert.Same(second, key.GetMacro(2));
            Assert.Same(elsewhere, otherKey.GetMacro(3));
            Assert.Equal(2, layout.MacroCount);
        }

        [Fact]
        public void Remove_ForASlotHoldingSomethingElse_RemovesNothing()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var other = MacroSiteFixtures.Typing(layout, "goodbye", "Farewell");

            var key = MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            Assert.False(MacroPlacement.Remove(layout, key, other, 1));
            Assert.Same(macro, key.GetMacro(1));
        }

        [Fact]
        public void Remove_OnTheFlatListDevice_TakesTheMacroOutOfTheFlatList()
        {
            var layout = MacroSiteFixtures.FlatListLayout();
            var first = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var second = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");

            MacroSiteFixtures.AddToFlatList(layout, 0, 4, first);
            MacroSiteFixtures.AddToFlatList(layout, 1, 6, second);

            Assert.True(MacroPlacement.Remove(layout, null, first, MacroSites.FlatListSlot));

            Assert.Same(second, Assert.Single(layout.Macros));
            Assert.False(MacroPlacement.Remove(layout, null, first, MacroSites.FlatListSlot));
        }

        [Fact]
        public void NullArguments_Throw()
        {
            var layout = MacroSiteFixtures.SlotLayout();
            var macro = MacroSiteFixtures.Typing(layout, "hello", "Sign-off");
            var key = MacroSiteFixtures.AssignToSlot(layout, 0, 5, macro);

            Assert.Throws<ArgumentNullException>(() => MacroPlacement.CopyTo(null!, macro, key));
            Assert.Throws<ArgumentNullException>(() => MacroPlacement.CopyTo(layout, null!, key));
            Assert.Throws<ArgumentNullException>(() => MacroPlacement.CopyTo(layout, macro, null!));
            Assert.Throws<ArgumentNullException>(() => MacroPlacement.Remove(null!, key, macro, 1));
            Assert.Throws<ArgumentNullException>(() => MacroPlacement.Remove(layout, key, null!, 1));
        }
    }
}
