using System.Collections.ObjectModel;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// The seven pedal inputs as file tokens, display names, and their fixed order
    /// (specs/12-savant-elite.md §1, §4.6, §5). The tokens and the order both come from
    /// <see cref="KeyRegistry.PedalPositionTokens"/> (specs/05-key-model.md §3.14) — those
    /// deliberately are not key-table entries, so <see cref="KeyRegistry.FindByToken(string?)"/>
    /// never resolves them and this type is the only way to name an input.
    /// </summary>
    public static class PedalInputs
    {
        /// <summary>Number of programmable inputs (12 §1).</summary>
        public static int Count => KeyRegistry.PedalPositionTokens.Count;

        /// <summary>
        /// Every input in file order — the order <see cref="KeyRegistry.PedalPositionTokens"/>
        /// registers them in, which 12 §4.6 fixes as the save order of a newly created file.
        /// </summary>
        public static IReadOnlyList<PedalInputId> All { get; }

        /// <summary>Row captions of the 12 §5 right panel, indexed like <see cref="KeyRegistry.PedalPositionTokens"/>.</summary>
        private static readonly string[] _displayNames =
        [
            "Left Pedal",
            "Middle Pedal",
            "Right Pedal",
            "Jack 1",
            "Jack 2",
            "Jack 3",
            "Jack 4"
        ];

        private static readonly Dictionary<string, PedalInputId> _idsByToken;

        static PedalInputs()
        {
            var tokens = KeyRegistry.PedalPositionTokens;
            var all = new PedalInputId[tokens.Count];

            _idsByToken = new Dictionary<string, PedalInputId>(tokens.Count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < tokens.Count; i++)
            {
                var id = (PedalInputId)(i + 1);

                all[i] = id;
                _idsByToken[tokens[i]] = id;
            }

            All = new ReadOnlyCollection<PedalInputId>(all);
        }

        /// <summary>The file token of <paramref name="input"/> (12 §4.2 <c>&lt;input&gt;</c>).</summary>
        public static string GetToken(PedalInputId input)
        {
            return KeyRegistry.PedalPositionTokens[ToIndex(input)];
        }

        /// <summary>The human-readable row caption of <paramref name="input"/> (12 §5).</summary>
        public static string GetDisplayName(PedalInputId input)
        {
            var index = ToIndex(input);

            return index < _displayNames.Length ? _displayNames[index] : GetToken(input);
        }

        /// <summary>
        /// Resolves a file token to its input, ignoring case (12 §4.1: lines are lowercased
        /// before matching). False — and <see cref="PedalInputId.None"/> — for anything else.
        /// </summary>
        public static bool TryParseToken(string? token, out PedalInputId input)
        {
            input = PedalInputId.None;

            return token is not null && _idsByToken.TryGetValue(token, out input);
        }

        private static int ToIndex(PedalInputId input)
        {
            var index = (int)input - 1;

            if (index < 0 || index >= KeyRegistry.PedalPositionTokens.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input, "Not a programmable pedal input.");
            }

            return index;
        }
    }
}
