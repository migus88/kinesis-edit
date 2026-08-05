using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// The routing verdict for one Advantage2 config-side token (04 §3.2): the layer it
    /// targets, the key-table entry it names (null when unknown), and the effective token left
    /// after exception/alias/prefix handling — the spelling a layer-scan fallback compares
    /// position tokens against.
    /// </summary>
    internal readonly record struct Advantage2TokenRoute(bool IsKeypadLayer, KeyDefinition? Key, string EffectiveToken);

    /// <summary>
    /// Advantage2 config-side token routing (specs/04-layout-file-format.md §3.2,
    /// specs/05-key-model.md §5.4): a token targets the keypad layer when it is a
    /// keypad-exception token, one of the §3.6 keypad value spellings, or contains the
    /// <c>kp-</c> prefix (which is stripped before lookup); anything else targets the top
    /// layer. Exception and alias membership is tested <em>before</em> prefix stripping —
    /// <c>kp-insert</c> is an exception token that happens to carry the prefix, and <c>kp-</c>
    /// is itself the keypad-minus value spelling, not an empty prefixed token.
    /// </summary>
    internal static class Advantage2TokenRouter
    {
        /// <summary>
        /// The §3.6 value spellings of the keypad operators — read-side aliases of the entries
        /// whose Legacy save tokens are <c>kpdiv</c>/<c>kpmult</c>/<c>kpmin</c>/<c>kpplus</c>.
        /// They are the Gen1/Gen2 tokens of the same keys, so cross-dialect lookup resolves
        /// them; this set only supplies the keypad-layer routing verdict.
        /// </summary>
        private static readonly HashSet<string> _keypadValueSpellings = new(StringComparer.OrdinalIgnoreCase)
        {
            "kp/",
            "kp*",
            "kp-",
            "kp+"
        };

        /// <summary>Routes one config-side token (04 §3.2; 05 §5.4).</summary>
        public static Advantage2TokenRoute Route(string token)
        {
            if (Advantage2KeypadExceptions.TryGetCode(token, out var code))
            {
                var effectiveToken = token.StartsWith(Advantage2KeypadExceptions.KeypadPrefix, StringComparison.Ordinal)
                    ? token[Advantage2KeypadExceptions.KeypadPrefix.Length..]
                    : token;

                return new Advantage2TokenRoute(true, KeyRegistry.FindByCode(code), effectiveToken);
            }

            if (_keypadValueSpellings.Contains(token))
            {
                return new Advantage2TokenRoute(true, LayoutTokenResolver.Resolve(token, TokenDialect.Legacy), token);
            }

            var prefixIndex = token.IndexOf(Advantage2KeypadExceptions.KeypadPrefix, StringComparison.Ordinal);

            if (prefixIndex >= 0)
            {
                var stripped = token.Remove(prefixIndex, Advantage2KeypadExceptions.KeypadPrefix.Length);

                return new Advantage2TokenRoute(
                    true,
                    stripped.Length == 0 ? null : LayoutTokenResolver.Resolve(stripped, TokenDialect.Legacy),
                    stripped);
            }

            return new Advantage2TokenRoute(false, LayoutTokenResolver.Resolve(token, TokenDialect.Legacy), token);
        }

        /// <summary>
        /// Finds the routed key on <paramref name="layer"/>: first by key code, then — because
        /// several exception codes (10052, 10056…) name dedicated keypad-layer identities while
        /// the geometry resolves the same tokens by registry first-match — by comparing the
        /// layer's position/trigger tokens against the effective token.
        /// </summary>
        public static KeyboardKey? Find(KeyboardLayer layer, Advantage2TokenRoute route, bool byTrigger)
        {
            if (route.Key is null)
            {
                return null;
            }

            var found = byTrigger
                ? layer.FindByTriggerKeyCode(route.Key.Code)
                : layer.FindByPositionKeyCode(route.Key.Code);

            if (found is not null)
            {
                return found;
            }

            foreach (var key in layer.Keys)
            {
                var candidate = byTrigger ? key.TriggerKey : key.PositionKey;

                if (string.Equals(candidate.LegacyToken, route.EffectiveToken, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }

            return null;
        }
    }
}
