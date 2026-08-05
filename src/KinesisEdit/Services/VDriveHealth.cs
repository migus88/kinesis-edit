namespace KinesisEdit.Services
{
    /// <summary>
    /// Result of the per-poll version-file re-check that drives the green "v-Drive OK" /
    /// red "v-Drive Error" indicator of specs/10-apps-and-ui.md ("Startup / v-Drive scan /
    /// demo mode": the idle timer "re-verifies the device's version file every tick").
    /// </summary>
    public enum VDriveHealth
    {
        /// <summary>No drive was present to check (device not detected).</summary>
        Unknown = 0,

        /// <summary>The version file was read and parsed — "v-Drive OK".</summary>
        Ok = 1,

        /// <summary>The version file is missing, unreadable, or unparseable — "v-Drive Error".</summary>
        Error = 2
    }
}
