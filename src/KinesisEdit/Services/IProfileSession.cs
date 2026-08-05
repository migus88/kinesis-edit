using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The load/edit/save unit of one numbered profile, as the editor view models see it
    /// (docs/app/profiles.md). <see cref="ProfileSession"/> is sealed with a static factory, so it
    /// cannot be substituted in a test; this interface — implemented for real by
    /// <see cref="ProfileSessionAdapter"/> — is the seam, exactly like
    /// <see cref="IFirmwareUpdatePresenter"/> is the seam over the update dialog.
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
    }
}
