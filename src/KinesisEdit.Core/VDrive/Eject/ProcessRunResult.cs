namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// Outcome of running an external process via <see cref="IProcessRunner"/>, used by the
    /// eject implementations of specs/03-vdrive-and-files.md §5.3.
    /// </summary>
    public sealed record ProcessRunResult
    {
        /// <summary>The process exit code; 0 conventionally means success.</summary>
        public required int ExitCode { get; init; }

        /// <summary>Everything the process wrote to its standard output stream.</summary>
        public required string StandardOutput { get; init; }

        /// <summary>Everything the process wrote to its standard error stream.</summary>
        public required string StandardError { get; init; }
    }
}
