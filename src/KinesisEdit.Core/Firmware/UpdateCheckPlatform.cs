namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The operating system whose SmartSet app build the update check compares against
    /// (specs/09-firmware.md §3 step 4 lists one app key per OS × master app). Supplied by the
    /// caller rather than probed, so the comparison logic stays pure and fully testable.
    /// </summary>
    public enum UpdateCheckPlatform
    {
        /// <summary>Unspecified — no app key resolves, so the app row reports no remote version.</summary>
        None = 0,

        /// <summary>Windows: <c>app_ver</c> (gaming) / <c>pc_app_version</c> (office).</summary>
        Windows = 1,

        /// <summary>macOS: <c>mac_app_ver</c> (gaming) / <c>mac_app_version</c> (office).</summary>
        MacOs = 2,

        /// <summary>
        /// Linux — a deliberate deviation from the legacy app, which had no Linux build and
        /// therefore no key for it: Linux reuses the macOS keys (<c>mac_app_ver</c> /
        /// <c>mac_app_version</c>) until the endpoint publishes a Linux-specific one.
        /// </summary>
        Linux = 3
    }
}
