namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// The arithmetic every preview effect shares: cycle phases, the direction-aware travel axis,
    /// hue-to-colour, colour blending, and the stable hash the pseudo-random effects draw from.
    /// Deliberately free of state — determinism is the sampler's contract.
    /// </summary>
    internal static class LightingPreviewMath
    {
        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;
        private const double UnitDivisor = 4294967296.0;

        /// <summary>The fractional part of <paramref name="value"/>, always in [0,1) including for negatives.</summary>
        public static double Fraction(double value)
        {
            return value - Math.Floor(value);
        }

        /// <summary>Clamps <paramref name="value"/> into [0,1].</summary>
        public static double Clamp01(double value)
        {
            return double.IsNaN(value) ? 0.0 : Math.Clamp(value, 0.0, 1.0);
        }

        /// <summary>A 0 → 1 → 0 triangle over one cycle, for effects that bounce rather than wrap.</summary>
        public static double Triangle(double phase)
        {
            var wrapped = Fraction(phase);

            return wrapped < 0.5 ? wrapped * 2.0 : (1.0 - wrapped) * 2.0;
        }

        /// <summary>
        /// The coordinate that <i>grows in the direction of travel</i>: X for
        /// <see cref="LightingDirection.Right"/>, 1-X for <see cref="LightingDirection.Left"/>,
        /// Y for <see cref="LightingDirection.Down"/>, 1-Y for <see cref="LightingDirection.Up"/>.
        /// Reversing the direction therefore mirrors the axis, which is what makes an effect's
        /// gradient run the other way. <see cref="LightingDirection.None"/> travels along X.
        /// </summary>
        public static double AxisCoordinate(LightingDirection direction, double x, double y)
        {
            return direction switch
            {
                LightingDirection.Left => 1.0 - x,
                LightingDirection.Up => 1.0 - y,
                LightingDirection.Down => y,
                _ => x
            };
        }

        /// <summary>A fully saturated colour at <paramref name="hue"/>, which wraps outside [0,1).</summary>
        public static LedColor FromHue(double hue)
        {
            var sector = Fraction(hue) * 6.0;
            var index = (int)Math.Floor(sector);
            var rising = sector - index;
            var falling = 1.0 - rising;

            return index switch
            {
                0 => Rgb(1.0, rising, 0.0),
                1 => Rgb(falling, 1.0, 0.0),
                2 => Rgb(0.0, 1.0, rising),
                3 => Rgb(0.0, falling, 1.0),
                4 => Rgb(rising, 0.0, 1.0),
                _ => Rgb(1.0, 0.0, falling)
            };
        }

        /// <summary>Blends <paramref name="from"/> toward <paramref name="to"/> by <paramref name="amount"/> (0..1).</summary>
        public static LedColor Lerp(LedColor from, LedColor to, double amount)
        {
            var weight = Clamp01(amount);

            return new LedColor(
                Component(from.Red + ((to.Red - from.Red) * weight)),
                Component(from.Green + ((to.Green - from.Green) * weight)),
                Component(from.Blue + ((to.Blue - from.Blue) * weight)));
        }

        /// <summary>A stable 32-bit hash of one integer — FNV-1a, so it never changes between runs.</summary>
        public static uint Hash(int value)
        {
            var hash = FnvOffsetBasis;

            for (var shift = 0; shift < 32; shift += 8)
            {
                hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
            }

            return hash;
        }

        /// <summary>A stable 32-bit hash of two integers, for per-key-per-cycle pseudo-randomness.</summary>
        public static uint Hash(int value, long other)
        {
            var hash = Hash(value);

            for (var shift = 0; shift < 64; shift += 8)
            {
                hash = (hash ^ (byte)(other >> shift)) * FnvPrime;
            }

            return hash;
        }

        /// <summary>A stable pseudo-random number in [0,1) derived from <paramref name="value"/>.</summary>
        public static double UnitFrom(int value)
        {
            return Hash(value) / UnitDivisor;
        }

        /// <summary>A stable pseudo-random number in [0,1) derived from both arguments.</summary>
        public static double UnitFrom(int value, long other)
        {
            return Hash(value, other) / UnitDivisor;
        }

        private static LedColor Rgb(double red, double green, double blue)
        {
            return new LedColor(Component(red * 255.0), Component(green * 255.0), Component(blue * 255.0));
        }

        private static byte Component(double value)
        {
            return (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0.0, 255.0);
        }
    }
}
