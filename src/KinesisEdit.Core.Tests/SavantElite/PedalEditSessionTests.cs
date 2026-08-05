using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// The edit buffer of specs/12-savant-elite.md §5: Configure loads the current entry, Single
    /// Action / Multiple Actions clears it on a real switch, typing replaces or appends, Backspace
    /// removes one *display item*, Clear empties, Done commits and Cancel is simply not calling
    /// Done — the session owns a private copy and touches the <see cref="PedalInput"/> only in
    /// <see cref="PedalEditSession.ApplyTo"/>.
    /// </summary>
    public sealed class PedalEditSessionTests
    {
        [Fact]
        public void BeginFor_WithAnUnassignedInput_StartsAsAnEmptySingleActionEntry()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            Assert.Equal(PedalInputId.LeftPedal, session.Input);
            Assert.Equal(PedalInputMode.Single, session.Mode);
            Assert.True(session.IsEmpty);
            Assert.Null(session.SingleAction);
            Assert.Null(session.Macro);
            Assert.Empty(session.DisplayItems);
            Assert.False(session.HoldWinModifier);
        }

        [Fact]
        public void BeginFor_WithASingleAction_LoadsIt()
        {
            var input = new PedalInput(PedalInputId.Jack1);

            input.SetSingleAction(PedalFixtures.Key("lmouse"));

            var session = PedalEditSession.BeginFor(input);

            Assert.Equal(PedalInputMode.Single, session.Mode);
            Assert.Equal(PedalFixtures.Key("lmouse"), session.SingleAction);
            Assert.False(session.IsEmpty);
        }

        [Fact]
        public void BeginFor_WithAMacro_LoadsItInMacroMode()
        {
            var input = MacroInput("a", "b");
            var session = PedalEditSession.BeginFor(input);

            Assert.Equal(PedalInputMode.Macro, session.Mode);
            Assert.Equal(new[] { "a", "b" }, session.DisplayItems);
        }

        [Fact]
        public void BeginFor_WithAMacro_ClonesItSoTheInputIsUntouchedUntilDone()
        {
            var input = MacroInput("a");
            var session = PedalEditSession.BeginFor(input);

            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);

            Assert.Single(input.Macro!.Keystrokes);
            Assert.Equal(2, session.Macro!.Keystrokes.Count);
        }

        [Fact]
        public void SetMode_WithADifferentMode_ClearsTheEntry()
        {
            // 12 §5 step 2: "switching modes clears the current entry".
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.SetMode(PedalInputMode.Macro);

            Assert.Equal(PedalInputMode.Macro, session.Mode);
            Assert.True(session.IsEmpty);
            Assert.Empty(session.Macro!.Keystrokes);
            Assert.Null(session.SingleAction);
        }

        [Fact]
        public void SetMode_WithTheModeAlreadyInEffect_KeepsTheEntry()
        {
            // Re-clicking the latched Single Action button must not destroy what was typed.
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.SetMode(PedalInputMode.Macro);

            Assert.Equal(new[] { "a" }, session.DisplayItems);
        }

        [Fact]
        public void SetMode_WithNone_Throws()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            Assert.Throws<ArgumentOutOfRangeException>(() => session.SetMode(PedalInputMode.None));
        }

        [Fact]
        public void AddKeystroke_InSingleMode_ReplacesTheEntryAndDropsModifiers()
        {
            // 12 §4.3: a single-action line is one bare token — there is nowhere to put a modifier.
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.Control | MacroModifiers.Shift);

            Assert.Equal(PedalFixtures.Key("b"), session.SingleAction);
            Assert.Equal(new[] { "[b]" }, session.DisplayItems);
        }

        [Fact]
        public void AddKeystroke_InMacroMode_AppendsWithItsModifiers()
        {
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.Control);

            Assert.Equal(new[] { "a", "{ctrl+b}" }, session.DisplayItems);
        }

        [Fact]
        public void AddKeystroke_WithNullKey_Throws()
        {
            var session = MacroSession();

            Assert.Throws<ArgumentNullException>(() => session.AddKeystroke(null!, MacroModifiers.None));
        }

        [Fact]
        public void AddKeystroke_WhileTheWinModifierIsHeld_AddsItToEveryKeystroke()
        {
            var session = MacroSession();

            session.Apply(HoldWin);
            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.Shift);

            Assert.Equal(MacroModifiers.LeftWin, session.Macro!.Keystrokes[0].Modifiers);
            Assert.Equal(MacroModifiers.Shift | MacroModifiers.LeftWin, session.Macro.Keystrokes[1].Modifiers);
        }

        [Fact]
        public void AddKeystroke_WithABareModifierWhileTheWinModifierIsHeld_LeavesItUnmodified()
        {
            // A key that is itself a modifier cannot carry a modifier string (Keystroke.Modifiers
            // normalizes one away), so the sticky Win must not be overlaid on it: {-win}{lshift}
            // is not a shape §4.4 can express, and the session states that intent rather than
            // relying on the model to undo it.
            var session = MacroSession();

            session.Apply(HoldWin);
            session.AddKeystroke(PedalFixtures.Key("lshift"), MacroModifiers.None);

            Assert.Equal(MacroModifiers.None, session.Macro!.Keystrokes[0].Modifiers);
            Assert.Equal(new[] { "lshift" }, session.DisplayItems);
        }

        [Fact]
        public void Clear_ReleasesTheHeldWinModifier()
        {
            var session = MacroSession();

            session.Apply(HoldWin);
            session.Clear();

            Assert.False(session.HoldWinModifier);
        }

        [Fact]
        public void SetMode_ReleasesTheHeldWinModifier()
        {
            var session = MacroSession();

            session.Apply(HoldWin);
            session.SetMode(PedalInputMode.Single);

            Assert.False(session.HoldWinModifier);
        }

        [Fact]
        public void Backspace_InSingleMode_ClearsTheAction()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.Backspace();

            Assert.Null(session.SingleAction);
            Assert.True(session.IsEmpty);
        }

        [Fact]
        public void Backspace_WithAPlainMacroTail_RemovesOneKeystroke()
        {
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);
            session.Backspace();

            Assert.Equal(new[] { "a" }, session.DisplayItems);
        }

        [Fact]
        public void Backspace_WithADoubleClickTail_RemovesTheWholeTriple()
        {
            // 12 §4.5: the lmouse / 125 ms / lmouse triple is one display item.
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            AddDoubleClick(session);

            Assert.Equal(new[] { "a", PedalActionDescriber.LeftMouseDoubleClickText }, session.DisplayItems);

            session.Backspace();

            Assert.Equal(new[] { "a" }, session.DisplayItems);
            Assert.Single(session.Macro!.Keystrokes);
        }

        [Fact]
        public void Backspace_WithOverlappingDoubleClickCandidates_GroupsGreedilyFromTheLeft()
        {
            // lmouse,d125,lmouse,d125,lmouse is a triple plus two singles — the same left-to-right
            // scan PedalActionDescriber.DescribeMacroItems performs — so one Backspace takes one key.
            var session = MacroSession();

            AddDoubleClick(session);
            session.AddKeystroke(PedalTokenMap.Delay125Key, MacroModifiers.None);
            session.AddKeystroke(PedalTokenMap.LeftMouseKey, MacroModifiers.None);

            Assert.Equal(
                new[] { PedalActionDescriber.LeftMouseDoubleClickText, "d125", "lmouse" },
                session.DisplayItems);

            session.Backspace();

            Assert.Equal(4, session.Macro!.Keystrokes.Count);
        }

        [Fact]
        public void Backspace_WithADifferentPressReleaseTail_RemovesTheFlaggedKeystroke()
        {
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);
            session.Apply(DifferentPressRelease);

            Assert.Equal(new[] { "a", "b{ }" }, session.DisplayItems);

            session.Backspace();

            Assert.Equal(new[] { "a" }, session.DisplayItems);
        }

        [Fact]
        public void Backspace_WithAnEmptyEntry_DoesNothing()
        {
            var session = MacroSession();

            session.Backspace();

            Assert.True(session.IsEmpty);

            var single = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            single.Backspace();

            Assert.True(single.IsEmpty);
        }

        [Fact]
        public void Clear_EmptiesTheEntryAndKeepsTheMode()
        {
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.Clear();

            Assert.Equal(PedalInputMode.Macro, session.Mode);
            Assert.True(session.IsEmpty);
            Assert.Empty(session.DisplayItems);
        }

        [Fact]
        public void ApplyTo_WithASingleAction_ProgramsTheInput()
        {
            var input = new PedalInput(PedalInputId.Jack2);
            var session = PedalEditSession.BeginFor(input);

            session.AddKeystroke(PedalFixtures.Key("F1"), MacroModifiers.None);
            session.ApplyTo(input);

            Assert.Equal(PedalInputMode.Single, input.Mode);
            Assert.Equal(PedalFixtures.Key("F1"), input.Action);
        }

        [Fact]
        public void ApplyTo_WithAMacro_ProgramsTheInputWithACopy()
        {
            var input = new PedalInput(PedalInputId.Jack2);
            var session = PedalEditSession.BeginFor(input);

            session.SetMode(PedalInputMode.Macro);
            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.ApplyTo(input);

            Assert.Equal(PedalInputMode.Macro, input.Mode);
            Assert.Single(input.Macro!.Keystrokes);

            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);

            Assert.Single(input.Macro.Keystrokes);
        }

        [Fact]
        public void ApplyTo_WithAnEmptyEntry_ClearsTheInput()
        {
            var input = MacroInput("a");
            var session = PedalEditSession.BeginFor(input);

            session.Clear();
            session.ApplyTo(input);

            Assert.Equal(PedalInputMode.None, input.Mode);
            Assert.False(input.IsAssigned);
        }

        [Fact]
        public void ApplyTo_WithAnUntouchedEmptyMacroInput_LeavesItsModeAlone()
        {
            // pedals.txt legitimately holds "{jack1}>" — macro mode, empty macro. Opening it and
            // pressing Done must not turn it into an unassigned single-action input, which would
            // rewrite the line as "[jack1]>" on the next save.
            var input = new PedalInput(PedalInputId.Jack1);

            input.SetMacro(new Macro());

            var session = PedalEditSession.BeginFor(input);

            Assert.False(session.DiffersFrom(input));

            session.ApplyTo(input);

            Assert.Equal(PedalInputMode.Macro, input.Mode);
            Assert.NotNull(input.Macro);
            Assert.False(input.IsAssigned);
        }

        [Fact]
        public void ApplyTo_WithAnEmptyEntryOverAnUnassignedInput_ChangesNothing()
        {
            var input = new PedalInput(PedalInputId.Jack1);
            var session = PedalEditSession.BeginFor(input);

            session.SetMode(PedalInputMode.Macro);

            Assert.False(session.DiffersFrom(input));

            session.ApplyTo(input);

            // The session is in macro mode and the input in none: an empty entry over an input that
            // fires nothing is not a change, so the commit does not invent one.
            Assert.Equal(PedalInputMode.None, input.Mode);
        }

        [Fact]
        public void ApplyTo_WithAnotherInput_Throws()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            Assert.Throws<ArgumentException>(() => session.ApplyTo(new PedalInput(PedalInputId.RightPedal)));
        }

        [Fact]
        public void DiffersFrom_WithAFreshlyOpenedSession_IsFalse()
        {
            var unassigned = new PedalInput(PedalInputId.LeftPedal);
            var single = new PedalInput(PedalInputId.MiddlePedal);

            single.SetSingleAction(PedalFixtures.Key("lmouse"));

            var macro = MacroInput("a", "b");

            Assert.False(PedalEditSession.BeginFor(unassigned).DiffersFrom(unassigned));
            Assert.False(PedalEditSession.BeginFor(single).DiffersFrom(single));
            Assert.False(PedalEditSession.BeginFor(macro).DiffersFrom(macro));
        }

        [Fact]
        public void DiffersFrom_WithAnEmptyEntryOverAnInputThatFiresNothing_IsFalse()
        {
            // 12 §4.1: an empty macro programs nothing, so "{jack1}>" and an unassigned input are
            // the same thing to the user — committing one over the other is not a change.
            var emptyMacro = new PedalInput(PedalInputId.Jack1);

            emptyMacro.SetMacro(new Macro());

            Assert.False(PedalEditSession.BeginFor(emptyMacro).DiffersFrom(emptyMacro));

            var unassigned = new PedalInput(PedalInputId.Jack2);
            var session = PedalEditSession.BeginFor(unassigned);

            session.SetMode(PedalInputMode.Macro);

            Assert.False(session.DiffersFrom(unassigned));
        }

        [Fact]
        public void DiffersFrom_AfterAnEdit_IsTrue()
        {
            var input = MacroInput("a");
            var session = PedalEditSession.BeginFor(input);

            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);

            Assert.True(session.DiffersFrom(input));
        }

        [Fact]
        public void DiffersFrom_AfterAModeSwitchOnly_IsTrue()
        {
            var input = new PedalInput(PedalInputId.LeftPedal);

            input.SetSingleAction(PedalFixtures.Key("a"));

            var session = PedalEditSession.BeginFor(input);

            session.SetMode(PedalInputMode.Macro);

            // The switch cleared the entry, so committing would clear the input.
            Assert.True(session.DiffersFrom(input));
        }

        [Fact]
        public void DiffersFrom_WithAnEquivalentRebuiltMacro_IsFalse()
        {
            var input = MacroInput("a", "b");
            var session = PedalEditSession.BeginFor(input);

            session.Clear();
            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);

            Assert.False(session.DiffersFrom(input));
        }

        [Fact]
        public void DisplayItems_RenderTheEntryTheWayTheMemoDoes()
        {
            var single = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            single.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

            Assert.Equal(new[] { "[a]" }, single.DisplayItems);

            var macro = MacroSession();

            macro.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            AddDoubleClick(macro);

            Assert.Equal(new[] { "a", PedalActionDescriber.LeftMouseDoubleClickText }, macro.DisplayItems);
        }

        private static PedalSpecialAction HoldWin => Require("hold-win-modifier");

        private static PedalSpecialAction DifferentPressRelease => Require("different-press-release");

        private static PedalSpecialAction Require(string id)
        {
            return PedalSpecialActions.Find(id) ?? throw new InvalidOperationException($"No special action \"{id}\".");
        }

        private static PedalEditSession MacroSession()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.SetMode(PedalInputMode.Macro);

            return session;
        }

        private static PedalInput MacroInput(params string[] tokens)
        {
            var input = new PedalInput(PedalInputId.Jack4);
            var macro = new Macro();

            foreach (var token in tokens)
            {
                macro.AddKeystroke(new Keystroke(PedalFixtures.Key(token)));
            }

            input.SetMacro(macro);

            return input;
        }

        private static void AddDoubleClick(PedalEditSession session)
        {
            session.AddKeystroke(PedalTokenMap.LeftMouseKey, MacroModifiers.None);
            session.AddKeystroke(PedalTokenMap.Delay125Key, MacroModifiers.None);
            session.AddKeystroke(PedalTokenMap.LeftMouseKey, MacroModifiers.None);
        }
    }
}
