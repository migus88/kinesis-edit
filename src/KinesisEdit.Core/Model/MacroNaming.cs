using System.Text;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The naming rules for macros: what a name may look like, and what to call one that has none.
    /// Pure functions over the model — no I/O, no UI, no device switch.
    /// <para>
    /// A macro name is <b>not in the layout file</b> (see <see cref="Macro.Name"/>): it rides
    /// <c>settings/app_settings.txt</c>, which is line-oriented <c>key=value</c> text (spec 08 §1).
    /// That is why <see cref="Sanitize"/> collapses a name to a single trimmed line and bounds its
    /// length — a newline in a name would split one settings line into two and corrupt the file.
    /// </para>
    /// </summary>
    public static class MacroNaming
    {
        /// <summary>
        /// Longest name this app stores or derives, in characters. Chosen so a derived fallback
        /// (<c>"Macro on [" + token + "]"</c>, 11 characters plus a file token of at most 8) always
        /// fits, and so a name stays readable in the inspector's one-line name field.
        /// </summary>
        public const int MaxNameLength = 24;

        /// <summary>Name of a macro that types nothing printable and names no trigger key.</summary>
        public const string UnnamedMacroName = "Unnamed macro";

        private const string TriggerNamePrefix = "Macro on [";
        private const string TriggerNameSuffix = "]";

        // The unshifted character each punctuation position types (05 §3.2). The key table carries
        // the *shifted* character verbatim (KeyDefinition.ShiftedValue) but spells the unshifted one
        // only inside dialect captions like "+\n=", which are display data and not a promise. Eleven
        // explicit rows are cheaper to trust than a caption parser, and the twelfth position
        // (Oem102, the international backslash) types no single character in every layout.
        private static readonly Dictionary<int, string> _unshiftedPunctuation = new()
        {
            [VirtualKey.OemPlus] = "=",
            [VirtualKey.OemMinus] = "-",
            [VirtualKey.Oem1] = ";",
            [VirtualKey.Oem2] = "/",
            [VirtualKey.Oem3] = "`",
            [VirtualKey.Oem4] = "[",
            [VirtualKey.Oem5] = "\\",
            [VirtualKey.Oem6] = "]",
            [VirtualKey.Oem7] = "'",
            [VirtualKey.OemComma] = ",",
            [VirtualKey.OemPeriod] = "."
        };

        // Every key-table entry that types a space (05 §3.3: the shared Space plus the Advantage360
        // left/right/middle space positions).
        private static readonly HashSet<int> _spaceKeyCodes = [VirtualKey.Space, 10034, 10035, 11093];

        /// <summary>
        /// Trims <paramref name="name"/>, collapses it to a single line, and bounds it to
        /// <see cref="MaxNameLength"/> characters. Null, empty and whitespace-only input return the
        /// empty string — the "unnamed" value of <see cref="Macro.Name"/>.
        /// <para>
        /// Line breaks (<c>\r</c>, <c>\n</c>) and every other control character are <b>stripped</b>
        /// rather than replaced: the name is written into a line-oriented settings file, and a
        /// second line would be read back as a stray key. Interior runs of whitespace collapse to a
        /// single space so "a  b" and "a\tb" cannot be two different names that look alike.
        /// </para>
        /// </summary>
        public static string Sanitize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            var pendingSpace = false;

            foreach (var character in name)
            {
                if (char.IsControl(character) || char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;

                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }

            return Bound(builder.ToString());
        }

        /// <summary>
        /// A name for a macro that has none, derived from what it does. Deterministic: the same
        /// macro always derives the same name, so a reload cannot renumber the library.
        /// <list type="number">
        /// <item>The printable characters the macro types, in playback order, bounded to
        /// <see cref="MaxNameLength"/>. Key-up events, modifier keys, delay/speed pseudo-keys and
        /// every key with no printable character contribute nothing.</item>
        /// <item>When it types nothing printable: <c>Macro on [token]</c>, naming the trigger key in
        /// <paramref name="layout"/>'s dialect — honest about where the macro is rather than
        /// inventing a description of it.</item>
        /// <item>When it has no trigger either (an unbound Gen2 flat-list macro, 06 §1):
        /// <see cref="UnnamedMacroName"/>.</item>
        /// </list>
        /// The result is never persisted — <see cref="Macro.Name"/> stays empty and the settings
        /// layer writes no key for it (see <see cref="Settings.MacroNameKey"/>), so a derived name
        /// follows the macro's content instead of freezing at the moment it was first shown.
        /// </summary>
        public static string DeriveDisplayName(Macro macro, KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(macro);
            ArgumentNullException.ThrowIfNull(layout);

            var typed = DeriveTypedText(macro);

            if (typed.Length > 0)
            {
                return typed;
            }

            var triggerToken = FindTriggerToken(macro, layout.Dialect);

            return triggerToken.Length > 0
                ? TriggerNamePrefix + triggerToken + TriggerNameSuffix
                : UnnamedMacroName;
        }

        /// <summary>
        /// The printable text <paramref name="macro"/> types, bounded to
        /// <see cref="MaxNameLength"/>; empty when it types nothing printable. The first half of
        /// <see cref="DeriveDisplayName"/>, exposed on its own because a macro list wants the same
        /// preview beside a macro that <em>does</em> carry a name.
        /// </summary>
        public static string DeriveTypedText(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            var builder = new StringBuilder();

            foreach (var keystroke in macro.Keystrokes)
            {
                if (builder.Length >= MaxNameLength)
                {
                    break;
                }

                builder.Append(GetTypedCharacters(keystroke));
            }

            return Bound(builder.ToString().Trim());
        }

        private static string GetTypedCharacters(Keystroke keystroke)
        {
            // A release types nothing, and neither does a held modifier or a {sN}/{dNNN} pseudo-key
            // (05 §3.12, flagged SingleEvent).
            if (keystroke.EffectiveDirection == KeyDirection.Up
                || keystroke.IsModifierKey
                || (keystroke.Key.Flags & KeyDefinitionFlags.SingleEvent) != 0)
            {
                return string.Empty;
            }

            if (keystroke.IsShifted && keystroke.Key.ShiftedValue.Length > 0)
            {
                return keystroke.Key.ShiftedValue;
            }

            // Any other modifier combination is a shortcut, not typing (Ctrl+C produces no text).
            if (keystroke.Modifiers != MacroModifiers.None && !keystroke.IsShifted)
            {
                return string.Empty;
            }

            return GetUnshiftedCharacters(keystroke.Key);
        }

        private static string GetUnshiftedCharacters(KeyDefinition key)
        {
            if (_spaceKeyCodes.Contains(key.Code))
            {
                return " ";
            }

            if (key.Table == KeyTable.LettersAndDigits)
            {
                // Letters and digits spell themselves as their file token in every dialect (05 §3.1):
                // "a".."z", "0".."9". DisplayText is the *capitalised* caption, which would render a
                // macro that types "hello" as "HELLO".
                return key.LegacyToken;
            }

            return _unshiftedPunctuation.GetValueOrDefault(key.Code, string.Empty);
        }

        private static string FindTriggerToken(Macro macro, TokenDialect dialect)
        {
            if (macro.TriggerKey == Macro.UnassignedIndex)
            {
                return string.Empty;
            }

            var key = KeyRegistry.FindByCode(macro.TriggerKey);

            if (key is null)
            {
                return string.Empty;
            }

            var token = key.GetToken(dialect);

            return token.Length > 0 ? token : key.GetToken(TokenDialect.Gen1);
        }

        private static string Bound(string value)
        {
            return value.Length <= MaxNameLength ? value : value[..MaxNameLength].TrimEnd();
        }
    }
}
