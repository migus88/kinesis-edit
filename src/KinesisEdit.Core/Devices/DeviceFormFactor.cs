namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Physical shape of a device in the design system's form-factor vocabulary
    /// (docs/design/mockups.md §1b, §2e — the first segment of a dashboard card's meta line).
    /// Deliberately not derived from <see cref="DeviceDefinition.HardwareNotes"/>: those carry
    /// specs/02-devices.md prose verbatim ("60% tenkeyless gaming board with tripartite space
    /// bar…"), which is not the vocabulary the cards speak. Member names mirror the rendered
    /// strings, which <c>DeviceMetaLine</c> owns.
    /// </summary>
    public enum DeviceFormFactor
    {
        /// <summary>Unclassified — the device has no card of its own (CROSSFIRE keypad).</summary>
        None = 0,

        /// <summary>"Split flat" — Freestyle Edge, Freestyle Pro, Freestyle Edge RGB.</summary>
        SplitFlat = 1,

        /// <summary>"Split contoured" — Advantage2, Advantage 360, Advantage 360 Professional.</summary>
        SplitContoured = 2,

        /// <summary>"60% gaming" — TKO.</summary>
        SixtyPercentGaming = 3,

        /// <summary>"3-button foot pedal" — Savant Elite 2.</summary>
        ThreeButtonFootPedal = 4
    }
}
