namespace KinesisEdit.Services
{
    /// <summary>
    /// One caller-supplied custom button of a message box (specs/11-feature-dialogs.md §11.9,
    /// e.g. the "Update Firmware" button). Widths and click handlers of the legacy dialog are
    /// not modeled: the view lays the buttons out and reports the clicked <see cref="Id"/>.
    /// </summary>
    public sealed record MessageBoxButton
    {
        /// <summary>Stable identifier returned in <see cref="MessageBoxOutcome.CustomButtonId"/>.</summary>
        public required string Id { get; init; }

        /// <summary>Button caption.</summary>
        public required string Caption { get; init; }
    }
}
