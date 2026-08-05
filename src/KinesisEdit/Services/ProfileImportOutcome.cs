using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Services
{
    /// <summary>
    /// What one run of <see cref="ProfileImporter.ImportAsync"/> produced: whether the session's
    /// content changed, and the one thing to tell the user about it. Rendering is the caller's —
    /// the importer never opens a dialog, so the whole flow stays assertable without a UI.
    /// </summary>
    public sealed record ProfileImportOutcome
    {
        /// <summary>The user cancelled the picker: nothing changed and nothing is said (07 §1.4).</summary>
        public static ProfileImportOutcome Cancelled { get; } = new();

        /// <summary>An import that was refused or failed, carrying the message to show.</summary>
        public static ProfileImportOutcome Failed(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return new ProfileImportOutcome { FailureMessage = message };
        }

        /// <summary>An import that replaced the session's content, carrying the confirmation.</summary>
        public static ProfileImportOutcome Applied(ImportedFileKind kind, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return new ProfileImportOutcome
            {
                WasApplied = true,
                Kind = kind,
                SuccessMessage = message
            };
        }

        /// <summary>Whether the session's layout or lighting was replaced.</summary>
        public bool WasApplied { get; private init; }

        /// <summary>What the file was read as; meaningful only when <see cref="WasApplied"/>.</summary>
        public ImportedFileKind Kind { get; private init; }

        /// <summary>The message box to show, or null when there is nothing to report.</summary>
        public string? FailureMessage { get; private init; }

        /// <summary>The toast to show, or null when nothing was imported.</summary>
        public string? SuccessMessage { get; private init; }

        private ProfileImportOutcome()
        {
        }
    }
}
