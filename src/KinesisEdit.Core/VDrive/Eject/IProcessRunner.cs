namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// Runs an external process to completion and captures its exit code and output streams.
    /// Abstracted so the eject sequence of specs/03-vdrive-and-files.md §5.3 is testable
    /// without invoking real OS tools.
    /// </summary>
    public interface IProcessRunner
    {
        /// <summary>
        /// Starts <paramref name="fileName"/> with <paramref name="arguments"/>, waits for it
        /// to exit, and returns its exit code plus captured standard output and error.
        /// </summary>
        ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments);
    }
}
