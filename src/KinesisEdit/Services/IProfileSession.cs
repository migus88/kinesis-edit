using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The load/edit/save unit of one numbered profile, as the editor view models see it
    /// (docs/app/profiles.md). <see cref="ProfileSession"/> is sealed with a static factory, so it
    /// cannot be substituted in a test; this interface — implemented for real by
    /// <see cref="ProfileSessionAdapter"/> — is the seam, exactly like
    /// <see cref="IMessageBoxPresenter"/> is the seam over the message-box dialog.
    /// </summary>
    public interface IProfileSession
    {
        /// <summary>The profile's key/layer/macro model.</summary>
        KeyboardLayout Layout { get; }

        /// <summary>
        /// The device-appropriate lighting model, or null where the device has no
        /// profile-orchestrated lighting file. Typed <see cref="object"/> because Core dispatches
        /// three unrelated models by device (docs/app/profiles.md).
        /// </summary>
        object? Lighting { get; }

        /// <summary>Layout lines that could not be applied on load (specs/04-layout-file-format.md §5).</summary>
        IReadOnlyList<LayoutInvalidLine> InvalidLines { get; }

        /// <summary>The numbered profile position this session was loaded from.</summary>
        int ProfileNumber { get; }

        /// <summary>Whether the profile may be written back at all (false for the Advantage 360's factory profile 0).</summary>
        bool CanSave { get; }

        /// <summary>Whether the model currently re-serializes to something different from what was loaded.</summary>
        bool IsDirty { get; }

        /// <summary>Runs the save sequence of specs/03-vdrive-and-files.md §5.3 and reports its outcome.</summary>
        ProfileSaveResult Save();

        /// <summary>
        /// Replaces <see cref="Layout"/> or <see cref="Lighting"/> with the content of an
        /// imported file (<see cref="ProfileSession.Import"/>). Nothing is written: the result is
        /// unsaved edit state, and <see cref="Layout"/>/<see cref="Lighting"/>/
        /// <see cref="InvalidLines"/> must all be re-read afterwards.
        /// </summary>
        ProfileImportResult Import(ImportedFileKind kind, IReadOnlyList<string> lines);

        /// <summary>
        /// Throws the <b>layout</b> edits away and rebuilds <see cref="Layout"/> from the baseline —
        /// what is on the drive for this profile (<see cref="ProfileSession.RevertLayout"/>), leaving
        /// <see cref="Lighting"/> untouched. Nothing is written and no drive is read.
        /// <see cref="Layout"/> and <see cref="InvalidLines"/> are replaced wholesale, so both must
        /// be re-read afterwards — the same contract <see cref="Import"/> carries.
        /// </summary>
        void RevertLayout();

        /// <summary>
        /// The lighting twin of <see cref="RevertLayout"/>: rebuilds <see cref="Lighting"/> from the
        /// baseline's led lines and leaves <see cref="Layout"/> alone. A no-op on a device
        /// with no profile-orchestrated led file.
        /// </summary>
        void RevertLighting();

        /// <summary>
        /// The files an export of <paramref name="selection"/> writes
        /// (specs/11-feature-dialogs.md §11.5), layout first. It is a member of the seam rather
        /// than a call the caller makes on <see cref="Lighting"/> because
        /// <see cref="ProfileExportPlanner"/> needs the concrete <see cref="ProfileSession"/>:
        /// Core's three lighting models share no base type, so the app side sees only
        /// <see cref="object"/> and cannot pick a serializer for it.
        /// </summary>
        IReadOnlyList<ExportFile> PlanExport(ProfileExportSelection selection);
    }
}
