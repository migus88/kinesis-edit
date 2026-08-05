using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The macro container of specs/05-key-model.md §1.2 and specs/06-macros.md §1, §4, §5:
    /// trigger metadata, co-triggers and their collision rule, per-family speed/repeat defaults
    /// read from the device catalog, deep copy, and the §1.2 comparison rule.
    /// </summary>
    public class MacroTests
    {
        [Fact]
        public void Constructor_ForABareMacro_LeavesTriggerAndLayerUnassigned()
        {
            var macro = new Macro();

            Assert.Equal(-1, macro.TriggerKey);
            Assert.Equal(-1, macro.LayerIndex);
            Assert.True(macro.IsMultiKey);
            Assert.True(macro.IsEmpty);
            Assert.NotEqual(Guid.Empty, macro.Id);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, 0, 0)]
        [InlineData(DeviceId.FreestyleEdge, 0, 0)]
        [InlineData(DeviceId.FreestylePro, 0, 0)]
        [InlineData(DeviceId.FreestyleEdgeRgb, 5, 1)]
        [InlineData(DeviceId.Tko, 5, 1)]
        [InlineData(DeviceId.Advantage360, 5, 1)]
        public void Constructor_WithADeviceCapability_TakesTheFamilySpeedAndRepeatDefaults(
            DeviceId deviceId,
            int expectedSpeed,
            int expectedRepeat)
        {
            var macro = new Macro(DeviceCatalog.GetById(deviceId).Macros);

            Assert.Equal(expectedSpeed, macro.Speed);
            Assert.Equal(expectedRepeat, macro.RepeatFrequency);
        }

        [Fact]
        public void Constructor_WithACapabilityWithoutRanges_FallsBackToZero()
        {
            var macro = new Macro(DeviceCatalog.GetById(DeviceId.SavantElite2).Macros);

            Assert.Equal(0, macro.Speed);
            Assert.Equal(0, macro.RepeatFrequency);
        }

        [Fact]
        public void AddCoTrigger_BeyondTheDeviceCap_IsNotRefused()
        {
            var macro = new Macro();

            for (var i = 0; i < 5; i++)
            {
                macro.AddCoTrigger(ModelTokens.Key("lshft"));
            }

            Assert.Equal(5, macro.CoTriggerCount);
            Assert.Equal(4, Macro.CoTriggerSlotCount);
        }

        [Fact]
        public void ContainsKeyCode_ForAKeystrokeInTheMacro_IsTrue()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));

            Assert.True(macro.ContainsKeyCode(ModelTokens.Key("a").Code));
            Assert.False(macro.ContainsKeyCode(ModelTokens.Key("b").Code));
        }

        [Fact]
        public void CollidesWith_ForTwoMacrosWithoutCoTriggers_IsTrue()
        {
            Assert.True(new Macro().CollidesWith(new Macro()));
        }

        [Fact]
        public void CollidesWith_ForTheSameCoTriggerSetInAnyOrder_IsTrue()
        {
            var first = new Macro();
            var second = new Macro();

            first.AddCoTrigger(ModelTokens.Key("lshft"));
            first.AddCoTrigger(ModelTokens.Key("lctrl"));
            second.AddCoTrigger(ModelTokens.Key("lctrl"));
            second.AddCoTrigger(ModelTokens.Key("lshft"));

            Assert.True(first.CollidesWith(second));
            Assert.True(second.CollidesWith(first));
        }

        [Fact]
        public void CollidesWith_ForDifferentCoTriggerCounts_IsFalse()
        {
            var first = new Macro();
            var second = new Macro();

            first.AddCoTrigger(ModelTokens.Key("lshft"));
            second.AddCoTrigger(ModelTokens.Key("lshft"));
            second.AddCoTrigger(ModelTokens.Key("lctrl"));

            Assert.False(first.CollidesWith(second));
        }

        [Fact]
        public void CollidesWith_ForDifferentCoTriggers_IsFalse()
        {
            var first = new Macro();
            var second = new Macro();

            first.AddCoTrigger(ModelTokens.Key("lshft"));
            second.AddCoTrigger(ModelTokens.Key("rctrl"));

            Assert.False(first.CollidesWith(second));
            Assert.False(second.CollidesWith(first));
        }

        [Fact]
        public void CollidesWith_ForARepeatedCoTriggerAgainstADistinctPair_IsFalseBothWays()
        {
            var repeated = new Macro();
            var distinct = new Macro();

            repeated.AddCoTrigger(ModelTokens.Key("lctrl"));
            repeated.AddCoTrigger(ModelTokens.Key("lctrl"));
            distinct.AddCoTrigger(ModelTokens.Key("lctrl"));
            distinct.AddCoTrigger(ModelTokens.Key("lshft"));

            // 06 §5 compares the co-trigger *sets*, so the answer cannot depend on which macro is
            // asked: {lctrl, lctrl} does not hold lshft, so the two never collide.
            Assert.False(repeated.CollidesWith(distinct));
            Assert.False(distinct.CollidesWith(repeated));
        }

        [Fact]
        public void AddCoTrigger_ForARepeatedKey_KeepsBothSlots()
        {
            var macro = new Macro();

            // Duplicates are file content and must survive a load/save round trip; 06 §5 counts
            // populated slots, not distinct keys.
            macro.AddCoTrigger(ModelTokens.Key("lctrl"));
            macro.AddCoTrigger(ModelTokens.Key("lctrl"));

            Assert.Equal(2, macro.CoTriggerCount);
        }

        [Fact]
        public void WeightedKeystrokeCount_ForAMacro_CountsOnePerKeystrokeAndTwoPerModifier()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(
                ModelTokens.Key("c"),
                MacroModifiers.LeftShift | MacroModifiers.LeftControl));

            Assert.Equal(1 + 3 + 5, macro.WeightedKeystrokeCount);
        }

        [Fact]
        public void HasBalancedKeyDirections_ForAHangingDown_IsFalse()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d")) { UpDown = KeyDirection.Down });

            Assert.False(macro.HasBalancedKeyDirections());
        }

        [Fact]
        public void HasBalancedKeyDirections_ForAMatchedDownAndUp_IsTrue()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d")) { UpDown = KeyDirection.Down });
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d")) { UpDown = KeyDirection.Up });

            Assert.True(macro.HasBalancedKeyDirections());
        }

        [Fact]
        public void HasBalancedKeyDirections_ForAnUpWithoutADown_IsTrue()
        {
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d")) { UpDown = KeyDirection.Up });

            Assert.True(macro.HasBalancedKeyDirections());
        }

        [Fact]
        public void IsEquivalentTo_ForMacrosWithTheSameKeystrokes_IsTrue()
        {
            var first = CreateWord();
            var second = CreateWord();

            Assert.True(first.IsEquivalentTo(second));
        }

        [Fact]
        public void IsEquivalentTo_ForDifferentKeystrokeCounts_IsFalse()
        {
            var first = CreateWord();
            var second = CreateWord();

            second.RemoveKeystrokeAt(0);

            Assert.False(first.IsEquivalentTo(second));
            Assert.False(first.IsEquivalentTo(null));
        }

        [Fact]
        public void IsEquivalentTo_ForMacrosDifferingOnlyInCoTriggersAndSettings_IsTrue()
        {
            var first = CreateWord();
            var second = CreateWord();

            second.AddCoTrigger(ModelTokens.Key("lshft"));
            second.Speed = 9;

            Assert.True(first.IsEquivalentTo(second));
        }

        [Fact]
        public void Clone_ForAMacro_DeepCopiesKeystrokesAndCarriesTheMetadata()
        {
            var macro = CreateWord();

            macro.TriggerKey = 42;
            macro.LayerIndex = 1;
            macro.MacroIndex = 3;
            macro.Speed = 4;
            macro.RepeatFrequency = 2;
            macro.AddCoTrigger(ModelTokens.Key("lshft"));

            var clone = macro.Clone();

            Assert.Equal(42, clone.TriggerKey);
            Assert.Equal(1, clone.LayerIndex);
            Assert.Equal(3, clone.MacroIndex);
            Assert.Equal(4, clone.Speed);
            Assert.Equal(2, clone.RepeatFrequency);
            Assert.Equal(macro.CoTriggerCount, clone.CoTriggerCount);
            Assert.True(clone.IsEquivalentTo(macro));
            Assert.NotEqual(macro.Id, clone.Id);

            clone.Keystrokes[0].Modifiers = MacroModifiers.LeftShift;

            Assert.NotSame(macro.Keystrokes[0], clone.Keystrokes[0]);
            Assert.Equal(MacroModifiers.None, macro.Keystrokes[0].Modifiers);
        }

        private static Macro CreateWord()
        {
            var macro = new Macro();

            macro.AddKeystrokes(
            [
                new Keystroke(ModelTokens.Key("a")),
                new Keystroke(ModelTokens.Key("b")),
                new Keystroke(ModelTokens.Key("c"))
            ]);

            return macro;
        }
    }
}
