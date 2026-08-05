using System.Numerics;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The two-character modifier codes of specs/05-key-model.md §5.1 and their mapping to
    /// <see cref="MacroModifiers"/> and to modifier key codes. Generic codes carry a trailing
    /// space ("S ", "C ", "A ", "W ") — substring tests in the legacy app rely on it to tell
    /// "S " from "LS"/"RS" — and a modifier set is written as the codes joined with ",".
    /// </summary>
    public static class MacroModifierCodes
    {
        /// <summary>Length of every modifier code, including the trailing space of the generic ones (§5.1).</summary>
        public const int CodeLength = 2;

        /// <summary>Character joining the codes of a modifier set (§5.1).</summary>
        public const char Separator = ',';

        /// <summary>Every single modifier flag in the order of the §5.1 table; the order codes are written in.</summary>
        public static IReadOnlyList<MacroModifiers> All { get; }

        /// <summary>
        /// Every flag the §5.1 table defines, ORed together. Bits outside this mask name no
        /// modifier code and are dropped when a keystroke's modifier set is assigned.
        /// </summary>
        public static MacroModifiers Known => _known;

        private static readonly ModifierCode[] _codes =
        [
            new(MacroModifiers.Shift, "S ", VirtualKey.Shift),
            new(MacroModifiers.LeftShift, "LS", VirtualKey.LeftShift),
            new(MacroModifiers.RightShift, "RS", VirtualKey.RightShift),
            new(MacroModifiers.Control, "C ", VirtualKey.Control),
            new(MacroModifiers.LeftControl, "LC", VirtualKey.LeftControl),
            new(MacroModifiers.RightControl, "RC", VirtualKey.RightControl),
            new(MacroModifiers.Alt, "A ", VirtualKey.Menu),
            new(MacroModifiers.LeftAlt, "LA", VirtualKey.LeftMenu),
            new(MacroModifiers.RightAlt, "RA", VirtualKey.RightMenu),
            // §5.1: converting back to keys maps both "W " and "LW" to the Left Win key.
            new(MacroModifiers.Win, "W ", VirtualKey.LeftWin),
            new(MacroModifiers.LeftWin, "LW", VirtualKey.LeftWin),
            new(MacroModifiers.RightWin, "RW", VirtualKey.RightWin)
        ];

        private static readonly MacroModifiers _known;

        static MacroModifierCodes()
        {
            var all = new MacroModifiers[_codes.Length];

            for (var i = 0; i < _codes.Length; i++)
            {
                all[i] = _codes[i].Modifier;
                _known |= _codes[i].Modifier;
            }

            All = all;
        }

        /// <summary>
        /// Returns the two-character code of a single modifier flag (§5.1). Throws for
        /// <see cref="MacroModifiers.None"/> and for combinations — use <see cref="Format"/> for sets.
        /// </summary>
        public static string GetCode(MacroModifiers modifier)
        {
            foreach (var code in _codes)
            {
                if (code.Modifier == modifier)
                {
                    return code.Code;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Not a single modifier flag.");
        }

        /// <summary>
        /// Returns the modifier key code a flag maps back to (§5.1): generic Shift/Ctrl/Alt map
        /// to the generic keys, and both <see cref="MacroModifiers.Win"/> and
        /// <see cref="MacroModifiers.LeftWin"/> map to the Left Win key.
        /// </summary>
        public static int GetKeyCode(MacroModifiers modifier)
        {
            foreach (var code in _codes)
            {
                if (code.Modifier == modifier)
                {
                    return code.KeyCode;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Not a single modifier flag.");
        }

        /// <summary>
        /// Renders a modifier set as its codes joined with "," in §5.1 table order — the exact
        /// text the legacy model stores on a keystroke. Empty for <see cref="MacroModifiers.None"/>.
        /// </summary>
        public static string Format(MacroModifiers modifiers)
        {
            var parts = new List<string>(_codes.Length);

            foreach (var code in _codes)
            {
                if ((modifiers & code.Modifier) == code.Modifier)
                {
                    parts.Add(code.Code);
                }
            }

            return string.Join(Separator, parts);
        }

        /// <summary>
        /// Parses a comma-separated modifier string (§5.1) back into flags. Matching is
        /// case-insensitive and a code that lost its trailing space ("S") is still recognised,
        /// per the tolerant-read rule. Null, empty, and whitespace parse to
        /// <see cref="MacroModifiers.None"/>; an unknown code fails the whole parse.
        /// </summary>
        public static bool TryParse(string? text, out MacroModifiers modifiers)
        {
            modifiers = MacroModifiers.None;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            foreach (var part in text.Split(Separator))
            {
                if (!TryParseCode(part, out var modifier))
                {
                    modifiers = MacroModifiers.None;
                    return false;
                }

                modifiers |= modifier;
            }

            return true;
        }

        /// <summary>Parses one two-character code (§5.1); see <see cref="TryParse"/> for the matching rules.</summary>
        public static bool TryParseCode(string? text, out MacroModifiers modifier)
        {
            modifier = MacroModifiers.None;

            if (text is null || text.Length == 0 || text.Length > CodeLength)
            {
                return false;
            }

            var padded = text.PadRight(CodeLength);

            foreach (var code in _codes)
            {
                if (string.Equals(code.Code, padded, StringComparison.OrdinalIgnoreCase))
                {
                    modifier = code.Modifier;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Number of distinct modifier codes in a set — the count that feeds keystroke
        /// accounting (§5.1; 2 extra keystrokes per modifier, specs/04-layout-file-format.md §5.3).
        /// Flags that resolve to the same physical modifier key count once, per
        /// <see cref="Canonicalize"/>: <c>W ,LW</c> is one held key, not two.
        /// </summary>
        public static int Count(MacroModifiers modifiers)
        {
            return BitOperations.PopCount((uint)Canonicalize(modifiers));
        }

        /// <summary>
        /// Reduces a modifier set to one flag per physical modifier key, always the *first*
        /// spelling of that key in §5.1 table order, and drops bits the table does not define.
        /// §5.1 maps both <c>"W "</c> and <c>"LW"</c> to the Left Win key and deduplicates the
        /// active-modifier set by key code, so <c>W </c>, <c>LW</c> and <c>W ,LW</c> all canonicalise
        /// to the same value. Comparing canonical sets is therefore comparing held *keys*, which is
        /// what keystroke accounting counts and what the macro renderer diffs between keystrokes.
        /// <see cref="Format"/> stays on the raw set, so a modifier string read from a file
        /// round-trips verbatim.
        /// </summary>
        public static MacroModifiers Canonicalize(MacroModifiers modifiers)
        {
            var canonical = MacroModifiers.None;

            foreach (var code in _codes)
            {
                if ((modifiers & code.Modifier) == code.Modifier)
                {
                    canonical |= FirstSpellingOf(code.KeyCode);
                }
            }

            return canonical;
        }

        /// <summary>Enumerates the single flags of a set in §5.1 table order.</summary>
        public static IEnumerable<MacroModifiers> Enumerate(MacroModifiers modifiers)
        {
            foreach (var code in _codes)
            {
                if ((modifiers & code.Modifier) == code.Modifier)
                {
                    yield return code.Modifier;
                }
            }
        }

        /// <summary>
        /// Whether a key code belongs to the modifier key set of §5.1 (generic, left, and right
        /// Shift/Ctrl/Alt plus Left/Right Win). Keys in this set never carry attached modifiers.
        /// </summary>
        public static bool IsModifierKeyCode(int keyCode)
        {
            foreach (var code in _codes)
            {
                if (code.KeyCode == keyCode)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Maps a modifier key code back to its flag (§5.1). The Left Win key resolves to
        /// <see cref="MacroModifiers.LeftWin"/>, the more specific of the two flags that write it.
        /// </summary>
        public static bool TryFromKeyCode(int keyCode, out MacroModifiers modifier)
        {
            foreach (var code in _codes)
            {
                if (code.KeyCode != keyCode || code.Modifier == MacroModifiers.Win)
                {
                    continue;
                }

                modifier = code.Modifier;
                return true;
            }

            modifier = MacroModifiers.None;
            return false;
        }

        private static MacroModifiers FirstSpellingOf(int keyCode)
        {
            foreach (var code in _codes)
            {
                if (code.KeyCode == keyCode)
                {
                    return code.Modifier;
                }
            }

            return MacroModifiers.None;
        }

        private sealed record ModifierCode(MacroModifiers Modifier, string Code, int KeyCode);
    }
}
