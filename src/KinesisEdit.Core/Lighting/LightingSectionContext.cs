using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// One configuration context the section engine runs in (specs/07-lighting.md §1.5): key
    /// backlight (RGB or TKO) or TKO edge. Carries the context's mode-token dialect (plain vs.
    /// <c>_edge</c>), its addressable key/LED codes in canonical file order (the device's
    /// top-layer geometry, §2.4 item 6), and whether the §2.4 item 7 Fn-layer token exceptions
    /// apply — specs/05-key-model.md §5.5 scopes them to the FS-family bottom layer, so only
    /// the RGB context translates; the TKO top layer has no F-row/pause/del positions.
    /// </summary>
    internal sealed class LightingSectionContext
    {
        /// <summary>Key-backlight context of the Freestyle Edge RGB.</summary>
        public static LightingSectionContext RgbKeyBacklight { get; } = new(
            isEdge: false,
            appliesFnTranslation: true,
            BuildKeyCodes(GeometryCatalog.FreestyleEdgeRgb.Layers[0].Keys));

        /// <summary>
        /// Key-backlight context of the TKO. No Fn-layer token translation: the §5.5 exception
        /// table is FS-family-only, and the TKO top layer has none of its file positions.
        /// </summary>
        public static LightingSectionContext TkoKeyBacklight { get; } = new(
            isEdge: false,
            appliesFnTranslation: false,
            BuildKeyCodes(GeometryCatalog.Tko.Layers[0].Keys));

        /// <summary>Edge-lighting context of the TKO (33 edge LEDs, §2.3).</summary>
        public static LightingSectionContext TkoEdge { get; } = new(
            isEdge: true,
            appliesFnTranslation: false,
            BuildKeyCodes(GeometryCatalog.Tko.Layers[0].EdgeZones));

        /// <summary>Whether this is the edge context (<c>_edge</c> mode tokens).</summary>
        public bool IsEdge { get; }

        /// <summary>Whether the Fn-layer save-token exceptions apply (§2.4 item 7).</summary>
        public bool AppliesFnTranslation { get; }

        /// <summary>The addressable key/LED codes in canonical file order.</summary>
        public IReadOnlyList<int> AddressableKeyCodes { get; }

        /// <summary>The context's fill-all/base-color token: <c>mono</c> or <c>mono_edge</c> (§2.4 item 4).</summary>
        public string MonoToken { get; }

        private readonly Dictionary<int, int> _ordinalByKeyCode;

        private LightingSectionContext(bool isEdge, bool appliesFnTranslation, IReadOnlyList<int> addressableKeyCodes)
        {
            IsEdge = isEdge;
            AppliesFnTranslation = appliesFnTranslation;
            AddressableKeyCodes = addressableKeyCodes;
            MonoToken = GetModeToken(LightingModeCatalog.Find(LightingMode.Monochrome))!;

            _ordinalByKeyCode = new Dictionary<int, int>(addressableKeyCodes.Count);

            for (var ordinal = 0; ordinal < addressableKeyCodes.Count; ordinal++)
            {
                _ordinalByKeyCode.TryAdd(addressableKeyCodes[ordinal], ordinal);
            }
        }

        /// <summary>
        /// Resolves a mode token in this context's dialect, skipping reserved tokens — the
        /// read side never matches <c>[black]</c> (§2.2).
        /// </summary>
        public LightingModeDefinition? FindReadableModeByToken(string token)
        {
            var definition = IsEdge
                ? LightingModeCatalog.FindByEdgeToken(token)
                : LightingModeCatalog.FindByKeyBacklightToken(token);

            return definition is { IsReserved: false } ? definition : null;
        }

        /// <summary>Returns the mode's file token in this context's dialect, or null.</summary>
        public string? GetModeToken(LightingModeDefinition definition)
        {
            return IsEdge ? definition.EdgeToken : definition.KeyBacklightToken;
        }

        /// <summary>Returns the mode's valid directions in this context (§2.3, §2.4 item 5).</summary>
        public IReadOnlyList<LightingDirection> GetDirections(LightingModeDefinition definition)
        {
            return IsEdge ? definition.EdgeDirections : definition.KeyBacklightDirections;
        }

        /// <summary>
        /// The canonical serialization position of a file key code: its top-layer geometry
        /// ordinal, with unknown codes sorted after every geometry key.
        /// </summary>
        public int GetSerializationOrdinal(int fileKeyCode)
        {
            return _ordinalByKeyCode.GetValueOrDefault(fileKeyCode, int.MaxValue);
        }

        private static IReadOnlyList<int> BuildKeyCodes(IReadOnlyList<KeyPosition> positions)
        {
            var codes = new List<int>(positions.Count);
            var seenCodes = new HashSet<int>(positions.Count);

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

            return codes;
        }
    }
}
