using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// The Special Actions catalog of specs/12-savant-elite.md §6: the five sections in menu order,
    /// each item's caption, mode restriction, kind and emitted keys, plus how
    /// <see cref="PedalEditSession.Apply"/> honours the "selecting an item automatically switches
    /// the editor to the required mode" rule and refuses the two shapes the "Different Press &amp;&amp;
    /// Release" split cannot survive.
    /// </summary>
    public sealed class PedalSpecialActionsTests
    {
        [Fact]
        public void Groups_AreTheFiveSectionsOfTheMenu_InSpecOrder()
        {
            Assert.Equal(
                new[]
                {
                    "MOUSE ACTIONS",
                    "EDITING TOOLS",
                    "MEDIA CONTROLS",
                    "COMMONLY USED SHORTCUTS",
                    "PEDAL RESPONSE"
                },
                PedalSpecialActions.Groups.Select(group => group.Name));
        }

        [Fact]
        public void MouseActions_MatchTheSpecTable()
        {
            AssertCaptions(
                "MOUSE ACTIONS",
                "Left Mouse Click",
                "Left Mouse Double Click",
                "Middle Mouse Click",
                "Right Mouse Click");
        }

        [Fact]
        public void EditingTools_ListTheCtrlShortcutsThenTheCmdOnes()
        {
            // No OS gating: §6's "Windows / macOS" column becomes two entries, both always shown.
            AssertCaptions(
                "EDITING TOOLS",
                "Cut (Ctrl + X)",
                "Copy (Ctrl + C)",
                "Paste (Ctrl + V)",
                "Select All (Ctrl + A)",
                "Undo (Ctrl + Z)",
                "Cut (Cmd + X)",
                "Copy (Cmd + C)",
                "Paste (Cmd + V)",
                "Select All (Cmd + A)",
                "Undo (Cmd + Z)");
        }

        [Fact]
        public void MediaControls_MatchTheSpecTable()
        {
            AssertCaptions(
                "MEDIA CONTROLS",
                "Mute",
                "Volume Up",
                "Volume Down",
                "Play / Pause",
                "Previous Track",
                "Next Track");
        }

        [Fact]
        public void Shortcuts_MatchTheSpecTable()
        {
            // The rows of specs/12-savant-elite.md §6, COMMONLY USED SHORTCUTS, read straight off
            // the table, top to bottom: Web Browser Forward / Back, Alt + Tab, Ctrl + Alt + Delete,
            // Calculator, Windows Combination (Win + _), Cmd + Tab, Snip to File, Snip to
            // Clipboard, Force Quit. "Windows Combination" is a shortcut, not a PEDAL RESPONSE
            // item. The only insertion is the second platform spelling of the browser items (no OS
            // gating), each kept next to its spec sibling.
            string[] expected =
            [
                "Web Browser Forward (Alt + →)",
                "Web Browser Back (Alt + ←)",
                "Web Browser Forward (Cmd + →)",
                "Web Browser Back (Cmd + ←)",
                "Alt + Tab",
                "Ctrl + Alt + Delete",
                "Calculator",
                "Windows Combination (Win + …)",
                "Cmd + Tab",
                "Snip to File (Shift + Cmd + 4)",
                "Snip to Clipboard (Shift + Ctrl + Cmd + 4)",
                "Force Quit (Cmd + Alt + Esc)"
            ];

            AssertCaptions("COMMONLY USED SHORTCUTS", expected);
        }

        [Fact]
        public void PedalResponse_MatchesTheSpecTable()
        {
            // specs/12-savant-elite.md §6, PEDAL RESPONSE, in table order — the three speeds, the
            // two delays, and "Different Press && Release" last. Nothing else belongs here.
            string[] expected =
            [
                "Slow Output (speed1)",
                "Default Output (speed3)",
                "Fast Output (speed5)",
                "Short Delay (125 ms)",
                "Long Delay (500 ms)",
                "Different Press & Release"
            ];

            AssertCaptions("PEDAL RESPONSE", expected);
        }

        [Theory]
        [InlineData("left-mouse-click", PedalSpecialActionModes.Single | PedalSpecialActionModes.Macro)]
        [InlineData("left-mouse-double-click", PedalSpecialActionModes.Macro)]
        [InlineData("middle-mouse-click", PedalSpecialActionModes.Single | PedalSpecialActionModes.Macro)]
        [InlineData("right-mouse-click", PedalSpecialActionModes.Single | PedalSpecialActionModes.Macro)]
        [InlineData("cut-ctrl", PedalSpecialActionModes.Macro)]
        [InlineData("undo-cmd", PedalSpecialActionModes.Macro)]
        [InlineData("mute", PedalSpecialActionModes.Single)]
        [InlineData("next-track", PedalSpecialActionModes.Single)]
        [InlineData("calculator", PedalSpecialActionModes.Single)]
        [InlineData("alt-tab", PedalSpecialActionModes.Macro)]
        [InlineData("short-delay", PedalSpecialActionModes.Macro)]
        [InlineData("different-press-release", PedalSpecialActionModes.Macro)]
        [InlineData("hold-win-modifier", PedalSpecialActionModes.Macro)]
        public void Modes_MatchTheSpecTable(string id, PedalSpecialActionModes expected)
        {
            Assert.Equal(expected, Find(id).Modes);
        }

        [Fact]
        public void Kinds_AreKeystrokesExceptForTheTwoEditorModeItems()
        {
            Assert.Equal(PedalSpecialActionKind.DifferentPressRelease, Find("different-press-release").Kind);
            Assert.Equal(PedalSpecialActionKind.ToggleWinModifier, Find("hold-win-modifier").Kind);
            Assert.Equal(0, Find("different-press-release").KeystrokeCount);
            Assert.Equal(0, Find("hold-win-modifier").KeystrokeCount);

            foreach (var action in AllActions())
            {
                if (action.Id is "different-press-release" or "hold-win-modifier")
                {
                    continue;
                }

                Assert.Equal(PedalSpecialActionKind.Keystrokes, action.Kind);
            }
        }

        [Theory]
        [InlineData("left-mouse-click", MacroModifiers.None, "lmouse")]
        [InlineData("left-mouse-double-click", MacroModifiers.None, "lmouse,d125,lmouse")]
        [InlineData("middle-mouse-click", MacroModifiers.None, "mmouse")]
        [InlineData("right-mouse-click", MacroModifiers.None, "rmouse")]
        [InlineData("cut-ctrl", MacroModifiers.Control, "x")]
        [InlineData("copy-ctrl", MacroModifiers.Control, "c")]
        [InlineData("paste-ctrl", MacroModifiers.Control, "v")]
        [InlineData("select-all-ctrl", MacroModifiers.Control, "a")]
        [InlineData("undo-ctrl", MacroModifiers.Control, "z")]
        [InlineData("cut-cmd", MacroModifiers.LeftWin, "x")]
        [InlineData("copy-cmd", MacroModifiers.LeftWin, "c")]
        [InlineData("paste-cmd", MacroModifiers.LeftWin, "v")]
        [InlineData("select-all-cmd", MacroModifiers.LeftWin, "a")]
        [InlineData("undo-cmd", MacroModifiers.LeftWin, "z")]
        [InlineData("mute", MacroModifiers.None, "mute")]
        [InlineData("volume-up", MacroModifiers.None, "vol+")]
        [InlineData("volume-down", MacroModifiers.None, "vol-")]
        [InlineData("play-pause", MacroModifiers.None, "play")]
        [InlineData("previous-track", MacroModifiers.None, "prev")]
        [InlineData("next-track", MacroModifiers.None, "next")]
        [InlineData("web-browser-forward-alt", MacroModifiers.Alt, "right")]
        [InlineData("web-browser-back-alt", MacroModifiers.Alt, "left")]
        [InlineData("web-browser-forward-cmd", MacroModifiers.LeftWin, "right")]
        [InlineData("web-browser-back-cmd", MacroModifiers.LeftWin, "left")]
        [InlineData("alt-tab", MacroModifiers.Alt, "tab")]
        [InlineData("ctrl-alt-delete", MacroModifiers.Control | MacroModifiers.Alt, "delete")]
        [InlineData("cmd-tab", MacroModifiers.LeftWin, "tab")]
        [InlineData("snip-to-file", MacroModifiers.Shift | MacroModifiers.LeftWin, "4")]
        [InlineData(
            "snip-to-clipboard",
            MacroModifiers.Shift | MacroModifiers.Control | MacroModifiers.LeftWin,
            "4")]
        [InlineData("force-quit", MacroModifiers.LeftWin | MacroModifiers.Alt, "escape")]
        [InlineData("calculator", MacroModifiers.None, "calc")]
        [InlineData("slow-output", MacroModifiers.None, "speed1")]
        [InlineData("default-output", MacroModifiers.None, "speed3")]
        [InlineData("fast-output", MacroModifiers.None, "speed5")]
        [InlineData("short-delay", MacroModifiers.None, "d125")]
        [InlineData("long-delay", MacroModifiers.None, "d500")]
        public void Keystrokes_EmitTheSpecTokensAndModifiers(string id, MacroModifiers modifiers, string tokens)
        {
            var keystrokes = Find(id).CreateKeystrokes();

            Assert.Equal(tokens.Split(','), keystrokes.Select(keystroke => PedalTokenMap.GetMacroToken(keystroke.Key)));
            Assert.All(keystrokes, keystroke => Assert.Equal(modifiers, keystroke.Modifiers));
        }

        [Fact]
        public void CreateKeystrokes_ReturnsIndependentInstances()
        {
            var action = Find("cut-ctrl");
            var first = action.CreateKeystrokes();

            first[0].Modifiers = MacroModifiers.None;
            first[0].DiffPressRelease = true;

            var second = action.CreateKeystrokes();

            Assert.Equal(MacroModifiers.Control, second[0].Modifiers);
            Assert.False(second[0].DiffPressRelease);
            Assert.NotSame(first[0], second[0]);
        }

        [Fact]
        public void Catalog_NeverOffersTheShutdownToken()
        {
            // 12 §4.4: "shutdn (shutdown; defined but not exposed in the menu)".
            foreach (var action in AllActions())
            {
                Assert.DoesNotContain(
                    action.CreateKeystrokes(),
                    keystroke => PedalTokenMap.GetSingleToken(keystroke.Key) == "shutdn");
            }
        }

        [Fact]
        public void Find_ReturnsTheCatalogEntryForEveryId()
        {
            foreach (var action in AllActions())
            {
                Assert.Same(action, PedalSpecialActions.Find(action.Id));
            }
        }

        [Fact]
        public void Find_WithAnUnknownId_ReturnsNull()
        {
            Assert.Null(PedalSpecialActions.Find("no-such-action"));
        }

        [Fact]
        public void Apply_WithASingleOnlyItemWhileInMacroMode_SwitchesToSingle()
        {
            // 12 §6: "selecting an item automatically switches the editor to the required mode".
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.SetMode(PedalInputMode.Macro);
            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

            Assert.True(session.Apply(Find("mute")).Applied);
            Assert.Equal(PedalInputMode.Single, session.Mode);
            Assert.Equal(PedalFixtures.Key("mute"), session.SingleAction);
            Assert.Null(session.Macro);
        }

        [Fact]
        public void Apply_WithAMacroOnlyItemWhileInSingleMode_SwitchesToMacro()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

            Assert.True(session.Apply(Find("cut-ctrl")).Applied);
            Assert.Equal(PedalInputMode.Macro, session.Mode);

            // The mode switch cleared the entry (§5 step 2), so only the shortcut is left.
            Assert.Equal(new[] { "{ctrl+x}" }, session.DisplayItems);
        }

        [Fact]
        public void Apply_WithAnItemValidInBothModes_StaysInTheCurrentMode()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            Assert.True(session.Apply(Find("left-mouse-click")).Applied);
            Assert.Equal(PedalInputMode.Single, session.Mode);

            session.SetMode(PedalInputMode.Macro);

            Assert.True(session.Apply(Find("left-mouse-click")).Applied);
            Assert.Equal(PedalInputMode.Macro, session.Mode);
        }

        [Fact]
        public void Apply_DifferentPressRelease_FlagsTheLastKeystroke()
        {
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);
            session.AddKeystroke(PedalFixtures.Key("b"), MacroModifiers.None);

            Assert.True(session.Apply(Find("different-press-release")).Applied);
            Assert.False(session.Macro!.Keystrokes[0].DiffPressRelease);
            Assert.True(session.Macro.Keystrokes[1].DiffPressRelease);

            // The menu item is a toggle: picking it again takes the flag off.
            Assert.True(session.Apply(Find("different-press-release")).Applied);
            Assert.False(session.Macro.Keystrokes[1].DiffPressRelease);
        }

        [Fact]
        public void Apply_DifferentPressReleaseOnAnEmptyEntry_IsRefused()
        {
            var session = MacroSession();
            var result = session.Apply(Find("different-press-release"));

            Assert.False(result.Applied);
            Assert.Equal(PedalSpecialActionFailure.EmptyEntry, result.Failure);
            Assert.False(session.CanApply(Find("different-press-release")));
        }

        [Fact]
        public void Apply_DifferentPressReleaseInSingleMode_IsRefusedAndKeepsTheEntry()
        {
            // Switching to macro mode would clear the entry, leaving nothing to flag — so the item is
            // refused outright instead of destroying the single action.
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

            var result = session.Apply(Find("different-press-release"));

            Assert.False(result.Applied);

            // Not EmptyEntry: there *is* an entry, it is simply in the wrong mode, and the fix is a
            // deliberate mode switch rather than typing something.
            Assert.Equal(PedalSpecialActionFailure.ModeNotSupported, result.Failure);
            Assert.Equal(PedalInputMode.Single, session.Mode);
            Assert.Equal(PedalFixtures.Key("a"), session.SingleAction);
        }

        [Fact]
        public void Apply_DifferentPressReleaseOnAModifierKeystroke_IsRefused()
        {
            // {-lshift}{ }{+lshift} reloads as a Shift-modified space (12 §4.4), so the flag would be
            // silently lost on the next save.
            var session = MacroSession();

            session.AddKeystroke(PedalFixtures.Key("lshift"), MacroModifiers.None);

            var result = session.Apply(Find("different-press-release"));

            Assert.False(result.Applied);
            Assert.Equal(PedalSpecialActionFailure.ModifierKeystroke, result.Failure);
            Assert.False(session.Macro!.Keystrokes[0].DiffPressRelease);
            Assert.Single(session.Macro.Keystrokes);
        }

        [Fact]
        public void Apply_HoldWinModifier_TogglesTheStickyModifier()
        {
            var session = MacroSession();

            Assert.False(session.HoldWinModifier);
            Assert.True(session.Apply(Find("hold-win-modifier")).Applied);
            Assert.True(session.HoldWinModifier);

            Assert.True(session.Apply(Find("hold-win-modifier")).Applied);
            Assert.False(session.HoldWinModifier);
        }

        [Fact]
        public void Apply_HoldWinModifier_DoesNotRideAlongWithASpecialAction()
        {
            // §6 latches the modifier onto "subsequently entered keystrokes" — the ones the user
            // types. A menu item already carries the modifiers §6 gives it, and overlaying Win on
            // those would rewrite the item into something else entirely.
            var session = MacroSession();

            session.Apply(Find("hold-win-modifier"));
            session.Apply(Find("left-mouse-click"));

            Assert.Equal(MacroModifiers.None, session.Macro!.Keystrokes[0].Modifiers);
            Assert.True(session.HoldWinModifier);
        }

        [Fact]
        public void Apply_WithTheDoubleClickWhileTheWinModifierIsHeld_KeepsTheTripleIntact()
        {
            // {-win}{lmouse}{d125}{lmouse}{+win} is no longer the §4.5 double click — it renders as
            // three items, deletes in three Backspaces, and wraps a *pause* in a Win press/release,
            // which plays back as a bare Win tap.
            var session = MacroSession();

            session.Apply(Find("hold-win-modifier"));
            session.Apply(Find("left-mouse-double-click"));

            Assert.Equal(new[] { PedalActionDescriber.LeftMouseDoubleClickText }, session.DisplayItems);
            Assert.All(session.Macro!.Keystrokes, keystroke => Assert.Equal(MacroModifiers.None, keystroke.Modifiers));

            // The latch is untouched by the item, so the next captured key still carries it.
            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

            Assert.Equal(MacroModifiers.LeftWin, session.Macro.Keystrokes[^1].Modifiers);
        }

        [Fact]
        public void CanApply_MatchesApply_ForEveryCatalogItem()
        {
            foreach (var action in AllActions())
            {
                var session = MacroSession();

                session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

                Assert.Equal(session.CanApply(action), session.Apply(action).Applied);
            }
        }

        private static void AssertCaptions(string group, params string[] captions)
        {
            var actions = PedalSpecialActions.Groups.Single(candidate => candidate.Name == group).Actions;

            Assert.Equal(captions, actions.Select(action => action.Caption));
        }

        private static IEnumerable<PedalSpecialAction> AllActions()
        {
            return PedalSpecialActions.Groups.SelectMany(group => group.Actions);
        }

        private static PedalSpecialAction Find(string id)
        {
            return PedalSpecialActions.Find(id) ?? throw new InvalidOperationException($"No special action \"{id}\".");
        }

        private static PedalEditSession MacroSession()
        {
            var session = PedalEditSession.BeginFor(new PedalInput(PedalInputId.LeftPedal));

            session.SetMode(PedalInputMode.Macro);

            return session;
        }
    }
}
