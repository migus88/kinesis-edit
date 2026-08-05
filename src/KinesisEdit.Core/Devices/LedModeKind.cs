namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// The value form of the <c>led_mode</c> settings key for a device, per the dual typing of
    /// specs/08-settings.md §2: a startup lighting file name on per-key RGB boards, a
    /// brightness/mode string on the Freestyle Edge.
    /// </summary>
    public enum LedModeKind
    {
        /// <summary>The app writes no <c>led_mode</c> key for the device.</summary>
        None = 0,

        /// <summary>Writes a startup lighting file name <c>led&lt;N&gt;.txt</c> (Edge RGB, TKO; spec 08 §2).</summary>
        LedFileName = 1,

        /// <summary>
        /// Writes a mode string: brightness <c>0</c>-<c>9</c>, <c>P</c> (pitch black) or
        /// <c>B</c> (breathe) (Freestyle Edge; spec 08 §2, §5.3).
        /// </summary>
        ModeString = 2
    }
}
