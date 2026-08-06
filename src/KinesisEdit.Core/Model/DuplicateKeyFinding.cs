namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One duplicate-key advisory produced by <see cref="DuplicateKeyScan"/>: a token that the
    /// user's edits left on more than one position of a layer. An observation about the layout the
    /// editor is showing, not a device limit — nothing refuses a save because of it.
    /// </summary>
    public sealed record DuplicateKeyFinding
    {
        /// <summary>The token as the layout file spells it, in the dialect it was scanned in.</summary>
        public required string Token { get; init; }

        /// <summary>The <see cref="KeyboardLayer.Index"/> the duplication sits on.</summary>
        public required int LayerIndex { get; init; }

        /// <summary>
        /// Every position of that layer currently resolving to <see cref="Token"/>, by
        /// <see cref="KeyboardKey.Index"/>, ascending. Always two or more.
        /// </summary>
        public required IReadOnlyList<int> KeyIndexes { get; init; }
    }
}
