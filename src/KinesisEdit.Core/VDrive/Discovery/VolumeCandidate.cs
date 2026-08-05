namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// One mounted volume reported by an <see cref="IVolumeEnumerator"/>, before any device
    /// matching: the raw input to the specs/03-vdrive-and-files.md §3 detection rules.
    /// </summary>
    /// <param name="RootPath">Absolute root path of the mounted volume (e.g. <c>/Volumes/ADV360</c> or <c>E:\</c>).</param>
    /// <param name="Label">Volume label as reported by the platform (mount directory name on macOS/Linux, <c>DriveInfo.VolumeLabel</c> on Windows), matched per 03 §2.</param>
    public readonly record struct VolumeCandidate(string RootPath, string Label);
}
