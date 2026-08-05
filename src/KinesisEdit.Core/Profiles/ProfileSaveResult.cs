using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// Outcome of <see cref="ProfileSession.Save"/>/<see cref="ProfileSession.SaveAs"/>
    /// (specs/03-vdrive-and-files.md §5.3): whether the write went through, the violations that
    /// stopped it (04 §5.3's "validate macro capacity first" gate), whether the v-Drive ejected
    /// afterwards, and the on-keyboard refresh wording to show the user.
    /// </summary>
    public sealed record ProfileSaveResult
    {
        /// <summary>Whether the profile (and settings, for a startup <see cref="ProfileSession.SaveAs"/>) were written.</summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Violations from <see cref="Model.KeyboardLayout.Validate"/> that stopped the save;
        /// empty when <see cref="Success"/> is true.
        /// </summary>
        public required IReadOnlyList<ModelViolation> Violations { get; init; }

        /// <summary>Whether <see cref="VDrive.Eject.IVDriveEjector.Eject"/> reported success after the write.</summary>
        public required bool Ejected { get; init; }

        /// <summary>The post-save on-keyboard refresh message from <see cref="ProfileSaveMessageCatalog"/>; null when the save did not succeed.</summary>
        public string? PostSaveMessage { get; init; }
    }
}
