namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The result of resolving an indicator function token (specs/07-lighting.md §5): the
    /// matched catalog row and the layer the token addresses — <see cref="IndicatorLayer.Base"/>
    /// for every token except the Layer function's <c>lay*</c> family.
    /// </summary>
    public sealed record IndicatorTokenMatch
    {
        /// <summary>The matched function row.</summary>
        public required IndicatorFunctionDefinition Definition { get; init; }

        /// <summary>The layer the token's color belongs to.</summary>
        public required IndicatorLayer Layer { get; init; }
    }
}
