using System.Globalization;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Key-table data for specs/05-key-model.md §3.5 (modifiers) and §3.6 (keypad keys,
    /// including the Legacy keypad-layer duplicate registrations the §3.6 notes describe).
    /// </summary>
    public static partial class KeyRegistry
    {
        private static void AddModifierKeys(List<KeyDefinition> entries)
        {
            AddDialectKey(entries, KeyTable.Modifiers, VirtualKey.LeftShift, "lshift", "lshft", "lshf", "Left\nShift");
            AddDialectKey(entries, KeyTable.Modifiers, VirtualKey.RightShift, "rshift", "rshft", "rshf", "Right\nShift");
            AddDialectKey(entries, KeyTable.Modifiers, VirtualKey.LeftControl, "lctrl", "lctrl", "lctr", "Left\nCtrl");
            AddDialectKey(entries, KeyTable.Modifiers, VirtualKey.RightControl, "rctrl", "rctrl", "rctr", "Right\nCtrl");

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.LeftMenu,
                Table = KeyTable.Modifiers,
                Dialects = TokenDialects.All,
                LegacyToken = "lalt",
                Gen1Token = "lalt",
                Gen2Token = "lalt",
                DisplayText = "Left\nAlt",
                MacDisplayText = "Left\nOpt"
            });

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.RightMenu,
                Table = KeyTable.Modifiers,
                Dialects = TokenDialects.All,
                LegacyToken = "ralt",
                Gen1Token = "ralt",
                Gen2Token = "ralt",
                DisplayText = "Right\nAlt",
                MacDisplayText = "Right\nOpt"
            });

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.LeftWin,
                Table = KeyTable.Modifiers,
                Dialects = TokenDialects.All,
                LegacyToken = "lwin",
                Gen1Token = "lwin",
                Gen2Token = "lwin",
                DisplayText = "Left\nWin",
                MacDisplayText = "Cmd"
            });

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.RightWin,
                Table = KeyTable.Modifiers,
                Dialects = TokenDialects.All,
                LegacyToken = "rwin",
                Gen1Token = "rwin",
                Gen2Token = "rwin",
                DisplayText = "Right\nWin",
                MacDisplayText = "Right\nCmd"
            });

            // Dedicated Left Cmd entry, Mac only, non-Gen2 apps (§3.5 row 10038).
            entries.Add(new KeyDefinition
            {
                Code = 10038,
                Table = KeyTable.Modifiers,
                Dialects = TokenDialects.Gen1,
                Gen1Token = "lwin",
                MacDisplayText = "Left\nCmd"
            });

            AddUniformKey(entries, KeyTable.Modifiers, VirtualKey.Shift, "shift", "Shift", TokenDialects.Legacy);
            AddUniformKey(entries, KeyTable.Modifiers, VirtualKey.Control, "ctrl", "Ctrl", TokenDialects.Legacy);

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.Menu,
                Table = KeyTable.Modifiers,
                Dialects = TokenDialects.Legacy,
                LegacyToken = "alt",
                DisplayText = "Alt",
                MacDisplayText = "Opt"
            });

            AddDialectKey(entries, KeyTable.Modifiers, 11090, "hyper", "hyper", "hypr", "Hyper");
            AddUniformKey(entries, KeyTable.Modifiers, 11091, "meh", "Meh");
        }

        private static void AddKeypadKeys(List<KeyDefinition> entries)
        {
            for (var digit = 0; digit <= 9; digit++)
            {
                AddUniformKey(
                    entries,
                    KeyTable.KeypadKeys,
                    VirtualKey.Numpad0 + digit,
                    "kp" + digit.ToString(CultureInfo.InvariantCulture),
                    digit.ToString(CultureInfo.InvariantCulture));
            }

            // Legacy keypad-layer duplicates of kp0..kp9 under codes 10056..10065 (§3.6 note).
            for (var digit = 0; digit <= 9; digit++)
            {
                AddUniformKey(
                    entries,
                    KeyTable.KeypadKeys,
                    10056 + digit,
                    "kp" + digit.ToString(CultureInfo.InvariantCulture),
                    digit.ToString(CultureInfo.InvariantCulture),
                    TokenDialects.Legacy);
            }

            AddDialectKey(entries, KeyTable.KeypadKeys, VirtualKey.Divide, "kpdiv", "kp/", "kp/", "/");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10054, "kpdiv", "/", TokenDialects.Legacy);
            AddDialectKey(entries, KeyTable.KeypadKeys, VirtualKey.Multiply, "kpmult", "kp*", "kp*", "*");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10055, "kpmult", "*", TokenDialects.Legacy);
            AddDialectKey(entries, KeyTable.KeypadKeys, VirtualKey.Subtract, "kpmin", "kp-", "kp-", "-");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10066, "kpmin", "-", TokenDialects.Legacy);
            AddDialectKey(entries, KeyTable.KeypadKeys, VirtualKey.Add, "kpplus", "kp+", "kp+", "+");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10067, "kpplus", "+", TokenDialects.Legacy);
            AddUniformKey(entries, KeyTable.KeypadKeys, VirtualKey.Decimal, "kp.", ".");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10070, "kp.", ".", TokenDialects.Legacy);

            AddDialectKey(entries, KeyTable.KeypadKeys, 10000, "kpenter", "kpent", "kpen", "Kp\nEnter");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10053, "kp=", "=");
            AddDialectKey(entries, KeyTable.KeypadKeys, 10052, "numlk", "numlk", "nmlk", "Num\nLock");
            AddUniformKey(entries, KeyTable.KeypadKeys, 10048, "kpshft", "Kp\nShift", TokenDialects.Legacy);
            AddUniformKey(entries, KeyTable.KeypadKeys, 10068, "kpenter1", "Kp\nEnter", TokenDialects.Legacy);
            AddUniformKey(entries, KeyTable.KeypadKeys, 10069, "kpenter2", "Kp\nEnter", TokenDialects.Legacy);
        }
    }
}
