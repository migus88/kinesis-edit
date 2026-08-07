using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Services
{
    /// <summary>
    /// An <see cref="IVDriveFileService"/> decorator that brackets every write in an
    /// <see cref="IVDriveWriteActivity"/> scope and forwards everything untouched.
    /// <para>
    /// <b>It is wired at the composition root, so every v-Drive write in the app is covered with no
    /// call-site changes.</b> That is the whole point: a save reaches the drive through half a dozen
    /// paths — the profile session's layout and lighting writes, the settings merges, the pedal
    /// file, an export — and asking each of them to remember a bracket is exactly how one of them
    /// forgets. Here there is one seam and nothing below it knows it exists.
    /// </para>
    /// <para>
    /// <b>Reads pass straight through.</b> A read is not what a liveness tick must keep out of the
    /// way of, and the detection service re-reads a version file on every scan — bracketing those
    /// would report the app as "writing" for most of a pass.
    /// </para>
    /// </summary>
    public sealed class WriteTrackingVDriveFileService : IVDriveFileService
    {
        private readonly IVDriveFileService _fileService;
        private readonly IVDriveWriteActivity _writeActivity;

        /// <summary>Decorates <paramref name="fileService"/>, recording its writes in <paramref name="writeActivity"/>.</summary>
        public WriteTrackingVDriveFileService(IVDriveFileService fileService, IVDriveWriteActivity writeActivity)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _writeActivity = writeActivity ?? throw new ArgumentNullException(nameof(writeActivity));
        }

        /// <summary>Forwards the read; a read opens no bracket.</summary>
        public IReadOnlyList<string> ReadAllLines(string path)
        {
            return _fileService.ReadAllLines(path);
        }

        /// <summary>Forwards the write inside a write bracket, which is closed even when it throws.</summary>
        public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
        {
            using var write = _writeActivity.Begin();

            _fileService.WriteAllLines(path, lines, allowCreate);
        }

        /// <summary>
        /// Forwards the settings merge inside a write bracket. A merge reads, rewrites and truncates
        /// the file, so it is a write on the same rule the demo decorator uses rather than a read
        /// that happens to end in one.
        /// </summary>
        public void UpdateSettingsFile(
            string path,
            IEnumerable<KeyValuePair<string, string>> values,
            IEnumerable<string>? removedKeys = null)
        {
            using var write = _writeActivity.Begin();

            _fileService.UpdateSettingsFile(path, values, removedKeys);
        }
    }
}
