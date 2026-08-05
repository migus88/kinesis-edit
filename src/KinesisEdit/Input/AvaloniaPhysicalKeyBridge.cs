using Avalonia.Input;
using KinesisEdit.Core.Input;

namespace KinesisEdit.Input
{
    /// <summary>
    /// Translates Avalonia's <see cref="PhysicalKey"/> into Core's toolkit-neutral
    /// <see cref="PhysicalKeyCode"/>. Both enums name their members after the W3C
    /// <c>KeyboardEvent.code</c> values, so the table is built once by name instead of being
    /// hand-written — see specs/10-apps-and-ui.md ("Keyboard-input capture"), which needs the
    /// physical position (left vs right modifier, keypad vs main Enter) rather than the
    /// layout-dependent character.
    /// <para>
    /// Checked by hand against Avalonia 11.3.12: every member of <see cref="PhysicalKeyCode"/> is
    /// spelled identically in <see cref="PhysicalKey"/>, so no alias table is needed. Nothing
    /// guards that — a renamed Avalonia member silently degrades its position to
    /// <see cref="PhysicalKeyCode.None"/>, which passes through to the OS. Should a future
    /// Avalonia version rename a position, add the alias here — never rename the Core enum,
    /// whose names are the W3C contract.
    /// </para>
    /// </summary>
    internal static class AvaloniaPhysicalKeyBridge
    {
        private static readonly Dictionary<PhysicalKey, PhysicalKeyCode> _keyCodesByPhysicalKey = BuildKeyCodes();

        /// <summary>
        /// Returns the Core position for <paramref name="key"/>, or
        /// <see cref="PhysicalKeyCode.None"/> for <see cref="PhysicalKey.None"/> and for every
        /// position Core deliberately does not declare (the Japanese <c>IntlRo</c>/<c>IntlYen</c>
        /// keys, browser and power keys, and so on). The recorder passes those through.
        /// </summary>
        public static PhysicalKeyCode Translate(PhysicalKey key)
        {
            if (_keyCodesByPhysicalKey.TryGetValue(key, out var code))
            {
                return code;
            }

            return PhysicalKeyCode.None;
        }

        private static Dictionary<PhysicalKey, PhysicalKeyCode> BuildKeyCodes()
        {
            var codes = new Dictionary<PhysicalKey, PhysicalKeyCode>();

            foreach (var name in Enum.GetNames<PhysicalKey>())
            {
                // Case-sensitive on purpose: a match must be the same W3C name, not a
                // coincidence of casing.
                if (!Enum.TryParse<PhysicalKeyCode>(name, ignoreCase: false, out var code))
                {
                    continue;
                }

                if (code == PhysicalKeyCode.None)
                {
                    continue;
                }

                codes[Enum.Parse<PhysicalKey>(name)] = code;
            }

            return codes;
        }
    }
}
