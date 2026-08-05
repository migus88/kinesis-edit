namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Identifies a device supported by the SmartSet ecosystem, per the master device
    /// table in specs/02-devices.md. The legacy app's numeric device id (0-8) is spec
    /// data carried by the device catalog, not by these enum values.
    /// </summary>
    public enum DeviceId
    {
        None = 0,
        SavantElite2 = 1,
        Advantage2 = 2,
        FreestyleEdge = 3,
        FreestylePro = 4,
        FreestyleEdgeRgb = 5,
        CrossfireKeypad = 6,
        Tko = 7,
        Advantage360 = 8,
        Advantage360Professional = 9
    }
}
