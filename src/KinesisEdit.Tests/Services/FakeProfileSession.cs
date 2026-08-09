using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Transfer;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IProfileSession"/> over a layout the test builds itself: every
    /// answer is settable, and <see cref="Save"/> records its calls and returns whatever the test
    /// staged.
    /// </summary>
    internal sealed class FakeProfileSession : IProfileSession
    {
        /// <summary>A save that went through, with no post-save wording.</summary>
        public static ProfileSaveResult SuccessfulSave { get; } = new()
        {
            Success = true,
            Violations = []
        };

        public KeyboardLayout Layout { get; private set; }

        public object? Lighting { get; set; }

        public IReadOnlyList<LayoutInvalidLine> InvalidLines { get; set; } = [];

        public int ProfileNumber { get; set; } = 1;

        public bool CanSave { get; set; } = true;

        public bool IsDirty { get; set; }

        /// <summary>How often <see cref="Save"/> was called.</summary>
        public int SaveCallCount { get; private set; }

        /// <summary>What <see cref="Save"/> reports.</summary>
        public ProfileSaveResult ResultToReturn { get; set; } = SuccessfulSave;

        /// <summary>When set, <see cref="Save"/> throws it instead of returning.</summary>
        public Exception? SaveExceptionToThrow { get; set; }

        /// <summary>
        /// When set, runs inside <see cref="Save"/> — the window in which the editor reports
        /// <c>IsBusy</c>, so a test can observe command state while the save is in flight.
        /// </summary>
        public Action? DuringSave { get; set; }

        /// <summary>What <see cref="PlanExport"/> hands back, whatever the selection.</summary>
        public IReadOnlyList<ExportFile> ExportPlan { get; set; } = [];

        /// <summary>Every selection <see cref="PlanExport"/> was asked for, in call order.</summary>
        public List<ProfileExportSelection> ExportSelections { get; } = [];

        /// <summary>Every <see cref="Import"/>, in call order.</summary>
        public List<ImportCall> ImportCalls { get; } = [];

        /// <summary>How often <see cref="RevertLayout"/> was called.</summary>
        public int RevertLayoutCallCount { get; private set; }

        /// <summary>How often <see cref="RevertLighting"/> was called.</summary>
        public int RevertLightingCallCount { get; private set; }

        /// <summary>
        /// What <see cref="RevertLayout"/> puts into <see cref="Layout"/>. Unset, it builds a fresh
        /// factory-default layout — a <b>different instance</b> either way, because that is the part
        /// of the real contract the editor has to survive: reverting replaces the model, so every
        /// layer view model over the old one is stale.
        /// </summary>
        public KeyboardLayout? LayoutToRevertTo { get; set; }

        /// <summary>
        /// What <see cref="RevertLighting"/> puts into <see cref="Lighting"/>. Unset, the lighting
        /// model is left as it is — the real session's own no-op for a device with no led file.
        /// </summary>
        public object? LightingToRevertTo { get; set; }

        /// <summary>What a lighting <see cref="Import"/> puts into <see cref="Lighting"/>.</summary>
        public object? LightingToImport { get; set; }

        /// <summary>When set, <see cref="Import"/> throws it instead of applying anything.</summary>
        public Exception? ImportExceptionToThrow { get; set; }

        public FakeProfileSession(KeyboardLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        /// <summary>
        /// Records the call, then models the one part of Core's save contract the editor now reads
        /// back: <b>a successful save moves the baseline</b>, so the session reports itself clean
        /// afterwards (issue #133). A throw and a rejected result both leave
        /// <see cref="IsDirty"/> alone, because neither wrote anything.
        /// </summary>
        public ProfileSaveResult Save()
        {
            SaveCallCount++;

            DuringSave?.Invoke();

            if (SaveExceptionToThrow is not null)
            {
                throw SaveExceptionToThrow;
            }

            if (ResultToReturn.Success)
            {
                IsDirty = false;
            }

            return ResultToReturn;
        }

        /// <summary>
        /// The layout half of the discard. It never touches <see cref="Lighting"/> and never
        /// touches <see cref="IsDirty"/>: the flag is staged by the test, because a fake that
        /// cleared it would be asserting the very thing the Core suite proves.
        /// </summary>
        public void RevertLayout()
        {
            RevertLayoutCallCount++;

            Layout = LayoutToRevertTo ?? KeyboardLayout.Create(Layout.Device.Id);
        }

        /// <summary>The lighting half; it never touches <see cref="Layout"/>.</summary>
        public void RevertLighting()
        {
            RevertLightingCallCount++;

            if (LightingToRevertTo is not null)
            {
                Lighting = LightingToRevertTo;
            }
        }

        public IReadOnlyList<ExportFile> PlanExport(ProfileExportSelection selection)
        {
            ExportSelections.Add(selection);

            return ExportPlan;
        }

        /// <summary>
        /// Applies the import the way <see cref="ProfileSession.Import"/> does — a real parse of
        /// the lines for a layout, the staged model for lighting — because what the editor has to
        /// do afterwards depends on the content actually changing.
        /// </summary>
        public ProfileImportResult Import(ImportedFileKind kind, IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            ImportCalls.Add(new ImportCall(kind, lines));

            if (ImportExceptionToThrow is not null)
            {
                throw ImportExceptionToThrow;
            }

            if (kind == ImportedFileKind.Lighting)
            {
                Lighting = LightingToImport;

                return new ProfileImportResult { Kind = kind, InvalidLines = InvalidLines };
            }

            var parseResult = new LayoutFileParser(Layout.Device.Id).Parse(lines);

            Layout = parseResult.Layout;
            InvalidLines = parseResult.InvalidLines;

            return new ProfileImportResult { Kind = kind, InvalidLines = InvalidLines };
        }

        /// <summary>One recorded import.</summary>
        internal sealed record ImportCall(ImportedFileKind Kind, IReadOnlyList<string> Lines);
    }
}
