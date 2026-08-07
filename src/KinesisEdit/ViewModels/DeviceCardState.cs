namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The three faces a device card wears (docs/design/mockups.md §1b, §2e). Deliberately not
    /// <c>VDriveConnectionStatus</c>: that enum is a three-member fact about a drive, while this
    /// one folds in whether a scan is in flight and is what the card's status line, rail and
    /// buttons are chosen from.
    /// <para>
    /// There is no <c>Resting</c> member and no <c>WebToolOnly</c> one. A card exists only while
    /// its drive is mounted — the roster is what the last scan found — so a device that goes away
    /// loses its card rather than falling to a resting state; and a board this app cannot edit
    /// appears nowhere at all rather than as a card with nothing to configure.
    /// </para>
    /// </summary>
    public enum DeviceCardState
    {
        /// <summary>A drive is mounted and writable — the only state that edits the device for real.</summary>
        Connected = 0,

        /// <summary>The drive is mounted but not writable — another app holds a file, or it mounted read-only.</summary>
        CannotAccess = 1,

        /// <summary>
        /// Transient: a detection pass is in flight. Visual only — nothing can be cancelled, so no
        /// Cancel button is rendered (issue #88, decision D3).
        /// </summary>
        Scanning = 2
    }
}
