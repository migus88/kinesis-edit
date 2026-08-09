using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The real <see cref="IProfileSession"/>: a straight pass-through to one
    /// <see cref="ProfileSession"/>. It adds no behavior of its own — every rule of loading,
    /// dirty tracking and saving stays in Core (docs/app/profiles.md).
    /// </summary>
    public sealed class ProfileSessionAdapter : IProfileSession
    {
        /// <inheritdoc />
        public KeyboardLayout Layout => _session.Layout;

        /// <inheritdoc />
        public object? Lighting => _session.Lighting;

        /// <inheritdoc />
        public IReadOnlyList<LayoutInvalidLine> InvalidLines => _session.InvalidLines;

        /// <inheritdoc />
        public int ProfileNumber => _session.ProfileNumber;

        /// <inheritdoc />
        public bool CanSave => _session.CanSave;

        /// <inheritdoc />
        public bool IsDirty => _session.IsDirty;

        private readonly ProfileSession _session;

        /// <summary>Wraps <paramref name="session"/>.</summary>
        public ProfileSessionAdapter(ProfileSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <inheritdoc />
        public ProfileSaveResult Save()
        {
            return _session.Save();
        }

        /// <inheritdoc />
        public ProfileImportResult Import(ImportedFileKind kind, IReadOnlyList<string> lines)
        {
            return _session.Import(kind, lines);
        }

        /// <inheritdoc />
        public void RevertLayout()
        {
            _session.RevertLayout();
        }

        /// <inheritdoc />
        public void RevertLighting()
        {
            _session.RevertLighting();
        }

        /// <inheritdoc />
        public IReadOnlyList<ExportFile> PlanExport(ProfileExportSelection selection)
        {
            return ProfileExportPlanner.Plan(_session, selection);
        }
    }
}
