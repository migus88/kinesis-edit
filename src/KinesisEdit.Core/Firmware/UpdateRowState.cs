namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Outcome of one row of the "Check for Updates" dialog (specs/09-firmware.md §3 steps 1, 5
    /// and 6). States only — the legacy captions (<c>Checking for update...</c>,
    /// <c>Update Now</c>, <c>No update available</c>, <c>Error fetching …</c>,
    /// <c>Error reading firmware file</c>, <c>Check connection</c>) are app-layer presentation.
    /// </summary>
    public enum UpdateRowState
    {
        None = 0,

        /// <summary>The check has not produced a result yet (the dialog's initial state).</summary>
        Checking = 1,

        /// <summary>Local version is smaller than the published one — the row links to the download page (§3 step 5).</summary>
        UpdateAvailable = 2,

        /// <summary>Local version is equal to or newer than the published one (§3 step 5).</summary>
        UpToDate = 3,

        /// <summary>
        /// The manifest carries no value under this row's key — the key is absent or its value is
        /// empty (§3 step 5). A present but unparseable value is not missing; it compares as
        /// 0.0.0 per §1.1.
        /// </summary>
        RemoteMissing = 4,

        /// <summary>
        /// No local version was read at all — the value came back empty. Per §3 step 1 an empty
        /// keyboard firmware puts every row — including the app row — into this state; a row whose
        /// own local value is empty (no LED line, no app version) reports it alone. A value that
        /// is present but unparseable is read: it compares as 0.0.0 per §1.1.
        /// </summary>
        LocalUnreadable = 5,

        /// <summary>The endpoint request failed or returned an unusable body (§3 step 6): every row gets this state.</summary>
        ConnectionError = 6
    }
}
