using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Maps a macro's keystrokes onto the display items the memo shows
    /// (specs/12-savant-elite.md §5) — the unit Backspace removes. The grouping must be the *same*
    /// greedy left-to-right scan <see cref="PedalActionDescriber.DescribeMacroItems"/> performs,
    /// because the two answers are shown and edited together: scanning backwards would read
    /// <c>lmouse, d125, lmouse, d125, lmouse</c> as a single + a triple where the describer reads a
    /// triple + two singles.
    /// </summary>
    internal static class PedalDisplayItems
    {
        /// <summary>
        /// Keystroke index the last display item starts at, or -1 when the macro is empty — the
        /// index Backspace truncates the keystroke list to.
        /// </summary>
        public static int GetLastItemStartIndex(Macro macro)
        {
            var start = -1;
            var index = 0;

            while (index < macro.Keystrokes.Count)
            {
                start = index;
                index += GetItemLength(macro, index);
            }

            return start;
        }

        /// <summary>
        /// Number of keystrokes the display item starting at <paramref name="startIndex"/> spans:
        /// three for the left-mouse double-click triple of §4.5, one for everything else.
        /// </summary>
        public static int GetItemLength(Macro macro, int startIndex)
        {
            return PedalActionDescriber.IsLeftMouseDoubleClickAt(macro, startIndex)
                ? PedalActionDescriber.DoubleClickLength
                : 1;
        }
    }
}
