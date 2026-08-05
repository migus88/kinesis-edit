using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Everything <see cref="UpdateCheckService.BuildRows"/> compares: the device, the versions
    /// read off its v-Drive, the app's own version, the operating system whose app build is being
    /// checked, and the fetched manifest (specs/09-firmware.md §3 steps 1-4).
    /// </summary>
    public sealed record UpdateCheckRequest
    {
        /// <summary>The connected device; rows are built only for <see cref="UpdateCheckEligibility.SupportedDevices"/>.</summary>
        public required DeviceId Device { get; init; }

        /// <summary>
        /// Versions parsed from the device's version file (§3 step 1). Use
        /// <see cref="VersionFileInfo.Empty"/> when nothing could be read — the keyboard version
        /// text is then empty and every row reports
        /// <see cref="UpdateRowState.LocalUnreadable"/>. A keyboard value that is present but
        /// unparseable is not "empty": it compares as 0.0.0 (§1.1).
        /// </summary>
        public required VersionFileInfo LocalVersions { get; init; }

        /// <summary>The manifest fetched from <see cref="VersionEndpoints"/>; <see cref="VersionManifest.Empty"/> is allowed.</summary>
        public required VersionManifest Manifest { get; init; }

        /// <summary>
        /// The app's own version as its build resource reports it — four components,
        /// <c>major.minor.revision.build</c> (§3 step 2). Parsed by the same rule as every other
        /// version, so the fourth component is ignored. Null or blank makes the app row
        /// <see cref="UpdateRowState.LocalUnreadable"/>; a non-blank but unparseable value
        /// compares as 0.0.0 (§1.1).
        /// </summary>
        public string? AppVersionText { get; init; }

        /// <summary>Operating system whose app key is read (§3 step 4); <c>None</c> resolves no key.</summary>
        public UpdateCheckPlatform Platform { get; init; } = UpdateCheckPlatform.None;
    }
}
