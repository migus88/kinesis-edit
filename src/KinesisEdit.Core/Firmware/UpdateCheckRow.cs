using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// One rendered row of the "Check for Updates" dialog (specs/09-firmware.md §3): what was
    /// compared, how it came out, and where the row's button navigates. Everything a view model
    /// needs — no version needs re-deriving. Produced by <see cref="UpdateCheckService"/>.
    /// </summary>
    public sealed record UpdateCheckRow
    {
        /// <summary>Which of the three rows this is.</summary>
        public required UpdateRowKind Kind { get; init; }

        /// <summary>Comparison outcome.</summary>
        public required UpdateRowState State { get; init; }

        /// <summary>
        /// The locally installed version as it was compared; null when the local value was empty
        /// or nothing was compared yet. An unparseable local value is not null — it compares as
        /// 0.0.0 (specs/09-firmware.md §1.1).
        /// </summary>
        public FirmwareVersion? LocalVersion { get; init; }

        /// <summary>
        /// Local version as read, keeping the device's own wording where it has any
        /// (e.g. "1.0.1709.us (4MB), 03/08/2019"); empty when unknown.
        /// </summary>
        public string LocalVersionText { get; init; } = string.Empty;

        /// <summary>
        /// The published version as it was compared; null only when the manifest carried no value
        /// under this row's key. An unparseable remote value compares as 0.0.0, same as the local
        /// side.
        /// </summary>
        public FirmwareVersion? RemoteVersion { get; init; }

        /// <summary>Published version exactly as the manifest carried it; empty when absent.</summary>
        public string RemoteVersionText { get; init; } = string.Empty;

        /// <summary>
        /// Page the row's button opens (§3 step 7) — the firmware section for keyboard/lighting,
        /// the SmartSet-app section for the app row. Set regardless of state; null only where the
        /// device has no such page.
        /// </summary>
        public string? TargetUrl { get; init; }
    }
}
