using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The synthetic drive a demo session reads from: its root path, the test that decides
    /// whether a path belongs to it, and the <see cref="VDriveLocation"/> handed to the code
    /// that would otherwise have been given a discovered one.
    /// <para>
    /// Demo mode is an app-layer concept (specs/03-vdrive-and-files.md §3.5); Core has none
    /// (see docs/app/profiles.md "Deliberately not here"). What makes a demo session work is
    /// that <see cref="VDriveLocation"/> derives every working path from
    /// <see cref="VDriveLocation.RootPath"/> plus catalog data, so a location with a root that
    /// is not a mount point still produces a complete, self-consistent set of paths — and an
    /// <see cref="Core.VDrive.Io.IVDriveFileService"/> that recognises that root can serve them
    /// from anywhere it likes.
    /// </para>
    /// </summary>
    public static class DemoVDrive
    {
        /// <summary>
        /// The prefix every demo path starts with. It is a URI-shaped string rather than a
        /// filesystem path on purpose, and that is what makes a collision with a real mount
        /// impossible: the volume enumerators produce <c>/Volumes/&lt;label&gt;</c> (macOS),
        /// a drive root such as <c>E:\</c> (Windows) and <c>/media|/run/media/&lt;user&gt;/&lt;label&gt;</c>
        /// (Linux), none of which can begin with a scheme. It is also not a legal Windows path
        /// at all (the <c>:</c>), so a demo path that somehow escaped the demo file service
        /// fails rather than touching a real file.
        /// </summary>
        public const string RootPrefix = "kinesis-edit://demo/";

        /// <summary>
        /// The demo root of <paramref name="deviceId"/>, e.g.
        /// <c>kinesis-edit://demo/FreestyleEdgeRgb</c>. The enum name rather than the volume
        /// label, because it is also the fixture folder's name — one spelling, so a fixture set
        /// can never be filed under a device the root does not address.
        /// </summary>
        public static string GetRootPath(DeviceId deviceId)
        {
            return RootPrefix + deviceId;
        }

        /// <summary>
        /// Whether <paramref name="path"/> addresses the demo drive of any device — the one test
        /// that scopes the demo file service's write refusal. Deliberately a <em>path</em> test
        /// and not a mode flag: an export writes through the same
        /// <see cref="Core.VDrive.Io.IVDriveFileService"/> to a folder the user picked
        /// (specs/11-feature-dialogs.md §11.5), and that write must go through untouched while
        /// every write to the fixture drive is refused.
        /// </summary>
        public static bool IsUnderRoot(string? path)
        {
            return path is not null && path.StartsWith(RootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The synthetic location of <paramref name="device"/>: the demo root, and
        /// <see cref="VDriveLocation.IsWritable"/> false, which is the same signal a
        /// present-but-unwritable drive gives (03 §3.5) and is what routes app preferences to
        /// <see cref="ReadOnlyAppPreferencesStore"/>.
        /// <para>
        /// Public so a caller that already knows the device has fixtures can build one directly;
        /// <see cref="IDemoDeviceProvider"/> is the gated way in and the one callers should
        /// normally use.
        /// </para>
        /// </summary>
        public static VDriveLocation CreateLocation(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return new VDriveLocation
            {
                Device = device,
                RootPath = GetRootPath(device.Id),
                IsWritable = false
            };
        }
    }
}
