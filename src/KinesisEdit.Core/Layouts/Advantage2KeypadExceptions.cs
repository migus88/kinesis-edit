using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// The Advantage2 keypad-exception tokens of specs/05-key-model.md §5.4 and
    /// specs/04-layout-file-format.md §3.2: bottom-layer tokens that carry no <c>kp-</c> prefix
    /// because they denote keypad-layer-only actions with their own legacy key identities
    /// (codes 10043-10070, plus <c>kp-insert</c> → the standard Insert key). On load an
    /// exception token routes the line to the keypad layer; on save an exception token is
    /// written without the <c>kp-</c> prefix the other bottom-layer tokens get.
    /// <para>
    /// The explicit token→code map exists because plain registry lookup resolves several of
    /// these tokens to a different entry (spec 05 §7 first-match: <c>numlk</c> → the VK NumLock
    /// row, <c>kp0</c> → the VK Numpad0 row, …), so the spec's dedicated keypad-layer codes
    /// have to be pinned here.
    /// </para>
    /// </summary>
    public static class Advantage2KeypadExceptions
    {
        /// <summary>The 3-character layer prefix of the Advantage2 dialect (04 §3.2, 05 §5.4).</summary>
        public const string KeypadPrefix = "kp-";

        /// <summary>The exception tokens in spec order with their legacy key codes (05 §5.4).</summary>
        public static IReadOnlyList<KeyValuePair<string, int>> TokenCodes { get; } =
        [
            new("menu", 10043),
            new("play", 10044),
            new("prev", 10045),
            new("next", 10046),
            new("calc", 10047),
            new("kpshft", 10048),
            new("mute", 10049),
            new("vol-", 10050),
            new("vol+", 10051),
            new("kp0", 10056),
            new("kp1", 10057),
            new("kp2", 10058),
            new("kp3", 10059),
            new("kp4", 10060),
            new("kp5", 10061),
            new("kp6", 10062),
            new("kp7", 10063),
            new("kp8", 10064),
            new("kp9", 10065),
            new("numlk", 10052),
            new("kp=", 10053),
            new("kpdiv", 10054),
            new("kpmult", 10055),
            new("kpmin", 10066),
            new("kpplus", 10067),
            new("kpenter1", 10068),
            new("kpenter2", 10069),
            new("kp.", 10070),
            // "kp-insert" is first stripped of its "kp-" prefix, leaving "insert" — the
            // standard VK_INSERT key (05 §5.4).
            new("kp-insert", VirtualKey.Insert)
        ];

        private static readonly Dictionary<string, int> _codesByToken = BuildIndex();

        /// <summary>Whether <paramref name="token"/> is one of the exception tokens, ignoring case (05 §5.4).</summary>
        public static bool IsExceptionToken(string? token)
        {
            return token is not null && _codesByToken.ContainsKey(token);
        }

        /// <summary>
        /// Maps an exception token to its dedicated keypad-layer key code (05 §5.4), ignoring
        /// case. False for anything outside the list.
        /// </summary>
        public static bool TryGetCode(string? token, out int code)
        {
            code = 0;

            return token is not null && _codesByToken.TryGetValue(token, out code);
        }

        private static Dictionary<string, int> BuildIndex()
        {
            var index = new Dictionary<string, int>(TokenCodes.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var (token, code) in TokenCodes)
            {
                index.Add(token, code);
            }

            return index;
        }
    }
}
