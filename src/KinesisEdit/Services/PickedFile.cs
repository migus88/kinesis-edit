namespace KinesisEdit.Services
{
    /// <summary>
    /// One file chosen through <see cref="IFilePickerService"/>, decoded exactly the way the
    /// v-Drive reads its own files (Latin1 bytes, tolerant line split — docs/app/vdrive.md), so
    /// an imported file parses identically to the same file read off the drive.
    /// <para>
    /// <see cref="ByteLength"/> is the file's true size and is reported faithfully even when the
    /// read was capped: the 50 KB import maximum of specs/07-lighting.md §1.4 is a user-visible
    /// validation owned by the import view model, and is deliberately never enforced here.
    /// </para>
    /// </summary>
    /// <param name="Name">The file's name as the platform reports it, e.g. <c>layout1.txt</c>.</param>
    /// <param name="Path">The absolute local path, or null when the platform exposes none.</param>
    /// <param name="ByteLength">The file's full length in bytes, however much of it was read.</param>
    /// <param name="Lines">The decoded lines of the content that was read.</param>
    /// <param name="IsTruncated">
    /// True when the file exceeded <see cref="PickedFileReader.MaxReadBytes"/> and
    /// <see cref="Lines"/> therefore holds only its beginning. Such a file is far past the import
    /// maximum and is refused on <see cref="ByteLength"/> before its content is ever parsed.
    /// </param>
    public sealed record PickedFile(
        string Name,
        string? Path,
        long ByteLength,
        IReadOnlyList<string> Lines,
        bool IsTruncated = false);
}
