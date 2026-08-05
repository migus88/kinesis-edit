namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Key-table data for specs/05-key-model.md §3.7 (media/volume keys), §3.8 (mouse actions),
    /// and §3.9 (special actions and device buttons, including the keypad-layer duplicate
    /// registrations the §3.9 notes describe).
    /// </summary>
    public static partial class KeyRegistry
    {
        private static void AddMediaKeys(List<KeyDefinition> entries)
        {
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, VirtualKey.VolumeMute, "mute", "\U0001F568", "Mute");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, VirtualKey.VolumeDown, "vol-", "\U0001F569", "Vol-");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, VirtualKey.VolumeUp, "vol+", "\U0001F56A", "Vol+");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, VirtualKey.MediaStop, "stop", "◼", "Stop");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, VirtualKey.MediaPrevTrack, "prev", "⏮", "Prev");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, VirtualKey.MediaNextTrack, "next", "⏭", "Next");

            entries.Add(new KeyDefinition
            {
                Code = VirtualKey.MediaPlayPause,
                Table = KeyTable.MediaKeys,
                Dialects = TokenDialects.All,
                LegacyToken = "play",
                Gen1Token = "play",
                Gen2Token = "plpa",
                GlyphText = "⏯",
                DisplayText = "Play\nPause"
            });

            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 11151, "play", "▶", "Play", TokenDialects.Gen2);
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 11147, "fwrd", "⏩", "Forward");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 11148, "rewd", "⏪", "Rewind");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 11149, "cpau", "⏸", "Pause");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 11150, "ejct", "⏏", "Eject");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 11152, "recr", "⏺", "Record");
            AddUniformKey(entries, KeyTable.MediaKeys, 11153, "ranp", "Rand\nPlay");
            AddUniformKey(entries, KeyTable.MediaKeys, 11154, "plsk", "Play\nSkip");

            // Keypad-layer duplicates for Advantage2; also registered on all devices (§3.7).
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 10044, "play", "⏯", "Play");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 10045, "prev", "⏮", "Prev");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 10046, "next", "⏭", "Next");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 10049, "mute", "\U0001F568", "Mute");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 10050, "vol-", "\U0001F569", "Vol-");
            AddUniformGlyphKey(entries, KeyTable.MediaKeys, 10051, "vol+", "\U0001F56A", "Vol+");
        }

        private static void AddMouseKeys(List<KeyDefinition> entries)
        {
            const TokenDialects gen1AndGen2 = TokenDialects.Gen1 | TokenDialects.Gen2;

            AddDialectKey(entries, KeyTable.MouseActions, 10001, "lmouse", "lmous", "lmou", "Left\nMouse");
            AddDialectKey(entries, KeyTable.MouseActions, 10002, "mmouse", "mmous", "mmou", "Middle\nMouse");
            AddDialectKey(entries, KeyTable.MouseActions, 10003, "rmouse", "rmous", "rmou", "Right\nMouse");
            AddDialectKey(entries, KeyTable.MouseActions, 10036, "", "mous4", "4mou", "Mouse\nBtn 4", gen1AndGen2);
            AddDialectKey(entries, KeyTable.MouseActions, 10037, "", "mous5", "5mou", "Mouse\nBtn 5", gen1AndGen2);
            AddUniformKey(entries, KeyTable.MouseActions, 11155, "sumo", "Mouse\nScroll Up", TokenDialects.Gen2);
            AddUniformKey(entries, KeyTable.MouseActions, 11156, "sdmo", "Mouse\nScroll Down", TokenDialects.Gen2);
            AddUniformKey(entries, KeyTable.MouseActions, 11157, "moul", "Mouse\nMove Left", TokenDialects.Gen2);
            AddUniformKey(entries, KeyTable.MouseActions, 11158, "mour", "Mouse\nMove Right", TokenDialects.Gen2);
            AddUniformKey(entries, KeyTable.MouseActions, 11159, "mouu", "Mouse\nMove Up", TokenDialects.Gen2);
            AddUniformKey(entries, KeyTable.MouseActions, 11160, "moud", "Mouse\nMove Down", TokenDialects.Gen2);
        }

        private static void AddSpecialActionKeys(List<KeyDefinition> entries)
        {
            entries.Add(new KeyDefinition
            {
                Code = 10042,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = "kptoggle",
                Gen1Token = "kptoggle",
                Gen2Token = "kp",
                DisplayText = "Key-\npad",
                Gen2DisplayText = "Kp\nToggle"
            });

            AddAppsKey(entries, VirtualKey.Apps);

            // Keypad-layer duplicate of the PC Menu key under 10043 (§3.9 note).
            AddAppsKey(entries, 10043);

            entries.Add(new KeyDefinition
            {
                Code = 10010,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = "shutdn",
                Gen1Token = "shtdn",
                Gen2Token = "pwdn",
                DisplayText = "Shut\ndown",
                Gen2DisplayText = "Power"
            });

            AddDialectKey(entries, KeyTable.SpecialActions, 10023, "micmute", "micmute", "mmut", "Mic\nMute");
            AddUniformKey(entries, KeyTable.SpecialActions, 10009, "calc", "Calc");

            // Keypad-layer duplicate of the Calculator key under 10047 (§3.9 note).
            AddUniformKey(entries, KeyTable.SpecialActions, 10047, "calc", "Calc");

            AddDialectKey(entries, KeyTable.SpecialActions, 10020, "fntoggle", "fntog", "fntog", "Fn\nToggle");
            AddDialectKey(entries, KeyTable.SpecialActions, 10021, "fnshift", "fnshf", "fnshf", "Fn\nShift");

            entries.Add(new KeyDefinition
            {
                Code = 10022,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = "LED",
                Gen1Token = "LED",
                Gen2Token = "ledt",
                GlyphText = "☀",
                DisplayText = "LED"
            });

            AddUniformGlyphKey(entries, KeyTable.SpecialActions, 11161, "led+", "\U0001F506", "Led+");
            AddUniformGlyphKey(entries, KeyTable.SpecialActions, 11162, "led-", "\U0001F505", "Led-");

            entries.Add(new KeyDefinition
            {
                Code = 10019,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = "null",
                Gen1Token = "null",
                Gen2Token = "null",
                GlyphText = "⊗",
                DisplayText = " ",
                Gen2DisplayText = "NUL"
            });

            // Program button: not writable to files, empty token (§3.9 row 10013).
            entries.Add(new KeyDefinition
            {
                Code = 10013,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                DisplayText = "Pro-\ngram",
                Flags = KeyDefinitionFlags.NotWritable
            });

            AddUniformKey(
                entries,
                KeyTable.SpecialActions,
                10017,
                "Fn",
                "Fn",
                TokenDialects.All,
                KeyDefinitionFlags.HiddenFromSearch);

            AddUniformKey(entries, KeyTable.SpecialActions, 11146, "ss");
            AddUniformKey(entries, KeyTable.SpecialActions, 11173, "mstp", "Stop\nmacro playback");
            AddUniformKey(entries, KeyTable.SpecialActions, 11207, "pedl", "Tab", TokenDialects.Gen2);
            AddUniformKey(entries, KeyTable.SpecialActions, 10039, "lp-tab", "Tab", TokenDialects.Legacy);
            AddUniformKey(entries, KeyTable.SpecialActions, 10040, "mp-kpshf", "Kp\nShift", TokenDialects.Legacy);
            AddUniformKey(entries, KeyTable.SpecialActions, 10041, "rp-kpent", "Kp\nEnter", TokenDialects.Legacy);

            // "Different press and release" marker (§3.9 row 10011).
            AddUniformKey(entries, KeyTable.SpecialActions, 10011, "{ }");
        }

        private static void AddAppsKey(List<KeyDefinition> entries, int code)
        {
            entries.Add(new KeyDefinition
            {
                Code = code,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = "menu",
                Gen1Token = "menu",
                Gen2Token = "app",
                DisplayText = "PC\nMenu",
                Gen2DisplayText = "App"
            });
        }
    }
}
