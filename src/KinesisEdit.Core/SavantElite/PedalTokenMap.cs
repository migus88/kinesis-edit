using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Token ↔ key resolution for <c>active/pedals.txt</c> (specs/12-savant-elite.md §4.4). The
    /// pedal file is written in the Legacy token dialect, so almost every token resolves straight
    /// through <see cref="KeyRegistry.FindByToken(string?, TokenDialect)"/>; this type holds the
    /// handful of §4.4 spellings the key table cannot express, in the spirit of
    /// <see cref="Layouts.Advantage2KeypadExceptions"/>.
    /// <para>
    /// Read side (<see cref="ExtraTokenCodes"/>): the macro-mode space, whose file token is a bare
    /// space character that <c>FindByToken</c> rejects as whitespace; the <c>{125}</c>/<c>{500}</c>
    /// delay spelling of firmware 1.0.44 field files (§4.4 "delay-token generation difference");
    /// and <c>win</c>, which §4.4 lists as the pedal's only Win/Cmd modifier token while the key
    /// table names that key <c>lwin</c>/<c>rwin</c> (05 §3.5).
    /// </para>
    /// <para>
    /// Write side: <see cref="GetMacroToken"/>/<see cref="GetSingleToken"/> emit the §4.4
    /// spellings — <c>win</c> for either Win key, and a macro space as a literal <c>{ }</c> while
    /// single mode writes <c>[space]</c> (§4.4 "the save value is a single space character, so a
    /// macro space is written <c>{ }</c>"). Everything else is the canonical registry token, so a
    /// <c>{125}</c> file upgrades to <c>{d125}</c> on the first save.
    /// </para>
    /// </summary>
    public static class PedalTokenMap
    {
        /// <summary>The macro-mode file token of the space bar — one space character (§4.4).</summary>
        public const string SpaceToken = " ";

        /// <summary>The §4.4 file token of the Win/Cmd modifier; the key table spells the same keys <c>lwin</c>/<c>rwin</c>.</summary>
        public const string WinToken = "win";

        /// <summary>Legacy key code of the 125 ms delay pseudo-key (05 §3.12).</summary>
        private const int Delay125Code = 10007;

        /// <summary>Legacy key code of the 500 ms delay pseudo-key (05 §3.12).</summary>
        private const int Delay500Code = 10008;

        /// <summary>Legacy key code of the left mouse button (05 §3.8).</summary>
        private const int LeftMouseCode = 10001;

        /// <summary>The 125 ms delay key of the double-click macro (§4.5, §6).</summary>
        public static KeyDefinition Delay125Key { get; }

        /// <summary>The space bar entry (§4.4); its Legacy token <c>space</c> is what single mode writes.</summary>
        public static KeyDefinition SpaceKey { get; }

        /// <summary>The left mouse button (§4.4, §6).</summary>
        public static KeyDefinition LeftMouseKey { get; }

        /// <summary>
        /// The §4.4 token spellings that are not in the key table's Legacy column, with the key
        /// codes they resolve to. Read-only tolerance: they are accepted on load, never written.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, int>> ExtraTokenCodes { get; } =
        [
            new(SpaceToken, VirtualKey.Space),
            new("125", Delay125Code),
            new("500", Delay500Code),
            new(WinToken, VirtualKey.LeftWin)
        ];

        private static readonly Dictionary<string, KeyDefinition> _extraKeysByToken;

        static PedalTokenMap()
        {
            Delay125Key = RequireCode(Delay125Code);
            SpaceKey = RequireCode(VirtualKey.Space);
            LeftMouseKey = RequireCode(LeftMouseCode);

            _extraKeysByToken = new Dictionary<string, KeyDefinition>(
                ExtraTokenCodes.Count,
                StringComparer.OrdinalIgnoreCase);

            foreach (var (token, code) in ExtraTokenCodes)
            {
                _extraKeysByToken[token] = RequireCode(code);
            }
        }

        /// <summary>
        /// Resolves one file token (§4.4), case-insensitively: the Legacy dialect first, then the
        /// pedal-only spellings above, then any dialect — the read-tolerantly rule, so a file
        /// carrying a keyboard-app spelling still loads. Null when nothing matches.
        /// </summary>
        public static KeyDefinition? Resolve(string? token)
        {
            if (token is null || token.Length == 0)
            {
                return null;
            }

            return KeyRegistry.FindByToken(token, TokenDialect.Legacy)
                   ?? _extraKeysByToken.GetValueOrDefault(token)
                   ?? KeyRegistry.FindByToken(token);
        }

        /// <summary>
        /// The token written inside a single-action <c>[...]</c> group (§4.3). The space bar is
        /// written <c>[space]</c> here — only macro mode uses the bare-space spelling.
        /// </summary>
        public static string GetSingleToken(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            // §4.4 lists only "win": both Win keys write it, and it reads back as Left Win.
            if (key.Code is VirtualKey.LeftWin or VirtualKey.RightWin)
            {
                return WinToken;
            }

            if (key.LegacyToken.Length > 0)
            {
                return key.LegacyToken;
            }

            return key.Gen1Token.Length > 0 ? key.Gen1Token : key.Gen2Token;
        }

        /// <summary>
        /// The token written inside a macro <c>{...}</c> group (§4.3): as
        /// <see cref="GetSingleToken"/>, except that the space bar writes the bare space of
        /// <see cref="SpaceToken"/>, producing the literal group <c>{ }</c> (§4.4).
        /// </summary>
        public static string GetMacroToken(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            return key.Code == VirtualKey.Space ? SpaceToken : GetSingleToken(key);
        }

        private static KeyDefinition RequireCode(int code)
        {
            return KeyRegistry.FindByCode(code)
                   ?? throw new InvalidOperationException($"Key code {code} is missing from the key registry.");
        }
    }
}
