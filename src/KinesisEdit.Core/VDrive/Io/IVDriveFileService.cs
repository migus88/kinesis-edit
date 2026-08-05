namespace KinesisEdit.Core.VDrive.Io
{
    /// <summary>
    /// Line-oriented file I/O for v-Drive configuration files, implementing the raw 8-bit
    /// read/write rules of specs/03-vdrive-and-files.md §5 and the settings read-modify-write
    /// rule of specs/08-settings.md §1.
    /// </summary>
    public interface IVDriveFileService
    {
        /// <summary>
        /// Reads a file line by line as raw 8-bit data with no encoding conversion or BOM
        /// handling (specs/03-vdrive-and-files.md §5.1). Lines are split tolerantly on CRLF,
        /// LF, or lone CR; a trailing final newline does not produce an empty last element.
        /// Throws <see cref="FileNotFoundException"/> when the file does not exist.
        /// </summary>
        IReadOnlyList<string> ReadAllLines(string path);

        /// <summary>
        /// Truncates and fully rewrites the file in place — no temp file, no atomic rename,
        /// no backup — writing each line followed by the native platform line ending
        /// (specs/03-vdrive-and-files.md §5.2). The write is refused with
        /// <see cref="FileNotFoundException"/> when the file does not exist, unless
        /// <paramref name="allowCreate"/> is true for the operations the spec permits to
        /// create files. Missing parent directories are never created.
        /// </summary>
        void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false);

        /// <summary>
        /// Read-modify-write settings update per specs/08-settings.md §1: every line whose
        /// key matches a managed key (case-insensitive prefix plus the '=' separator, which
        /// disambiguates prefix collisions such as v_drive vs v_drive_open_on_startup) is
        /// replaced in place with "key=value"; unmanaged lines are preserved verbatim in
        /// order; managed keys with no matching line are appended in caller order. The file
        /// must already exist (specs/03-vdrive-and-files.md §5.2 write-refusal rule).
        /// </summary>
        void UpdateSettingsFile(string path, IEnumerable<KeyValuePair<string, string>> values);
    }
}
