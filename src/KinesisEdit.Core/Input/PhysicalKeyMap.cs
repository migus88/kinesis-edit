using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// Translates a physical key position (<see cref="PhysicalKeyCode"/>) into the key-table
    /// entry the app records for it (specs/05-key-model.md §3), which is the first half of the
    /// capture contract of specs/10-apps-and-ui.md ("Keyboard-input capture").
    /// <para>
    /// Two spec rules shape the table: the left/right modifier positions resolve to their
    /// dedicated codes (§3.5) rather than the generic Shift/Ctrl/Alt codes, and the keypad
    /// Enter resolves to the distinct internal code 10000, never to the main Enter's
    /// <c>0x0D</c> (specs/10-apps-and-ui.md; specs/12-savant-elite.md §"Programming a pedal").
    /// </para>
    /// </summary>
    public static class PhysicalKeyMap
    {
        /// <summary>Distinct internal code of the keypad Enter, token <c>kpenter</c> (specs/05-key-model.md §3.6).</summary>
        private const int KeypadEnterCode = 10000;

        /// <summary>Internal code of the keypad equals key, token <c>kp=</c> (specs/05-key-model.md §3.6).</summary>
        private const int KeypadEqualCode = 10053;

        /// <summary>Internal code of the eject key, token <c>ejct</c> (specs/05-key-model.md §3.7).</summary>
        private const int EjectCode = 11150;

        private static readonly IReadOnlyDictionary<PhysicalKeyCode, int> _keyCodesByPosition = BuildKeyCodes();
        private static readonly IReadOnlySet<PhysicalKeyCode> _modifierPositions = BuildModifierPositions();

        /// <summary>
        /// Returns the key-table entry for <paramref name="key"/>, or null for
        /// <see cref="PhysicalKeyCode.None"/> and any position this map does not cover.
        /// Resolution goes through <see cref="KeyRegistry.FindByCode"/>, so the spec's
        /// first-match semantics (§7) apply.
        /// </summary>
        public static KeyDefinition? Resolve(PhysicalKeyCode key)
        {
            if (!_keyCodesByPosition.TryGetValue(key, out var code))
            {
                return null;
            }

            return KeyRegistry.FindByCode(code);
        }

        /// <summary>
        /// Returns whether <paramref name="key"/> is one of the eight left/right modifier
        /// positions (specs/05-key-model.md §3.5) whose state the recorder accumulates while
        /// capturing (specs/10-apps-and-ui.md, "Keyboard-input capture").
        /// </summary>
        public static bool IsModifier(PhysicalKeyCode key)
        {
            return _modifierPositions.Contains(key);
        }

        private static Dictionary<PhysicalKeyCode, int> BuildKeyCodes()
        {
            var codes = new Dictionary<PhysicalKeyCode, int>();

            AddLetterAndDigitCodes(codes);
            AddPunctuationCodes(codes);
            AddNavigationCodes(codes);
            AddFunctionKeyCodes(codes);
            AddKeypadCodes(codes);
            AddModifierCodes(codes);
            AddMediaCodes(codes);

            return codes;
        }

        private static void AddLetterAndDigitCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            for (var offset = 0; offset < 26; offset++)
            {
                codes.Add(PhysicalKeyCode.A + offset, VirtualKey.A + offset);
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                codes.Add(PhysicalKeyCode.Digit0 + digit, VirtualKey.Digit0 + digit);
            }
        }

        private static void AddPunctuationCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            codes.Add(PhysicalKeyCode.Backquote, VirtualKey.Oem3);
            codes.Add(PhysicalKeyCode.Minus, VirtualKey.OemMinus);
            codes.Add(PhysicalKeyCode.Equal, VirtualKey.OemPlus);
            codes.Add(PhysicalKeyCode.BracketLeft, VirtualKey.Oem4);
            codes.Add(PhysicalKeyCode.BracketRight, VirtualKey.Oem6);
            codes.Add(PhysicalKeyCode.Backslash, VirtualKey.Oem5);
            codes.Add(PhysicalKeyCode.Semicolon, VirtualKey.Oem1);
            codes.Add(PhysicalKeyCode.Quote, VirtualKey.Oem7);
            codes.Add(PhysicalKeyCode.Comma, VirtualKey.OemComma);
            codes.Add(PhysicalKeyCode.Period, VirtualKey.OemPeriod);
            codes.Add(PhysicalKeyCode.Slash, VirtualKey.Oem2);
            codes.Add(PhysicalKeyCode.IntlBackslash, VirtualKey.Oem102);
        }

        private static void AddNavigationCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            codes.Add(PhysicalKeyCode.Escape, VirtualKey.Escape);
            codes.Add(PhysicalKeyCode.Backspace, VirtualKey.Back);
            codes.Add(PhysicalKeyCode.Tab, VirtualKey.Tab);
            codes.Add(PhysicalKeyCode.Enter, VirtualKey.Return);
            codes.Add(PhysicalKeyCode.Space, VirtualKey.Space);
            codes.Add(PhysicalKeyCode.CapsLock, VirtualKey.CapsLock);

            codes.Add(PhysicalKeyCode.PrintScreen, VirtualKey.Snapshot);
            codes.Add(PhysicalKeyCode.ScrollLock, VirtualKey.ScrollLock);
            codes.Add(PhysicalKeyCode.Pause, VirtualKey.Pause);

            codes.Add(PhysicalKeyCode.Insert, VirtualKey.Insert);
            codes.Add(PhysicalKeyCode.Delete, VirtualKey.Delete);
            codes.Add(PhysicalKeyCode.Home, VirtualKey.Home);
            codes.Add(PhysicalKeyCode.End, VirtualKey.End);
            codes.Add(PhysicalKeyCode.PageUp, VirtualKey.PageUp);
            codes.Add(PhysicalKeyCode.PageDown, VirtualKey.PageDown);

            codes.Add(PhysicalKeyCode.ArrowUp, VirtualKey.Up);
            codes.Add(PhysicalKeyCode.ArrowDown, VirtualKey.Down);
            codes.Add(PhysicalKeyCode.ArrowLeft, VirtualKey.Left);
            codes.Add(PhysicalKeyCode.ArrowRight, VirtualKey.Right);

            codes.Add(PhysicalKeyCode.ContextMenu, VirtualKey.Apps);
        }

        private static void AddFunctionKeyCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            for (var offset = 0; offset < 24; offset++)
            {
                codes.Add(PhysicalKeyCode.F1 + offset, VirtualKey.F1 + offset);
            }
        }

        private static void AddKeypadCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            codes.Add(PhysicalKeyCode.NumLock, VirtualKey.NumLock);

            for (var digit = 0; digit <= 9; digit++)
            {
                codes.Add(PhysicalKeyCode.NumPad0 + digit, VirtualKey.Numpad0 + digit);
            }

            codes.Add(PhysicalKeyCode.NumPadDivide, VirtualKey.Divide);
            codes.Add(PhysicalKeyCode.NumPadMultiply, VirtualKey.Multiply);
            codes.Add(PhysicalKeyCode.NumPadSubtract, VirtualKey.Subtract);
            codes.Add(PhysicalKeyCode.NumPadAdd, VirtualKey.Add);
            codes.Add(PhysicalKeyCode.NumPadDecimal, VirtualKey.Decimal);
            codes.Add(PhysicalKeyCode.NumPadEqual, KeypadEqualCode);

            // The keypad Enter is a distinct internal code, never VirtualKey.Return
            // (specs/10-apps-and-ui.md; specs/12-savant-elite.md §"Programming a pedal").
            codes.Add(PhysicalKeyCode.NumPadEnter, KeypadEnterCode);
        }

        private static void AddModifierCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            codes.Add(PhysicalKeyCode.ShiftLeft, VirtualKey.LeftShift);
            codes.Add(PhysicalKeyCode.ShiftRight, VirtualKey.RightShift);
            codes.Add(PhysicalKeyCode.ControlLeft, VirtualKey.LeftControl);
            codes.Add(PhysicalKeyCode.ControlRight, VirtualKey.RightControl);
            codes.Add(PhysicalKeyCode.AltLeft, VirtualKey.LeftMenu);
            codes.Add(PhysicalKeyCode.AltRight, VirtualKey.RightMenu);
            codes.Add(PhysicalKeyCode.MetaLeft, VirtualKey.LeftWin);
            codes.Add(PhysicalKeyCode.MetaRight, VirtualKey.RightWin);
        }

        private static void AddMediaCodes(Dictionary<PhysicalKeyCode, int> codes)
        {
            // The media/volume block of specs/05-key-model.md §3.7. Declaring these keeps the
            // capture layer from handing a Mute or Play press back to the OS instead of recording it.
            codes.Add(PhysicalKeyCode.AudioVolumeMute, VirtualKey.VolumeMute);
            codes.Add(PhysicalKeyCode.AudioVolumeDown, VirtualKey.VolumeDown);
            codes.Add(PhysicalKeyCode.AudioVolumeUp, VirtualKey.VolumeUp);
            codes.Add(PhysicalKeyCode.MediaTrackNext, VirtualKey.MediaNextTrack);
            codes.Add(PhysicalKeyCode.MediaTrackPrevious, VirtualKey.MediaPrevTrack);
            codes.Add(PhysicalKeyCode.MediaStop, VirtualKey.MediaStop);
            codes.Add(PhysicalKeyCode.MediaPlayPause, VirtualKey.MediaPlayPause);
            codes.Add(PhysicalKeyCode.Eject, EjectCode);
        }

        private static HashSet<PhysicalKeyCode> BuildModifierPositions()
        {
            return new HashSet<PhysicalKeyCode>
            {
                PhysicalKeyCode.ShiftLeft,
                PhysicalKeyCode.ShiftRight,
                PhysicalKeyCode.ControlLeft,
                PhysicalKeyCode.ControlRight,
                PhysicalKeyCode.AltLeft,
                PhysicalKeyCode.AltRight,
                PhysicalKeyCode.MetaLeft,
                PhysicalKeyCode.MetaRight
            };
        }
    }
}
