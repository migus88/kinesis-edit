using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The <see cref="IVDriveFileService"/> a demo session runs on: a decorator that serves reads
    /// of the demo drive from <see cref="DemoVDriveFixtures"/>, refuses writes to it, and passes
    /// everything else through to the real service untouched.
    /// <para>
    /// <b>The refusal is scoped by path, never by mode.</b> An export writes through this very
    /// interface to a folder the user picked (specs/11-feature-dialogs.md §11.5), and demo mode is
    /// exactly where export is most useful — so a service that refused every write would silently
    /// break export, and one that refused none would break demo mode's central promise. The line
    /// between them is <see cref="DemoVDrive.IsUnderRoot"/> and nothing else.
    /// </para>
    /// <para>
    /// Nothing here knows it is "in demo mode": handing this service to
    /// <see cref="ProfileSessionFactory"/> is what makes a session read fixtures, which is
    /// precisely the seam docs/app/profiles.md describes — Core cannot tell a fixture-backed file
    /// service from a drive-backed one, and does not need to.
    /// </para>
    /// </summary>
    public sealed class DemoVDriveFileService : IVDriveFileService
    {
        private readonly IVDriveFileService _fileService;
        private readonly DemoVDriveFixtures _fixtures;

        /// <summary>
        /// Decorates <paramref name="fileService"/> — the real
        /// <see cref="Core.VDrive.Io.VDriveFileService"/> at a composition root — with
        /// <paramref name="fixtures"/>, defaulting to the ones embedded in this assembly.
        /// </summary>
        public DemoVDriveFileService(IVDriveFileService fileService, DemoVDriveFixtures? fixtures = null)
        {
            ArgumentNullException.ThrowIfNull(fileService);

            _fileService = fileService;
            _fixtures = fixtures ?? DemoVDriveFixtures.Default;
        }

        /// <summary>
        /// Reads a demo path from the fixtures — <see cref="FileNotFoundException"/> when the
        /// fixture set has no such file, exactly as the real service reports a missing one — and
        /// delegates every other path.
        /// </summary>
        public IReadOnlyList<string> ReadAllLines(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            return DemoVDrive.IsUnderRoot(path)
                ? _fixtures.ReadLines(path)
                : _fileService.ReadAllLines(path);
        }

        /// <summary>
        /// Refuses a write to the demo drive with <see cref="DemoVDriveWriteException"/>; delegates
        /// every other write, including an export to a user-chosen folder.
        /// </summary>
        public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(lines);

            ThrowIfDemoPath(path);

            _fileService.WriteAllLines(path, lines, allowCreate);
        }

        /// <summary>
        /// Refuses a settings merge on the demo drive; delegates every other one. A merge is a
        /// write — it reads, rewrites and truncates the file — so it is refused on the same rule
        /// rather than being allowed through because it starts with a read.
        /// </summary>
        public void UpdateSettingsFile(
            string path,
            IEnumerable<KeyValuePair<string, string>> values,
            IEnumerable<string>? removedKeys = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(values);

            ThrowIfDemoPath(path);

            _fileService.UpdateSettingsFile(path, values, removedKeys);
        }

        private static void ThrowIfDemoPath(string path)
        {
            if (DemoVDrive.IsUnderRoot(path))
            {
                throw new DemoVDriveWriteException(path);
            }
        }
    }
}
