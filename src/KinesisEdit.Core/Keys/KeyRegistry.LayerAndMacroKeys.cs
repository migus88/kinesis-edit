using System.Globalization;

namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Key-table data for specs/05-key-model.md §3.10 (layer shift/toggle keys), §3.11
    /// (profiles and hotkeys), §3.12 (macro speed and timing-delay pseudo-keys), and §3.13
    /// (TKO edge-lighting zones).
    /// </summary>
    public static partial class KeyRegistry
    {
        private static void AddLayerKeys(List<KeyDefinition> entries)
        {
            // Layer key without a file token: not writable (§3.10 row 10014).
            entries.Add(new KeyDefinition
            {
                Code = 10014,
                Table = KeyTable.LayerKeys,
                Dialects = TokenDialects.All,
                DisplayText = "Key-\npad",
                Flags = KeyDefinitionFlags.NotWritable
            });

            AddUniformKey(entries, KeyTable.LayerKeys, 10016, "kpshft", "Kp\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11163, "defs", "Base\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11164, "deft", "Base\nToggle");
            AddUniformKey(entries, KeyTable.LayerKeys, 11165, "keys", "Kp\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11166, "keyt", "Kp\nToggle");
            AddUniformKey(entries, KeyTable.LayerKeys, 11201, "lfn", "Left Fn\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11202, "rfn", "Right Fn\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11167, "fn1s", "Fn1\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11168, "fn1t", "Fn1\nToggle");
            AddUniformKey(entries, KeyTable.LayerKeys, 11169, "fn2s", "Fn2\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11170, "fn2t", "Fn2\nToggle");
            AddUniformKey(entries, KeyTable.LayerKeys, 11171, "fn3s", "Fn3\nShift");
            AddUniformKey(entries, KeyTable.LayerKeys, 11172, "fn3t", "Fn3\nToggle");
        }

        private static void AddProfileAndHotkeyKeys(List<KeyDefinition> entries)
        {
            for (var number = 0; number <= 9; number++)
            {
                var suffix = number.ToString(CultureInfo.InvariantCulture);

                AddUniformKey(entries, KeyTable.ProfilesAndHotkeys, 11174 + number, "pro" + suffix, "Profile " + suffix);
            }

            AddUniformKey(entries, KeyTable.ProfilesAndHotkeys, 10071, "hk0", " ");

            for (var number = 1; number <= 8; number++)
            {
                AddUniformKey(
                    entries,
                    KeyTable.ProfilesAndHotkeys,
                    10023 + number,
                    "hk" + number.ToString(CultureInfo.InvariantCulture),
                    " ");
            }

            AddUniformKey(entries, KeyTable.ProfilesAndHotkeys, 10032, "hk9", "Fn\nToggle");
            AddUniformKey(entries, KeyTable.ProfilesAndHotkeys, 10033, "hk10", "PC\nMenu");
        }

        private static void AddMacroTimingKeys(List<KeyDefinition> entries)
        {
            const KeyDefinitionFlags timingFlags =
                KeyDefinitionFlags.HiddenFromSearch | KeyDefinitionFlags.SingleEvent;

            AddUniformKey(entries, KeyTable.MacroTiming, 10005, "speed1", "", TokenDialects.All, timingFlags);
            AddUniformKey(entries, KeyTable.MacroTiming, 10006, "speed3", "", TokenDialects.All, timingFlags);
            AddUniformKey(entries, KeyTable.MacroTiming, 10012, "speed5", "", TokenDialects.All, timingFlags);

            for (var number = 1; number <= 9; number++)
            {
                AddUniformKey(
                    entries,
                    KeyTable.MacroTiming,
                    11191 + number,
                    "s" + number.ToString(CultureInfo.InvariantCulture),
                    "",
                    TokenDialects.All,
                    timingFlags);
            }

            AddUniformKey(entries, KeyTable.MacroTiming, 10007, "d125", "", TokenDialects.All, timingFlags);
            AddUniformKey(entries, KeyTable.MacroTiming, 10008, "d500", "", TokenDialects.All, timingFlags);
            AddUniformKey(entries, KeyTable.MacroTiming, 10087, "dran", "", TokenDialects.All, timingFlags);

            // Precise delays d001..d999 = codes 10086..11084 (§2, §3.12); token is 'd' plus the
            // delay in ms zero-padded to three digits.
            for (var delay = 1; delay <= 999; delay++)
            {
                AddUniformKey(
                    entries,
                    KeyTable.MacroTiming,
                    10085 + delay,
                    "d" + delay.ToString("000", CultureInfo.InvariantCulture),
                    "",
                    TokenDialects.All,
                    timingFlags);
            }
        }

        private static void AddEdgeZoneKeys(List<KeyDefinition> entries)
        {
            AddEdgeZoneRange(entries, 11113, "L", 9);
            AddEdgeZoneRange(entries, 11122, "B", 15);
            AddEdgeZoneRange(entries, 11137, "R", 9);
        }

        private static void AddEdgeZoneRange(List<KeyDefinition> entries, int firstCode, string prefix, int count)
        {
            for (var number = 1; number <= count; number++)
            {
                AddUniformKey(
                    entries,
                    KeyTable.EdgeZones,
                    firstCode + number - 1,
                    prefix + number.ToString(CultureInfo.InvariantCulture),
                    "",
                    TokenDialects.All,
                    KeyDefinitionFlags.HiddenFromSearch);
            }
        }
    }
}
