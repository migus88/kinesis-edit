namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// Outcome of an <see cref="IVDriveEjector.Eject"/> attempt
    /// (specs/03-vdrive-and-files.md §5.3).
    /// </summary>
    public sealed record VDriveEjectResult
    {
        /// <summary>Whether the volume was flushed and released.</summary>
        public required bool Succeeded { get; init; }

        /// <summary>Optional detail for the user: OS tool output on success, error text on failure.</summary>
        public string? Message { get; init; }
    }
}
