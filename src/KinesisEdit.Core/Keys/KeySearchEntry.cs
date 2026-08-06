namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// One row of the Search Keys list (specs/11-feature-dialogs.md §11.6): the assignable
    /// action, the parts a query is matched against, and the composed item text the list shows.
    /// Built by <see cref="KeySearchCatalog"/>; the dialog itself owns selection, double-click
    /// accept, and the "You must select a key" validation.
    /// </summary>
    /// <param name="Definition">The key-table entry the row selects (05 §3).</param>
    /// <param name="SearchName">
    /// The action's single-line name — the entry's default caption with two-line breaks flattened,
    /// or its file token when the caption is blank.
    /// </param>
    /// <param name="FileToken">The layout-file token of the entry in the catalog's dialect (05 §3).</param>
    /// <param name="DisplayText">
    /// The composed item text of §11.6: the search name, its display text when different, and
    /// <c>' (' + token + ')'</c> when the display text differs from the file token.
    /// </param>
    public sealed record KeySearchEntry(
        KeyDefinition Definition,
        string SearchName,
        string FileToken,
        string DisplayText);
}
