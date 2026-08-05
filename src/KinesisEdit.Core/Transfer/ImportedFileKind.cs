namespace KinesisEdit.Core.Transfer
{
    /// <summary>
    /// What an imported <c>.txt</c> file is read as, per the classification rule of
    /// specs/07-lighting.md §1.4. <see cref="Layout"/> is the fallback: a file that fails the led
    /// heuristic "is tried as a layout".
    /// </summary>
    public enum ImportedFileKind
    {
        /// <summary>Read as a layout file (specs/04-layout-file-format.md).</summary>
        Layout = 0,

        /// <summary>Read as a led file (specs/07-lighting.md §2, §5).</summary>
        Lighting = 1
    }
}
