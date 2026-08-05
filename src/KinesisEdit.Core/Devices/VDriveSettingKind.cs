namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Which v-Drive auto-open settings key the app writes for a device, per the
    /// specs/08-settings.md §2 table. The two keys are a spec §1 prefix-collision pair
    /// (<c>v_drive</c> is a prefix of <c>v_drive_open_on_startup</c>) and must never be
    /// matched by bare prefix.
    /// </summary>
    public enum VDriveSettingKind
    {
        /// <summary>The app writes no v-Drive key for the device.</summary>
        None = 0,

        /// <summary>Writes <c>v_drive=auto</c> / <c>v_drive=manual</c> (Edge RGB, TKO, FS Edge/Pro; spec 08 §2).</summary>
        AutoManual = 1,

        /// <summary>Writes <c>v_drive_open_on_startup=ON</c> (true) or literally <c>off</c> (false) (Advantage2; spec 08 §2).</summary>
        OpenOnStartup = 2
    }
}
