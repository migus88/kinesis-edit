namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Which legacy SmartSet application serves a device, per specs/02-devices.md:
    /// two container ("master") apps exist — SmartSet Master (gaming: RGB, TKO) and
    /// SmartSet Master Office (Advantage2, Advantage 360, Savant Elite 2) — while the
    /// Freestyle Edge/Pro share one standalone binary hosted by neither master app.
    /// </summary>
    public enum ServingApp
    {
        /// <summary>No SmartSet app ever served the device (CROSSFIRE keypad).</summary>
        None = 0,

        /// <summary>A standalone SmartSet app only, not hosted in a master app (FS Edge/Pro).</summary>
        StandaloneOnly = 1,

        /// <summary>The gaming SmartSet Master dashboard (Freestyle Edge RGB, TKO).</summary>
        SmartSetMasterGaming = 2,

        /// <summary>
        /// The SmartSet Master Office dashboard (Advantage2, Advantage 360, Savant Elite 2;
        /// the Advantage 360 Professional is listed there too but only via "Access Website").
        /// </summary>
        SmartSetMasterOffice = 3
    }
}
