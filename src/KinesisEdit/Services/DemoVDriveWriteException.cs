namespace KinesisEdit.Services
{
    /// <summary>
    /// Thrown by <see cref="DemoVDriveFileService"/> when something tries to write to the demo
    /// drive. Demo mode saves nothing (specs/03-vdrive-and-files.md §3.5), and the fixtures are
    /// embedded resources with no file behind them, so there is no write to perform.
    /// <para>
    /// It exists as its own type, and derives from <see cref="InvalidOperationException"/> rather
    /// than <see cref="IOException"/>, on purpose: a silent no-op would let "demo mode persists
    /// nothing" pass while demo mode persisted everything, and the app's I/O call sites already
    /// swallow <see cref="IOException"/> as "the drive went away"
    /// (<see cref="VDriveAppPreferencesStore"/>). A demo write is a programming error, not a
    /// drive failure, and it must be loud enough to fail a test.
    /// </para>
    /// </summary>
    public sealed class DemoVDriveWriteException : InvalidOperationException
    {
        /// <summary>The demo path the refused write addressed.</summary>
        public string Path { get; }

        /// <summary>Creates the exception for a write to <paramref name="path"/>.</summary>
        public DemoVDriveWriteException(string path)
            : base($"Demo mode writes nothing; refused a write to {path}.")
        {
            Path = path;
        }
    }
}
