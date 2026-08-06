using System.Collections.ObjectModel;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The immutable catalog of every LED mode: file tokens, line shapes, direction rules, menu
    /// availability, and firmware gates, encoding the mode tables of specs/07-lighting.md §2.2,
    /// §2.3, and §3. Pure data — parsing lives in <see cref="LedFileParser"/>, gate evaluation
    /// in <see cref="FirmwareGateService"/>.
    /// </summary>
    public static class LightingModeCatalog
    {
        /// <summary>All mode rows, one per <see cref="LightingMode"/> value.</summary>
        public static IReadOnlyList<LightingModeDefinition> All { get; }

        private static readonly Dictionary<LightingMode, LightingModeDefinition> _byMode;
        private static readonly Dictionary<string, LightingModeDefinition> _byKeyBacklightToken;
        private static readonly Dictionary<string, LightingModeDefinition> _byEdgeToken;

        static LightingModeCatalog()
        {
            All = new ReadOnlyCollection<LightingModeDefinition>(BuildDefinitions());

            _byMode = new Dictionary<LightingMode, LightingModeDefinition>(All.Count);
            _byKeyBacklightToken = new Dictionary<string, LightingModeDefinition>(StringComparer.OrdinalIgnoreCase);
            _byEdgeToken = new Dictionary<string, LightingModeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in All)
            {
                _byMode.Add(definition.Mode, definition);

                if (definition.KeyBacklightToken is not null)
                {
                    _byKeyBacklightToken.Add(definition.KeyBacklightToken, definition);
                }

                if (definition.EdgeToken is not null)
                {
                    _byEdgeToken.Add(definition.EdgeToken, definition);
                }
            }
        }

        /// <summary>Returns the catalog row for <paramref name="mode"/>.</summary>
        public static LightingModeDefinition Find(LightingMode mode)
        {
            return _byMode.TryGetValue(mode, out var definition)
                ? definition
                : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown lighting mode.");
        }

        /// <summary>
        /// Returns the row whose key-backlight token matches <paramref name="token"/>
        /// case-insensitively, or null. The reserved <c>[black]</c> row is returned too —
        /// callers implementing the §2.4 read rules must skip reserved rows.
        /// </summary>
        public static LightingModeDefinition? FindByKeyBacklightToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return _byKeyBacklightToken.GetValueOrDefault(token);
        }

        /// <summary>
        /// Returns the row whose <c>_edge</c> token matches <paramref name="token"/>
        /// case-insensitively, or null (§2.3).
        /// </summary>
        public static LightingModeDefinition? FindByEdgeToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return _byEdgeToken.GetValueOrDefault(token);
        }

        private static List<LightingModeDefinition> BuildDefinitions()
        {
            var allFourDirections = new[]
            {
                LightingDirection.Down,
                LightingDirection.Left,
                LightingDirection.Up,
                LightingDirection.Right
            };
            var leftAndRight = new[] { LightingDirection.Left, LightingDirection.Right };
            var leftAndUp = new[] { LightingDirection.Left, LightingDirection.Up };

            return
            [
                new LightingModeDefinition
                {
                    Mode = LightingMode.Disabled,
                    DisplayName = "Disable",
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Freestyle,
                    DisplayName = "Freestyle",
                    HasPerKeyColors = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Monochrome,
                    DisplayName = "Monochrome",
                    KeyBacklightToken = "mono",
                    EdgeToken = "mono_edge",
                    WritesEffectColor = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Breathe,
                    DisplayName = "Breathe",
                    KeyBacklightToken = "breathe",
                    EdgeToken = "breathe_edge",
                    WritesSpeed = true,
                    HasPerKeyColors = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Spectrum,
                    DisplayName = "Spectrum",
                    KeyBacklightToken = "spectrum",
                    EdgeToken = "spectrum_edge",
                    WritesSpeed = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Wave,
                    DisplayName = "Wave",
                    KeyBacklightToken = "wave",
                    EdgeToken = "wave_edge",
                    WritesSpeed = true,
                    KeyBacklightDirections = allFourDirections,
                    EdgeDirections = leftAndRight,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.FrozenWave,
                    DisplayName = "Frozen Wave",
                    EdgeToken = "frozenwave_edge",
                    HasPerKeyColors = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Reactive,
                    DisplayName = "Reactive",
                    KeyBacklightToken = "reactive",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Ripple,
                    DisplayName = "Ripple",
                    KeyBacklightToken = "ripple",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    GatedByFeature = FirmwareFeature.RippleAndFireballEffects
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Fireball,
                    DisplayName = "Fireball",
                    KeyBacklightToken = "fireball",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    KeyBacklightDirections = leftAndRight,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    GatedByFeature = FirmwareFeature.RippleAndFireballEffects
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Starlight,
                    DisplayName = "Starlight",
                    KeyBacklightToken = "star",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Rebound,
                    DisplayName = "Rebound",
                    KeyBacklightToken = "rebound",
                    EdgeToken = "rebound_edge",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    KeyBacklightDirections = leftAndUp,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Loop,
                    DisplayName = "Loop",
                    KeyBacklightToken = "loop",
                    EdgeToken = "loop_edge",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    KeyBacklightDirections = allFourDirections,
                    EdgeDirections = leftAndRight,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Pulse,
                    DisplayName = "Pulse",
                    KeyBacklightToken = "pulse",
                    EdgeToken = "pulse_edge",
                    WritesSpeed = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true,
                    OffersInTkoEdgeMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.Rain,
                    DisplayName = "Rain",
                    KeyBacklightToken = "rain",
                    WritesSpeed = true,
                    WritesEffectColor = true,
                    HasBaseMonoLine = true,
                    OffersInRgbKeyBacklightMenu = true,
                    OffersInTkoKeyBacklightMenu = true
                },
                new LightingModeDefinition
                {
                    Mode = LightingMode.PitchBlack,
                    DisplayName = "Pitch Black",
                    KeyBacklightToken = "black",
                    IsReserved = true
                }
            ];
        }
    }
}
