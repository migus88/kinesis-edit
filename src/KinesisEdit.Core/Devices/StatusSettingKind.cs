namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Which status-report play-speed key the app writes for a device, per the
    /// specs/08-settings.md §2 table. Both keys carry the same integer 0-4 (0 = disabled).
    /// </summary>
    public enum StatusSettingKind
    {
        /// <summary>The app writes no status play-speed key for the device.</summary>
        None = 0,

        /// <summary>Writes the long key <c>status_play_speed</c> (Edge RGB, TKO, FS Edge/Pro, Advantage2; spec 08 §2).</summary>
        StatusPlaySpeedKey = 1,

        /// <summary>Writes the Advantage 360 short key <c>status</c> (spec 08 §2).</summary>
        StatusKey = 2
    }
}
