using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// One co-trigger toggle. <c>[AvaloniaFact]</c> rather than <c>[Fact]</c> because the caption
    /// half of the view model asks the app's real glyph coverage what its type can print, which
    /// reads <c>Application.Current</c>; the mark half is pure and is spelled by
    /// <see cref="MacroModifierMarks"/>, whose own table is asserted in
    /// <c>MacroModifierMarksTests</c>.
    /// </summary>
    public class MacroCoTriggerViewModelTests
    {
        /// <summary>
        /// The six toggles the panel draws, in spec order — <c>⇧ R⇧ ⌃ R⌃ ⌥ R⌥</c>. <b>Only the
        /// right-hand three spell a side</b>: left is the unmarked case (see
        /// <see cref="MacroModifierMarks.LeftSide"/>), so a left toggle is the bare mark and a
        /// single run. The <c>R</c> is a separate part on purpose: it is a Latin letter and the
        /// key-symbol subset carries none, so the view sets the two runs in two faces.
        /// </summary>
        [AvaloniaFact]
        public void CreateAll_GivesEverySixToggleItsMark_SpellingOnlyTheRightSide()
        {
            var expected = new[]
            {
                (string.Empty, MacroModifierMarks.ShiftMark),
                ("R", MacroModifierMarks.ShiftMark),
                (string.Empty, MacroModifierMarks.ControlMark),
                ("R", MacroModifierMarks.ControlMark),
                (string.Empty, MacroModifierMarks.AltMark),
                ("R", MacroModifierMarks.AltMark)
            };

            var toggles = MacroCoTriggerViewModel.CreateAll(TokenDialect.Gen1);

            Assert.Equal(expected.Length, toggles.Count);

            for (var index = 0; index < expected.Length; index++)
            {
                var toggle = toggles[index];

                Assert.Equal(expected[index].Item1, toggle.Side);
                Assert.Equal(expected[index].Item2, toggle.Symbol);
                Assert.True(toggle.HasMark);

                // The left three draw no side run at all — which is what the view's
                // `IsVisible="{Binding HasSide}"` acts on.
                Assert.Equal(index % 2 == 1, toggle.HasSide);
            }

            Assert.Equal(
                ["⇧", "R⇧", "⌃", "R⌃", "⌥", "R⌥"],
                toggles.Select(toggle => toggle.Side + toggle.Symbol));
        }

        /// <summary>
        /// A mark is one line, which is the whole reason for the change: the same six toggles used
        /// to carry <c>Left⏎Shift</c> … <c>Right⏎Opt</c> in a rail 300 px wide.
        /// </summary>
        [AvaloniaFact]
        public void EveryMark_IsOneLine_WhileTheCaptionItReplacedIsTwo()
        {
            foreach (var toggle in MacroCoTriggerViewModel.CreateAll(TokenDialect.Gen1))
            {
                Assert.DoesNotContain('\n', toggle.Side);
                Assert.DoesNotContain('\n', toggle.Symbol);

                // Kept, and still the two-line spelling: it is the toggle's tooltip, which is the
                // accessible text a bare `⇧` is not — and, with left unmarked, the only thing on
                // this toggle that says which side it is.
                Assert.Contains('\n', toggle.Caption);
            }
        }

        /// <summary>
        /// The six stay distinguishable from each other even with left unmarked, because these six
        /// are the <em>sided</em> flags and nothing else: within the row, a bare mark can only be
        /// the left one, and the <c>R</c> names the other. (The collision left-unmarked really does
        /// cost — a bare mark against a <em>generic</em> modifier — cannot arise here, because
        /// <c>CreateAll</c> builds no generic co-trigger.)
        /// </summary>
        [AvaloniaFact]
        public void TheSixMarks_AreAllDistinct()
        {
            var marks = MacroCoTriggerViewModel.CreateAll(TokenDialect.Gen1)
                .Select(toggle => toggle.Side + toggle.Symbol)
                .ToArray();

            Assert.Equal(marks.Length, marks.Distinct(StringComparer.Ordinal).Count());
        }

        /// <summary>
        /// A key that names no modifier keeps its caption rather than throwing or rendering blank.
        /// <see cref="MacroCoTriggerViewModel.CreateAll"/> builds only modifiers today, so this is
        /// the defensive half of the contract the view's <c>!HasMark</c> fallback draws.
        /// </summary>
        [AvaloniaFact]
        public void AKeyThatNamesNoModifier_FallsBackToItsCaption()
        {
            var key = KeyRegistry.FindByToken("a", TokenDialect.Gen1)!;

            var toggle = new MacroCoTriggerViewModel(key, TokenDialect.Gen1);

            Assert.False(toggle.HasMark);
            Assert.False(toggle.HasSide);
            Assert.Equal(string.Empty, toggle.Symbol);
            Assert.Equal(string.Empty, toggle.Side);
            Assert.NotEmpty(toggle.Caption);
        }
    }
}
