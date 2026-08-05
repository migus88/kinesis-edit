using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The factory "Expansion Pack 2" lighting defaults of specs/07-lighting.md §2.6 — the led
    /// file lines the legacy app writes into lighting profiles. The spec quotes profiles 1, 2,
    /// and 6 verbatim (in the legacy effect-first line order, see §2.4); the remaining
    /// profiles' contents are not pinned by the spec and are deliberately not invented here.
    /// The full-pack write flow belongs to profile orchestration.
    /// </summary>
    public static class ExpansionPackDefaults
    {
        /// <summary>The §2.6-quoted profile files, keyed by profile number (1, 2, 6).</summary>
        public static IReadOnlyDictionary<int, IReadOnlyList<string>> ProfileLines { get; }

        static ExpansionPackDefaults()
        {
            var profiles = new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = new ReadOnlyCollection<string>(new[]
                {
                    "[wave]>[spd5][dirright]",
                    "fn [wave]>[spd5][dirdown]"
                }),
                [2] = new ReadOnlyCollection<string>(new[]
                {
                    "[rain]>[0][255][0][spd5]",
                    "[mono]>[0][0][0]",
                    "fn [rain]>[0][255][0][spd5]",
                    "fn [mono]>[255][255][255]"
                }),
                [6] = new ReadOnlyCollection<string>(new[]
                {
                    "[mono]>[0][0][255]",
                    "fn [breathe]>[spd5]",
                    "fn [mono]>[0][0][255]"
                })
            };

            ProfileLines = new ReadOnlyDictionary<int, IReadOnlyList<string>>(profiles);
        }

        /// <summary>Returns the spec-pinned lines for <paramref name="profileNumber"/>, or null.</summary>
        public static IReadOnlyList<string>? FindProfileLines(int profileNumber)
        {
            return ProfileLines.GetValueOrDefault(profileNumber);
        }
    }
}
