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
            Violations = [],
            Ejected = true
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

        /// <summary>What a lighting <see cref="Import"/> puts into <see cref="Lighting"/>.</summary>
        public object? LightingToImport { get; set; }

        /// <summary>When set, <see cref="Import"/> throws it instead of applying anything.</summary>
        public Exception? ImportExceptionToThrow { get; set; }

        public FakeProfileSession(KeyboardLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public ProfileSaveResult Save()
        {
            SaveCallCount++;

            DuringSave?.Invoke();

            if (SaveExceptionToThrow is not null)
            {
                throw SaveExceptionToThrow;
            }

            return ResultToReturn;
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
