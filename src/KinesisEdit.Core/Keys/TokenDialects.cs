namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Flags describing which token dialects an entry applies to. The spec's "All"
    /// scope marker (specs/05-key-model.md §3 legend) maps to <see cref="All"/>.
    /// </summary>
    [Flags]
    public enum TokenDialects
    {
        None = 0,
        Legacy = 1 << 0,
        Gen1 = 1 << 1,
        Gen2 = 1 << 2,
        All = Legacy | Gen1 | Gen2
    }
}
