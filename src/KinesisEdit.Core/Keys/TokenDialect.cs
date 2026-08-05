namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// One of the three config-file token dialects, one per device generation, per
    /// specs/README.md and specs/05-key-model.md §3.
    /// </summary>
    public enum TokenDialect
    {
        None = 0,
        Legacy = 1,
        Gen1 = 2,
        Gen2 = 3
    }
}
