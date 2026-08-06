using System.Collections.ObjectModel;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The single derivation of "which key codes an led file can address" for a set of geometry
    /// positions (specs/07-lighting.md §2.4 item 6, §4): each position's default token resolved
    /// through the Gen1 <see cref="KeyRegistry"/>, in geometry order, de-duplicated, skipping
    /// positions that carry no file token. Shared by <see cref="LightingSectionContext"/> (which
    /// uses it for the canonical parse/serialize order) and <see cref="LightingZoneCatalog"/>
    /// (which uses it for the §4 color zones), so both always speak the same key codes as
    /// <see cref="LayerLightingState.KeyColors"/>.
    /// </summary>
    internal static class LightingKeyCodeResolver
    {
        /// <summary>Resolves <paramref name="positions"/> to their Gen1 key codes, in order.</summary>
        public static IReadOnlyList<int> Resolve(IEnumerable<KeyPosition> positions)
        {
            ArgumentNullException.ThrowIfNull(positions);

            var codes = new List<int>();
            var seenCodes = new HashSet<int>();

            foreach (var position in positions)
            {
                if (position.DefaultToken.Length == 0)
                {
                    continue;
                }

                var entry = KeyRegistry.FindByToken(position.DefaultToken, TokenDialect.Gen1);

                if (entry is not null && seenCodes.Add(entry.Code))
                {
                    codes.Add(entry.Code);
                }
            }

            return new ReadOnlyCollection<int>(codes);
        }
    }
}
