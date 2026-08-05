using System.Text;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Renders the value side of a macro line in the canonical form of
    /// specs/12-savant-elite.md §4.3: "modifiers first as <c>{-mod}</c> entries, then the key as
    /// <c>{token}</c> (one group, no <c>-</c>/<c>+</c> pair for normal keys), then <c>{+mod}</c>
    /// releases", with consecutive Shift-only keys sharing one wrapper because the modifier
    /// transitions are the diff between successive keystrokes.
    /// <para>
    /// The algorithm is <see cref="MacroKeystrokeRenderer"/>'s (06 §3) — including its
    /// <c>{-key}{ }{+key}</c> spelling of a "different press and release" key — but the pedal
    /// resolves its tokens through <see cref="PedalTokenMap"/>, whose §4.4 spellings differ from
    /// the registry's Legacy column for the space bar (<c>{ }</c>) and the Win key (<c>win</c>).
    /// That is why the renderer cannot simply be called here.
    /// </para>
    /// </summary>
    internal static class PedalMacroWriter
    {
        /// <summary>Renders every keystroke of <paramref name="macro"/>, closing any still-held modifier at the end.</summary>
        public static string Write(Macro macro)
        {
            var builder = new StringBuilder();
            var held = MacroModifiers.None;

            foreach (var keystroke in macro.Keystrokes)
            {
                var token = PedalTokenMap.GetMacroToken(keystroke.Key);

                if (token.Length == 0)
                {
                    // A key with no file token in any dialect writes no group at all, so it must
                    // not drag its modifier transitions in either (05 §3.9).
                    continue;
                }

                var current = MacroModifierCodes.Canonicalize(keystroke.Modifiers);

                AppendModifierTransitions(builder, held, current);
                AppendKeystroke(builder, keystroke, token);

                held = current;
            }

            AppendModifierTransitions(builder, held, MacroModifiers.None);

            return builder.ToString();
        }

        private static void AppendModifierTransitions(
            StringBuilder builder,
            MacroModifiers previous,
            MacroModifiers current)
        {
            foreach (var released in MacroModifierCodes.Enumerate(previous & ~current))
            {
                AppendDirectedToken(builder, KeyDirection.Up, ResolveModifierToken(released));
            }

            foreach (var pressed in MacroModifierCodes.Enumerate(current & ~previous))
            {
                AppendDirectedToken(builder, KeyDirection.Down, ResolveModifierToken(pressed));
            }
        }

        private static void AppendKeystroke(StringBuilder builder, Keystroke keystroke, string token)
        {
            if (keystroke.DiffPressRelease)
            {
                // §4.3, §6: the split marker sits between the press and the release of the key,
                // inside its modifier wrap.
                AppendDirectedToken(builder, KeyDirection.Down, token);
                builder.Append(MacroKeystrokeRenderer.DifferentPressReleaseToken);
                AppendDirectedToken(builder, KeyDirection.Up, token);

                return;
            }

            // §6: the speed and delay pseudo-keys always serialize as a single {speedN}/{dNNN}
            // group — the key table flags them SingleEvent, which clears the direction.
            AppendDirectedToken(builder, keystroke.EffectiveDirection, token);
        }

        private static void AppendDirectedToken(StringBuilder builder, KeyDirection direction, string token)
        {
            if (token.Length == 0)
            {
                return;
            }

            builder.Append('{');

            switch (direction)
            {
                case KeyDirection.Down:
                    builder.Append('-');
                    break;

                case KeyDirection.Up:
                    builder.Append('+');
                    break;
            }

            builder.Append(token).Append('}');
        }

        private static string ResolveModifierToken(MacroModifiers modifier)
        {
            var key = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(modifier));

            return key is null ? string.Empty : PedalTokenMap.GetMacroToken(key);
        }
    }
}
