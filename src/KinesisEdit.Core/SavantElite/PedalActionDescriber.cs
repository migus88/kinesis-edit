using System.Text;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Renders a pedal assignment to display text per the conventions of
    /// specs/12-savant-elite.md §5: "single keys as <c>[x]</c>, macro items as <c>x</c> or
    /// <c>{mod+key}</c>", with the left-mouse double click of §4.5/§6 collapsed to
    /// <see cref="LeftMouseDoubleClickText"/>. Pure and presentation-neutral — plain strings, no
    /// colours: which items §5 paints red is the UI's decision.
    /// </summary>
    public static class PedalActionDescriber
    {
        /// <summary>How the <c>lmouse</c> + 125 ms + <c>lmouse</c> triple is displayed (§4.5).</summary>
        public const string LeftMouseDoubleClickText = "{lmouse-dblclick}";

        /// <summary>Number of keystrokes the double-click macro is made of (§4.5, §6).</summary>
        public const int DoubleClickLength = 3;

        private const char ModifierSeparator = '+';

        /// <summary>The assignment text of one input; empty when it is unassigned.</summary>
        public static string Describe(PedalInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input.Mode == PedalInputMode.Macro && input.Macro is not null)
            {
                return string.Concat(DescribeMacroItems(input.Macro));
            }

            return input.Mode == PedalInputMode.Single && input.Action is not null
                ? "[" + PedalTokenMap.GetSingleToken(input.Action) + "]"
                : string.Empty;
        }

        /// <summary>
        /// One display string per macro item, in playback order (§5) — the form the memo's
        /// Backspace works on. Every left-mouse double-click triple collapses into one item.
        /// </summary>
        public static IReadOnlyList<string> DescribeMacroItems(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            var items = new List<string>(macro.Keystrokes.Count);

            for (var i = 0; i < macro.Keystrokes.Count; i++)
            {
                if (IsLeftMouseDoubleClickAt(macro, i))
                {
                    items.Add(LeftMouseDoubleClickText);
                    i += DoubleClickLength - 1;

                    continue;
                }

                items.Add(DescribeKeystroke(macro.Keystrokes[i]));
            }

            return items;
        }

        /// <summary>
        /// Whether the whole macro is the left-mouse double click of §4.5 — <c>lmouse</c>, a 125 ms
        /// delay, <c>lmouse</c>. Both field spellings qualify, because
        /// <c>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}</c> and <c>{lmouse}{d125}{lmouse}</c> parse
        /// into the same three keystrokes.
        /// </summary>
        public static bool IsLeftMouseDoubleClick(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            return macro.Keystrokes.Count == DoubleClickLength && IsLeftMouseDoubleClickAt(macro, 0);
        }

        /// <summary>Whether the double-click triple of §4.5 starts at <paramref name="index"/>.</summary>
        public static bool IsLeftMouseDoubleClickAt(Macro macro, int index)
        {
            ArgumentNullException.ThrowIfNull(macro);

            if (index < 0 || index + DoubleClickLength > macro.Keystrokes.Count)
            {
                return false;
            }

            return IsPlain(macro.Keystrokes[index], PedalTokenMap.LeftMouseKey.Code)
                   && IsPlain(macro.Keystrokes[index + 1], PedalTokenMap.Delay125Key.Code)
                   && IsPlain(macro.Keystrokes[index + 2], PedalTokenMap.LeftMouseKey.Code);
        }

        /// <summary>
        /// §5: an unmodified item shows its bare file token — so a macro space shows as a space and
        /// a typed word reads as itself — while a modified one shows as <c>{mod+key}</c>. A
        /// "different press and release" key additionally carries the <c>{ }</c> split marker.
        /// </summary>
        private static string DescribeKeystroke(Keystroke keystroke)
        {
            var token = PedalTokenMap.GetMacroToken(keystroke.Key);
            var modifiers = MacroModifierCodes.Canonicalize(keystroke.Modifiers);
            var builder = new StringBuilder();

            if (modifiers == MacroModifiers.None)
            {
                builder.Append(token);
            }
            else
            {
                builder.Append('{');

                foreach (var modifier in MacroModifierCodes.Enumerate(modifiers))
                {
                    builder.Append(ResolveModifierToken(modifier)).Append(ModifierSeparator);
                }

                builder.Append(token).Append('}');
            }

            if (keystroke.DiffPressRelease)
            {
                builder.Append(MacroKeystrokeRenderer.DifferentPressReleaseToken);
            }

            return builder.ToString();
        }

        private static string ResolveModifierToken(MacroModifiers modifier)
        {
            var key = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(modifier));

            return key is null ? string.Empty : PedalTokenMap.GetMacroToken(key);
        }

        private static bool IsPlain(Keystroke keystroke, int keyCode)
        {
            return keystroke.Key.Code == keyCode
                   && keystroke.Modifiers == MacroModifiers.None
                   && !keystroke.DiffPressRelease;
        }
    }
}
