namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// One row of the Advantage360 indicator-function catalog (specs/07-lighting.md §5): the
    /// function's file token(s) and whether it carries a color. All instances live in
    /// <see cref="IndicatorFunctionCatalog"/>.
    /// </summary>
    public sealed record IndicatorFunctionDefinition
    {
        /// <summary>The function this row describes.</summary>
        public required IndicatorFunction Function { get; init; }

        /// <summary>
        /// The function's file tokens without brackets, in layer order. One token for every
        /// function except Layer, which writes five (<c>layd</c>/<c>layk</c>/<c>lay1</c>/
        /// <c>lay2</c>/<c>lay3</c> — one per <see cref="IndicatorLayer"/>).
        /// </summary>
        public required IReadOnlyList<string> Tokens { get; init; }

        /// <summary>Whether the token is followed by <c>[R][G][B]</c>; Battery and Disabled are colorless (§5).</summary>
        public required bool HasColor { get; init; }
    }
}
