namespace KinesisEdit.Services
{
    /// <summary>
    /// The one question <see cref="DeviceLivenessWatcher"/> asks between scans: is the volume that
    /// was mounted at this path still there?
    /// <para>
    /// <b>This is a liveness check, not discovery.</b> It is only ever asked about a root path a
    /// completed scan already found, so it needs no volume enumeration, no label match, no version
    /// file and no writability probe — one stat per connected drive per tick and nothing more.
    /// Anything richer here would re-create the background poll issue
    /// <see href="https://github.com/migus88/kinesis-edit/issues/118">#118</see> deleted, only
    /// hidden behind a different name (docs/app/app-shell.md, invariant 5).
    /// </para>
    /// </summary>
    public interface IVolumeLivenessProbe
    {
        /// <summary>
        /// Whether the volume mounted at <paramref name="rootPath"/> is still present. Never
        /// throws: an answer that cannot be obtained is "not present", which costs one scan.
        /// </summary>
        bool IsPresent(string rootPath);
    }
}
