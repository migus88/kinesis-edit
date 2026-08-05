using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Every Special Actions item of specs/12-savant-elite.md §6 must survive the file: applied to a
    /// session, committed to a <see cref="PedalInput"/>, serialized per §4.3 and parsed back per
    /// §4.2, the assignment must come back equivalent. The theory is driven off
    /// <see cref="PedalSpecialActions"/> itself, so a new catalog entry is covered automatically;
    /// the facts underneath pin the exact file text of the interesting spellings.
    /// </summary>
    public sealed class PedalSpecialActionRoundTripTests
    {
        private const PedalInputId Input = PedalInputId.Jack1;

        public static TheoryData<string> KeystrokeActionIds()
        {
            var ids = new TheoryData<string>();

            foreach (var group in PedalSpecialActions.Groups)
            {
                foreach (var action in group.Actions)
                {
                    if (action.Kind == PedalSpecialActionKind.Keystrokes)
                    {
                        ids.Add(action.Id);
                    }
                }
            }

            return ids;
        }

        [Theory]
        [MemberData(nameof(KeystrokeActionIds))]
        public void EveryKeystrokeAction_SurvivesASaveAndReload(string id)
        {
            var action = Require(id);
            var committed = Commit(action);
            var line = PedalFileSerializer.SerializeInput(committed);
            var result = PedalFixtures.Parse(line);

            Assert.Empty(result.InvalidLines);

            var reloaded = result.Configuration[Input];

            Assert.Equal(committed.Mode, reloaded.Mode);
            Assert.Equal(committed.Action, reloaded.Action);

            if (committed.Macro is null)
            {
                Assert.Null(reloaded.Macro);
            }
            else
            {
                Assert.True(
                    committed.Macro.IsEquivalentTo(reloaded.Macro),
                    $"\"{line}\" did not reload into an equivalent macro.");
            }

            // A second save must be byte-identical, so the item is stable on disk (§4.6).
            Assert.Equal(line, PedalFileSerializer.SerializeInput(reloaded));
        }

        [Theory]
        [InlineData("left-mouse-click", "{jack1}>{lmouse}")]
        [InlineData("left-mouse-double-click", "{jack1}>{lmouse}{d125}{lmouse}")]
        [InlineData("cut-ctrl", "{jack1}>{-ctrl}{x}{+ctrl}")]
        [InlineData("cut-cmd", "{jack1}>{-win}{x}{+win}")]
        [InlineData("ctrl-alt-delete", "{jack1}>{-ctrl}{-alt}{delete}{+ctrl}{+alt}")]
        [InlineData("snip-to-clipboard", "{jack1}>{-shift}{-ctrl}{-win}{4}{+shift}{+ctrl}{+win}")]
        [InlineData("slow-output", "{jack1}>{speed1}")]
        [InlineData("short-delay", "{jack1}>{d125}")]
        [InlineData("mute", "[jack1]>[mute]")]
        [InlineData("calculator", "[jack1]>[calc]")]
        public void SerializedText_IsTheSpecSpelling(string id, string expected)
        {
            // 12 §4.4 lists "win" as the pedal's only Win/Cmd modifier token — never lwin/rwin.
            Assert.Equal(expected, PedalFileSerializer.SerializeInput(Commit(Require(id))));
        }

        [Fact]
        public void CmdShortcut_ReloadsAsTheWinModifier()
        {
            var line = PedalFileSerializer.SerializeInput(Commit(Require("cut-cmd")));
            var reloaded = PedalFixtures.Parse(line).Configuration[Input];

            Assert.Equal(MacroModifiers.LeftWin, reloaded.Macro!.Keystrokes[0].Modifiers);
            Assert.Equal(PedalFixtures.Key("x"), reloaded.Macro.Keystrokes[0].Key);
        }

        [Fact]
        public void DifferentPressRelease_SurvivesASaveAndReload()
        {
            // 12 §6: the split serializes as {-key}{ }{+key} and must come back flagged.
            var session = PedalEditSession.BeginFor(new PedalInput(Input));

            session.SetMode(PedalInputMode.Macro);
            session.AddKeystroke(PedalFixtures.Key("a"), MacroModifiers.None);

            Assert.True(session.Apply(Require("different-press-release")).Applied);

            var input = new PedalInput(Input);

            session.ApplyTo(input);

            var line = PedalFileSerializer.SerializeInput(input);

            Assert.Equal("{jack1}>{-a}{ }{+a}", line);

            var reloaded = PedalFixtures.Parse(line).Configuration[Input];

            Assert.Single(reloaded.Macro!.Keystrokes);
            Assert.True(reloaded.Macro.Keystrokes[0].DiffPressRelease);
            Assert.True(input.Macro!.IsEquivalentTo(reloaded.Macro));
        }

        /// <summary>
        /// Applies <paramref name="action"/> to a fresh session — in macro mode where the item allows
        /// it, single mode otherwise — and commits it to a <see cref="PedalInput"/>.
        /// </summary>
        private static PedalInput Commit(PedalSpecialAction action)
        {
            var input = new PedalInput(Input);
            var session = PedalEditSession.BeginFor(input);

            session.SetMode(
                action.SupportsMode(PedalInputMode.Macro) ? PedalInputMode.Macro : PedalInputMode.Single);

            Assert.True(session.Apply(action).Applied);

            session.ApplyTo(input);

            return input;
        }

        private static PedalSpecialAction Require(string id)
        {
            return PedalSpecialActions.Find(id) ?? throw new InvalidOperationException($"No special action \"{id}\".");
        }
    }
}
