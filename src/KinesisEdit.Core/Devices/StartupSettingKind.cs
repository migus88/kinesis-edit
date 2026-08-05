namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Which startup-profile settings key the app writes for a device, per the
    /// specs/08-settings.md §2 table. Both forms carry the same profile number 1-9 and are
    /// parsed with the same file-number logic.
    /// </summary>
    public enum StartupSettingKind
    {
        /// <summary>The app writes no startup-profile key for the device.</summary>
        None = 0,

        /// <summary>Writes <c>startup_file=layout&lt;N&gt;.txt</c> (Edge RGB, TKO; spec 08 §2).</summary>
        LayoutFileName = 1,

        /// <summary>Writes <c>profile=&lt;N&gt;</c> as a bare number (Advantage 360; spec 08 §2).</summary>
        ProfileNumber = 2
    }
}
