using KinesisEdit.Core.Keys;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.Integration
{
    /// <summary>
    /// Cross-module fidelity check between the Savant Elite2 file engine and the key-token
    /// registry: every input name of <see cref="KeyRegistry.PedalPositionTokens"/>
    /// (specs/05-key-model.md §3.14) is parseable as a pedal line, and every file token of the
    /// specs/12-savant-elite.md §4.4 catalog resolves — through the Legacy dialect or through the
    /// explicit <see cref="PedalTokenMap"/> — and writes back to a token that resolves again.
    /// Mirrors <see cref="LightingTokenIntegrationTests"/>.
    /// </summary>
    public class PedalTokenIntegrationTests
    {
        /// <summary>The complete file-token catalog of 12 §4.4, category by category.</summary>
        public static TheoryData<string> CatalogTokens
        {
            get
            {
                var data = new TheoryData<string>();

                foreach (var token in BuildCatalog())
                {
                    data.Add(token);
                }

                return data;
            }
        }

        [Fact]
        public void Parse_WithEveryRegisteredPedalPositionToken_RecognisesItAsAnInput()
        {
            foreach (var token in KeyRegistry.PedalPositionTokens)
            {
                var result = PedalFileParser.Parse([$"[{token}]>[a]"]);

                Assert.Empty(result.InvalidLines);
                Assert.Single(result.SourceLines);
                Assert.NotEqual(PedalInputId.None, result.SourceLines[0].Input);
                Assert.Equal(token, PedalInputs.GetToken(result.SourceLines[0].Input));
            }
        }

        [Theory]
        [MemberData(nameof(CatalogTokens))]
        public void Resolve_WithEveryTokenOfTheSpecCatalog_ReturnsAKey(string token)
        {
            Assert.NotNull(PedalTokenMap.Resolve(token));
        }

        [Theory]
        [MemberData(nameof(CatalogTokens))]
        public void GetMacroToken_ForEveryTokenOfTheSpecCatalog_WritesATokenThatResolvesBack(string token)
        {
            var key = PedalTokenMap.Resolve(token)!;
            var written = PedalTokenMap.GetMacroToken(key);

            Assert.NotEmpty(written);
            Assert.Equal(key, PedalTokenMap.Resolve(written));
        }

        [Fact]
        public void Parse_WithEveryTokenOfTheSpecCatalogInOneMacro_ReportsNoInvalidLine()
        {
            var groups = BuildCatalog()
                .Where(token => token != PedalTokenMap.SpaceToken)
                .Select(token => "{" + token + "}");

            var result = PedalFileParser.Parse(["{jack1}>" + string.Concat(groups)]);

            Assert.Empty(result.InvalidLines);
        }

        private static IEnumerable<string> BuildCatalog()
        {
            for (var letter = 'a'; letter <= 'z'; letter++)
            {
                yield return letter.ToString();
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                yield return digit.ToString();
            }

            for (var number = 1; number <= 24; number++)
            {
                yield return "F" + number;
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                yield return "kp" + digit;
            }

            string[] rest =
            [
                // Control keys
                "escape", "pause", "prtscr", "scroll", "tab", "caps", "insert", "home", "end",
                "pdown", "pup", "right", "left", "up", "down", "numlk", "enter", "bspace", "delete",
                // Modifiers
                "shift", "lshift", "rshift", "ctrl", "lctrl", "rctrl", "alt", "lalt", "ralt", "win",
                // Punctuation
                "=", "hyphen", "/", "\\", "'", "`", ";", ",", ".", "obrack", "cbrack", "intl-\\",
                // Numpad
                "kpdiv", "kpmult", "kpmin", "kpplus", "kp.", "kpenter",
                // Space
                PedalTokenMap.SpaceToken,
                // Mouse
                "lmouse", "mmouse", "rmouse",
                // Pedal response
                "speed1", "speed3", "speed5", "d125", "d500",
                // Media
                "mute", "vol-", "vol+", "play", "prev", "next",
                // Other pseudo-keys
                "calc", "shutdn"
            ];

            foreach (var token in rest)
            {
                yield return token;
            }
        }
    }
}
