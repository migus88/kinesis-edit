namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Coarse grouping of a <see cref="KeyVisual"/> inside a board, used by the
    /// keyboard view for styling and hit-test grouping. Purely presentational: the
    /// logical geometry (<see cref="KeyPosition"/>) carries no cluster concept.
    /// </summary>
    public enum KeyCluster
    {
        /// <summary>Unclassified. Never produced by the authored boards.</summary>
        None = 0,

        /// <summary>
        /// The alphanumeric/navigation body of a board — the two halves of the
        /// Freestyle Edge RGB, and the equivalent blocks on every other device.
        /// </summary>
        Main = 1,

        /// <summary>
        /// Thumb cluster of the contoured boards: Advantage360 (#41) and
        /// Advantage2 (#42).
        /// </summary>
        Thumb = 2,

        /// <summary>
        /// Dedicated macro/hotkey column outside the typing block: the Freestyle
        /// Edge RGB left-edge hotkeys hk0-hk10, and the same column on the
        /// Freestyle Edge / Pro (#39).
        /// </summary>
        Function = 3,

        /// <summary>
        /// Case-edge lighting zones, which are LEDs rather than keys — the TKO's
        /// 33 zones L1-L9 / B1-B15 / R1-R9 (#40).
        /// </summary>
        EdgeZone = 4,

        /// <summary>Foot-pedal positions: Savant Elite2 and the Advantage360 pedal.</summary>
        Pedal = 5
    }
}
