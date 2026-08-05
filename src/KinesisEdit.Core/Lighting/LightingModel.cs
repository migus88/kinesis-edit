namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The two-layer lighting state of one configuration context (specs/07-lighting.md §1.5):
    /// independent top-layer and Fn/embedded-layer states. Serves as the whole in-memory model
    /// of a Freestyle Edge RGB led file, and as each of the two sections (key backlight, edge)
    /// of a TKO led file (<see cref="TkoLightingModel"/>).
    /// </summary>
    public sealed class LightingModel
    {
        /// <summary>The top layer's state; its file lines carry no prefix (§2.1).</summary>
        public LayerLightingState TopLayer { get; } = new();

        /// <summary>The Fn/embedded layer's state; its file lines carry the <c>fn </c> prefix (§2.1).</summary>
        public LayerLightingState FnLayer { get; } = new();

        /// <summary>Restores both layers to the reset defaults of specs/07-lighting.md §6.</summary>
        public void Reset()
        {
            TopLayer.Reset();
            FnLayer.Reset();
        }

        /// <summary>Whether this model and <paramref name="other"/> hold the same values.</summary>
        public bool IsEquivalentTo(LightingModel? other)
        {
            return other is not null
                && TopLayer.IsEquivalentTo(other.TopLayer)
                && FnLayer.IsEquivalentTo(other.FnLayer);
        }
    }
}
