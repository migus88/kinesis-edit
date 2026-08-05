using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The in-memory model of an Advantage360 led file (specs/07-lighting.md §5): six
    /// configurable indicator LEDs, addressed as <c>[IND1]</c>..<c>[IND6]</c>. A full lighting
    /// reset resets all indicators to Disabled with black colors.
    /// </summary>
    public sealed class Advantage360LightingModel
    {
        /// <summary>Number of configurable indicator LEDs (specs/07-lighting.md §5).</summary>
        public const int IndicatorCount = 6;

        /// <summary>The six indicators in file order: index 0 is <c>[IND1]</c>.</summary>
        public IReadOnlyList<IndicatorState> Indicators { get; }

        /// <summary>Creates a model with all indicators in the reset state.</summary>
        public Advantage360LightingModel()
        {
            var indicators = new IndicatorState[IndicatorCount];

            for (var index = 0; index < IndicatorCount; index++)
            {
                indicators[index] = new IndicatorState();
            }

            Indicators = new ReadOnlyCollection<IndicatorState>(indicators);
        }

        /// <summary>Resets every indicator: function Disabled, all colors black (§5).</summary>
        public void Reset()
        {
            foreach (var indicator in Indicators)
            {
                indicator.Reset();
            }
        }

        /// <summary>Whether this model and <paramref name="other"/> hold the same values.</summary>
        public bool IsEquivalentTo(Advantage360LightingModel? other)
        {
            if (other is null)
            {
                return false;
            }

            for (var index = 0; index < IndicatorCount; index++)
            {
                if (!Indicators[index].IsEquivalentTo(other.Indicators[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
