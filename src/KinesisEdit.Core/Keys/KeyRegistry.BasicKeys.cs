using System.Globalization;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Key-table data for specs/05-key-model.md §3.1 (letters and digits), §3.2 (punctuation),
    /// §3.3 (whitespace, editing, and navigation), and §3.4 (function keys).
    /// </summary>
    public static partial class KeyRegistry
    {
        private static void AddLettersAndDigits(List<KeyDefinition> entries)
        {
            const KeyDefinitionFlags layoutFlags =
                KeyDefinitionFlags.ConvertToUnicode | KeyDefinitionFlags.ShowShiftedValue;

            for (var letter = 'a'; letter <= 'z'; letter++)
            {
                var token = letter.ToString();
                var upper = char.ToUpperInvariant(letter).ToString();

                entries.Add(new KeyDefinition
                {
                    Code = VirtualKey.A + (letter - 'a'),
                    Table = KeyTable.LettersAndDigits,
                    Dialects = TokenDialects.All,
                    LegacyToken = token,
                    Gen1Token = token,
                    Gen2Token = token,
                    DisplayText = upper,
                    ShiftedValue = upper,
                    Flags = layoutFlags
                });
            }

            var shiftedDigits = new[] { ")", "!", "@", "#", "$", "%", "^", "&", "*", "(" };

            for (var digit = 0; digit <= 9; digit++)
            {
                var token = digit.ToString(CultureInfo.InvariantCulture);
                var shifted = shiftedDigits[digit];

                entries.Add(new KeyDefinition
                {
                    Code = VirtualKey.Digit0 + digit,
                    Table = KeyTable.LettersAndDigits,
                    Dialects = TokenDialects.All,
                    LegacyToken = token,
                    Gen1Token = token,
                    Gen2Token = token,
                    DisplayText = token + " " + shifted,
                    LegacyDisplayText = shifted + "\n" + token,
                    ShiftedValue = shifted,
                    Flags = layoutFlags
                });
            }
        }

        private static void AddPunctuationKeys(List<KeyDefinition> entries)
        {
            AddPunctuationKey(entries, VirtualKey.OemPlus, "=", "=", "eql", "+", "+\n=", "= +");
            AddPunctuationKey(entries, VirtualKey.OemMinus, "hyphen", "hyph", "hyph", "_", "_\n-", "- _");
            AddPunctuationKey(entries, VirtualKey.Oem2, "/", "/", "fsls", "?", "?\n/", "/ ?");
            AddPunctuationKey(entries, VirtualKey.Oem5, "\\", "\\", "bsls", "|", "|\n\\", "\\ |");
            AddPunctuationKey(entries, VirtualKey.Oem7, "'", "apos", "apos", "\"", "\"\n'", "' \"");
            AddPunctuationKey(entries, VirtualKey.Oem3, "`", "tilde", "grav", "~", "~\n`", "` ~");
            AddPunctuationKey(entries, VirtualKey.Oem1, ";", "colon", "scol", ":", ":\n;", "; :");
            AddPunctuationKey(entries, VirtualKey.OemComma, ",", "com", "comm", "<", "<\n,", ", <");
            AddPunctuationKey(entries, VirtualKey.OemPeriod, ".", "per", "perd", ">", ">\n.", ". >");
            AddPunctuationKey(entries, VirtualKey.Oem4, "obrack", "obrk", "obrk", "{", "{\n[", "[ {");
            AddPunctuationKey(entries, VirtualKey.Oem6, "cbrack", "cbrk", "cbrk", "}", "}\n]", "] }");
            AddPunctuationKey(entries, VirtualKey.Oem102, "intl-\\", "intl\\", "int#", "intl-\\", "", "");
        }

        private static void AddNavigationKeys(List<KeyDefinition> entries)
        {
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Escape, "escape", "esc", "esc", "Esc");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Space, "space", "spc", "spc", "Space");
            AddUniformKey(entries, KeyTable.Navigation, 10034, "lspc", "Space");
            AddUniformKey(entries, KeyTable.Navigation, 10035, "rspc", "Space");
            AddUniformKey(entries, KeyTable.Navigation, 11093, "mspc", "Space");
            AddUniformKey(entries, KeyTable.Navigation, VirtualKey.Tab, "tab", "Tab");
            AddUniformKey(entries, KeyTable.Navigation, VirtualKey.CapsLock, "caps", "Caps\nLock");

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.Return,
                Table = KeyTable.Navigation,
                Dialects = TokenDialects.All,
                LegacyToken = "enter",
                Gen1Token = "ent",
                Gen2Token = "ent",
                DisplayText = "Enter",
                MacDisplayText = "Return"
            });

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.Back,
                Table = KeyTable.Navigation,
                Dialects = TokenDialects.All,
                LegacyToken = "bspace",
                Gen1Token = "bspc",
                Gen2Token = "bspc",
                DisplayText = "Back\nSpace",
                MacDisplayText = "Delete"
            });

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.Delete,
                Table = KeyTable.Navigation,
                Dialects = TokenDialects.All,
                LegacyToken = "delete",
                Gen1Token = "del",
                Gen2Token = "del",
                DisplayText = "Delete",
                MacDisplayText = "Fwd \nDelete"
            });

            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Insert, "insert", "ins", "ins", "Insert");
            AddUniformKey(entries, KeyTable.Navigation, VirtualKey.Home, "home", "Home");
            AddUniformKey(entries, KeyTable.Navigation, VirtualKey.End, "end", "End");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.PageUp, "pup", "pup", "pgup", "Page\nUp");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.PageDown, "pdown", "pdn", "pgdn", "Page\nDown");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Snapshot, "prtscr", "prnt", "prnt", "Print\nScrn");

            // The legacy Print key code is registered with the same token in each dialect (§3.3 note).
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Print, "prtscr", "prnt", "prnt", "Print\nScrn");

            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.ScrollLock, "scroll", "scrlk", "sclk", "Scroll\nLock");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Pause, "pause", "pause", "paus", "Pause\nBreak");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.NumLock, "numlk", "numlk", "nmlk", "Num\nLock");
            AddUniformKey(entries, KeyTable.Navigation, VirtualKey.Up, "up", "↑");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Down, "down", "dwn", "down", "↓");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Left, "left", "lft", "left", "←");
            AddDialectKey(entries, KeyTable.Navigation, VirtualKey.Right, "right", "rght", "rght", "→");
        }

        private static void AddFunctionKeys(List<KeyDefinition> entries)
        {
            for (var number = 1; number <= 24; number++)
            {
                var token = "F" + number.ToString(CultureInfo.InvariantCulture);

                AddUniformKey(entries, KeyTable.FunctionKeys, VirtualKey.F1 + number - 1, token, token);
            }
        }

        private static void AddPunctuationKey(
            List<KeyDefinition> entries,
            int code,
            string legacyToken,
            string gen1Token,
            string gen2Token,
            string shiftedValue,
            string legacyDisplayText,
            string displayText)
        {
            entries.Add(new KeyDefinition
            {
                Code = code,
                Table = KeyTable.Punctuation,
                Dialects = TokenDialects.All,
                LegacyToken = legacyToken,
                Gen1Token = gen1Token,
                Gen2Token = gen2Token,
                DisplayText = displayText,
                LegacyDisplayText = legacyDisplayText,
                ShiftedValue = shiftedValue,
                Flags = KeyDefinitionFlags.ConvertToUnicode | KeyDefinitionFlags.ShowShiftedValue
            });
        }
    }
}
