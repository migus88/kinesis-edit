using System.Collections.ObjectModel;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Transfer
{
    /// <summary>
    /// Produces the content the Export files dialog writes (specs/11-feature-dialogs.md §11.5):
    /// the current layout and/or lighting configuration serialized under the current files' base
    /// names. Content only — the directory-selection dialog, the writes, and the success/error
    /// messages are the app layer's.
    /// <para>
    /// The unit of export is a <see cref="ProfileSession"/> because that is where the three
    /// inputs §11.5 names live together: the layout, the lighting state, and the profile number
    /// the file names come from. It is also the only type that can carry the lighting state at
    /// all — Core's three lighting models (RGB, TKO, Advantage360) share no base type, which is
    /// why <see cref="ProfileSession.Lighting"/> is the boundary and the serializer is picked by
    /// device.
    /// </para>
    /// </summary>
    public static class ProfileExportPlanner
    {
        /// <summary>
        /// Returns the files <paramref name="selection"/> exports for <paramref name="session"/>,
        /// layout first. Both are serialized exactly as a save to the v-Drive would write them,
        /// kept invalid lines included (04 §4.3, §5.2), so an exported backup restores what the
        /// device would have held. A selection that includes lighting on a profile or device
        /// without a led file simply yields no led file — §11.5 gives no error for it.
        /// </summary>
        public static IReadOnlyList<ExportFile> Plan(ProfileSession session, ProfileExportSelection selection)
        {
            ArgumentNullException.ThrowIfNull(session);

            if (!Enum.IsDefined(selection))
            {
                throw new ArgumentOutOfRangeException(nameof(selection), selection, "Unknown export selection.");
            }

            var files = new List<ExportFile>();

            if (selection is ProfileExportSelection.LayoutAndLighting or ProfileExportSelection.LayoutOnly)
            {
                files.Add(new ExportFile(
                    ProfileFileNames.Layout(session.ProfileNumber),
                    LayoutFileSerializer.Serialize(session.Layout, session.InvalidLines)));
            }

            if (selection is ProfileExportSelection.LayoutAndLighting or ProfileExportSelection.LightingOnly
                && session.Lighting is not null)
            {
                files.Add(new ExportFile(
                    ProfileFileNames.Lighting(session.ProfileNumber),
                    ProfileLightingCodec.Serialize(session.Device, session.Lighting)));
            }

            return new ReadOnlyCollection<ExportFile>(files);
        }
    }
}
