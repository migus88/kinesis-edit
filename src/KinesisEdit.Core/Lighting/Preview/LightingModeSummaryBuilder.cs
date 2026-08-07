namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// Builds the one-line parameter caption of <see cref="LightingModeParameters.Summary"/> from
    /// facts, never from a per-mode string table. See that property for the grammar.
    /// </summary>
    internal static class LightingModeSummaryBuilder
    {
        private const string Separator = " · ";
        private const string DirectionSeparator = "/";
        private const string OneColorFragment = "color";
        private const string TwoColorsFragment = "2 colors";
        private const string SpeedFragment = "spd";

        /// <summary>Composes the caption; returns <see cref="LightingModeParameters.NoParametersSummary"/> when nothing applies.</summary>
        public static string Build(
            bool acceptsEffectColor,
            bool acceptsBaseColor,
            bool acceptsSpeed,
            IReadOnlyList<LightingDirection> directions)
        {
            var fragments = new List<string>(3);

            if (acceptsEffectColor && acceptsBaseColor)
            {
                fragments.Add(TwoColorsFragment);
            }
            else if (acceptsEffectColor || acceptsBaseColor)
            {
                fragments.Add(OneColorFragment);
            }

            if (acceptsSpeed)
            {
                fragments.Add(SpeedFragment);
            }

            var directionFragment = BuildDirections(directions);

            if (directionFragment.Length > 0)
            {
                fragments.Add(directionFragment);
            }

            return fragments.Count == 0
                ? LightingModeParameters.NoParametersSummary
                : string.Join(Separator, fragments);
        }

        private static string BuildDirections(IReadOnlyList<LightingDirection> directions)
        {
            var letters = new List<string>(directions.Count);

            foreach (var direction in directions)
            {
                var letter = LetterFor(direction);

                if (letter.Length > 0)
                {
                    letters.Add(letter);
                }
            }

            return string.Join(DirectionSeparator, letters);
        }

        private static string LetterFor(LightingDirection direction)
        {
            return direction switch
            {
                LightingDirection.Down => "D",
                LightingDirection.Left => "L",
                LightingDirection.Up => "U",
                LightingDirection.Right => "R",
                _ => string.Empty
            };
        }
    }
}
