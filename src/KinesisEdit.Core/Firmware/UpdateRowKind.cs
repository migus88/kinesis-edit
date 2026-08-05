namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The three rows of the "Check for Updates" dialog (specs/09-firmware.md §3), in display
    /// order. The legacy captions are <c>Keyboard Firmware :</c>, <c>Lighting Firmware :</c>,
    /// <c>SmartSet App :</c> — wording belongs to the app layer, not here.
    /// </summary>
    public enum UpdateRowKind
    {
        None = 0,

        /// <summary>Keyboard firmware on the device.</summary>
        Keyboard = 1,

        /// <summary>Lighting (LED) firmware on the device; omitted for the Advantage 360.</summary>
        Lighting = 2,

        /// <summary>The SmartSet app's own version.</summary>
        App = 3
    }
}
