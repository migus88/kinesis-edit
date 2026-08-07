namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Maps every <see cref="LightingMode"/> to the effect that draws it. The lookup is total by
    /// construction — it is built from <c>Enum.GetValues</c> and throws at type-initialisation if
    /// a mode were ever added without a row — so nothing falls through to a meaningless default.
    /// Effects are stateless, so one instance per mode is shared by every sampler.
    /// </summary>
    internal static class LightingEffectCatalog
    {
        private static readonly Dictionary<LightingMode, ILightingEffect> _byMode;

        static LightingEffectCatalog()
        {
            var steadyPaint = new SteadyPaintEffect();

            _byMode = new Dictionary<LightingMode, ILightingEffect>
            {
                [LightingMode.Disabled] = new UnlitEffect(),
                [LightingMode.Freestyle] = steadyPaint,
                [LightingMode.Monochrome] = new SolidFillEffect(),
                [LightingMode.Breathe] = new PaintFadeEffect(),
                [LightingMode.Spectrum] = new HueCycleEffect(),
                [LightingMode.Wave] = new TravellingRainbowEffect(),
                [LightingMode.FrozenWave] = steadyPaint,
                [LightingMode.Reactive] = new ReactiveEffect(),
                [LightingMode.Ripple] = new RippleEffect(),
                [LightingMode.Fireball] = new FireballEffect(),
                [LightingMode.Starlight] = new TwinkleEffect(),
                [LightingMode.Rebound] = new ReboundEffect(),
                [LightingMode.Loop] = new SweepEffect(),
                [LightingMode.Pulse] = new BoardFadeEffect(),
                [LightingMode.Rain] = new RainEffect(),
                [LightingMode.PitchBlack] = new PitchBlackEffect()
            };

            foreach (var mode in Enum.GetValues<LightingMode>())
            {
                if (!_byMode.ContainsKey(mode))
                {
                    throw new InvalidOperationException($"No preview effect is mapped for lighting mode {mode}.");
                }
            }
        }

        /// <summary>The effect that draws <paramref name="mode"/>.</summary>
        public static ILightingEffect For(LightingMode mode)
        {
            return _byMode.TryGetValue(mode, out var effect)
                ? effect
                : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown lighting mode.");
        }
    }
}
