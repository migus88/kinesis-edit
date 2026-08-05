namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The in-memory model of a TKO led file (specs/07-lighting.md §1.5, §2.3): a key-backlight
    /// state set and a fully parallel, independent edge-lighting state set, each with its own
    /// two layers. Both sections live in the same <c>ledN.txt</c> — the edge section is
    /// appended after the key-backlight section on save (§1.4).
    /// </summary>
    public sealed class TkoLightingModel
    {
        /// <summary>The key-backlight configuration context.</summary>
        public LightingModel KeyBacklight { get; } = new();

        /// <summary>The edge-lighting configuration context (33 edge LEDs, §2.3).</summary>
        public LightingModel Edge { get; } = new();

        /// <summary>Resets both contexts to the defaults of specs/07-lighting.md §6.</summary>
        public void Reset()
        {
            KeyBacklight.Reset();
            Edge.Reset();
        }

        /// <summary>Whether this model and <paramref name="other"/> hold the same values.</summary>
        public bool IsEquivalentTo(TkoLightingModel? other)
        {
            return other is not null
                && KeyBacklight.IsEquivalentTo(other.KeyBacklight)
                && Edge.IsEquivalentTo(other.Edge);
        }
    }
}
