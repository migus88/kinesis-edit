namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The four faces a device card wears (docs/design/mockups.md §1b, §2e). Deliberately not
    /// <c>VDriveConnectionStatus</c>: that enum is a three-member fact about a drive, while this
    /// one folds in whether a scan is in flight and is what the card's status line, rail and
    /// buttons are chosen from.
    /// <para>
    /// There is no <c>WebToolOnly</c> member. The Advantage 360 Professional exposes no v-Drive,
    /// so the scanner never produces a status for it and it can never carry a
    /// <c>DeviceSnapshot</c>; its card is <see cref="WebToolCardViewModel"/>, a different type
    /// with a different anatomy, not a fifth state of this one.
    /// </para>
    /// </summary>
    public enum DeviceCardState
    {
        /// <summary>A drive is mounted and writable — the only state that edits the device for real.</summary>
        Connected = 0,

        /// <summary>
        /// A device seen at least once this session whose drive is not mounted right now. Idle and
        /// quiet: no red and no spinner, because this is the resting state and not an error.
        /// </summary>
        Resting = 1,

        /// <summary>The drive is mounted but not writable — another app holds a file, or it mounted read-only.</summary>
        CannotAccess = 2,

        /// <summary>
        /// Transient: a detection pass is in flight. Visual only — nothing can be cancelled, so no
        /// Cancel button is rendered (issue #88, decision D3).
        /// </summary>
        Scanning = 3
    }
}
